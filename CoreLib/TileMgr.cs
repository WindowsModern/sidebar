using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using ICSharpCode.SharpZipLib.Zip;
using static Sidebar.TilePackageWriteManager;

namespace Sidebar
{
	/// <summary>
	/// Compact version type that encodes 4 x 16-bit parts into a 64-bit value:
	/// bits 48..63 = major, 32..47 = minor, 16..31 = build, 0..15 = revision.
	/// </summary>
	internal static class PublisherIdHelper
	{
		// Base32 编码表 (Crockford 变体，去掉 I L O U 避免混淆)
		private const string Base32Chars = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
		/// <summary>
		/// 从证书的可分辨名称 (Distinguished Name) 计算出对应的 Publisher ID。
		/// </summary>
		/// <param name="distinguishedName">
		/// 证书 DN 字符串，例如 "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US"
		/// </param>
		/// <returns>13 位小写字母数字组成的 Publisher ID</returns>
		/// <exception cref="ArgumentNullException">distinguishedName 为 null 时抛出</exception>
		public static string GetPublisherId (string distinguishedName)
		{
			if (distinguishedName == null)
				throw new ArgumentNullException (nameof (distinguishedName));
			byte [] dnBytes = Encoding.Unicode.GetBytes (distinguishedName);
			byte [] hash;
			using (var sha256 = SHA256.Create ())
			{
				hash = sha256.ComputeHash (dnBytes);
			}
			byte [] first8Bytes = new byte [8];
			Array.Copy (hash, 0, first8Bytes, 0, 8);
			var binaryBuilder = new StringBuilder (64);
			for (int i = 0; i < first8Bytes.Length; i++)
			{
				binaryBuilder.Append (Convert.ToString (first8Bytes [i], 2).PadLeft (8, '0'));
			}
			string binaryString = binaryBuilder.ToString ().PadRight (65, '0');
			var resultBuilder = new StringBuilder (13);
			for (int i = 0; i < 65; i += 5)
			{
				string fiveBits = binaryString.Substring (i, 5);
				int index = Convert.ToInt32 (fiveBits, 2);
				resultBuilder.Append (Base32Chars [index]);
			}
			return resultBuilder.ToString ().ToLowerInvariant ();
		}
	}
	[XmlRoot ("Identity")]
	public class TileIdentity: ITileIdentity, IXmlSerializable
	{
		[XmlAttribute ("Name")]
		public string Name { get; private set; }
		[XmlAttribute ("Publisher")]
		public string Publisher { get; private set; }
		[XmlAttribute ("Version")]
		public Version Version { get; private set; }
		[XmlAttribute ("ProcessorArchitecture")]
		public ProcessorArchitecture ProcessorArchitecture { get; private set; }
		[XmlIgnore]
		public string PublisherId => PublisherIdHelper.GetPublisherId (Publisher);
		[XmlIgnore]
		public string FamilyName => $"{Name}_{PublisherId}";
		[XmlIgnore]
		public string FullName => $"{Name}_{Version.Expression}_{PublisherId}";
		[XmlIgnore]
		public Guid Id
		{
			get
			{
				var familyName = FamilyName;
				using (SHA256 sha256 = SHA256.Create ())
				{
					byte [] hash = sha256.ComputeHash (Encoding.UTF8.GetBytes (familyName));
					byte [] guidBytes = new byte [16];
					Buffer.BlockCopy (hash, 0, guidBytes, 0, 16);
					return new Guid (guidBytes);
				}
			}
		}
		[XmlIgnore]
		private static readonly string [] reservedNames =
			{
				"CON", "PRN", "AUX", "NUL",
				"COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
				"LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
			};
		private static void ValidateName (string name)
		{
			if (string.IsNullOrWhiteSpace (name)) throw new ArgumentException ("Name cannot be null or whitespace.", nameof (name));
			if (name.Length < 3 || name.Length > 50) throw new ArgumentException ("Name length must be between 3 and 50 characters.", nameof (name));
			if (!System.Text.RegularExpressions.Regex.IsMatch (name, @"^[a-zA-Z0-9.-]+$")) throw new ArgumentException ("Name can only contain letters, digits, periods, and hyphens.", nameof (name));
			if (Array.Exists (reservedNames, r => string.Equals (r, name, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException ($"Name cannot be a reserved system device name (e.g., {string.Join (", ", reservedNames)}).", nameof (name));
		}
		private static void ValidatePublisher (string publisher)
		{
			if (string.IsNullOrWhiteSpace (publisher)) throw new ArgumentException ("Publisher cannot be null or whitespace.", nameof (publisher));
			if (publisher.Length < 1 || publisher.Length > 8192) throw new ArgumentException ("Publisher length must be between 1 and 8192 characters.", nameof (publisher));
			try
			{
				var _ = new X500DistinguishedName (publisher);
			}
			catch (Exception ex)
			{
				throw new ArgumentException ("Publisher must be a valid X.500 distinguished name (e.g., CN=..., O=...).", nameof (publisher), ex);
			}
		}
		public TileIdentity (string name, string publisher, Version version = new Version (), ProcessorArchitecture archi = ProcessorArchitecture.Neutral)
		{
			if (string.IsNullOrWhiteSpace (name)) throw new ArgumentException ("Name cannot be null or whitespace.", nameof (name));
			if (string.IsNullOrWhiteSpace (publisher)) throw new ArgumentException ("Publisher cannot be null or whitespace.", nameof (publisher));
			if (version.Major == 0) throw new ArgumentException ("Version::Major is not 0.", nameof (version));
			ValidateName (name);
			ValidatePublisher (publisher);
			Name = name.Trim ();
			Publisher = publisher.Trim ();
			Version = version;
			ProcessorArchitecture = archi;
		}
		private TileIdentity () { }
		public XmlSchema GetSchema () => null;
		public void ReadXml (XmlReader reader)
		{
			reader.MoveToContent ();
			Name = reader.GetAttribute ("Name");
			Publisher = reader.GetAttribute ("Publisher");
			string verStr = reader.GetAttribute ("Version");
			Version = string.IsNullOrEmpty (verStr) ? new Version () : Version.Parse (verStr);
			string archStr = reader.GetAttribute ("ProcessorArchitecture");
			ProcessorArchitecture arch;
			if (Enum.TryParse (archStr, out arch))
				ProcessorArchitecture = arch;
			else
				ProcessorArchitecture = ProcessorArchitecture.Neutral;
			reader.ReadStartElement ("Identity");
		}
		public void WriteXml (XmlWriter writer)
		{
			writer.WriteAttributeString ("Name", Name);
			writer.WriteAttributeString ("Publisher", Publisher);
			writer.WriteAttributeString ("Version", Version.ToString ());
			writer.WriteAttributeString ("ProcessorArchitecture", ProcessorArchitecture.ToString ());
		}
	}
	[XmlRoot ("Properties")]
	public class TileProperties: ITileProperties, IXmlSerializable
	{
		[XmlElement ("DisplayName")]
		public string DisplayName { get; private set; }
		[XmlElement ("PublisherDisplayName")]
		public string PublisherDisplayName { get; private set; }
		[XmlIgnore]
		public string Publisher => PublisherDisplayName;
		[XmlElement ("Description")]
		public string Description { get; private set; }
		[XmlElement ("Logo")]
		public string Logo { get; private set; }
		[XmlElement ("Type")]
		public TileType Type { get; private set; }
		public TileProperties (string displayName, string publisherDisplayName, string description, string logo, TileType type)
		{
			if (displayName == null) throw new ArgumentNullException (nameof (displayName));
			if (string.IsNullOrWhiteSpace (displayName)) throw new ArgumentException ("Display name cannot be empty or whitespace.", nameof (displayName));
			if (publisherDisplayName == null) throw new ArgumentNullException (nameof (publisherDisplayName));
			if (string.IsNullOrWhiteSpace (publisherDisplayName)) throw new ArgumentException ("Publisher display name cannot be empty or whitespace.", nameof (publisherDisplayName));
			if (logo == null) throw new ArgumentNullException (nameof (logo));
			if (string.IsNullOrWhiteSpace (logo)) throw new ArgumentException ("Logo path cannot be empty or whitespace.", nameof (logo));
			Description = description;
			DisplayName = displayName;
			PublisherDisplayName = publisherDisplayName;
			Logo = logo;
			Type = type;
		}
		public TileProperties (string displayName, string publisherDisplayName, string logo, TileType type) :
			this (displayName, publisherDisplayName, null, logo, type)
		{ }
		private TileProperties () { }
		public XmlSchema GetSchema () => null;
		public void ReadXml (XmlReader reader)
		{
			reader.MoveToContent ();
			reader.ReadStartElement ("Properties");
			while (reader.NodeType != XmlNodeType.EndElement)
			{
				if (reader.NodeType == XmlNodeType.Element)
				{
					switch (reader.LocalName)
					{
						case "DisplayName": DisplayName = reader.ReadElementContentAsString (); break;
						case "PublisherDisplayName": PublisherDisplayName = reader.ReadElementContentAsString (); break;
						case "Description": Description = reader.ReadElementContentAsString (); break;
						case "Logo": Logo = reader.ReadElementContentAsString (); break;
						case "Type": Type = (TileType)Enum.Parse (typeof (TileType), reader.ReadElementContentAsString ()); break;
						default: reader.Skip (); break;
					}
				}
				else reader.Read ();
			}
			reader.ReadEndElement ();
		}
		public void WriteXml (XmlWriter writer)
		{
			writer.WriteElementString ("DisplayName", DisplayName);
			writer.WriteElementString ("PublisherDisplayName", PublisherDisplayName);
			if (!string.IsNullOrEmpty (Description))
				writer.WriteElementString ("Description", Description);
			writer.WriteElementString ("Logo", Logo);
			writer.WriteElementString ("Type", Type.ToString ());
		}
	}
	[XmlRoot ("Prerequisites")]
	public class TilePrerequisites: ITilePrerequisites, IXmlSerializable
	{
		[XmlElement ("OSMaxVersionTested")]
		public Version OSMaxVersionTested { get; private set; }
		[XmlElement ("OSMinVersion")]
		public Version OSMinVersion { get; private set; }
		public TilePrerequisites (Version osMin, Version osMax)
		{
			OSMinVersion = osMin;
			OSMaxVersionTested = osMax;
		}
		private TilePrerequisites ()
		{
			OSMinVersion = new Version ();
			OSMaxVersionTested = new Version ();
		}
		public XmlSchema GetSchema () => null;
		public void ReadXml (XmlReader reader)
		{
			reader.MoveToContent ();
			bool isEmpty = reader.IsEmptyElement;
			reader.ReadStartElement ();
			if (!isEmpty)
			{
				while (reader.NodeType != XmlNodeType.EndElement)
				{
					if (reader.NodeType == XmlNodeType.Element)
					{
						switch (reader.LocalName)
						{
							case "OSMinVersion":
								OSMinVersion = Version.Parse (reader.ReadElementContentAsString ());
								break;
							case "OSMaxVersionTested":
								OSMaxVersionTested = Version.Parse (reader.ReadElementContentAsString ());
								break;
							default:
								reader.Skip ();
								break;
						}
					}
					else
					{
						reader.Read ();
					}
				}
				reader.ReadEndElement (); 
			}
		}
		public void WriteXml (XmlWriter writer)
		{
			writer.WriteElementString ("OSMinVersion", OSMinVersion.ToString ());
			writer.WriteElementString ("OSMaxVersionTested", OSMaxVersionTested.ToString ());
		}
	}
	[XmlRoot ("RailStyle")]
	public class TileRailStyle: ITileRailStyle, IXmlSerializable
	{
		[XmlElement ("MinHeight")]
		public int MinHeight { get; private set; }
		[XmlElement ("MaxHeight")]
		public int MaxHeight { get; private set; }
		[XmlElement ("DefaultHeight")]
		public int DefaultHeight { get; private set; }
		[XmlElement ("CanPinBottom")]
		public bool CanPinBottom { get; private set; }
		[XmlElement ("TileHasFlyout")]
		public bool TileHasFlyout { get; private set; }
		[XmlElement ("FlyoutWidth")]
		public int FlyoutWidth { get; private set; }
		[XmlElement ("FlyoutHeight")]
		public int FlyoutHeight { get; private set; }
		[XmlElement ("FlyoutCanResize")]
		public bool FlyoutCanResize { get; private set; }
		[XmlElement ("Overflow")]
		public TileOverflow Overflow { get; private set; }
		[XmlElement ("DisplayName")]
		public string DisplayName { get; private set; }
		[XmlElement ("TileHasProperties")]
		public bool TileHasProperties { get; private set; }
		[XmlElement ("Logo")]
		public string Logo { get; private set; }
		public TileRailStyle (int minHeight, int maxHeight, int dfHeight, bool canPin, bool hasContent, int contentW, int contentH, bool contentCanResize, TileOverflow overflow, string dispName, bool hasProperties, string logo)
		{
			MinHeight = minHeight;
			MaxHeight = maxHeight;
			DefaultHeight = dfHeight;
			CanPinBottom = canPin;
			TileHasFlyout = hasContent;
			FlyoutWidth = contentW;
			FlyoutHeight = contentH;
			FlyoutCanResize = contentCanResize;
			Overflow = overflow;
			DisplayName = dispName;
			TileHasProperties = hasProperties;
			Logo = logo;
		}
		private TileRailStyle () { }
		public XmlSchema GetSchema () => null;
		public void ReadXml (XmlReader reader)
		{
			reader.MoveToContent ();
			reader.ReadStartElement ("RailStyle");
			while (reader.NodeType != XmlNodeType.EndElement)
			{
				if (reader.NodeType == XmlNodeType.Element)
				{
					switch (reader.LocalName)
					{
						case "MinHeight": MinHeight = reader.ReadElementContentAsInt (); break;
						case "MaxHeight": MaxHeight = reader.ReadElementContentAsInt (); break;
						case "DefaultHeight": DefaultHeight = reader.ReadElementContentAsInt (); break;
						case "CanPinBottom": CanPinBottom = reader.ReadElementContentAsBoolean (); break;
						case "TileHasFlyout": TileHasFlyout = reader.ReadElementContentAsBoolean (); break;
						case "FlyoutWidth": FlyoutWidth = reader.ReadElementContentAsInt (); break;
						case "FlyoutHeight": FlyoutHeight = reader.ReadElementContentAsInt (); break;
						case "FlyoutCanResize": FlyoutCanResize = reader.ReadElementContentAsBoolean (); break;
						case "Overflow": Overflow = (TileOverflow)Enum.Parse (typeof (TileOverflow), reader.ReadElementContentAsString ()); break;
						case "DisplayName": DisplayName = reader.ReadElementContentAsString (); break;
						case "TileHasProperties": TileHasProperties = reader.ReadElementContentAsBoolean (); break;
						case "Logo": Logo = reader.ReadElementContentAsString (); break;
						default: reader.Skip (); break;
					}
				}
				else reader.Read ();
			}
			reader.ReadEndElement ();
		}
		public void WriteXml (XmlWriter writer)
		{
			writer.WriteElementString ("MinHeight", MinHeight.ToString ());
			writer.WriteElementString ("MaxHeight", MaxHeight.ToString ());
			writer.WriteElementString ("DefaultHeight", DefaultHeight.ToString ());
			writer.WriteElementString ("CanPinBottom", CanPinBottom.ToString ().ToLower ());
			writer.WriteElementString ("TileHasFlyout", TileHasFlyout.ToString ().ToLower ());
			writer.WriteElementString ("FlyoutWidth", FlyoutWidth.ToString ());
			writer.WriteElementString ("FlyoutHeight", FlyoutHeight.ToString ());
			writer.WriteElementString ("FlyoutCanResize", FlyoutCanResize.ToString ().ToLower ());
			writer.WriteElementString ("Overflow", Overflow.ToString ());
			writer.WriteElementString ("DisplayName", DisplayName);
			writer.WriteElementString ("TileHasProperties", TileHasProperties.ToString ().ToLower ());
			writer.WriteElementString ("Logo", Logo);
		}
	}
	[XmlRoot ("GridStyle")]
	public class TileGridStyle: ITileGridStyle, IXmlSerializable
	{
		[XmlElement ("Badge")]
		public string Badge { get; private set; }
		[XmlElement ("DefaultTileSize")]
		public TileSize DefaultTileSize { get; private set; }
		[XmlElement ("DisplayName")]
		public string DisplayName { get; private set; }
		[XmlElement ("SmallTile")]
		public string SmallTile { get; private set; }
		[XmlElement ("MediumTile")]
		public string MediumTile { get; private set; }
		[XmlElement ("WideTile")]
		public string WideTile { get; private set; }
		[XmlElement ("LargeTile")]
		public string LargeTile { get; private set; }
		[XmlElement ("BackgroundColor")]
		public string BackgroundColor { get; private set; }
		[XmlElement ("ForegroundColor")]
		public TileForegroundColor ForegroundColor { get; private set; }
		[XmlElement ("ShowNameOnMediumTile")]
		public bool ShowNameOnMediumTile { get; private set; }
		[XmlElement ("ShowNameOnWideTile")]
		public bool ShowNameOnWideTile { get; private set; }
		[XmlElement ("ShowNameOnLargeTile")]
		public bool ShowNameOnLargeTile { get; private set; }
		[XmlElement ("EnableInteraction")]
		public bool EnableInteraction { get; private set; }
		public TileGridStyle (string badge, TileSize defaultTileSize, string smallTile, string mediumTile, string wideTile, string largeTile, string backgroundColor, TileForegroundColor foregroundColor, bool showNameOnMediumTile, bool showNameOnWideTile, bool showNameOnLargeTile, bool enableInteraction, string dispName)
		{
			// Validate required string parameters
			if (false)
			{
				if (badge == null) throw new ArgumentNullException (nameof (badge));
				if (string.IsNullOrWhiteSpace (badge)) throw new ArgumentException ("Badge URI cannot be empty or whitespace.", nameof (badge));
				if (smallTile == null) throw new ArgumentNullException (nameof (smallTile));
				if (string.IsNullOrWhiteSpace (smallTile)) throw new ArgumentException ("Small tile URI cannot be empty or whitespace.", nameof (smallTile));
				if (mediumTile == null) throw new ArgumentNullException (nameof (mediumTile));
				if (string.IsNullOrWhiteSpace (mediumTile)) throw new ArgumentException ("Medium tile URI cannot be empty or whitespace.", nameof (mediumTile));
				if (wideTile == null) throw new ArgumentNullException (nameof (wideTile));
				if (string.IsNullOrWhiteSpace (wideTile)) throw new ArgumentException ("Wide tile URI cannot be empty or whitespace.", nameof (wideTile));
				if (largeTile == null) throw new ArgumentNullException (nameof (largeTile));
				if (string.IsNullOrWhiteSpace (largeTile)) throw new ArgumentException ("Large tile URI cannot be empty or whitespace.", nameof (largeTile));
			}
			Badge = badge;
			DefaultTileSize = defaultTileSize;
			SmallTile = smallTile;
			MediumTile = mediumTile;
			WideTile = wideTile;
			LargeTile = largeTile;
			BackgroundColor = backgroundColor;
			ForegroundColor = foregroundColor;
			ShowNameOnMediumTile = showNameOnMediumTile;
			ShowNameOnWideTile = showNameOnWideTile;
			ShowNameOnLargeTile = showNameOnLargeTile;
			EnableInteraction = enableInteraction;
			DisplayName = dispName;
		}
		private TileGridStyle () { }
		public XmlSchema GetSchema () => null;
		public void ReadXml (XmlReader reader)
		{
			reader.MoveToContent ();
			reader.ReadStartElement ("GridStyle");
			while (reader.NodeType != XmlNodeType.EndElement)
			{
				if (reader.NodeType == XmlNodeType.Element)
				{
					switch (reader.LocalName)
					{
						case "Badge": Badge = reader.ReadElementContentAsString (); break;
						case "DefaultTileSize": DefaultTileSize = (TileSize)Enum.Parse (typeof (TileSize), reader.ReadElementContentAsString ()); break;
						case "DisplayName": DisplayName = reader.ReadElementContentAsString (); break;
						case "SmallTile": SmallTile = reader.ReadElementContentAsString (); break;
						case "MediumTile": MediumTile = reader.ReadElementContentAsString (); break;
						case "WideTile": WideTile = reader.ReadElementContentAsString (); break;
						case "LargeTile": LargeTile = reader.ReadElementContentAsString (); break;
						case "BackgroundColor": BackgroundColor = reader.ReadElementContentAsString (); break;
						case "ForegroundColor": ForegroundColor = (TileForegroundColor)Enum.Parse (typeof (TileForegroundColor), reader.ReadElementContentAsString ()); break;
						case "ShowNameOnMediumTile": ShowNameOnMediumTile = reader.ReadElementContentAsBoolean (); break;
						case "ShowNameOnWideTile": ShowNameOnWideTile = reader.ReadElementContentAsBoolean (); break;
						case "ShowNameOnLargeTile": ShowNameOnLargeTile = reader.ReadElementContentAsBoolean (); break;
						case "EnableInteraction": EnableInteraction = reader.ReadElementContentAsBoolean (); break;
						default: reader.Skip (); break;
					}
				}
				else reader.Read ();
			}
			reader.ReadEndElement ();
		}
		public void WriteXml (XmlWriter writer)
		{
			writer.WriteElementString ("Badge", Badge);
			writer.WriteElementString ("DefaultTileSize", DefaultTileSize.ToString ());
			writer.WriteElementString ("DisplayName", DisplayName);
			writer.WriteElementString ("SmallTile", SmallTile);
			writer.WriteElementString ("MediumTile", MediumTile);
			writer.WriteElementString ("WideTile", WideTile);
			writer.WriteElementString ("LargeTile", LargeTile);
			writer.WriteElementString ("BackgroundColor", BackgroundColor);
			writer.WriteElementString ("ForegroundColor", ForegroundColor.ToString ());
			writer.WriteElementString ("ShowNameOnMediumTile", ShowNameOnMediumTile.ToString ().ToLower ());
			writer.WriteElementString ("ShowNameOnWideTile", ShowNameOnWideTile.ToString ().ToLower ());
			writer.WriteElementString ("ShowNameOnLargeTile", ShowNameOnLargeTile.ToString ().ToLower ());
			writer.WriteElementString ("EnableInteraction", EnableInteraction.ToString ().ToLower ());
		}
	}
	[XmlRoot ("VisualElements")]
	public class TileVisualElements: ITileVisualElements, IXmlSerializable
	{
		ITileRailStyle ITileVisualElements.RailStyle => railStyle;
		ITileGridStyle ITileVisualElements.GridStyle => gridStyle;
		public TileRailStyle RailStyle => railStyle;
		public TileGridStyle GridStyle => gridStyle;
		private TileRailStyle railStyle;
		private TileGridStyle gridStyle;
		public TileVisualElements () { }
		public TileVisualElements (TileRailStyle railStyle, TileGridStyle gridStyle)
		{
			this.railStyle = railStyle;
			this.gridStyle = gridStyle;
		}
		public XmlSchema GetSchema () => null;
		public void ReadXml (XmlReader reader)
		{
			reader.MoveToContent ();
			reader.ReadStartElement ("VisualElements");
			while (reader.NodeType != XmlNodeType.EndElement)
			{
				if (reader.NodeType == XmlNodeType.Element)
				{
					if (reader.LocalName == "RailStyle")
					{
						var ser = new XmlSerializer (typeof (TileRailStyle), new XmlRootAttribute ("RailStyle"));
						railStyle = (TileRailStyle)ser.Deserialize (reader);
					}
					else if (reader.LocalName == "GridStyle")
					{
						var ser = new XmlSerializer (typeof (TileGridStyle), new XmlRootAttribute ("GridStyle"));
						gridStyle = (TileGridStyle)ser.Deserialize (reader);
					}
					else reader.Skip ();
				}
				else reader.Read ();
			}
			reader.ReadEndElement ();
		}
		public void WriteXml (XmlWriter writer)
		{
			var ns = new XmlSerializerNamespaces ();
			ns.Add ("", "");
			var railSer = new XmlSerializer (typeof (TileRailStyle), new XmlRootAttribute ("RailStyle"));
			var gridSer = new XmlSerializer (typeof (TileGridStyle), new XmlRootAttribute ("GridStyle"));
			railSer.Serialize (writer, railStyle, ns);
			gridSer.Serialize (writer, gridStyle, ns);
		}
	}
	[XmlRoot ("Manifest")]
	public class TileManifest: ITileManifest, IXmlSerializable
	{
		private TileIdentity identity;
		private TileProperties properties;
		private TileVisualElements visualElements;
		private TilePrerequisites prerequisites;
		ITileIdentity ITileManifest.Identity => identity;
		ITileProperties ITileManifest.Properties => properties;
		ITileVisualElements ITileManifest.VisualElements => visualElements;
		[XmlElement ("Identity")]
		public TileIdentity Identity => identity;
		[XmlElement ("Properties")]
		public TileProperties Properties => properties;
		[XmlElement ("Prerequisites")]
		public TilePrerequisites Prerequisites => prerequisites;
		[XmlElement ("VisualElements")]
		public TileVisualElements VisualElements => visualElements;
		ITilePrerequisites ITileManifest.Prerequisites => prerequisites;
		public TileManifest (TileIdentity id, TileProperties prop, TilePrerequisites pre, TileVisualElements ve)
		{
			identity = id;
			properties = prop;
			visualElements = ve;
			prerequisites = pre;
		}
		public TileManifest (TileIdentity id, TileProperties prop, TilePrerequisites pre, TileRailStyle rs, TileGridStyle gs)
		{
			identity = id;
			properties = prop;
			prerequisites = pre;
			visualElements = new TileVisualElements (rs, gs);
		}
		private TileManifest () { }
		public static TileManifest FromStream (Stream s)
		{
			var serializer = new XmlSerializer (typeof (TileManifest));
			return (TileManifest)serializer.Deserialize (s);
		}
		public static TileManifest FromFile (string fp)
		{
			using (var fs = File.Open (fp, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				return FromStream (fs);
			}
		}
		public XmlSchema GetSchema () => null;
		public void ReadXml (XmlReader reader)
		{
			reader.MoveToContent ();
			reader.ReadStartElement ("Manifest");
			while (reader.NodeType != XmlNodeType.EndElement)
			{
				if (reader.LocalName == "Identity")
				{
					var ser = new XmlSerializer (typeof (TileIdentity), new XmlRootAttribute ("Identity"));
					identity = (TileIdentity)ser.Deserialize (reader);
				}
				else if (reader.LocalName == "Properties")
				{
					var ser = new XmlSerializer (typeof (TileProperties), new XmlRootAttribute ("Properties"));
					properties = (TileProperties)ser.Deserialize (reader);
				}
				else if (reader.LocalName == "Prerequisites")   // 新增
				{
					var ser = new XmlSerializer (typeof (TilePrerequisites), new XmlRootAttribute ("Prerequisites"));
					prerequisites = (TilePrerequisites)ser.Deserialize (reader);
				}
				else if (reader.LocalName == "VisualElements")
				{
					var ser = new XmlSerializer (typeof (TileVisualElements), new XmlRootAttribute ("VisualElements"));
					visualElements = (TileVisualElements)ser.Deserialize (reader);
				}
				else
				{
					reader.Skip ();
				}
			}
			reader.ReadEndElement ();
		}
		public void WriteXml (XmlWriter writer)
		{
			var ns = new XmlSerializerNamespaces ();
			ns.Add ("", "");
			var idSer = new XmlSerializer (typeof (TileIdentity), new XmlRootAttribute ("Identity"));
			var propSer = new XmlSerializer (typeof (TileProperties), new XmlRootAttribute ("Properties"));
			var preSer = new XmlSerializer (typeof (TilePrerequisites), new XmlRootAttribute ("Prerequisites")); // 新增
			var veSer = new XmlSerializer (typeof (TileVisualElements), new XmlRootAttribute ("VisualElements"));
			idSer.Serialize (writer, identity, ns);
			propSer.Serialize (writer, properties, ns);
			if (prerequisites != null) 
				preSer.Serialize (writer, prerequisites, ns);
			veSer.Serialize (writer, visualElements, ns);
		}
	}
	public static class TIExtra
	{
		public static void ToText (this TileManifest info, TextWriter twt)
		{
			var serializer = new XmlSerializer (typeof (TileManifest));
			var namespaces = new XmlSerializerNamespaces ();
			namespaces.Add ("", "");
			serializer.Serialize (twt, info, namespaces);
		}
		public static void ToFile (this TileManifest info, string filepath)
		{
			using (var fp = new StreamWriter (filepath, false, Encoding.UTF8))
			{
				ToText (info, fp);
			}
		}
	}
	public partial class TileStorage
	{
		public string FolderPath { get; }
		public ProgramFolder TileFolder { get; }
		public ProgramFolder TileCurrentUserFolder { get; }
		public TileManifest Manifest { get; }
		public string TileFilePath { get; }
		public TileStorage (string folderPath)
		{
			var dllFile = Path.Combine (folderPath, "Tile.dll");
			var infoFile = Path.Combine (folderPath, "Manifest.xml");
			if (File.Exists (folderPath) || !Directory.Exists (folderPath) || !File.Exists (dllFile) || !File.Exists (infoFile))
				throw new ArgumentException ("The instance of Tile needs the folder stored tile files.");
			TileFilePath = dllFile;
			FolderPath = folderPath;
			Manifest = TileManifest.FromFile (infoFile);
			TileFolder = ProgramFolder.CreateFromPath (folderPath);
			TileCurrentUserFolder = ProgramFolder.CreateFromPath (Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "Windows Modern\\Sidebar\\Tiles", Manifest.Identity.FamilyName));
		}
		public override string ToString ()
		{
			if (Manifest == null) return base.ToString ();
			else return Manifest.Identity.FullName;
		}
	}
	public class TileManager: IDisposable, INotifyPropertyChanged
	{
		public string BaseDir { get; }
		public TileManager (string baseDir) { BaseDir = baseDir; StartWatching (); }
        public TileManager () : this (AppDomain.CurrentDomain.BaseDirectory) { }
		private FileSystemWatcher watcher;
		private readonly object cacheLock = new object ();
		private volatile bool disposed = false;
		private List<TileStorage> cacheList = new List<TileStorage> ();
		private Dictionary<Guid, TileStorage> cacheById = new Dictionary<Guid, TileStorage> ();
		private Dictionary<string, TileStorage> cacheByFullName = new Dictionary<string, TileStorage> (StringComparer.OrdinalIgnoreCase);
		private Dictionary<string, TileStorage> cacheByFamilyName = new Dictionary<string, TileStorage> (StringComparer.OrdinalIgnoreCase);
		private void StartWatching ()
		{
			string gadgetPath = GetGadgetFolder ();
			if (!Directory.Exists (gadgetPath))
				return;
			watcher = new FileSystemWatcher (gadgetPath) {
				IncludeSubdirectories = false,       // 只监控 Gadgets 下的直接子目录
				NotifyFilter = NotifyFilters.DirectoryName,
				EnableRaisingEvents = false
			};
			watcher.Created += OnDirectoryChanged;
			watcher.Deleted += OnDirectoryChanged;
			watcher.Renamed += OnDirectoryRenamed;
			watcher.EnableRaisingEvents = true;
			RefreshCacheImmediate ();
		}
		private void OnDirectoryChanged (object sender, FileSystemEventArgs e)
		{
			RefreshCache ();
		}
		private void OnDirectoryRenamed (object sender, RenamedEventArgs e)
		{
			RefreshCache ();
		}
		private Timer debounceTimer;
		private const int DebounceDelay = 500;
		private void RefreshCache ()
		{
			debounceTimer?.Dispose ();
			debounceTimer = new Timer (_ =>
			{
				RefreshCacheImmediate ();
			}, null, DebounceDelay, Timeout.Infinite);
		}
		private List<Tuple<bool, string, TileStorage>> ScanAllTiles ()
		{
			var tileFolder = GetGadgetFolder ();
			var ret = new List<Tuple<bool, string, TileStorage>> ();
			try
			{
				if (!Directory.Exists (tileFolder))
					return ret;
				foreach (var s in Directory.EnumerateDirectories (tileFolder))
				{
					try
					{
						ret.Add (new Tuple<bool, string, TileStorage> (true, "", new TileStorage (s)));
					}
					catch (Exception e)
					{
						ret.Add (new Tuple<bool, string, TileStorage> (false, e.Message, null));
					}
				}
			}
			catch { }
			return ret;
		}
		private string GetGadgetFolder () =>
			Path.Combine (ProgramFolder.GlobalFolder.FolderPath, "Gadgets");
		public List<Tuple<bool, string, TileStorage>> Get ()
		{
			lock (cacheLock)
			{
				return cacheList.Select (t => new Tuple<bool, string, TileStorage> (true, "", t)).ToList ();
			}
		}
		public List<TileStorage> ValidTiles
		{
			get
			{
				lock (cacheLock)
				{
					return cacheList.ToList (); 
				}
			}
		}
		public TileStorage GetById (Guid id)
		{
			TileStorage t;
			lock (cacheLock)
				return cacheById.TryGetValue (id, out t) ? t : null;
		}
		public TileStorage GetByFullName (string fullname)
		{
			TileStorage t;
			lock (cacheLock)
				return cacheByFullName.TryGetValue (fullname, out t) ? t : null;
		}
		public TileStorage GetByFamilyName (string familyName)
		{
			TileStorage t;
			lock (cacheLock)
				return cacheByFamilyName.TryGetValue (familyName, out t) ? t : null;
		}
		public TileStorage GetByIdentity (string name, string publisher)
		{
			TileStorage t;
			var familyName = $"{name}_{PublisherIdHelper.GetPublisherId (publisher)}";
			return GetByFamilyName (familyName);
		}
		public void ForceRefresh ()
		{
			RefreshCacheImmediate ();
		}
		public void Dispose ()
		{
			if (disposed) return;
			disposed = true;

			watcher?.Dispose ();
			debounceTimer?.Dispose ();
		}
		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged (string propertyName = null)
		{
			PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (propertyName));
		}
		private ObservableCollection<TileStorage> _validTilesObservable;
		public ObservableCollection<TileStorage> ValidTilesObservable
		{
			get
			{
				if (_validTilesObservable == null)
					_validTilesObservable = new ObservableCollection<TileStorage> (cacheList);
				return _validTilesObservable;
			}
		}
		private void UpdateCacheAndNotify (List<TileStorage> newValidTiles)
		{
			lock (cacheLock)
			{
				cacheList = newValidTiles;
				cacheById = newValidTiles.ToDictionary (t => t.Manifest.Identity.Id);
				cacheByFullName = newValidTiles.ToDictionary (t => t.Manifest.Identity.FullName, StringComparer.OrdinalIgnoreCase);
				cacheByFamilyName = newValidTiles.ToDictionary (t => t.Manifest.Identity.FamilyName, StringComparer.OrdinalIgnoreCase);
			}
			if (_validTilesObservable != null)
			{
				_validTilesObservable.Clear ();
				foreach (var tile in newValidTiles)
					_validTilesObservable.Add (tile);
			}
			OnPropertyChanged (nameof (ValidTiles));
			OnPropertyChanged (nameof (ValidTilesObservable));
		}
		private void RefreshCacheImmediate ()
		{
			var raw = ScanAllTiles ();
			var valid = raw.Where (t => t.Item1 && t.Item3 != null).Select (t => t.Item3).ToList ();
			var uniqueByName = new Dictionary<string, TileStorage> (StringComparer.OrdinalIgnoreCase);
			foreach (var tile in valid)
			{
				string name = tile.Manifest.Identity.Name?.Trim ();
				if (string.IsNullOrEmpty (name))
					continue;
				uniqueByName [name] = tile;
			}
			var distinctValid = uniqueByName.Values.ToList ();
			UpdateCacheAndNotify (distinctValid);
		}
		/// <summary>
		/// 安装包（支持 .sgpkg 单包或 .sgpkgbundle 捆绑包）。
		/// </summary>
		/// <param name="filePath">包文件路径</param>
		/// <param name="callback">进度回调（可选）</param>
		/// <exception cref="InvalidOperationException">架构不兼容或安装失败</exception>
		public void InstallPackage (string filePath, ProgressCallback callback = null)
		{
			InstallOrUpdateInternal (filePath, false, callback);
		}
		/// <summary>
		/// 更新包（如果已安装则替换，未安装则安装）。
		/// </summary>
		/// <param name="filePath">包文件路径</param>
		/// <param name="callback">进度回调（可选）</param>
		/// <exception cref="InvalidOperationException">架构不兼容或更新失败</exception>
		public void UpdatePackage (string filePath, ProgressCallback callback = null)
		{
			InstallOrUpdateInternal (filePath, true, callback);
		}
		private void InstallOrUpdateInternal (string filePath, bool isUpdate, TilePackageWriteManager.ProgressCallback callback)
		{
			if (string.IsNullOrEmpty (filePath))
				throw new ArgumentNullException (nameof (filePath));
			if (!File.Exists (filePath))
				throw new FileNotFoundException ($"Package file not found: {filePath}");
			TilePackageBase package = TilePackageReadManager.GetPackage (filePath);
			if (package == null)
				throw new InvalidDataException ("Failed to load package.");
			TilePackage targetPackage = null;
			TileIdentity identity = null;
			TilePackageBundle bundle = package as TilePackageBundle;
			if (bundle != null)
			{
				ProcessorArchitecture currentArch = GetSystemArchitecture ();
				TilePackage selected = null;
				foreach (var p in bundle.Packages)
				{
					if (p.Manifest.Identity.ProcessorArchitecture == currentArch)
					{
						selected = p;
						break;
					}
				}
				if (selected == null && currentArch != ProcessorArchitecture.Neutral)
				{
					foreach (var p in bundle.Packages)
					{
						if (p.Manifest.Identity.ProcessorArchitecture == ProcessorArchitecture.Neutral)
						{
							selected = p;
							break;
						}
					}
				}
				if (selected == null)
					throw new InvalidOperationException (
						$"No package in bundle supports the current system architecture ({currentArch}).");
				targetPackage = selected;
				identity = targetPackage.Manifest.Identity;
			}
			else if (package is TilePackage)
			{
				targetPackage = (TilePackage)package;
				identity = targetPackage.Manifest.Identity;
			}
			else
			{
				throw new InvalidDataException ("Unknown package type.");
			}
			if (targetPackage.Manifest.Prerequisites.OSMinVersion > GetSystemVersion ())
				throw new InvalidOperationException ($"Package requires OS version >= {targetPackage.Manifest.Prerequisites.OSMinVersion}, but current OS version is {GetSystemVersion ()}.");
			ProcessorArchitecture requiredArch = identity.ProcessorArchitecture;
			ProcessorArchitecture systemArch = GetSystemArchitecture ();
			if (!IsArchitectureCompatible (requiredArch, systemArch))
				throw new InvalidOperationException (
					$"Package architecture '{requiredArch}' is not compatible with the current system architecture '{systemArch}'.");
			string gadgetRoot = GetGadgetFolder ();
			string familyName = identity.FamilyName;
			string targetDir = Path.Combine (gadgetRoot, familyName);
			bool exists = Directory.Exists (targetDir);
			if (isUpdate && !exists)
			{
				isUpdate = false;
			}
			if (!isUpdate && exists)
				throw new InvalidOperationException ($"Package with FamilyName '{familyName}' is already installed. Use UpdatePackage to replace.");
			string backupDir = null;
			if (exists)
			{
				backupDir = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ());
				BackupDirectory (targetDir, backupDir);
			}
			try
			{
				ExtractPackage (targetPackage.FileStream, targetDir, callback);
				ForceRefresh ();
			}
			catch (Exception ex)
			{
				if (Directory.Exists (targetDir))
					DeleteDirectorySafe (targetDir);
				if (backupDir != null && Directory.Exists (backupDir))
				{
					RestoreBackup (backupDir, targetDir);
				}
				throw new InvalidOperationException ($"Failed to {(isUpdate ? "update" : "install")} package: {ex.Message}", ex);
			}
			finally
			{
				if (backupDir != null && Directory.Exists (backupDir))
					DeleteDirectorySafe (backupDir);
			}
		}
		/// <summary>
		/// 获取当前操作系统的处理器架构（只区分 x86 / x64 / Neutral）。
		/// </summary>
		private ProcessorArchitecture GetSystemArchitecture () => ProcessorDetector.GetCurrentArchitecture ();
		private Sidebar.Version GetSystemVersion () => new Version (
				(ushort)Environment.OSVersion.Version.Major,
				(ushort)Environment.OSVersion.Version.Minor,
				(ushort)Environment.OSVersion.Version.Build,
				(ushort)Environment.OSVersion.Version.Revision
			);
		/// <summary>
		/// 检查包的架构是否与系统架构兼容。
		/// </summary>
		private bool IsArchitectureCompatible (ProcessorArchitecture packageArch, ProcessorArchitecture systemArch)
		{
			if (packageArch == ProcessorArchitecture.Neutral)
				return true;
			if (packageArch == systemArch)
				return true;
			return false;
		}
		/// <summary>
		/// 将 ZipFile 中的所有条目（排除 BlockMap.xml 和 Signature.bin）解压到目标目录。
		/// </summary>
		private void ExtractPackage (ZipFile zip, string targetDir, ProgressCallback callback)
		{
			if (zip == null) throw new ArgumentNullException (nameof (zip));
			if (string.IsNullOrEmpty (targetDir)) throw new ArgumentNullException (nameof (targetDir));
			var entries = zip.Cast<ZipEntry> ()
				.Where (e => e.IsFile && e.Name != "BlockMap.xml" && e.Name != "Signature.bin")
				.ToList ();
			int total = entries.Count;
			int current = 0;
			foreach (ZipEntry entry in entries)
			{
				string relativePath = entry.Name.Replace ('/', '\\');
				string destPath = Path.Combine (targetDir, relativePath);
				string destDir = Path.GetDirectoryName (destPath);
				if (!Directory.Exists (destDir))
					Directory.CreateDirectory (destDir);
				using (Stream stream = zip.GetInputStream (entry))
				using (FileStream fs = File.Create (destPath))
				{
					stream.CopyTo (fs);
				}
				File.SetLastWriteTime (destPath, entry.DateTime);
				current++;
				callback?.Invoke (current, total, (double)current / total);
			}
		}
		private void BackupDirectory (string sourceDir, string backupDir)
		{
			if (!Directory.Exists (sourceDir))
				return;
			Directory.CreateDirectory (backupDir);
			foreach (string dirPath in Directory.GetDirectories (sourceDir, "*", SearchOption.AllDirectories))
			{
				string relative = GetRelativePath (sourceDir, dirPath);
				Directory.CreateDirectory (Path.Combine (backupDir, relative));
			}
			foreach (string filePath in Directory.GetFiles (sourceDir, "*", SearchOption.AllDirectories))
			{
				string relative = GetRelativePath (sourceDir, filePath);
				string dest = Path.Combine (backupDir, relative);
				File.Copy (filePath, dest, true);
			}
		}
		/// <summary>
		/// 恢复备份（将备份目录覆盖回目标目录）。
		/// </summary>
		private void RestoreBackup (string backupDir, string targetDir)
		{
			if (!Directory.Exists (backupDir))
				return;
			if (Directory.Exists (targetDir))
				DeleteDirectorySafe (targetDir);
			foreach (string dirPath in Directory.GetDirectories (backupDir, "*", SearchOption.AllDirectories))
			{
				string relative = GetRelativePath (backupDir, dirPath);
				Directory.CreateDirectory (Path.Combine (targetDir, relative));
			}
			foreach (string filePath in Directory.GetFiles (backupDir, "*", SearchOption.AllDirectories))
			{
				string relative = GetRelativePath (backupDir, filePath);
				string dest = Path.Combine (targetDir, relative);
				string destDir = Path.GetDirectoryName (dest);
				if (!Directory.Exists (destDir))
					Directory.CreateDirectory (destDir);
				File.Copy (filePath, dest, true);
			}
		}
		/// <summary>
		/// 安全删除目录（处理只读文件和被占用文件，若被占用则抛出异常）。
		/// </summary>
		/// <summary>
		/// 将目录移动到回收站（若失败则回退到直接删除，并保持原有异常）。
		/// </summary>
		private void DeleteDirectorySafe (string dir)
		{
			if (!Directory.Exists (dir))
				return;

			try
			{
				// 尝试移动到回收站
				Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory (dir, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
			}
			catch (OperationCanceledException)
			{
				// 用户取消操作时（可能弹出对话框被取消），抛出 IOException 保持接口一致
				throw new IOException ($"User cancelled deletion of directory: {dir}");
			}
			catch (Exception ex)
			{
				// 其他异常（如权限不足、路径过长等），尝试直接删除（保留原有行为）
				try
				{
					// 先移除只读属性
					foreach (string file in Directory.GetFiles (dir, "*", SearchOption.AllDirectories))
					{
						File.SetAttributes (file, FileAttributes.Normal);
					}
					Directory.Delete (dir, true);
				}
				catch (Exception innerEx)
				{
					throw new IOException ($"Failed to delete directory {dir} to recycle bin and also direct deletion failed: {innerEx.Message}", innerEx);
				}
			}
		}
		/// <summary>
		/// 获取相对路径（辅助 BackupDirectory 和 RestoreBackup）。
		/// </summary>
		private string GetRelativePath (string baseDir, string fullPath)
		{
			if (!baseDir.EndsWith (Path.DirectorySeparatorChar.ToString ()))
				baseDir += Path.DirectorySeparatorChar;
			if (fullPath.StartsWith (baseDir, StringComparison.OrdinalIgnoreCase))
				return fullPath.Substring (baseDir.Length);
			Uri baseUri = new Uri (baseDir);
			Uri fullUri = new Uri (fullPath);
			Uri relativeUri = baseUri.MakeRelativeUri (fullUri);
			return Uri.UnescapeDataString (relativeUri.ToString ()).Replace ('/', '\\');
		}
	}
}

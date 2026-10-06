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
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;
using ICSharpCode.SharpZipLib.Zip;
using ImageSource = System.Windows.Media.ImageSource;
using BitmapImage = System.Windows.Media.Imaging.BitmapImage;
using System.Threading.Tasks;
using AnyDiff;
using System.Globalization;

namespace Sidebar
{
	/// <summary>
	/// Compact version type that encodes 4 x 16-bit parts into a 64-bit value:
	/// bits 48..63 = major, 32..47 = minor, 16..31 = build, 0..15 = revision.
	/// </summary>
	public static class PublisherIdHelper
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
		public string Name { get; set; }
		[XmlAttribute ("Publisher")]
		public string Publisher { get; set; }
		[XmlAttribute ("Version")]
		public Version Version { get; set; }
		[XmlAttribute ("ProcessorArchitecture")]
		public ProcessorArchitecture ProcessorArchitecture { get; set; }
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
		public string DisplayName { get; set; }
		[XmlElement ("PublisherDisplayName")]
		public string PublisherDisplayName { get; set; }
		[XmlIgnore]
		public string Publisher => PublisherDisplayName;
		[XmlElement ("Description")]
		public string Description { get; set; }
		[XmlElement ("Logo")]
		public string Logo { get; set; }
		public TileProperties (string displayName, string publisherDisplayName, string description, string logo)
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
		}
		public TileProperties (string displayName, string publisherDisplayName, string logo) :
			this (displayName, publisherDisplayName, null, logo)
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
		}
	}
	[XmlRoot ("Prerequisites")]
	public class TilePrerequisites: ITilePrerequisites, IXmlSerializable
	{
		[XmlElement ("OSMaxVersion")]
		public Version? OSMaxVersion { get; set; }
		[XmlElement ("OSMinVersion")]
		public Version OSMinVersion { get; set; }
		public TilePrerequisites (Version osMin, Version? osMax = null)
		{
			OSMinVersion = osMin;
			OSMaxVersion = osMax;
		}
		private TilePrerequisites ()
		{
			OSMinVersion = new Version ();
			OSMaxVersion = null;
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
							case "OSMaxVersion":
								try { OSMaxVersion = Version.Parse (reader.ReadElementContentAsString ()); }
								catch { OSMaxVersion = null; }
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
			if (OSMaxVersion != null)
				writer.WriteElementString ("OSMaxVersion", OSMaxVersion.ToString ());
		}
	}
	[XmlRoot ("Tile")]
	public class TileVisualElementTile: ITileVisualElementTile, IXmlSerializable
	{
		[XmlAttribute ("MinHeight")]
		public double MinHeight { get; set; }
		[XmlAttribute ("MaxHeight")]
		public double MaxHeight { get; set; }
		[XmlAttribute ("DefaultHeight")]
		public double DefaultHeight { get; set; }
		[XmlAttribute ("CanPinBottom")]
		public bool CanPinBottom { get; set; }
		[XmlAttribute ("Overflow")]
		public TileOverflow Overflow { get; set; }
		public TileVisualElementTile (double minHeight, double maxHeight, double dfHeight, bool canPin, TileOverflow overflow) { MinHeight = minHeight; MaxHeight = maxHeight; DefaultHeight = dfHeight; CanPinBottom = canPin; Overflow = overflow; }
		private TileVisualElementTile () { }
		public XmlSchema GetSchema () => null;
		public void ReadXml (XmlReader reader)
		{
			reader.MoveToContent ();
			string v;
			v = reader.GetAttribute ("MinHeight"); if (v != null) MinHeight = double.Parse (v);
			v = reader.GetAttribute ("MaxHeight"); if (v != null) MaxHeight = double.Parse (v);
			v = reader.GetAttribute ("DefaultHeight"); if (v != null) DefaultHeight = double.Parse (v);
			v = reader.GetAttribute ("CanPinBottom"); if (v != null) CanPinBottom = bool.Parse (v);
			v = reader.GetAttribute ("Overflow"); if (v != null) Overflow = (TileOverflow)Enum.Parse (typeof (TileOverflow), v);
			reader.Skip ();
		}
		public void WriteXml (XmlWriter writer)
		{
			writer.WriteAttributeString ("MinHeight", MinHeight.ToString ());
			writer.WriteAttributeString ("MaxHeight", MaxHeight.ToString ());
			writer.WriteAttributeString ("DefaultHeight", DefaultHeight.ToString ());
			writer.WriteAttributeString ("CanPinBottom", CanPinBottom.ToString ().ToLower ());
			writer.WriteAttributeString ("Overflow", Overflow.ToString ());
		}
	}
	[XmlRoot ("Flyout")]
	public class TileVisualElementFlyout: ITileVisualElementFlyout, IXmlSerializable
	{
		[XmlAttribute ("FlyoutWidth")]
		public double FlyoutWidth { get; set; }
		[XmlAttribute ("FlyoutHeight")]
		public double FlyoutHeight { get; set; }
		[XmlAttribute ("FlyoutCanResize")]
		public bool FlyoutCanResize { get; set; }
		public TileVisualElementFlyout (double width, double height, bool canResize) { FlyoutWidth = width; FlyoutHeight = height; FlyoutCanResize = canResize; }
		private TileVisualElementFlyout () { }
		public XmlSchema GetSchema () => null;
		public void ReadXml (XmlReader reader)
		{
			reader.MoveToContent ();
			string v;
			v = reader.GetAttribute ("FlyoutWidth"); if (v != null) FlyoutWidth = double.Parse (v);
			v = reader.GetAttribute ("FlyoutHeight"); if (v != null) FlyoutHeight = double.Parse (v);
			v = reader.GetAttribute ("FlyoutCanResize"); if (v != null) FlyoutCanResize = bool.Parse (v);
			reader.Skip ();
		}
		public void WriteXml (XmlWriter writer)
		{
			writer.WriteAttributeString ("FlyoutWidth", FlyoutWidth.ToString ());
			writer.WriteAttributeString ("FlyoutHeight", FlyoutHeight.ToString ());
			writer.WriteAttributeString ("FlyoutCanResize", FlyoutCanResize.ToString ().ToLower ());
		}
	}
	[XmlRoot ("VisualElements")]
	public class TileVisualElements: ITileVisualElements, IXmlSerializable
	{
		[XmlAttribute ("DisplayName")]
		public string DisplayName { get; set; }
		[XmlAttribute ("Logo")]
		public string Logo { get; set; }
		[XmlElement ("Tile")]
		public TileVisualElementTile Tile { get; set; }
		[XmlElement ("Flyout")]
		public TileVisualElementFlyout Flyout { get; set; }
		ITileVisualElementTile ITileVisualElements.Tile => Tile;
		ITileVisualElementFlyout ITileVisualElements.Flyout => Flyout;
		public TileVisualElements () { }
		public TileVisualElements (string displayName, string logo, TileVisualElementTile tile, TileVisualElementFlyout flyout) { DisplayName = displayName; Logo = logo; Tile = tile; Flyout = flyout; }
		public XmlSchema GetSchema () => null;
		public void ReadXml (XmlReader reader)
		{
			reader.MoveToContent ();
			DisplayName = reader.GetAttribute ("DisplayName");
			Logo = reader.GetAttribute ("Logo");
			bool isEmpty = reader.IsEmptyElement;
			reader.ReadStartElement ("VisualElements");
			if (!isEmpty)
			{
				while (reader.NodeType != XmlNodeType.EndElement)
				{
					if (reader.NodeType != XmlNodeType.Element) { reader.Read (); continue; }
					switch (reader.LocalName)
					{
						case "Tile":
							{
								var ser = new XmlSerializer (typeof (TileVisualElementTile), new XmlRootAttribute ("Tile"));
								Tile = (TileVisualElementTile)ser.Deserialize (reader);
								break;
							}
						case "Flyout":
							{
								var ser = new XmlSerializer (typeof (TileVisualElementFlyout), new XmlRootAttribute ("Flyout"));
								Flyout = (TileVisualElementFlyout)ser.Deserialize (reader);
								break;
							}
						default:
							reader.Skip ();
							break;
					}
				}
				reader.ReadEndElement ();
			}
		}
		public void WriteXml (XmlWriter writer)
		{
			if (!string.IsNullOrEmpty (DisplayName)) writer.WriteAttributeString ("DisplayName", DisplayName);
			if (!string.IsNullOrEmpty (Logo)) writer.WriteAttributeString ("Logo", Logo);
			var ns = new XmlSerializerNamespaces ();
			ns.Add ("", "");
			if (Tile != null)
			{
				var tileSer = new XmlSerializer (typeof (TileVisualElementTile), new XmlRootAttribute ("Tile"));
				tileSer.Serialize (writer, Tile, ns);
			}
			if (Flyout != null)
			{
				var flySer = new XmlSerializer (typeof (TileVisualElementFlyout), new XmlRootAttribute ("Flyout"));
				flySer.Serialize (writer, Flyout, ns);
			}
		}
	}
	[XmlRoot ("Application")]
	public class TileApplication: ITileApplication, IXmlSerializable
	{
		[XmlAttribute ("Id")]
		public string Id { get; set; }
		[XmlAttribute ("Executable")]
		public string Executable { get; set; }
		[XmlAttribute ("EntryPoint")]
		public string EntryPoint { get; set; }
		[XmlAttribute ("HasFlyout")]
		public bool HasFlyout { get; set; }
		[XmlAttribute ("HasProperties")]
		public bool HasProperties { get; set; }
		[XmlElement ("VisualElements")]
		public TileVisualElements VisualElements { get; set; }
		ITileVisualElements ITileApplication.VisualElements => VisualElements;
		public TileApplication () { }
		public TileApplication (string id, string exe, string entryPoint, bool hasFlyout, bool hasProps, TileVisualElements ve)
		{
			Id = id; Executable = exe; EntryPoint = entryPoint;
			HasFlyout = hasFlyout; HasProperties = hasProps; VisualElements = ve;
		}
		public XmlSchema GetSchema () => null;
		public void ReadXml (XmlReader reader)
		{
			reader.MoveToContent ();
			Id = reader.GetAttribute ("Id");
			Executable = reader.GetAttribute ("Executable");
			EntryPoint = reader.GetAttribute ("EntryPoint");
			string v;
			v = reader.GetAttribute ("HasFlyout"); if (v != null) HasFlyout = bool.Parse (v);
			v = reader.GetAttribute ("HasProperties"); if (v != null) HasProperties = bool.Parse (v);
			bool isEmpty = reader.IsEmptyElement;
			reader.ReadStartElement ("Application");
			if (!isEmpty)
			{
				while (reader.NodeType != XmlNodeType.EndElement)
				{
					if (reader.NodeType != XmlNodeType.Element) { reader.Read (); continue; }
					switch (reader.LocalName)
					{
						case "VisualElements":
							{
								var ser = new XmlSerializer (typeof (TileVisualElements), new XmlRootAttribute ("VisualElements"));
								VisualElements = (TileVisualElements)ser.Deserialize (reader);
								break;
							}
						default:
							reader.Skip ();
							break;
					}
				}
				reader.ReadEndElement ();
			}
		}
		public void WriteXml (XmlWriter writer)
		{
			if (!string.IsNullOrEmpty (Id)) writer.WriteAttributeString ("Id", Id);
			if (!string.IsNullOrEmpty (Executable)) writer.WriteAttributeString ("Executable", Executable);
			if (!string.IsNullOrEmpty (EntryPoint)) writer.WriteAttributeString ("EntryPoint", EntryPoint);
			writer.WriteAttributeString ("HasFlyout", HasFlyout.ToString ().ToLower ());
			writer.WriteAttributeString ("HasProperties", HasProperties.ToString ().ToLower ());
			if (VisualElements != null)
			{
				var ns = new XmlSerializerNamespaces ();
				ns.Add ("", "");
				var veSer = new XmlSerializer (typeof (TileVisualElements), new XmlRootAttribute ("VisualElements"));
				veSer.Serialize (writer, VisualElements, ns);
			}
		}
	}
	[XmlRoot ("Manifest")]
	public class TileManifest: ITileManifest, IXmlSerializable
	{
		private TileIdentity identity;
		private TileProperties properties;
		private TilePrerequisites prerequisites;
		private List<TileApplication> applications = new List<TileApplication> ();
		[XmlElement ("Identity")]
		public TileIdentity Identity => identity;
		[XmlElement ("Properties")]
		public TileProperties Properties => properties;
		[XmlElement ("Prerequisites")]
		public TilePrerequisites Prerequisites => prerequisites;
		[XmlElement ("Applications")]
		public List<TileApplication> Applications => applications;
		ITileIdentity ITileManifest.Identity => identity;
		ITileProperties ITileManifest.Properties => properties;
		ITilePrerequisites ITileManifest.Prerequisites => prerequisites;
		private IList<ITileApplication> tmlcache = null;
		IList<ITileApplication> ITileManifest.Applications => tmlcache ?? (tmlcache = tmlcache ?? applications.Cast<ITileApplication> ().ToList ());
		public uint Version { get; set; } = 2;
		public TileManifest (TileIdentity id, TileProperties prop, TilePrerequisites pre, List<TileApplication> apps)
		{
			identity = id;
			properties = prop;
			prerequisites = pre;
			applications = apps ?? new List<TileApplication> ();
		}
		public TileManifest (TileIdentity id, TileProperties prop, TilePrerequisites pre, params TileApplication [] apps)
			: this (id, prop, pre, apps == null ? new List<TileApplication> () : new List<TileApplication> (apps)) { }
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
			applications = new List<TileApplication> ();
			while (reader.NodeType != XmlNodeType.EndElement)
			{
				if (reader.NodeType != XmlNodeType.Element) { reader.Read (); continue; }
				switch (reader.LocalName)
				{
					case "Identity":
						{
							var ser = new XmlSerializer (typeof (TileIdentity), new XmlRootAttribute ("Identity"));
							identity = (TileIdentity)ser.Deserialize (reader);
							break;
						}
					case "Properties":
						{
							var ser = new XmlSerializer (typeof (TileProperties), new XmlRootAttribute ("Properties"));
							properties = (TileProperties)ser.Deserialize (reader);
							break;
						}
					case "Prerequisites":
						{
							var ser = new XmlSerializer (typeof (TilePrerequisites), new XmlRootAttribute ("Prerequisites"));
							prerequisites = (TilePrerequisites)ser.Deserialize (reader);
							break;
						}
					case "Applications":
						{
							bool isEmpty = reader.IsEmptyElement;
							reader.ReadStartElement ("Applications");
							if (!isEmpty)
							{
								while (reader.NodeType != XmlNodeType.EndElement)
								{
									if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Application")
									{
										var ser = new XmlSerializer (typeof (TileApplication), new XmlRootAttribute ("Application"));
										applications.Add ((TileApplication)ser.Deserialize (reader));
									}
									else reader.Skip ();
								}
								reader.ReadEndElement ();
							}
							break;
						}
					default:
						reader.Skip ();
						break;
				}
			}
			reader.ReadEndElement ();
		}
		public void WriteXml (XmlWriter writer)
		{
			writer.WriteAttributeString ("Version", Version.ToString ());
			var ns = new XmlSerializerNamespaces ();
			ns.Add ("", "http://schemas.windowsmodern.com/sidebar/tile/2026/09/v2");
			var idSer = new XmlSerializer (typeof (TileIdentity), new XmlRootAttribute ("Identity"));
			var propSer = new XmlSerializer (typeof (TileProperties), new XmlRootAttribute ("Properties"));
			var preSer = new XmlSerializer (typeof (TilePrerequisites), new XmlRootAttribute ("Prerequisites"));
			var appSer = new XmlSerializer (typeof (TileApplication), new XmlRootAttribute ("Application"));
			idSer.Serialize (writer, identity, ns);
			propSer.Serialize (writer, properties, ns);
			if (prerequisites != null)
				preSer.Serialize (writer, prerequisites, ns);
			writer.WriteStartElement ("Applications");
			if (applications != null)
			{
				foreach (var app in applications)
					appSer.Serialize (writer, app, ns);
			}
			writer.WriteEndElement ();
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
	/// <summary>
	/// 将版本 1（RailStyle/GridStyle）的清单转换为版本 2（Applications/Tile/Flyout）的清单。
	/// </summary>
	public static class TileManifestV1ToV2Converter
	{
		/// <summary>
		/// 检测指定清单文档的版本。
		/// 没有 Version 属性或 Version="1" 都视为版本 1；其余视为版本 2。
		/// </summary>
		public static uint DetectVersion (XDocument doc)
		{
			var root = doc.Root;
			if (root == null) return 0;
			var attr = root.Attribute ("Version");
			if (attr == null) return 1;
			uint v;
			if (!uint.TryParse (attr.Value, out v)) return 1;
			return v;
		}
		public static uint DetectVersion (string filePath)
		{
			return DetectVersion (XDocument.Load (filePath));
		}
		public static uint DetectVersion (Stream stream)
		{
			return DetectVersion (XDocument.Load (stream));
		}
		/// <summary>
		/// 从文件读取清单；若为版本 1 则自动转换为版本 2，否则按版本 2 解析。
		/// </summary>
		public static TileManifest ConvertFromFile (string filePath)
		{
			var doc = XDocument.Load (filePath);
			uint v = DetectVersion (doc);
			if (v <= 1)
				return Convert (doc);
			using (var ms = new MemoryStream ())
			{
				doc.Save (ms);
				ms.Position = 0;
				return TileManifest.FromStream (ms);
			}
		}
		public static TileManifest ConvertFromStream (Stream s)
		{
			var doc = XDocument.Load (s);
			uint v = DetectVersion (doc);
			if (v <= 1)
				return Convert (doc);
			using (var ms = new MemoryStream ())
			{
				doc.Save (ms);
				ms.Position = 0;
				return TileManifest.FromStream (ms);
			}
		}
		/// <summary>
		/// 将版本 1 的 XDocument 转换为版本 2 的 TileManifest。
		/// </summary>
		public static TileManifest Convert (XDocument doc)
		{
			if (doc?.Root == null)
				throw new ArgumentException ("Manifest document is empty.", nameof (doc));

			var root = doc.Root;
			// 版本 ≥2 的直接反序列化，不做转换
			uint version = DetectVersion (doc);
			if (version > 1)
				throw new InvalidOperationException ($"Document version is {version}, only version 1 needs conversion.");

			// ---- Identity ----
			var idEl = root.Element ("Identity");
			var identity = new TileIdentity (
				(string)idEl?.Attribute ("Name"),
				(string)idEl?.Attribute ("Publisher"),
				ParseVersion ((string)idEl?.Attribute ("Version")),
				ParseArch ((string)idEl?.Attribute ("ProcessorArchitecture"))
			);

			// ---- Properties ----
			var propEl = root.Element ("Properties");
			var properties = new TileProperties (
				(string)propEl?.Element ("DisplayName"),
				(string)propEl?.Element ("PublisherDisplayName"),
				(string)propEl?.Element ("Description"),
				(string)propEl?.Element ("Logo")
			);

			// ---- Prerequisites ----
			var preEl = root.Element ("Prerequisites");
			var prerequisites = new TilePrerequisites (
				ParseVersion ((string)preEl?.Element ("OSMinVersion")),
				null
			);

			// ---- VisualElements / RailStyle ----
			var veEl = root.Element ("VisualElements");
			var railEl = veEl?.Element ("RailStyle");

			// GridStyle 在版本 2 中已废弃，忽略。
			var tile = new TileVisualElementTile (
				ParseDouble ((string)railEl?.Element ("MinHeight")),
				ParseDouble ((string)railEl?.Element ("MaxHeight")),
				ParseDouble ((string)railEl?.Element ("DefaultHeight")),
				ParseBool ((string)railEl?.Element ("CanPinBottom")),
				ParseOverflow ((string)railEl?.Element ("Overflow"))
			);
			var flyout = new TileVisualElementFlyout (
				Math.Max (0, ParseDouble ((string)railEl?.Element ("FlyoutWidth")) - 8),
				Math.Max (0, ParseDouble ((string)railEl?.Element ("FlyoutHeight")) - 21),
				ParseBool ((string)railEl?.Element ("FlyoutCanResize"))
			);
			var visualElements = new TileVisualElements (
				(string)railEl?.Element ("DisplayName"),
				(string)railEl?.Element ("Logo"),
				tile,
				flyout
			);

			// ---- Application ----
			// 版本 1 没有 Application 概念，创建一个默认应用，Id 固定为 "App"。
			var application = new TileApplication (
				"App",
				"Tile.dll",
				null,
				ParseBool ((string)railEl?.Element ("TileHasFlyout")),
				ParseBool ((string)railEl?.Element ("TileHasProperties")),
				visualElements
			);

			return new TileManifest (identity, properties, prerequisites, application);
		}
		#region Parse helpers
		private static Sidebar.Version ParseVersion (string s)
		{
			if (string.IsNullOrWhiteSpace (s)) return new Version ();
			return Version.Parse (s);
		}
		private static int ParseInt (string s)
		{
			int v;
			return int.TryParse (s, out v) ? v : 0;
		}
		private static bool ParseBool (string s)
		{
			bool v;
			return bool.TryParse (s, out v) && v;
		}
		private static ProcessorArchitecture ParseArch (string s)
		{
			ProcessorArchitecture a;
			if (Enum.TryParse (s, out a)) return a;
			return ProcessorArchitecture.Neutral;
		}
		private static TileOverflow ParseOverflow (string s)
		{
			TileOverflow o;
			if (Enum.TryParse (s, out o)) return o;
			return default (TileOverflow);
		}
		/// <summary>
		/// 解析 XML 属性里的 double。兼容旧版整数字符串（"28"）和新版小数字符串（"28.5"）。
		/// 使用 InvariantCulture，避免区域设置（如 fr-FR 的逗号）导致解析失败。
		/// </summary>
		public static double ParseDouble (string s)
		{
			if (string.IsNullOrWhiteSpace (s)) return 0;
			double v;
			if (double.TryParse (s, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
				return v;
			int end = 0;
			while (end < s.Length && (char.IsDigit (s [end]) || s [end] == '.'
				|| s [end] == '-' || s [end] == '+' || s [end] == 'e' || s [end] == 'E'))
				end++;
			if (end > 0 && double.TryParse (s.Substring (0, end),
				NumberStyles.Float, CultureInfo.InvariantCulture, out v))
				return v;
			return 0;
		}
		#endregion
	}
	public partial class TileStorage
	{
		public string FolderPath { get; }
		public ProgramFolder TileFolder { get; }
		public ProgramFolder TileCurrentUserFolder { get; }
		public TileManifest Manifest { get; }
		public string FamilyName => Manifest.Identity.FamilyName;
		public List <TileApplication> Applications => Manifest.Applications;
		public TileStorage (string folderPath)
		{
			var infoFile = Path.Combine (folderPath, "Manifest.xml");
			if (File.Exists (folderPath) || !File.Exists (infoFile))
				throw new ArgumentException ("The instance of Tile needs the folder stored tile files.");
			FolderPath = folderPath;
			Manifest = TileManifest.FromFile (infoFile);
			TileFolder = ProgramFolder.CreateFromPath (folderPath);
			TileCurrentUserFolder = ProgramFolder.CreateFromPath (Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "Windows Modern\\Sidebar\\Tiles", Manifest.Identity.FamilyName));
			foreach (var a in Manifest.Applications)
			{
				var dllfile = Path.Combine (folderPath, a.Executable);
				if (!File.Exists (dllfile))
					throw new ArgumentException ("The instance of Tile needs the folder stored tile files.");
				Items.Add (new TileApplicationStorage (this, a));
			}
		}
		public TileStorage (string folderPath, ITileApplication application)
		{
			var infoFile = Path.Combine (folderPath, "Manifest.xml");
			if (File.Exists (folderPath) || !File.Exists (infoFile))
				throw new ArgumentException ("The instance of Tile needs the folder stored tile files.");
			FolderPath = folderPath;
			Manifest = TileManifest.FromFile (infoFile);
			foreach (var a in Manifest.Applications)
			{
				var dllfile = Path.Combine (folderPath, a.Executable);
				if (!File.Exists (dllfile))
					throw new ArgumentException ("The instance of Tile needs the folder stored tile files.");
				Items.Add (new TileApplicationStorage (this, a));
			}
			TileFolder = ProgramFolder.CreateFromPath (folderPath);
			TileCurrentUserFolder = ProgramFolder.CreateFromPath (Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "Windows Modern\\Sidebar\\Tiles", Manifest.Identity.FamilyName, application.Id));
		}
		public List<TileApplicationStorage> Items { get; private set; } = new List<TileApplicationStorage> ();
		public string DisplayName
		{
			get
			{
				var ret = Manifest?.Properties?.DisplayName;
				ret = TileFolder?.StringResources?.SuitableResource (ret, ret) ?? ret;
				return ret;
			}
		}
		public string Publisher
		{
			get
			{
				var ret = Manifest?.Properties?.Publisher;
				ret = TileFolder?.StringResources?.SuitableResource (ret, ret) ?? ret;
				return ret;
			}
		}
		public string Description
		{
			get
			{
				var ret = Manifest?.Properties?.Description;
				ret = TileFolder?.StringResources?.SuitableResource (ret, ret) ?? ret;
				return ret;
			}
		}
		public string Logo
		{
			get
			{
				var ret = Manifest?.Properties?.Logo;
				ret = TileFolder?.FileResources?.SuitableResource (ret, ret) ?? ret;
				try
				{
					ret = Path.Combine (TileFolder.FolderPath, ret);
					if (File.Exists (ret)) return ret;
					ret = Path.Combine (TileFolder.FolderPath, Manifest?.Properties?.Logo);
					if (File.Exists (ret)) return ret;
					return null;
				}
				catch
				{
					return null;
				}
				return ret;
			}
		}
		private ImageSource imgsrc = null;
		public ImageSource LogoImage
		{
			get
			{
				if (imgsrc != null) return imgsrc;
				var path = Logo;
				if (string.IsNullOrEmpty (path) || !File.Exists (path)) return null;
				try
				{
					var bmp = new BitmapImage ();
					using (var fs = new FileStream (path, FileMode.Open, FileAccess.Read, FileShare.Read))
					{
						bmp.BeginInit ();
						bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
						//bmp.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
						bmp.StreamSource = fs;
						bmp.EndInit ();
					}
					bmp.Freeze ();
					imgsrc = bmp;
					return imgsrc;
				}
				catch
				{
					return null;
				}
			}
		}
		public override string ToString ()
		{
			if (Manifest == null) return base.ToString ();
			else return Manifest.Identity.FullName;
		}
		public override bool Equals (object obj)
		{
			if (obj is TileStorage)
			{
				return FamilyName.NEquals ((obj as TileStorage)?.FamilyName);
			}
			return base.Equals (obj);
		}
		public override int GetHashCode () => FamilyName.GetHashCode ();
	}
	public class TileApplicationStorage
	{
		public TileStorage Storage { get; }
		public ProgramFolder AppCurrentUserFolder { get; }
		public ITileApplication Infomation { get; }
		public string Id => Infomation.Id;
		public string FamilyName => Storage.Manifest.Identity.FamilyName;
		public string UserModelId => $"{FamilyName}!{Infomation.Id}";
		public override string ToString () => UserModelId;
		public override bool Equals (object obj)
		{
			if (obj is TileApplicationStorage)
			{
				return UserModelId.NEquals ((obj as TileApplicationStorage)?.UserModelId);
			}
			return base.Equals (obj);
		}
		public override int GetHashCode () => UserModelId.NNormalize ().GetHashCode ();
		public TileApplicationStorage (TileStorage ts, TileApplication ta)
		{
			Storage = ts;
			Infomation = ta;
			AppCurrentUserFolder = ProgramFolder.CreateFromPath (Path.Combine (Storage.TileCurrentUserFolder.FolderPath, Infomation.Id));
		}
		public string DisplayName
		{
			get
			{
				var ret = Infomation?.VisualElements?.DisplayName;
				ret = Storage?.TileFolder?.StringResources?.SuitableResource (ret, ret) ?? ret;
				return ret;
			}
		}
		public string Logo
		{
			get
			{
				var ret = Infomation?.VisualElements?.Logo;
				ret = Storage?.TileFolder?.FileResources?.SuitableResource (ret, ret) ?? ret;
				try
				{
					ret = Path.Combine (Storage?.TileFolder.FolderPath, ret);
					if (File.Exists (ret)) return ret;
					ret = Path.Combine (Storage?.TileFolder.FolderPath, Infomation?.VisualElements?.Logo);
					if (File.Exists (ret)) return ret;
					return null;
				}
				catch
				{
					return null;
				}
				return ret;
			}
		}
		private ImageSource imgsrc = null;
		public ImageSource LogoImage
		{
			get
			{
				if (imgsrc != null) return imgsrc;
				var path = Logo;
				if (string.IsNullOrEmpty (path) || !File.Exists (path)) return null;
				try
				{
					var bmp = new BitmapImage ();
					using (var fs = new FileStream (path, FileMode.Open, FileAccess.Read, FileShare.Read))
					{
						bmp.BeginInit ();
						bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
						//bmp.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
						bmp.StreamSource = fs;
						bmp.EndInit ();
					}
					bmp.Freeze ();
					imgsrc = bmp;
					return imgsrc;
				}
				catch
				{
					return null;
				}
			}
		}
	}
	public class TileManager: IDisposable, INotifyPropertyChanged
	{
		public string [] Roots { get { return _roots; } }
		private readonly string [] _roots;
		private readonly MultiRootWatcher _watcher;
		private readonly object _cacheLock = new object ();
		private int _refreshing;
		private int _pending;
		private volatile bool _disposed;
		private Dictionary<Guid, TileStorage> _cacheById = new Dictionary<Guid, TileStorage> ();
		private Dictionary<string, TileStorage> _cacheByFullName = new Dictionary<string, TileStorage> (StringComparer.OrdinalIgnoreCase);
		private Dictionary<string, TileStorage> _cacheByFamilyName = new Dictionary<string, TileStorage> (StringComparer.OrdinalIgnoreCase);
		public ObservableCollection<TileStorage> Tiles { get; private set; }
		public ObservableCollection<TileApplicationStorage> Applications { get; private set; }
		public TileManager (params string [] roots)
		{
			Tiles = new ObservableCollection<TileStorage> ();
			Applications = new ObservableCollection<TileApplicationStorage> ();
			_roots = roots != null
				? Array.FindAll (roots, r => !string.IsNullOrWhiteSpace (r))
				: new string [0];
			_watcher = new MultiRootWatcher (_roots, TimeSpan.FromMilliseconds (500), OnWatched);
			_watcher.Start ();
			ForceRefresh ();
		}
		public TileManager () : this (DefaultRoots ()) { }
		private static string [] DefaultRoots ()
		{
			return new string [] {
			Path.Combine (ProgramFolder.GlobalFolder.FolderPath, "Gadgets"),
			Path.Combine (ProgramFolder.CurrentUserFolder.FolderPath, "Gadgets"),
		};
		}
		private void OnWatched () { ForceRefresh (); }
		public void ForceRefresh ()
		{
			if (_disposed) return;
			if (Interlocked.CompareExchange (ref _refreshing, 1, 0) != 0)
			{
				Interlocked.Exchange (ref _pending, 1);
				return;
			}
			Task.Factory.StartNew (RefreshLoop, CancellationToken.None,
				TaskCreationOptions.None, TaskScheduler.Default);
		}
		private void RefreshLoop ()
		{
			try
			{
				do
				{
					Interlocked.Exchange (ref _pending, 0);
					if (_disposed) return;
					try { RefreshCore (); }
					catch { }
				}
				while (!_disposed && Interlocked.CompareExchange (ref _pending, 0, 0) != 0);
			}
			finally
			{
				Interlocked.Exchange (ref _refreshing, 0);
				if (!_disposed && Interlocked.CompareExchange (ref _pending, 0, 0) != 0)
					ForceRefresh ();
			}
		}
		private void RefreshCore ()
		{
			var desiredTiles = ScanAllTiles ();
			var desiredApps = new List<TileApplicationStorage> ();
			foreach (var t in desiredTiles)
				foreach (var item in t.Items)
					desiredApps.Add (item);
			var newById = desiredTiles.ToDictionary (t => t.Manifest.Identity.Id);
			var newByFull = desiredTiles.ToDictionary (t => t.Manifest.Identity.FullName, StringComparer.OrdinalIgnoreCase);
			var newByFamily = desiredTiles.ToDictionary (t => t.FamilyName, StringComparer.OrdinalIgnoreCase);
			UiDispatch.Run (() =>
			{
				if (_disposed) return;
				lock (_cacheLock)
				{
					_cacheById = newById;
					_cacheByFullName = newByFull;
					_cacheByFamilyName = newByFamily;
				}
				ObservableSync.Sync (
					Tiles,
					desiredTiles,
					t => t.FamilyName,
					StringComparer.OrdinalIgnoreCase,
					TilesContentEqual);
				ObservableSync.Sync (
					Applications,
					desiredApps,
					a => a.UserModelId,
					StringComparer.OrdinalIgnoreCase,
					AppsContentEqual);
				OnPropertyChanged (nameof (Tiles));
				OnPropertyChanged (nameof (Applications));
			});
		}
		private List<TileStorage> ScanAllTiles ()
		{
			var osVer = GetSystemVersion ();
			var sysArch = GetSystemArchitecture ();
			var candidates = new Dictionary<string, List<TileStorage>> (StringComparer.OrdinalIgnoreCase);
			foreach (var root in _roots)
			{
				if (string.IsNullOrEmpty (root) || !Directory.Exists (root)) continue;
				IEnumerable<string> subdirs;
				try { subdirs = Directory.EnumerateDirectories (root); }
				catch { continue; }
				foreach (var dir in subdirs)
				{
					try
					{
						var t = new TileStorage (dir);
						if (!IsTileCompatible (t, osVer, sysArch)) continue;
						List<TileStorage> bucket;
						if (!candidates.TryGetValue (t.FamilyName, out bucket))
							candidates [t.FamilyName] = bucket = new List<TileStorage> ();
						bucket.Add (t);
					}
					catch { }
				}
			}
			var result = new List<TileStorage> ();
			foreach (var kv in candidates)
			{
				var best = kv.Value [0];
				for (int i = 1; i < kv.Value.Count; i++)
				{
					if (kv.Value [i].Manifest.Identity.Version > best.Manifest.Identity.Version)
						best = kv.Value [i];
				}
				result.Add (best);
			}
			return result;
		}
		private bool IsTileCompatible (TileStorage t, Sidebar.Version osVer, ProcessorArchitecture sysArch)
		{
			var pkgArch = t.Manifest.Identity.ProcessorArchitecture;
			if (!IsArchitectureCompatible (pkgArch, sysArch)) return false;
			var pre = t.Manifest.Prerequisites;
			if (pre == null) return true;
			if (pre.OSMinVersion > osVer) return false;
			if (pre.OSMaxVersion != null && osVer > pre.OSMaxVersion) return false;
			return true;
		}
		private static bool TilesContentEqual (TileStorage a, TileStorage b)
		{
			if (ReferenceEquals (a, b)) return true;
			if (a == null || b == null) return false;
			if (a.Manifest == null || b.Manifest == null) return a.Manifest == b.Manifest;
			if (a.Manifest.Identity == null || b.Manifest.Identity == null)
				return a.Manifest.Identity == b.Manifest.Identity;
			// 版本不同必然需要替换
			if (a.Manifest.Identity.Version != b.Manifest.Identity.Version) return false;
			// 展示相关属性：只比较真正影响 UI 的部分
			if (!string.Equals (SafeName (a), SafeName (b), StringComparison.Ordinal)) return false;
			if (!string.Equals (SafeLogo (a), SafeLogo (b), StringComparison.Ordinal)) return false;
			return true;
		}
		private static string SafeName (TileStorage t)
		{
			try { return t.DisplayName ?? string.Empty; }
			catch { return string.Empty; }
		}
		private static string SafeLogo (TileStorage t)
		{
			try { return t.Logo ?? string.Empty; }
			catch { return string.Empty; }
		}
		private static bool AppsContentEqual (TileApplicationStorage a, TileApplicationStorage b)
		{
			if (ReferenceEquals (a, b)) return true;
			if (a == null || b == null) return false;
			return ReferenceEquals (a.Storage, b.Storage);
		}
		public TileStorage GetById (Guid id)
		{
			TileStorage t;
			lock (_cacheLock) _cacheById.TryGetValue (id, out t);
			return t;
		}
		public TileStorage GetByFullName (string fullname)
		{
			TileStorage t;
			lock (_cacheLock) _cacheByFullName.TryGetValue (fullname, out t);
			return t;
		}
		public TileStorage GetByFamilyName (string familyName)
		{
			TileStorage t;
			lock (_cacheLock) _cacheByFamilyName.TryGetValue (familyName, out t);
			return t;
		}
		public TileStorage GetByIdentity (string name, string publisher)
		{
			var familyName = string.Format ("{0}_{1}", name, PublisherIdHelper.GetPublisherId (publisher));
			return GetByFamilyName (familyName);
		}
		public TileApplicationStorage GetApplicationByUserModelId (string aumid)
		{
			if (string.IsNullOrEmpty (aumid)) return null;
			return Applications.FirstOrDefault (a =>
				string.Equals (a.UserModelId, aumid, StringComparison.OrdinalIgnoreCase));
		}
		public void InstallPackage (string filePath, TilePackageWriteManager.ProgressCallback callback = null)
		{
			InstallOrUpdateInternal (filePath, false, callback);
		}
		public void UpdatePackage (string filePath, TilePackageWriteManager.ProgressCallback callback = null)
		{
			InstallOrUpdateInternal (filePath, true, callback);
		}
		private void InstallOrUpdateInternal (string filePath, bool isUpdate, TilePackageWriteManager.ProgressCallback callback)
		{
			if (string.IsNullOrEmpty (filePath))
				throw new ArgumentNullException (nameof (filePath));
			if (!File.Exists (filePath))
				throw new FileNotFoundException (string.Format ("Package file not found: {0}", filePath));
			TilePackageBase package = TilePackageReadManager.GetPackage (filePath);
			if (package == null)
				throw new InvalidDataException ("Failed to load package.");
			TilePackage targetPackage = null;
			TileIdentity identity = null;
			TilePackageBundle bundle = package as TilePackageBundle;
			var osVer = GetSystemVersion ();
			if (bundle != null)
			{
				ProcessorArchitecture currentArch = GetSystemArchitecture ();
				TilePackage selected = null;
				var byMin = new SortedDictionary<Version, List<TilePackage>> (new ReverseVersionComparer ());
				foreach (var p in bundle.Packages)
				{
					var min = p.Manifest.Prerequisites.OSMinVersion;
					List<TilePackage> bucket;
					if (!byMin.TryGetValue (min, out bucket))
						byMin [min] = bucket = new List<TilePackage> ();
					bucket.Add (p);
				}
				foreach (var kv in byMin)
				{
					if (kv.Key > osVer)
						continue;
					var candidates = kv.Value.Where (p => {
						var max = p.Manifest.Prerequisites.OSMaxVersion;
						return max == null || osVer <= max;
					}).ToList ();
					if (candidates.Count == 0)
						continue;
					var match = candidates.FirstOrDefault (
						p => p.Manifest.Identity.ProcessorArchitecture == currentArch);
					if (match == null)
						match = candidates.FirstOrDefault (
							p => p.Manifest.Identity.ProcessorArchitecture == ProcessorArchitecture.Neutral);
					if (match != null)
					{
						selected = match;
						break;
					}
				}
				if (selected == null)
					throw new InvalidOperationException (string.Format (
						"No package in bundle supports the current system architecture ({0}).", currentArch));
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
			var pre = targetPackage.Manifest.Prerequisites;
			if (pre.OSMinVersion > osVer)
				throw new InvalidOperationException (string.Format (
					"Package requires OS version >= {0}, but current OS version is {1}.", pre.OSMinVersion, osVer));
			if (pre.OSMaxVersion != null && osVer > pre.OSMaxVersion)
				throw new InvalidOperationException (string.Format (
					"Package requires OS version <= {0}, but current OS version is {1}.", pre.OSMaxVersion, osVer));
			ProcessorArchitecture requiredArch = identity.ProcessorArchitecture;
			ProcessorArchitecture systemArch = GetSystemArchitecture ();
			if (!IsArchitectureCompatible (requiredArch, systemArch))
				throw new InvalidOperationException (string.Format (
					"Package architecture '{0}' is not compatible with the current system architecture '{1}'.",
					requiredArch, systemArch));
			string gadgetRoot;
			if (_roots.Length > 0)
				gadgetRoot = _roots [_roots.Length - 1];
			else
				throw new InvalidOperationException ("No Gadgets root configured.");
			Directory.CreateDirectory (gadgetRoot);
			string familyName = identity.FamilyName;
			string targetDir = Path.Combine (gadgetRoot, familyName);
			bool exists = Directory.Exists (targetDir);
			if (isUpdate && !exists)
				isUpdate = false;
			if (!isUpdate && exists)
				throw new InvalidOperationException (string.Format (
					"Package with FamilyName '{0}' is already installed. Use UpdatePackage to replace.", familyName));
			string backupDir = null;
			if (exists)
			{
				backupDir = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ());
				BackupDirectory (targetDir, backupDir);
			}
			try
			{
				ExtractPackage (targetPackage.FileStream, targetDir, callback);
				var _ = new TileStorage (targetDir);
				ForceRefresh ();
			}
			catch (Exception ex)
			{
				if (Directory.Exists (targetDir))
					DeleteDirectorySafe (targetDir);
				if (backupDir != null && Directory.Exists (backupDir))
					RestoreBackup (backupDir, targetDir);
				throw new InvalidOperationException (string.Format (
					"Failed to {0} package: {1}", isUpdate ? "update" : "install", ex.Message), ex);
			}
			finally
			{
				if (backupDir != null && Directory.Exists (backupDir))
					DeleteDirectorySafe (backupDir);
			}
		}
		private ProcessorArchitecture GetSystemArchitecture ()
		{
			return ProcessorDetector.GetCurrentArchitecture ();
		}
		private Sidebar.Version GetSystemVersion ()
		{
			return new Version (
				(ushort)Environment.OSVersion.Version.Major,
				(ushort)Environment.OSVersion.Version.Minor,
				(ushort)Environment.OSVersion.Version.Build,
				(ushort)Environment.OSVersion.Version.Revision);
		}
		private bool IsArchitectureCompatible (ProcessorArchitecture packageArch, ProcessorArchitecture systemArch)
		{
			if (packageArch == ProcessorArchitecture.Neutral) return true;
			if (packageArch == systemArch) return true;
			return false;
		}
		private void ExtractPackage (ZipFile zip, string targetDir, TilePackageWriteManager.ProgressCallback callback)
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
				if (callback != null) callback (current, total, (double)current / total);
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
		private void DeleteDirectorySafe (string dir)
		{
			if (!Directory.Exists (dir))
				return;
			try
			{
				Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory (dir,
					Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
					Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
			}
			catch (OperationCanceledException)
			{
				throw new IOException (string.Format ("User cancelled deletion of directory: {0}", dir));
			}
			catch
			{
				try
				{
					foreach (string file in Directory.GetFiles (dir, "*", SearchOption.AllDirectories))
						File.SetAttributes (file, FileAttributes.Normal);
					Directory.Delete (dir, true);
				}
				catch (Exception innerEx)
				{
					throw new IOException (string.Format ("Failed to delete directory {0}: {1}", dir, innerEx.Message), innerEx);
				}
			}
		}
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
		public void Dispose ()
		{
			if (_disposed) return;
			_disposed = true;
			_watcher.Dispose ();
		}
		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged (string propertyName = null)
		{
			var h = PropertyChanged;
			if (h != null) h (this, new PropertyChangedEventArgs (propertyName));
		}
	}
}
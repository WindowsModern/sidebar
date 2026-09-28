using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

namespace Sidebar
{
	[ComVisible (true)]
	[XmlRoot ("Identity")]
	public interface ITileIdentity
	{
		[XmlAttribute ("Name")]
		string Name { get; }
		[XmlAttribute ("Publisher")]
		string Publisher { get; }
		[XmlAttribute ("Version")]
		Version Version { get; }
		[XmlAttribute ("ProcessorArchitecture")]
		ProcessorArchitecture ProcessorArchitecture { get; }
		[XmlIgnore]
		string PublisherId { get; }
		[XmlIgnore]
		string FamilyName { get; }
		[XmlIgnore]
		string FullName { get; }
		[XmlIgnore]
		Guid Id { get; }
	}
	[ComVisible (true)]
	[XmlRoot ("Properties")]
	public interface ITileProperties
	{
		[XmlElement ("DisplayName")]
		string DisplayName { get; }
		[XmlElement ("PublisherDisplayName")]
		string PublisherDisplayName { get; }
		[XmlIgnore]
		string Publisher { get; }
		[XmlElement ("Description")]
		string Description { get; }
		[XmlElement ("Logo")]
		string Logo { get; }
	}
	[ComVisible (true)]
	[XmlRoot ("Prerequisites")]
	public interface ITilePrerequisites
	{
		[XmlElement ("OSMinVersion")]
		Version OSMinVersion { get; }
		[XmlElement ("OSMaxVersion")]
		Version ?OSMaxVersion { get; }
	}
	[ComVisible (true)]
	[XmlRoot ("Tile")]
	public interface ITileVisualElementTile
	{
		[XmlAttribute ("MinHeight")]
		int MinHeight { get; }
		[XmlAttribute ("MaxHeight")]
		int MaxHeight { get; }
		[XmlAttribute ("DefaultHeight")]
		int DefaultHeight { get; }
		[XmlAttribute ("CanPinBottom")]
		bool CanPinBottom { get; }
		[XmlAttribute ("Overflow")]
		TileOverflow Overflow { get; }
	}
	[ComVisible (true)]
	[XmlRoot ("Flyout")]
	public interface ITileVisualElementFlyout
	{
		[XmlAttribute ("FlyoutWidth")]
		int FlyoutWidth { get; }
		[XmlAttribute ("FlyoutHeight")]
		int FlyoutHeight { get; }
		[XmlAttribute ("FlyoutCanResize")]
		bool FlyoutCanResize { get; }
	}
	[ComVisible (true)]
	[XmlRoot ("VisualElements")]
	public interface ITileVisualElements
	{
		[XmlAttribute ("DisplayName")]
		string DisplayName { get; }
		[XmlAttribute ("Logo")]
		string Logo { get; }
		[XmlElement ("Tile")]
		ITileVisualElementTile Tile { get; }
		[XmlElement ("Flyout")]
		ITileVisualElementFlyout Flyout { get; }
	}
	[ComVisible (true)]
	[XmlRoot ("Application")]
	public interface ITileApplication
	{
		[XmlAttribute ("Id")]
		string Id { get; }
		[XmlAttribute ("Executable")]
		string Executable { get; }
		[XmlAttribute ("EntryPoint")]
		string EntryPoint { get; }
		[XmlAttribute ("HasFlyout")]
		bool HasFlyout { get; }
		[XmlAttribute ("HasProperties")]
		bool HasProperties { get; }
		[XmlElement ("VisualElements")]
		ITileVisualElements VisualElements { get; }
	}
	[ComVisible (true)]
	[XmlRoot ("Manifest")]
	public interface ITileManifest
	{
		[XmlAttribute ("Version")]
		uint Version { get; }
		[XmlElement ("Identity")]
		ITileIdentity Identity { get; }
		[XmlElement ("Properties")]
		ITileProperties Properties { get; }
		[XmlElement ("Prerequisites")]
		ITilePrerequisites Prerequisites { get; }
		[XmlElement ("Applications")]
		IList<ITileApplication> Applications { get; }
	}
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sidebar;

namespace Slate.General
{
	public class Sidebar
	{
		public enum Side
		{
			Left = 0,
			Top = 1,
			Right = 2,
			Bottom = 3
		};
	}
}
namespace TileLib
{
	public class TileInfo: Attribute
	{
		public string Name;
		public readonly bool hasflyout;
		public readonly bool hasOptions;
		public TileInfo (string name, bool hf, bool ho)
		{
			Name = name;
			hasflyout = hf;
			hasOptions = ho;
		}
	}
	public class TileIdentity: ITileIdentity
	{
		public string Name { get; set; }
		public ProcessorArchitecture ProcessorArchitecture { get; set; }
		public string Publisher { get; set; }
		public Sidebar.Version Version { get; set; }
		public string PublisherId => PublisherIdHelper.GetPublisherId (Publisher);
		public string FamilyName => $"{Name}_{PublisherId}";
		public string FullName => $"{Name}_{Version.Expression}_{PublisherId}";
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
	}
	public class TileProperties: ITileProperties
	{
		public string DisplayName { get; set; }
		public string PublisherDisplayName { get; set; }
		public string Publisher => PublisherDisplayName;
		public string Description { get; set; }
		public string Logo { get; set; }
		public TileType Type { get; set; }
	}
	public class TilePrerequisites: ITilePrerequisites
	{
		public Sidebar.Version OSMaxVersionTested { get; private set; }
		public Sidebar.Version OSMinVersion { get; private set; }
	}
	public class TileRailStyle: ITileRailStyle
	{
		public int MinHeight { get; private set; }
		public int MaxHeight { get; private set; }
		public int DefaultHeight { get; private set; }
		public bool CanPinBottom { get; private set; }
		public bool TileHasFlyout { get; private set; }
		public int FlyoutWidth { get; private set; }
		public int FlyoutHeight { get; private set; }
		public bool FlyoutCanResize { get; private set; }
		public TileOverflow Overflow { get; private set; }
		public string DisplayName { get; private set; }
		public bool TileHasProperties { get; private set; }
		public string Logo { get; private set; }
	}
	public class TileGridStyle: ITileGridStyle
	{
		public string Badge { get; private set; }
		public TileSize DefaultTileSize { get; private set; }
		public string DisplayName { get; private set; }
		public string SmallTile { get; private set; }
		public string MediumTile { get; private set; }
		public string WideTile { get; private set; }
		public string LargeTile { get; private set; }
		public string BackgroundColor { get; private set; }
		public TileForegroundColor ForegroundColor { get; private set; }
		public bool ShowNameOnMediumTile { get; private set; }
		public bool ShowNameOnWideTile { get; private set; }
		public bool ShowNameOnLargeTile { get; private set; }
		public bool EnableInteraction { get; private set; }
	}
	public class TileVisualElements: ITileVisualElements
	{
		ITileRailStyle ITileVisualElements.RailStyle => railStyle;
		ITileGridStyle ITileVisualElements.GridStyle => gridStyle;
		public TileRailStyle RailStyle { get { return railStyle; } set { railStyle = value; } }
		public TileGridStyle GridStyle { get { return gridStyle; } set { gridStyle = value; } }
		private TileRailStyle railStyle;
		private TileGridStyle gridStyle;
	}
	public class TileManifest: ITileManifest
	{
		private TileIdentity identity;
		private TileProperties properties;
		private TileVisualElements visualElements;
		private TilePrerequisites prerequisites;
		ITileIdentity ITileManifest.Identity => identity;
		ITileProperties ITileManifest.Properties => properties;
		ITileVisualElements ITileManifest.VisualElements => visualElements;
		public TileIdentity Identity { get { return identity; } set { identity = value; } }
		public TileProperties Properties { get { return properties; } set { properties = value; } }
		public TilePrerequisites Prerequisites { get { return prerequisites; } set { prerequisites = value; } }
		public TileVisualElements VisualElements { get { return visualElements; } set { visualElements = value; } }
		ITilePrerequisites ITileManifest.Prerequisites => prerequisites;
	}
	public abstract class BaseTile: Sidebar.TileBase
	{
		public delegate void CaptionChangedEventHandler (string value);
		public delegate void IconChangedEventHandler (BitmapImage image);
		public delegate void ShowFlyoutEventHandler ();
		public delegate void ShowOptionsEventHandler ();
		public delegate void HeightChangedEventHandler (double height);
		public delegate void ShowNotificationEventHandler (string header, string message);
		public event CaptionChangedEventHandler CaptionChanged;
		public event IconChangedEventHandler IconChanged;
		public event ShowFlyoutEventHandler ShowFlyoutEvent;
		public event ShowOptionsEventHandler ShowOptionsEvent;
		public event HeightChangedEventHandler HeightChangedEvent;
		public event ShowNotificationEventHandler ShowNotificationEvent;
		private string _title = "";
		private ImageSource _icon = null;
		private bool _ismin = false;
		private UserControl _flyoutContent;
		private UserControl _optionsContent;
		public BaseTile (): base () { }
		public string Caption
		{
			get
			{
				var f = Features;
				f.Request (new SidebarRequest (this) {
					RequestName = "GetTitle"
				});
				return _title;
			}
			set
			{
				var f = Features;
				f.Request (new SidebarRequest (this) {
					RequestName = "SetTitle",
					RequestDatas = value
				});
			}
		}
		public BitmapImage Icon
		{
			get
			{
				var f = Features;
				f.Request (new SidebarRequest (this) {
					RequestName = "GetIcon"
				});
				return _icon as BitmapImage;
			}
			set
			{
				var f = Features;
				f.Request (new SidebarRequest (this) {
					RequestName = "SetIcon",
					RequestDatas = value
				});
			}
		}
		public bool IsMinimized { get { return _ismin; } set { _ismin = value; } }
		public bool IsPinned { get { return Config.Pinned; } set { (Config as TileConfig).Pinned = value; } }
		public double Height
		{
			get { return Config.Height; }
			set
			{
				var f = Features;
				f.Request (new SidebarRequest (this) {
					RequestName = "ResizeForever",
					RequestDatas = value
				});
			}
		}
		public UserControl FlyoutContent
		{
			get { return _flyoutContent; }
			set { _flyoutContent = value; }
		}
		public UserControl OptionsContent
		{
			get { return _optionsContent; }
			set { _optionsContent = value; }
		}
		public override void OnInitialize ()
		{
			base.OnInitialize ();
			var panel = TileUI as Panel;
			var uc = Load ();
			panel.Children.Add (uc);
			eventRouter = new TileEventRouter (this);
		}
		public override void OnDestroy ()
		{
			eventRouter?.Dispose ();
			eventRouter = null;
			base.OnDestroy ();
			var panel = TileUI as Panel;
			Unload ();
			panel?.Children?.Clear ();
		}
		private class TileEventRouter: TileBaseEventRouter
		{
			private BaseTile bt;
			public TileEventRouter (BaseTile _bt): base (_bt)
			{
				bt = _bt;
			}
			public override void FlyoutForm_Init (object sender, FlyoutAboutEventArgs e)
			{
				bt.flyoutArgs = e;
				bt.ShowFlyout ();
			}
			public override void FlyoutForm_Closed (object sender, EventArgs e)
			{
				bt.flyoutArgs?.ClientArea?.Children?.Clear ();
				bt.flyoutArgs = null;
			}
			public override void PropertiesForm_Init (object sender, PropertiesAboutEventArgs e)
			{
				bt.propArgs = e;
				bt.ShowOptions ();
			}
			public override void PropertiesForm_Closed (object sender, EventArgs e)
			{
				bt.propArgs?.ClientArea?.Children?.Clear ();
				bt.propArgs = null;
			}
			public override void Router_AlreadyDestroy ()
			{
				bt = null;
			}
			public override void Sidebar_DirectionChanged (object sender, SidebarDirectionChangedEventArgs e)
			{
				switch (e.Direction)
				{
					case SidebarDirection.Left:
						bt.ChangeSide ((int)Slate.General.Sidebar.Side.Left); break;
					case SidebarDirection.Right:
						bt.ChangeSide ((int)Slate.General.Sidebar.Side.Right); break;
				}
			}
			public override void Sidebar_ThemeChanged (object sender, ThemeChangedEventArgs e)
			{
				bt.ChangeTheme (e.Theme.ThemeMainFile);
			}
		}
		private TileEventRouter eventRouter = null;
		internal FlyoutAboutEventArgs flyoutArgs = null;
		internal PropertiesAboutEventArgs propArgs = null;
		internal void InitLocale ()
		{
			var langName = "English";
			try
			{
				CultureInfo culture = new CultureInfo (Locale.CurrentLocale);
				langName = culture.Parent.EnglishName;
			}
			catch
			{
				langName = "English"; 
			}
			ChangeLocale (langName);
		}
		public abstract UserControl Load ();
		public virtual void Unload () { }
		public virtual void ChangeSide (int side) { }
		public virtual void ChangeLocale (string locale) { }
		public virtual void ChangeTheme (string theme) { }
		public virtual void Minimized () { }
		public virtual void Unminimized () { }
		public virtual void ShowFlyout ()
		{
			ShowFlyoutEvent ();
			if (FlyoutContent != null && flyoutArgs != null)
			{
				flyoutArgs.ClientArea.Children.Add (FlyoutContent);
			}
		}
		public virtual void ShowOptions ()
		{
			ShowOptionsEvent ();
			if (propArgs != null && OptionsContent != null)
			{
				propArgs.Window.SizeToContent = System.Windows.SizeToContent.WidthAndHeight;
				var scrollViewer = new ScrollViewer ();
				scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
				scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
				propArgs.ClientArea.Children.Add (scrollViewer);
				scrollViewer.Content = OptionsContent;
				var ptm = propArgs.Window as IPropertiesToolMembers;
				ptm.OKButton.Visibility = System.Windows.Visibility.Collapsed;
				ptm.CancelButton.Visibility = System.Windows.Visibility.Collapsed;
			}
		}
		public virtual void ShowNotification (string header, string message)
		{
			ShowNotificationEvent (header, message);
		}
		public void WriteSetting (string tileName, string setting, string value)
		{
			Config.Xml.Global.Set (setting, value);
		}
		public void WriteSetting (string tileName, string setting, int value)
		{
			Config.Xml.Global.Set (setting, value);
		}
		public void WriteSetting (string tileName, string setting, string [] value)
		{
			Config.Xml.Global.Set (setting, value);
		}
		public object ReadSetting (string tileName, string setting)
		{
			return Config.Xml.Global.Get<object> (setting);
		}
		public override bool OnResponse (ITileResponse resp)
		{
			switch (resp.ResponseSource)
			{
				case "Sidebar":
					switch (resp.ResponseName)
					{
						case "ReturnGetTitle":
							_title = resp.ResponseData as string;
							return true;
							break;
						case "ReturnGetIcon":
							_icon = resp.ResponseData as ImageSource;
							return true;
							break;
					}
					break;
			}
			return false;
		}
	}
}

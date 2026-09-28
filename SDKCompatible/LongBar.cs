using System;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32.Init.NativeCompatible;
using TileLib;

namespace Sidebar
{
	public class LongBarTile: TileBase
	{
		public BaseTile Instance { get; private set; }
		private UserControl _tileContent;
		private Panel _flyoutContentPanel;
		private Panel _propertiesContentPanel;
		private bool _isDestroyed;
		public LongBarTile (BaseTile bt)
		{
			if (bt == null) throw new ArgumentNullException ("bt");
			Instance = bt;
		}
		public override void OnInitialize ()
		{
			if (Instance == null) return;
			Instance.SetSettingsStorage (new InitConfig (Config.Ini.FilePath), new NativeCompatible.XmlConfig (Config.Xml.FilePath));
			_tileContent = Instance.Load ();
			if (_tileContent != null)
			{
				var panel = TileUI as Panel;
				if (panel != null)
				{
					panel.Children.Add (_tileContent);
				}
				else
				{
					var cc = TileUI as ContentControl;
					if (cc != null) cc.Content = _tileContent;
				}
			}
			Instance.ShowFlyoutEvent += Instance_ShowFlyoutEvent;
			Instance.ShowOptionsEvent += Instance_ShowOptionsEvent;
			Instance.CaptionChanged += Instance_CaptionChanged;
			Instance.IconChanged += Instance_IconChanged;
			Instance.HeightChangedEvent += Instance_HeightChangedEvent;
			FlyoutInit += LongBarTile_FlyoutInit;
			FlyoutClosing += LongBarTile_FlyoutClosing;
			FlyoutClosed += LongBarTile_FlyoutClosed;
			PropertiesInit += LongBarTile_PropertiesInit;
			PropertiesShow += LongBarTile_PropertiesShow;
			PropertiesClosing += LongBarTile_PropertiesClosing;
			PropertiesClosed += LongBarTile_PropertiesClosed;
			SidebarDirectionChanged += LongBarTile_SidebarDirectionChanged;
			ThemeChanged += LongBarTile_ThemeChanged;
			PushCurrentTitle ();
			PushCurrentIcon ();
		}
		public override void OnDestroy ()
		{
			if (_isDestroyed) return;
			_isDestroyed = true;
			if (Instance != null)
			{
				Instance.ShowFlyoutEvent -= Instance_ShowFlyoutEvent;
				Instance.ShowOptionsEvent -= Instance_ShowOptionsEvent;
				Instance.CaptionChanged -= Instance_CaptionChanged;
				Instance.IconChanged -= Instance_IconChanged;
				Instance.HeightChangedEvent -= Instance_HeightChangedEvent;
			}
			FlyoutInit -= LongBarTile_FlyoutInit;
			FlyoutClosing -= LongBarTile_FlyoutClosing;
			FlyoutClosed -= LongBarTile_FlyoutClosed;
			PropertiesInit -= LongBarTile_PropertiesInit;
			PropertiesShow -= LongBarTile_PropertiesShow;
			PropertiesClosing -= LongBarTile_PropertiesClosing;
			PropertiesClosed -= LongBarTile_PropertiesClosed;
			SidebarDirectionChanged -= LongBarTile_SidebarDirectionChanged;
			ThemeChanged -= LongBarTile_ThemeChanged;
			if (_flyoutContentPanel != null)
			{
				_flyoutContentPanel.Children.Clear ();
				_flyoutContentPanel = null;
			}
			if (_propertiesContentPanel != null)
			{
				_propertiesContentPanel.Children.Clear ();
				_propertiesContentPanel = null;
			}
			if (Instance != null)
			{
				try { Instance.Unload (); } catch { }
			}
			var panel = TileUI as Panel;
			if (panel != null) panel.Children.Clear ();
			_tileContent = null;
			Instance = null;
		}
		private void Instance_CaptionChanged (string value)
		{
			SendRequest ("SetTitle", value);
		}
		private void Instance_IconChanged (BitmapImage image)
		{
			SendRequest ("SetIcon", image);
		}
		private void Instance_HeightChangedEvent (double height)
		{
			SendRequest ("Resize", height);
		}
		private void PushCurrentTitle ()
		{
			if (Instance == null) return;
			try
			{
				string caption = Instance.Caption;
				if (!string.IsNullOrEmpty (caption))
					SendRequest ("SetTitle", caption);
			}
			catch { }
		}
		private void PushCurrentIcon ()
		{
			if (Instance == null) return;
			try
			{
				BitmapImage icon = Instance.Icon;
				if (icon != null)
					SendRequest ("SetIcon", icon);
			}
			catch { }
		}
		private void SendRequest (string name, object data)
		{
			if (Features == null) return;
			if (Manifest == null || Manifest.Identity == null) return;
			try
			{
				var req = new SidebarRequest (this) {
					RequestName = name,
					RequestDatas = data
				};
				Features.Request (req);
			}
			catch { }
		}
		public override bool OnRequest (ITileRequest req)
		{
			if (req == null) return false;
			switch (req.RequestName)
			{
				case "GetTitle":
					if (Features != null && Instance != null)
					{
						var resp = new TileResponse (req) {
							Success = true,
							ResponseName = "ReturnGetTitle",
							ResponseData = Instance.Caption ?? ""
						};
						Features.Response (resp);
						return true;
					}
					break;

				case "GetIcon":
					if (Features != null && Instance != null)
					{
						var resp = new TileResponse (req) {
							Success = true,
							ResponseName = "ReturnGetIcon",
							ResponseData = Instance.Icon
						};
						Features.Response (resp);
						return true;
					}
					break;
			}
			return base.OnRequest (req);
		}
		private void Instance_ShowFlyoutEvent () { }
		private void Instance_ShowOptionsEvent () { }
		private void LongBarTile_FlyoutInit (object sender, FlyoutAboutEventArgs e)
		{
			if (Instance == null) return;
			if (_flyoutContentPanel != null)
			{
				_flyoutContentPanel.Children.Clear ();
				_flyoutContentPanel = null;
			}
			_flyoutContentPanel = e.ClientArea as Panel;
			if (_flyoutContentPanel == null) return;
			var content = Instance.FlyoutContent;
			if (content == null) return;
			_flyoutContentPanel.Children.Add (content);
		}
		private void LongBarTile_FlyoutClosing (object sender, FlyoutAboutEventArgs e) { }
		private void LongBarTile_FlyoutClosed (object sender, EventArgs e)
		{
			if (_flyoutContentPanel != null)
			{
				_flyoutContentPanel.Children.Clear ();
				_flyoutContentPanel = null;
			}
		}
		private void LongBarTile_PropertiesInit (object sender, PropertiesAboutEventArgs e)
		{
			if (Instance == null) return;
			if (_propertiesContentPanel != null)
			{
				_propertiesContentPanel.Children.Clear ();
				_propertiesContentPanel = null;
			}
			_propertiesContentPanel = e.ClientArea as Panel;
			if (_propertiesContentPanel == null) return;
			var content = Instance.OptionsContent;
			if (content == null) return;
			_propertiesContentPanel.Children.Add (content);
		}
		private void LongBarTile_PropertiesShow (object sender, PropertiesAboutEventArgs e) { }
		private void LongBarTile_PropertiesClosing (object sender, PropertiesAboutEventArgs e) { }
		private void LongBarTile_PropertiesClosed (object sender, EventArgs e)
		{
			if (_propertiesContentPanel != null)
			{
				_propertiesContentPanel.Children.Clear ();
				_propertiesContentPanel = null;
			}
		}
		private void LongBarTile_SidebarDirectionChanged (object sender, SidebarDirectionChangedEventArgs e)
		{
			if (Instance != null)
				Instance.ChangeSide ((int)e.Direction);
		}
		private void LongBarTile_ThemeChanged (object sender, ThemeChangedEventArgs e)
		{
			if (Instance != null)
				Instance.ChangeTheme (e.ThemeName);
		}
	}
}
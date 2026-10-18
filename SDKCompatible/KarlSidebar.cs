using System;
using System.Windows;
using System.Windows.Controls;
using Applications.Sidebar;

namespace Sidebar
{
	public class KarlSidebarTile: TileBase
	{
		public BaseTile Instance { get; private set; }
		private Panel _flyoutContentPanel;
		private Window _legacyConfigWindow;
		private Window _hostPropertiesWindow;
		private bool _isDestroyed;
		public KarlSidebarTile (BaseTile bt)
		{
			if (bt == null) throw new ArgumentNullException (nameof (bt));
			Instance = bt;
		}
		public override void OnInitialize ()
		{
			if (Instance == null) return;
			var tilePanel = TileUI as Panel;
			if (tilePanel != null)
			{
				var content = Instance.SidebarContent;
				if (content != null)
					tilePanel.Children.Add (content);
			}
			FlyoutInit += KarlSidebarTile_FlyoutInit;
			FlyoutClosing += KarlSidebarTile_FlyoutClosing;
			FlyoutClosed += KarlSidebarTile_FlyoutClosed;
			PropertiesInit += KarlSidebarTile_PropertiesInit;
			PropertiesShow += KarlSidebarTile_PropertiesShow;
			PropertiesClosing += KarlSidebarTile_PropertiesClosing;
			PropertiesClosed += KarlSidebarTile_PropertiesClosed;
		}
		public override void OnDestroy ()
		{
			if (_isDestroyed) return;
			_isDestroyed = true;
			FlyoutInit -= KarlSidebarTile_FlyoutInit;
			FlyoutClosing -= KarlSidebarTile_FlyoutClosing;
			FlyoutClosed -= KarlSidebarTile_FlyoutClosed;
			PropertiesInit -= KarlSidebarTile_PropertiesInit;
			PropertiesShow -= KarlSidebarTile_PropertiesShow;
			PropertiesClosing -= KarlSidebarTile_PropertiesClosing;
			PropertiesClosed -= KarlSidebarTile_PropertiesClosed;
			if (_flyoutContentPanel != null)
			{
				_flyoutContentPanel.Children.Clear ();
				_flyoutContentPanel = null;
			}
			CleanupProperties (true);
			var tilePanel = TileUI as Panel;
			if (tilePanel != null)
				tilePanel.Children.Clear ();
			Instance = null;
		}
		private void KarlSidebarTile_FlyoutInit (object sender, FlyoutAboutEventArgs e)
		{
			if (Instance == null || !Instance.hasFlyout) return;
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
		private void KarlSidebarTile_FlyoutClosing (object sender, FlyoutAboutEventArgs e)
		{
			if (Instance != null)
				Instance.OnFlyoutClosing ();
		}
		private void KarlSidebarTile_FlyoutClosed (object sender, EventArgs e)
		{
			if (_flyoutContentPanel != null)
			{
				_flyoutContentPanel.Children.Clear ();
				_flyoutContentPanel = null;
			}
		}
		private void KarlSidebarTile_PropertiesInit (object sender, PropertiesAboutEventArgs e)
		{
			if (Instance == null || !Instance.hasConfigWindow) return;
			CleanupProperties (true);
			_hostPropertiesWindow = e.Window;
			if (_hostPropertiesWindow != null)
			{
				_hostPropertiesWindow.Visibility = Visibility.Hidden;
				_hostPropertiesWindow.Activated -= HostPropertiesWindow_Activated;
				_hostPropertiesWindow.Activated += HostPropertiesWindow_Activated;
			}
			_legacyConfigWindow = Instance.ConfigurationWindow;
			if (_legacyConfigWindow == null)
			{
				CloseHostPropertiesWindow ();
				return;
			}
			if (_hostPropertiesWindow != null && _hostPropertiesWindow.IsLoaded)
				_legacyConfigWindow.Owner = _hostPropertiesWindow;
			_legacyConfigWindow.Closed -= LegacyConfigWindow_Closed;
			_legacyConfigWindow.Closed += LegacyConfigWindow_Closed;
			_legacyConfigWindow.Show ();
		}
		private void HostPropertiesWindow_Activated (object sender, EventArgs e)
		{
			if (_legacyConfigWindow != null && _legacyConfigWindow.IsVisible)
				_legacyConfigWindow.Activate ();
		}
		private void KarlSidebarTile_PropertiesShow (object sender, PropertiesAboutEventArgs e)
		{
			if (Instance != null && _legacyConfigWindow != null)
				Instance.OnConfigurationWindowOpened (_legacyConfigWindow);
		}
		private void KarlSidebarTile_PropertiesClosing (object sender, PropertiesAboutEventArgs e)
		{
			try
			{
				if (Instance != null && _legacyConfigWindow != null)
					Instance.OnConfigurationWindowClosing (_legacyConfigWindow);
			}
			catch { }
		}
		private void KarlSidebarTile_PropertiesClosed (object sender, EventArgs e)
		{
			CleanupProperties (false);
		}
		private void LegacyConfigWindow_Closed (object sender, EventArgs e)
		{
			if (_legacyConfigWindow != null)
				_legacyConfigWindow.Closed -= LegacyConfigWindow_Closed;
			CloseHostPropertiesWindow ();
		}
		private void CloseHostPropertiesWindow ()
		{
			var host = _hostPropertiesWindow;
			if (host != null)
			{
				try { host.Close (); } catch { }
			}
		}
		private void CleanupProperties (bool closeWindows)
		{
			if (_hostPropertiesWindow != null)
			{
				_hostPropertiesWindow.Activated -= HostPropertiesWindow_Activated;
				if (closeWindows)
				{
					try { _hostPropertiesWindow.Close (); } catch { }
				}
				_hostPropertiesWindow = null;
			}
			if (_legacyConfigWindow != null)
			{
				_legacyConfigWindow.Closed -= LegacyConfigWindow_Closed;
				if (closeWindows)
				{
					try { _legacyConfigWindow.Close (); } catch { }
				}
				_legacyConfigWindow = null;
			}
		}
	}
}
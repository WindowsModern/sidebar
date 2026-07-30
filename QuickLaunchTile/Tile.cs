using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Sidebar;
namespace WindowsModern.QuickLaunchTile
{
	public class Tile: TileBase
	{
		public static IProgramFolder TileFolder { get; set; }
		public static ISidebarFeatures SidebarFeatures { get; set; }
		public static TileBase TileInstance { get; set; }
		private TilePanel tilePanel;
		private FlyoutPanel flyoutPanel;
		public override void OnInitialize ()
		{
			TileInstance = this;
			TileFolder = Region;
			SidebarFeatures = Features;
			Region?.StringResources?.CleanRedundantValues ();
			UserRegion?.StringResources?.CleanRedundantValues ();
			var panel = TileUI as Panel;
			tilePanel = tilePanel ?? new TilePanel ();
			(tilePanel.Parent as Panel)?.Children?.Clear ();
			panel.Children.Add (tilePanel);
			PropertiesInit += Tile_PropertiesInit;
			FlyoutInit += Tile_FlyoutInit;
			FlyoutClosed += Tile_FlyoutClosed;
			FlyoutShow += Tile_FlyoutShow;
			Utilities.ReleaseLargeResourcesAsync ();
		}
		private void Tile_FlyoutShow (object sender, FlyoutAboutEventArgs e)
		{
			Features.Request (new SidebarRequest (this) {
				RequestName = "FlyoutUpdatePosition"
			});
		}
		private void Tile_FlyoutClosed (object sender, EventArgs e)
		{
			(flyoutPanel.Parent as Panel)?.Children?.Clear ();
		}
		private void Tile_FlyoutInit (object sender, FlyoutAboutEventArgs e)
		{
			flyoutPanel = flyoutPanel ?? new FlyoutPanel ();
			(flyoutPanel.Parent as Panel)?.Children?.Clear ();
			var panel = e.FlyoutUI as Panel;
			panel.Children.Add (flyoutPanel);
			var wnd = Window.GetWindow (panel);
			wnd.SizeToContent = SizeToContent.WidthAndHeight;
			wnd.MaxWidth = 300;
			wnd.MaxHeight = 200;
			Features.Request (new SidebarRequest (this) {
				RequestName = "FlyoutUpdatePosition"
			});
		}
		private void Tile_PropertiesInit (object sender, PropertiesAboutEventArgs e)
		{
			(e.PropertiesContent as Panel)?.Children?.Add (new OptionsPanel ());
		}
		public override void OnDestroy ()
		{
			(tilePanel?.Parent as Panel)?.Children?.Clear ();
			(flyoutPanel?.Parent as Panel)?.Children?.Clear ();
			tilePanel?.Dispose ();
			flyoutPanel?.Dispose ();
			tilePanel = null;
			flyoutPanel = null;
			PropertiesInit -= Tile_PropertiesInit;
			FlyoutInit -= Tile_FlyoutInit;
			FlyoutClosed -= Tile_FlyoutClosed;
			FlyoutShow -= Tile_FlyoutShow;
			TileInstance = null;
			TileFolder = null;
			SidebarFeatures = null;
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using Sidebar;
namespace WindowsModern.SearchTile
{
	public class Tile: TileBase
	{
		public static IProgramFolder TileFolder { get; set; }
		public static ISidebarFeatures SidebarFeatures { get; set; }
		public static Options TileOptions { get; set; }
		public TilePanel tilePanel = null;
		public override void OnInitialize ()
		{
			TileFolder = Region;
			SidebarFeatures = this.Features;
			TileOptions = new Options (this.Config.Ini);
			Region?.StringResources?.CleanRedundantValues ();
			UserRegion?.StringResources?.CleanRedundantValues ();
			var panel = TileUI as Panel;
			tilePanel = tilePanel ?? new TilePanel ();
			panel.Children.Add (tilePanel);
			FlyoutInit += Tile_FlyoutInit;
			FlyoutClosed += Tile_FlyoutClosed;
			Utilities.ReleaseLargeResourcesAsync ();
		}
		private void Tile_FlyoutClosed (object sender, EventArgs e)
		{
			tilePanel?.OnFlyoutClose ();
		}
		private void Tile_FlyoutInit (object sender, FlyoutAboutEventArgs e)
		{
			tilePanel?.OnFlyoutInit (e.FlyoutUI);
		}
		public override void OnDestroy ()
		{
			(tilePanel?.Parent as Panel)?.Children?.Clear ();
			tilePanel?.Dispose ();
			tilePanel = null;
			FlyoutInit -= Tile_FlyoutInit;
			FlyoutClosed -= Tile_FlyoutClosed;
			TileFolder = null;
			SidebarFeatures = null;
			TileOptions = null;
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using Sidebar;
namespace WindowsModern.TrayTile
{
	public class Tile: TileBase
	{
		public static IProgramFolder TileFolder { get; set; }
		public static ISidebarFeatures SidebarFeatures { get; set; }
		private TilePanel tilePanel = null;
		public override void OnInitialize ()
		{
			TileFolder = this.Region;
			SidebarFeatures = this.Features;
			Region?.StringResources?.CleanRedundantValues ();
			UserRegion?.StringResources?.CleanRedundantValues ();
			tilePanel = tilePanel ?? new TilePanel ();
			var tuip = TileUI as Panel;
			tuip.Children.Add (tilePanel);
			FlyoutInit += Tile_FlyoutInit;
			FlyoutClosed += Tile_FlyoutClosed;
			Utilities.ReleaseLargeResourcesAsync ();
		}
		private void Tile_FlyoutClosed (object sender, EventArgs e)
		{
			tilePanel?.OnFlyoutDestroy ();
		}
		private void Tile_FlyoutInit (object sender, FlyoutAboutEventArgs e)
		{
			e.Window.SizeToContent = System.Windows.SizeToContent.WidthAndHeight;
			e.Window.MaxWidth = 300;
			e.Window.MaxHeight = 200;
			e.FlyoutWindowFeatures.FixPosition ();
			tilePanel?.OnFlyoutInit (e);
		}
		public override void OnDestroy ()
		{
			tilePanel?.Dispose ();
			tilePanel = null;
			FlyoutInit -= Tile_FlyoutInit;
			FlyoutClosed -= Tile_FlyoutClosed;
		}
	}
}

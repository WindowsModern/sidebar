using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using Sidebar;
namespace WindowsModern.UserTile
{
	public class Tile: TileBase
	{
		public static Tile Instance { get; set; }
		TilePanel tilePanel = null;
		public override void OnInitialize ()
		{
			Instance = this;
			Region?.StringResources?.CleanRedundantValues ();
			UserRegion?.StringResources?.CleanRedundantValues ();
			tilePanel = tilePanel ?? new TilePanel ();
			(tilePanel?.Parent as Panel)?.Children?.Clear ();
			var panel = TileUI as Panel;
			panel.Children.Add (tilePanel);
			FlyoutInit += Tile_FlyoutInit;
			FlyoutClosed += Tile_FlyoutClosed;
		}
		private void Tile_FlyoutClosed (object sender, EventArgs e)
		{
			tilePanel?.OnFlyoutClosed ();
		}
		private void Tile_FlyoutInit (object sender, FlyoutAboutEventArgs e)
		{
			tilePanel?.OnFlyoutInit (e);
		}
		public override void OnDestroy ()
		{
			(tilePanel?.Parent as Panel)?.Children?.Clear ();
			tilePanel = null;
			Instance = null;
			FlyoutInit -= Tile_FlyoutInit;
			FlyoutClosed -= Tile_FlyoutClosed;
		}
	}
}

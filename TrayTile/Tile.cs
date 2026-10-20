using System;
using System.Collections.Generic;
using System.Diagnostics;
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
		private MenuItem itemCustom = null;
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
			itemCustom = new MenuItem ();
			itemCustom.Header = Region.StringResources.SuitableResource ("TILE_MENU_CUSTOMTRAY");
			itemCustom.Click += ItemCustom_Click;
			ContextMenu = new ContextMenu ();
			ContextMenu.Items.Add (itemCustom);
		}
		private void ItemCustom_Click (object sender, System.Windows.RoutedEventArgs e)
		{
			try
			{
				Process.Start ("explorer.exe", "shell:::{05d7b0f4-2121-4eff-bf6b-ed3f69b894d9}");
			}
			catch
			{
				try
				{
					Process.Start ("rundll32.exe", "shell32.dll,Options_RunDLL 1");
				}
				catch { }
			}
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
			e.ClientArea.Width = double.NaN;
			e.ClientArea.Height = double.NaN;
			e.FlyoutWindowFeatures.FixPosition ();
			tilePanel?.OnFlyoutInit (e);
		}
		public override void OnDestroy ()
		{
			itemCustom.Click -= ItemCustom_Click;
			tilePanel?.Dispose ();
			tilePanel = null;
			FlyoutInit -= Tile_FlyoutInit;
			FlyoutClosed -= Tile_FlyoutClosed;
		}
	}
}

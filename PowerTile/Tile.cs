using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;
using Sidebar;
namespace WindowsModern.PowerTile
{
	public class Tile: TileBase
	{
		public static IProgramFolder TileFolder { get; set; }
		public static ISidebarFeatures SidebarFeatures { get; set; }
		public static Options TileOptions { get; set; }
		public static Tile TileInstance { get; set; }
		private TilePanel tilePanel;
		private TileEventRouter router;
		public override void OnInitialize ()
		{
			TileInstance = this;
			TileFolder = Region;
			SidebarFeatures = Features;
			Region?.StringResources?.CleanRedundantValues ();
			UserRegion?.StringResources?.CleanRedundantValues ();
			TileOptions = new Options (Config.Ini);
			tilePanel = new TilePanel ();
			var panel = TileUI as Panel;
			panel.Children.Add (tilePanel = tilePanel ?? new TilePanel ());
			router = new TileEventRouter (this);
			TileOptions.PropertyChanged += TileOptions_PropertyChanged;
			SystemPowerHelper.SetScreenKeepAwake (TileOptions.KeepScreen);
			Utilities.ReleaseLargeResourcesAsync ();
		}
		private void TileOptions_PropertyChanged (object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case "KeepScreen":
					SystemPowerHelper.SetScreenKeepAwake (TileOptions.KeepScreen);
					break;
			}
		}
		public override void OnDestroy ()
		{
			(tilePanel?.Parent as Panel)?.Children?.Clear ();
			tilePanel = null;
			router?.Dispose ();
			router = null;
			TileOptions.PropertyChanged -= TileOptions_PropertyChanged;
			TileOptions = null;
			SystemPowerHelper.SetScreenKeepAwake (false);
			TileInstance = null;
			ImageSourceManager.ClearCache ();
		}
		private class TileEventRouter: TileBaseEventRouter
		{
			private FlyoutPanel flyoutPanel;
			public TileEventRouter (TileBase tileBase) : base (tileBase) {} 
			public override void FlyoutForm_Init (object sender, FlyoutAboutEventArgs e)
			{
				flyoutPanel = flyoutPanel ?? new FlyoutPanel ();
				e.ClientArea.Background = Brushes.White;
				e.ClientArea.Children.Add (flyoutPanel);
				e.Window.SizeToContent = System.Windows.SizeToContent.Height;
				e.Window.MaxHeight = 550;
			}
			public override void FlyoutForm_Loaded (object sender, FlyoutAboutEventArgs e)
			{
				Instance.Features.Request (new SidebarRequest (Instance) {
					RequestName = "FlyoutUpdatePosition"
				});
			}
			public override void Router_WillDestroy ()
			{
				(flyoutPanel?.Parent as Panel)?.Children?.Clear ();
				flyoutPanel = null;
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Sidebar
{
	/// <summary>
	/// HideTilesContainWindow.xaml 的交互逻辑
	/// </summary>
	public partial class HideTilesContainWindow: Window
	{
		private MainWindow mainWnd = null;
		public HideTilesContainWindow (MainWindow mainWnd)
		{
			InitializeComponent ();
			this.mainWnd = mainWnd;
		}
		private void Window_Loaded (object sender, RoutedEventArgs e)
		{
			App.CurrentUserConfig.PropertyChanged -= CurrentUserConfig_PropertyChanged;
			App.CurrentUserConfig.PropertyChanged += CurrentUserConfig_PropertyChanged;
			Width = App.CurrentUserConfig.Width;
			OverflowTilesRegion.Width = App.CurrentUserConfig.Width;
			Topmost = App.CurrentUserConfig.Topmost;
		}
		private void CurrentUserConfig_PropertyChanged (object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case "Width":
					Width = App.CurrentUserConfig.Width;
					OverflowTilesRegion.Width = App.CurrentUserConfig.Width;
					foreach (Tile t in OverflowTilesRegion.Children)
					{
						t.Width = App.CurrentUserConfig.Width;
					}
					break;
				case "Direction":
					UpdatePosition ();
					break;
				case nameof (App.CurrentUserConfig.Topmost):
					Topmost = App.CurrentUserConfig.Topmost;
					break;
			}
		}
		private void Window_Closed (object sender, EventArgs e)
		{
			App.CurrentUserConfig.PropertyChanged -= CurrentUserConfig_PropertyChanged;
		}
		private void Window_SizeChanged (object sender, System.Windows.SizeChangedEventArgs e)
		{
			UpdatePosition ();
		}
		private void UpdatePosition ()
		{
			Top = mainWnd?.Top ?? 0;
			Left = mainWnd?.Left ?? 0;
			Height = mainWnd?.Height ?? 480;
		}
	}
}

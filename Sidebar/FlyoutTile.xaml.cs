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
using System.Windows.Threading;

// 已废弃
namespace Sidebar
{
	/// <summary>
	/// FlyoutTile.xaml 的交互逻辑
	/// </summary>
	public partial class FlyoutTile: Window
	{
		public FlyoutTile ()
		{
			InitializeComponent ();
		}
		public TileControl Tile
		{
			get
			{
				if (TileTemplateContainer.Children.Count <= 0) return null;
				else
				{
					return TileTemplateContainer.Children [0] as TileControl;
				}
			}
			set
			{
				if (Tile != null) Tile.SizeChanged -= Value_SizeChanged;
				TileTemplateContainer?.Children?.Clear ();
				if (value == null) return;
				value.SizeChanged += Value_SizeChanged;
				TileTemplateContainer?.Children?.Add (value);
			}
		}
		private void Value_SizeChanged (object sender, System.Windows.SizeChangedEventArgs e)
		{
			var tn = TileTemplateContainer.Margin;
			Width = tn.Left + tn.Right + App.CurrentUserConfig.Width;
			Height = tn.Top + tn.Bottom + Tile.ActualHeight;
		}
		private void Window_Closed (object sender, EventArgs e)
		{
			if (Tile != null) Tile.SizeChanged -= Value_SizeChanged;
			TileTemplateContainer?.Children?.Clear ();
		}
		private void Window_Deactivated (object sender, EventArgs e)
		{
			try { if (IsLoaded) Dispatcher.BeginInvoke (new Action (() => Close ()), DispatcherPriority.Background); } catch { }
		}
	}
}

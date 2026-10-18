using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WindowsModern.TrayTile
{
	/// <summary>
	/// FlyoutPanel.xaml 的交互逻辑
	/// </summary>
	public partial class FlyoutPanel: UserControl
	{
		public FlyoutPanel ()
		{
			InitializeComponent ();
		}
		private static T FindVisualParent<T> (DependencyObject child) where T : DependencyObject
		{
			DependencyObject parent = child;
			while (parent != null && !(parent is T))
				parent = VisualTreeHelper.GetParent (parent);
			return parent as T;
		}
		private void Button_Click (object sender, RoutedEventArgs e)
		{
			Button btn = FindVisualParent<Button> (e.OriginalSource as DependencyObject);
			if (btn == null) return;
			var info = btn.Tag as Utils.ITrayIcon;
			if (info == null) return;
			info.OnClick ();
			e.Handled = true;
		}

		private void Button_MouseDoubleClick (object sender, MouseButtonEventArgs e)
		{
			Button btn = sender as Button;
			if (btn == null) return;
			var info = btn.Tag as Utils.ITrayIcon;
			if (info == null) return;
			info.OnDoubleClick ();
			e.Handled = true;
		}

		private void ItemContainer_PreviewMouseRightButtonDown (object sender, MouseButtonEventArgs e)
		{
			Button btn = FindVisualParent<Button> (e.OriginalSource as DependencyObject);
			if (btn == null) return;
			var info = btn.Tag as Utils.ITrayIcon;
			if (info == null) return;
			info.OnRightClick ();
			e.Handled = true;
		}

		private void Button_MouseEnter (object sender, MouseEventArgs e)
		{
			Button btn = FindVisualParent<Button> (e.OriginalSource as DependencyObject);
			if (btn == null) return;
			var info = btn.Tag as Utils.ITrayIcon;
			if (info == null) return;
			info.OnHover ();
			e.Handled = true;
		}

		private void Button_MouseLeave (object sender, MouseEventArgs e)
		{
			Button btn = FindVisualParent<Button> (e.OriginalSource as DependencyObject);
			if (btn == null) return;
			var info = btn.Tag as Utils.ITrayIcon;
			if (info == null) return;
			info.OnLeave ();
			e.Handled = true;
		}

		private void Button_TouchEnter (object sender, TouchEventArgs e)
		{
			Button btn = FindVisualParent<Button> (e.OriginalSource as DependencyObject);
			if (btn == null) return;
			var info = btn.Tag as Utils.ITrayIcon;
			if (info == null) return;
			info.OnHover ();
			e.Handled = true;
		}

		private void Button_TouchLeave (object sender, TouchEventArgs e)
		{
			Button btn = FindVisualParent<Button> (e.OriginalSource as DependencyObject);
			if (btn == null) return;
			var info = btn.Tag as Utils.ITrayIcon;
			if (info == null) return;
			info.OnLeave ();
			e.Handled = true;
		}
	}
}

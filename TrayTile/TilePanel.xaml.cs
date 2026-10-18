using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq;
using WindowsModern.TrayTile.Utils;

namespace WindowsModern.TrayTile
{
	public partial class TilePanel: UserControl, IDisposable
	{
		private TrayIconWatcher _watcher;
		private FlyoutPanel flyoutPanel = null;
		public TilePanel ()
		{
			InitializeComponent ();
			DataContext = this;
			_watcher = new TrayIconWatcher ();
			SystemTrayContainer.ItemsSource = _watcher.Icons;
		}
		public void OnFlyoutInit (Sidebar.FlyoutAboutEventArgs e)
		{
			(flyoutPanel?.Parent as Panel)?.Children?.Clear ();
			if (flyoutPanel == null) flyoutPanel = new FlyoutPanel ();
			e.ClientArea.Children.Add (flyoutPanel);
			flyoutPanel.DataContext = this;
			flyoutPanel.ItemContainer.ItemsSource = SystemTrayContainer.ItemsSource;
		}
		public void OnFlyoutDestroy ()
		{
			if (flyoutPanel != null)
			{
				flyoutPanel.DataContext = null;
				flyoutPanel.ItemContainer.ItemsSource = null;
			}
			(flyoutPanel?.Parent as Panel)?.Children?.Clear ();
		}
		private void TilePanel_Loaded (object sender, RoutedEventArgs e)
		{
			_watcher.Start ();
		}
		private void TilePanel_Unloaded (object sender, RoutedEventArgs e)
		{
			_watcher?.Stop ();
		}
		// 预览右键：阻止父菜单并模拟托盘右键（不移动鼠标）
		private void SystemTrayContainer_PreviewMouseRightButtonDown (object sender, MouseButtonEventArgs e)
		{
			Button btn = FindVisualParent<Button> (e.OriginalSource as DependencyObject);
			if (btn == null) return;
			var info = btn.Tag as Utils.ITrayIcon;
			if (info == null) return;
			info.OnRightClick ();
			e.Handled = true;
		}
		private void TilePanel_ContextMenuOpening (object sender, ContextMenuEventArgs e)
		{
			if (e.OriginalSource is Button ||
				FindVisualParent<Button> (e.OriginalSource as DependencyObject) != null)
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
		public void Dispose ()
		{
			if (flyoutPanel != null)
			{
				flyoutPanel.DataContext = null;
				flyoutPanel.ItemContainer.ItemsSource = null;
			}
			(flyoutPanel?.Parent as Panel)?.Children?.Clear ();
			flyoutPanel = null;
			_watcher?.Stop ();
			_watcher?.Dispose ();
			_watcher = null;
		}
	}
}
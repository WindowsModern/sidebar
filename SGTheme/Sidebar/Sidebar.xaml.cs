using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Sidebar;
using NativePart = Sidebar;

namespace SGTheme.Sidebar
{
	/// <summary>
	/// Sidebar.xaml 的交互逻辑
	/// </summary>
	public partial class Sidebar: UserControl, INeedWindowControl
	{
		public Sidebar ()
		{
			InitializeComponent ();
			var tile = new Tile ();
			var pinnedTile = new Tile ();
			pinnedTile.IsPinned = true;
			TilesControl.Children.Add (tile);
			PinnedTilesControl.Children.Add (pinnedTile);
		}
		public bool AllowTransparency
		{
			get
			{
				if (Environment.OSVersion.Version.Major < 6)
					return false;
				else return true;
			}
		}
		public bool CanShowOnWindow => true;
		public WindowStyle WindowStyle => WindowStyle.None;
		bool isSourceInitialized = false;
		public void Window_Closed (object sender, EventArgs e)
		{
			isSourceInitialized = false;
			if (HandleSource != null)
			{
				HandleSource?.Dispose ();
				HandleSource = null;
			}
		}
		public void Window_Loaded (object sender, RoutedEventArgs e)
		{
			var wnd = sender as Window;
			if (wnd != null)
			{
				wnd.Width = 150;
				wnd.Height = 480;
			}
			this.Width = double.NaN;
			this.Height = double.NaN;
		}
		bool lastEnableAero = false;
		public void Window_OnThemeChanged ()
		{
			if (!isSourceInitialized) return;
			bool enableBlur = false;
			if (Application.Current.Resources.Contains ("EnableBlur"))
				enableBlur = (bool)Application.Current.Resources ["EnableBlur"];
			if (lastEnableAero != enableBlur)
			{
				lastEnableAero = enableBlur;
				ChangeAeroEnable ();
			}
		}
		IntPtr Handle = IntPtr.Zero;
		HwndSource HandleSource = null;
		public void Window_SourceInitialized (object sender, EventArgs e)
		{
			var wnd = sender as Window;
			isSourceInitialized = true;
			Handle = new WindowInteropHelper (wnd).Handle;
			var _source = HwndSource.FromHwnd (Handle);
			HandleSource = _source;
			IntPtr hwnd = Handle;
			bool enableBlur = false;
			if (Application.Current.Resources.Contains ("EnableBlur"))
				enableBlur = (bool)Application.Current.Resources ["EnableBlur"];
			lastEnableAero = enableBlur;
			ChangeAeroStatus (enableBlur);
			var h = (HWND)hwnd;
			DwmThemeProvider.Instance.StartListening (_source);
			h.StylesEx |= NativePart.Win32.ExtendedWindowStyles.WS_EX_TOOLWINDOW;
			h.StylesEx |= NativePart.Win32.ExtendedWindowStyles.WS_EX_LAYERED;
		}
		public void Window_Unloaded (object sender, RoutedEventArgs e)
		{
		}
		public void ChangeAeroEnable ()
		{
			ChangeAeroStatus (lastEnableAero);
		}
		public void ChangeAeroStatus (bool state)
		{
			if (!DWMAPI.IsDwmAvailable ()) return;
			IntPtr hwnd = Handle;
			if (state)
			{
				DWMAPI.EnableBlur (ref hwnd, IntPtr.Zero);
			}
			else
			{
				DWMAPI.DisableBlur (ref hwnd);
			}
		}
	}
}

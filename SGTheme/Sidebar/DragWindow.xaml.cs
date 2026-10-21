using System;
using System.Collections.Generic;
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
	/// DragWindow.xaml 的交互逻辑
	/// </summary>
	public partial class DragWindow: UserControl, INeedWindowControl
	{
		public DragWindow ()
		{
			InitializeComponent ();
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
		}
		public void Window_Loaded (object sender, RoutedEventArgs e)
		{
			var wnd = sender as Window;
			wnd.Width = 150;
			wnd.Height = 150;
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
			if (!enableBlur)
			{
				if (Application.Current.Resources.Contains ("EnableBlurForDrag"))
					enableBlur = (bool)Application.Current.Resources ["EnableBlurForDrag"];
			}
			if (lastEnableAero != enableBlur)
			{
				lastEnableAero = enableBlur;
				ChangeAeroEnable ();
			}
		}
		IntPtr Handle = IntPtr.Zero;
		public void Window_SourceInitialized (object sender, EventArgs e)
		{
			var wnd = sender as Window;
			isSourceInitialized = true;
			Handle = new WindowInteropHelper (wnd).Handle;
			var _source = HwndSource.FromHwnd (Handle);
			IntPtr hwnd = Handle;
			bool enableBlur = false;
			if (Application.Current.Resources.Contains ("EnableBlur"))
				enableBlur = (bool)Application.Current.Resources ["EnableBlur"];
			if (!enableBlur)
			{
				if (Application.Current.Resources.Contains ("EnableBlurForDrag"))
					enableBlur = (bool)Application.Current.Resources ["EnableBlurForDrag"];
			}
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

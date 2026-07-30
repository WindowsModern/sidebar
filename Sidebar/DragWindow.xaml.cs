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
using System.Windows.Shapes;

namespace Sidebar
{
	/// <summary>
	/// DragWindow.xaml 的交互逻辑
	/// </summary>
	public partial class DragWindow: Window, IWindowInterop, IWin32WindowInterop
	{
		public DragWindow ()
		{
			InitializeComponent ();
			InteropHelper = new WindowInteropHelper (this);
			//if (DWMAPI.IsElderWindows ()) AllowsTransparency = false;
		}
		public IntPtr Handle => InteropHelper.Handle;
		public HWND HWnd => (HWND)Handle;
		public WindowInteropHelper InteropHelper { get; }
		public IntPtr WndOwner => InteropHelper.Owner;
		private void Window_SourceInitialized (object sender, EventArgs e)
		{
			IntPtr hwnd = this.HWnd;
			bool enableBlur = false;
			object val;
			if (Application.Current.Resources.Contains ("EnableBlur"))
				enableBlur = (bool)Application.Current.Resources ["EnableBlur"];
			if (!enableBlur)
			{
				if (Application.Current.Resources.Contains ("EnableBlurForDrag"))
					enableBlur = (bool)Application.Current.Resources ["EnableBlurForDrag"];
			}
			if (!enableBlur || !DWMAPI.IsDwmAvailable ())
			{
				DWMAPI.DisableBlur (ref hwnd);
				return;
			}
			DWMAPI.EnableBlur (ref hwnd, IntPtr.Zero);
		}
		private void Window_Closed (object sender, EventArgs e)
		{
			App.ReleaseLargeResourcesAsync ();
		}
	}
}

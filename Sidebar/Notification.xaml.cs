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
using System.Windows.Threading;

namespace Sidebar
{
	/// <summary>
	/// Notification.xaml 的交互逻辑
	/// </summary>
	public partial class Notification: Window, IWindowInterop
	{
		private DispatcherTimer widthRefreshTimer = new DispatcherTimer () {
			Interval = TimeSpan.FromSeconds (0.1)
		};
		private WindowInteropHelper wih = null;
		public WindowInteropHelper InteropHelper => wih;
		public IntPtr Handle => InteropHelper?.Handle ?? IntPtr.Zero;
		public IntPtr WndOwner => InteropHelper?.Owner ?? IntPtr.Zero;
		public Notification ()
		{
			InitializeComponent ();
			wih = new WindowInteropHelper (this);
		}
		private void CloseButton_Click (object sender, RoutedEventArgs e)
		{
			try
			{
				Close ();
			}
			catch { }
		}
		private void BackgroundContainer_SizeChanged (object sender, System.Windows.SizeChangedEventArgs e)
		{
			var tag = BackgroundHeightChange.Tag;
			var splited = (tag as string ?? "").Split (',');
			List<double> members = null;
			try
			{
				members = splited.Select (s => Convert.ToDouble (s)).ToList ();
				if (members.Count <= 0) members.Add (Convert.ToDouble (tag as string));
			}
			catch { }
			if (members == null) members = new List<double> ();
			if (members.Count == 0)
			{
				members.Add (0);
				members.Add (0);
				members.Add (0);
				members.Add (0);
			}
			else if (members.Count == 1)
			{
				members.Add (members [0]);
				members.Add (members [0]);
				members.Add (members [0]);
			}
			else if (members.Count == 2)
			{
				members.Add (members [0]);
				members.Add (members [1]);
			}
			else if (members.Count == 3) members.Add (members [1]);
			var height = BackgroundContainer.ActualHeight - BackgroundHeightChange.ActualHeight - BackgroundHeightChange.ActualWidth;
			if (height < 0) height = 0;
			BackgroundContainer.Clip = new RectangleGeometry (
				new Rect (
					0, 
					BackgroundHeightChange.ActualHeight, 
					BackgroundContainer.ActualWidth,
					height
				), // 裁剪区域
				members [0],
				members [1]
			);
		}
		private void UpdateWidth ()
		{
			ClearValue (WidthProperty);
			UpdateLayout ();
			var width = MessageTitle.ActualWidth +
				(MessageTitle.Margin.Left + MessageTitle.Margin.Right) +
				TextAreaBorderOuter.BorderThickness.Left + TextAreaBorderOuter.BorderThickness.Right + TextAreaBorderOuter.Margin.Left + TextAreaBorderOuter.Margin.Right + TextAreaBorderOuter.Padding.Left + TextAreaBorderOuter.Padding.Right +
				TextAreaBorderInner.BorderThickness.Left + TextAreaBorderInner.BorderThickness.Right + TextAreaBorderInner.Margin.Left + TextAreaBorderInner.Margin.Right + TextAreaBorderInner.Padding.Left + TextAreaBorderInner.Padding.Right +
				TextAreaTextArea.BorderThickness.Left + TextAreaTextArea.BorderThickness.Right + TextAreaTextArea.Margin.Left + TextAreaTextArea.Margin.Right + TextAreaTextArea.Padding.Left + TextAreaTextArea.Padding.Right +
				NotificationUI.Margin.Left + NotificationUI.Margin.Right + NotificationUI.BorderThickness.Left + NotificationUI.BorderThickness.Right + NotificationUI.Padding.Left + NotificationUI.Padding.Right;
			if (double.IsNaN (width) || width < MinWidth) ClearValue (WidthProperty);
			Width = width;
			UpdateLayout ();
		}
		private void Root_Loaded (object sender, RoutedEventArgs e)
		{
			widthRefreshTimer.Tick -= WidthRefreshTimer_Tick;
			widthRefreshTimer.Tick += WidthRefreshTimer_Tick;
			widthRefreshTimer.Start ();
			UpdateLocation ();
		}
		private void WidthRefreshTimer_Tick (object sender, EventArgs e)
		{
			widthRefreshTimer?.Stop ();
			UpdateWidth ();
			UpdateLocation ();
		}
		private void Root_Unloaded (object sender, RoutedEventArgs e)
		{
			widthRefreshTimer.Tick -= WidthRefreshTimer_Tick;
		}
		public static void ShowNotification (NotifyIconNotification nin, ImageSource icon = null)
		{
			var wnd = new Notification ();
			wnd.MessageTitle.Text = nin?.Title;
			wnd.MessageContent.Text = nin?.Content;
			wnd.Icon.Source = wnd.IconRef.Source = icon ?? wnd.Icon.Source;
			wnd.Show ();
		}
		private void Root_SizeChanged (object sender, System.Windows.SizeChangedEventArgs e)
		{
			UpdateLocation ();
		}
		private void UpdateLocation ()
		{
			HWND hwnd = Handle;
			var scr = ScreenHelper.GetScreenByHWND (hwnd) ?? System.Windows.Forms.Screen.PrimaryScreen;
			var wa = scr.WorkingArea;
			hwnd.Move (wa.Left + wa.Width - this.GetPixelWidth (), wa.Top + wa.Height - this.GetPixelHeight ());
		}
		private void Root_Closed (object sender, EventArgs e)
		{
			
		}
		private void Root_SourceInitialized (object sender, EventArgs e)
		{
		}
		private bool isdown = false;
		private void ScrollViewer_MouseDown (object sender, MouseButtonEventArgs e)
		{
			isdown = true;
		}
		private void ScrollViewer_MouseUp (object sender, MouseButtonEventArgs e)
		{
			if (isdown)
			{
				isdown = false;
				OnNotificationClick ();
			}
		}
		private void ScrollViewer_TouchDown (object sender, TouchEventArgs e)
		{
			isdown = true;
		}
		private void ScrollViewer_TouchUp (object sender, TouchEventArgs e)
		{
			if (isdown)
			{
				isdown = false;
				OnNotificationClick ();
			}
		}
		private void OnNotificationClick ()
		{
			try
			{

			}
			finally
			{
				try { Close (); } catch { }
			}
		}
	}
}

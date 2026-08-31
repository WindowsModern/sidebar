using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Sidebar
{
	/// <summary>
	/// Flyout.xaml 的交互逻辑
	/// </summary>
	public partial class Flyout: Window, IWindowInterop, IWin32WindowInterop, IFlyoutToolMembers
	{
		Tile tileComp = null;
		public Flyout (Tile tile)
		{
			InitializeComponent ();
			tileComp = tile;
			if (DWMAPI.IsElderWindows ()) AllowsTransparency = false;
			InteropHelper = new WindowInteropHelper (this);
		}
		public IntPtr Handle => InteropHelper.Handle;
		public HWND HWnd => (HWND)Handle;
		public WindowInteropHelper InteropHelper { get; set; }
		public IntPtr WndOwner => InteropHelper.Owner;
		public System.Drawing.Rectangle TileRect
		{
			get
			{
				if (tileComp == null || !tileComp.IsLoaded) return System.Drawing.Rectangle.Empty;
				var mainWindow = Window.GetWindow (tileComp);
				if (mainWindow == null) return System.Drawing.Rectangle.Empty;
				var interop = new WindowInteropHelper (mainWindow);
				if (interop == null) return System.Drawing.Rectangle.Empty;
				HWND hwnd = interop.Handle;
				if (hwnd == IntPtr.Zero) return System.Drawing.Rectangle.Empty;
				Win32.RECT windowRect;
				if (!Sidebar.Win32WindowNative.GetWindowRect (hwnd, out windowRect))
					return System.Drawing.Rectangle.Empty;
				Point relativeDip = tileComp.TransformToAncestor (mainWindow)
											.Transform (new Point (0, 0));
				var source = PresentationSource.FromVisual (tileComp);
				if (source == null)
					return System.Drawing.Rectangle.Empty;
				Matrix m = source.CompositionTarget.TransformToDevice;
				int pixelOffsetX = (int)Math.Round (relativeDip.X * m.M11);
				int pixelOffsetY = (int)Math.Round (relativeDip.Y * m.M22);
				int tileScreenX = windowRect.Left + pixelOffsetX;
				int tileScreenY = windowRect.Top + pixelOffsetY;
				double pixelWidth = tileComp.ActualWidth * m.M11;
				double pixelHeight = tileComp.ActualHeight * m.M22;
				return new System.Drawing.Rectangle (
					tileScreenX,
					tileScreenY,
					(int)Math.Round (pixelWidth),
					(int)Math.Round (pixelHeight)
				);
			}
		}
		public System.Drawing.Size PixelSize => HWnd.Size;
		public void FixPosition ()
		{
			var cc = App.CurrentUserConfig;
			if (tileComp == null ) return;
			try { UpdateLayout (); } catch { }
			var mainWindow = Window.GetWindow (tileComp) ?? Application.Current.MainWindow;
			HWND hwnd = new WindowInteropHelper (mainWindow).Handle;
			var tsz = TileRect;
			var fsz = PixelSize;
			int newBottom = (int)(5 * App.CurrentUserConfig.CurrentScreen.GetDPI ());
			int newRight = (int)(9 * App.CurrentUserConfig.CurrentScreen.GetDPI ());
			var newLeft = 0;
			switch (cc.Direction)
			{
				case SidebarDirection.Left:
					newLeft = hwnd.Left + newRight; break;
				case SidebarDirection.Right:
					newLeft = hwnd.Left + hwnd.Width - newRight - fsz.Width;
					break;
			}
			var newTop = tsz.Top;
			var delta = hwnd.Height - newTop - fsz.Height - newBottom;
			if (delta < 0) newTop += delta;
			Top = newTop / App.CurrentUserConfig.CurrentScreen.GetDPI ();
			Left = newLeft / App.CurrentUserConfig.CurrentScreen.GetDPI ();
		}
		private void Window_SizeChanged (object sender, System.Windows.SizeChangedEventArgs e)
		{
			FixPosition ();
		}
		private void Window_Loaded (object sender, RoutedEventArgs e)
		{
			FixPosition ();
		}
		private void Window_Deactivated (object sender, EventArgs e)
		{
			//return;
			try { if (IsLoaded && !isclosing) Dispatcher.BeginInvoke (new Action (() => Close ()), DispatcherPriority.Background); } catch { }
		}
		internal TopOptions HeaderOptions => FlyoutHeader;
		bool isclosing = false;
		private void Window_Closing (object sender, System.ComponentModel.CancelEventArgs e)
		{
			isclosing = true;
		}
		private void Window_SourceInitialized (object sender, EventArgs e)
		{
			IntPtr hwnd = this.HWnd;
			bool enableBlur = false;
			object val;
			if (Application.Current.Resources.Contains ("EnableBlur"))
				enableBlur = (bool)Application.Current.Resources ["EnableBlur"];
			if (!AllowsTransparency) enableBlur = false;
			if (!enableBlur)
			{
				if (Application.Current.Resources.Contains ("EnableBlurForFlyout"))
					enableBlur = (bool)Application.Current.Resources ["EnableBlurForFlyout"];
			}
			try { if (Environment.OSVersion.Version.Major > 6) WindowAccent.SetAccentPolicy (hwnd, enableBlur); } catch { }
			if (!enableBlur || !DWMAPI.IsDwmAvailable ())
			{
				DWMAPI.DisableBlur (ref hwnd);
				return;
			}
			DWMAPI.EnableBlur (ref hwnd, IntPtr.Zero);
		}
		private DoubleAnimation _currentHeightAnimation = null;
		private bool _isHeightAnimating = false;
		private int _heightAnimationVersion = 0;
		public Task TransToNewHeight (FrameworkElement component, double elderHeight, double? newHeight = null, TimeSpan? timeout = null)
		{
			var tcs = new TaskCompletionSource<bool> ();
			if (component == null ||
				!component.IsLoaded ||
				component.Visibility == Visibility.Collapsed)
			{
				tcs.SetResult (true);
				return tcs.Task;
			}
			component.UpdateLayout ();
			double targetHeight = newHeight ?? component.ActualHeight;
			if (
				double.IsNaN (targetHeight) ||
				double.IsInfinity (targetHeight) ||
				targetHeight < 0 ||

				double.IsNaN (elderHeight) ||
				double.IsInfinity (elderHeight) ||
				elderHeight < 0)
			{
				tcs.SetResult (true);
				return tcs.Task;
			}
			if (Math.Abs (targetHeight - elderHeight) < 0.01)
			{
				tcs.SetResult (true);
				return tcs.Task;
			}
			int version = ++_heightAnimationVersion;
			double originalHeight = component.Height;
			component.BeginAnimation (
				FrameworkElement.HeightProperty,
				null);
			_isHeightAnimating = true;
			component.Height = elderHeight;
			var animation = new DoubleAnimation {
				From = elderHeight,
				To = targetHeight,
				Duration = timeout ?? TimeSpan.FromSeconds (0.4),
				FillBehavior = FillBehavior.Stop
			};
			EventHandler completedHandler = null;
			completedHandler = (s, e) => {
				animation.Completed -= completedHandler;
				if (version != _heightAnimationVersion)
				{
					tcs.TrySetCanceled ();
					return;
				}
				component.BeginAnimation (
					FrameworkElement.HeightProperty,
					null);
				component.Height = originalHeight;
				_isHeightAnimating = false;
				tcs.TrySetResult (true);
			};
			animation.Completed += completedHandler;
			component.BeginAnimation (
				FrameworkElement.HeightProperty,
				animation,
				HandoffBehavior.SnapshotAndReplace);
			return tcs.Task;
		}
		private void OnHeightAnimationCompleted (object sender, EventArgs e)
		{
		}
		private double? _lastTileContentHeight = null;
		private bool isResizing = false;
		private void FlyoutContent_SizeChanged (object sender, System.Windows.SizeChangedEventArgs e)
		{
			FixPosition ();
			return;
			if (_lastTileContentHeight == null)
			{
				_lastTileContentHeight = FlyoutContent.ActualHeight;
				return;
			}
			if (_isHeightAnimating)
			{
				_lastTileContentHeight = FlyoutContent.ActualHeight;
				return;
			}
			if (isResizing)
			{
				_lastTileContentHeight = FlyoutContent.ActualHeight;
				return;
			}
			TransToNewHeight (FlyoutContent, _lastTileContentHeight ?? 0).ContinueWith (task => FixPosition (), TaskScheduler.FromCurrentSynchronizationContext ());
			_lastTileContentHeight = FlyoutContent.ActualHeight;
		}
		public ContextMenu FlyoutContextMenu
		{
			get { return FlyoutHeader.ContextMenu; }
			set { FlyoutHeader.ContextMenu = value; }
		}
	}
}

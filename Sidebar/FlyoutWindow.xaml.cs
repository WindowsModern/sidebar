using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
	/// FlyoutWindow.xaml 的交互逻辑
	/// </summary>
	public partial class FlyoutWindow: Window, IWindowInterop, IWin32WindowInterop, IFlyoutToolMembers
	{
		TileControl tileComp = null;
		public TileControl Tile => tileComp;
		public FlyoutWindow (TileControl tc)
		{
			InitializeComponent ();
			tileComp = tc;
			if (DWMAPI.IsElderWindows () && !App.CurrentUserConfig.ForceTransparency) AllowsTransparency = false;
			InteropHelper = new WindowInteropHelper (this);
		}
		#region Public Members
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
				var relativeDip = tileComp.TransformToAncestor (mainWindow)
											.Transform (new System.Windows.Point (0, 0));
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
		public ContextMenu FlyoutContextMenu
		{
			get { return FlyoutHeader.ContextMenu; }
			set { FlyoutHeader.ContextMenu = value; }
		}
		#endregion
		public SidebarWindow SidebarHost => tileComp?.SidebarHost;
		private void FlyoutContentContainer_SizeChanged (object sender, System.Windows.SizeChangedEventArgs e)
		{
			Width = FlyoutContentContainer.Margin.Left + FlyoutContentContainer.Margin.Right + FlyoutContentContainer.ActualWidth;
			Height = FlyoutContentContainer.Margin.Top + FlyoutContentContainer.Margin.Bottom + FlyoutContentContainer.ActualHeight;
		}
		bool isclosing = false;
		private void Window_Deactivated (object sender, EventArgs e)
		{
			try { if (IsLoaded && !isclosing) Dispatcher.BeginInvoke (new Action (() => Close ()), DispatcherPriority.Background); } catch { }
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
		public void FixPosition ()
		{
			SidebarHost?.FixFlyoutPosition (this);
		}
		public Task TransToNewHeight (FrameworkElement component, double elderHeight, double? newHeight = default (double?), TimeSpan? timeout = default (TimeSpan?))
		{
			return Task.Factory.StartNew (() => { });
		}
		private void Window_SizeChanged (object sender, System.Windows.SizeChangedEventArgs e)
		{
			FixPosition ();
		}
	}
	internal sealed class FlyoutDoubleClickHelper: IDisposable
	{
		private const int DoubleTapIntervalMs = 300;
		private const double DoubleTapDistancePx = 16.0;
		private readonly FrameworkElement _target;
		private readonly DependencyObject _filterRoot;
		private readonly Func<DependencyObject, bool> _customIgnore;
		private DateTime _lastStylusTime = DateTime.MinValue;
		private System.Windows.Point _lastStylusPos;
		private DateTime _lastTouchTime = DateTime.MinValue;
		private System.Windows.Point _lastTouchPos;
		private bool _attached;
		private bool _disposed;
		public event EventHandler DoubleClicked;
		public bool IsAttached { get { return _attached; } }
		public FlyoutDoubleClickHelper (FrameworkElement target, DependencyObject filterRoot = null, Func<DependencyObject, bool> customIgnore = null)
		{
			if (target == null) throw new ArgumentNullException ("target");
			_target = target;
			_filterRoot = filterRoot ?? target;
			_customIgnore = customIgnore;
		}
		public void Attach ()
		{
			if (_disposed) return;
			if (_attached) return;
			_attached = true;
			_target.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
			_target.PreviewStylusDown += OnPreviewStylusDown;
			_target.PreviewTouchDown += OnPreviewTouchDown;
		}
		public void Detach ()
		{
			if (!_attached) return;
			_attached = false;
			_target.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
			_target.PreviewStylusDown -= OnPreviewStylusDown;
			_target.PreviewTouchDown -= OnPreviewTouchDown;
			ResetPending ();
		}
		public void Dispose ()
		{
			if (_disposed) return;
			_disposed = true;
			Detach ();
			DoubleClicked = null;
		}
		private void ResetPending ()
		{
			_lastStylusTime = DateTime.MinValue;
			_lastTouchTime = DateTime.MinValue;
		}
		private void RaiseDoubleClicked ()
		{
			EventHandler handler = DoubleClicked;
			if (handler != null) handler (this, EventArgs.Empty);
		}
		private bool IsIgnored (DependencyObject source)
		{
			if (source == null) return false;
			if (_customIgnore != null && _customIgnore (source)) return true;
			DependencyObject cur = source;
			while (cur != null && !ReferenceEquals (cur, _filterRoot))
			{
				if (cur is Thumb) return true;
				if (cur is ScrollBar) return true;
				if (cur is ButtonBase) return true;
				if (cur is TextBoxBase) return true;
				if (cur is Selector) return true;
				if (cur is Slider) return true;
				if (cur is PasswordBox) return true;
				cur = GetParentSafe (cur);
			}
			return false;
		}
		private void OnPreviewMouseLeftButtonDown (object sender, MouseButtonEventArgs e)
		{
			if (e.StylusDevice != null) return;
			if (e.ChangedButton != MouseButton.Left) return;
			if (e.ClickCount != 2) return;
			if (IsIgnored (e.OriginalSource as DependencyObject)) return;
			RaiseDoubleClicked ();
			e.Handled = true;
		}
		private void OnPreviewStylusDown (object sender, StylusDownEventArgs e)
		{
			StylusDevice dev = e.StylusDevice;
			if (dev == null || dev.TabletDevice == null) return;
			if (dev.TabletDevice.Type == TabletDeviceType.Touch) return;
			if (dev.InAir) return;
			if (IsIgnored (e.OriginalSource as DependencyObject)) return;
			DateTime now = DateTime.UtcNow;
			var pos = e.GetPosition (_target);
			bool isDouble =
				(now - _lastStylusTime).TotalMilliseconds < DoubleTapIntervalMs &&
				Math.Abs (pos.X - _lastStylusPos.X) < DoubleTapDistancePx &&
				Math.Abs (pos.Y - _lastStylusPos.Y) < DoubleTapDistancePx;
			if (isDouble)
			{
				ResetPending ();
				RaiseDoubleClicked ();
				e.Handled = true;
			}
			else
			{
				_lastStylusTime = now;
				_lastStylusPos = pos;
			}
		}
		private void OnPreviewTouchDown (object sender, TouchEventArgs e)
		{
			TouchDevice dev = e.TouchDevice;
			if (dev == null) return;
			if (IsIgnored (e.OriginalSource as DependencyObject)) return;
			DateTime now = DateTime.UtcNow;
			var pos = e.GetTouchPoint (_target).Position;
			bool isDouble =
				(now - _lastTouchTime).TotalMilliseconds < DoubleTapIntervalMs &&
				Math.Abs (pos.X - _lastTouchPos.X) < DoubleTapDistancePx &&
				Math.Abs (pos.Y - _lastTouchPos.Y) < DoubleTapDistancePx;
			if (isDouble)
			{
				ResetPending ();
				RaiseDoubleClicked ();
				e.Handled = true;
			}
			else
			{
				_lastTouchTime = now;
				_lastTouchPos = pos;
			}
		}
		private static DependencyObject GetParentSafe (DependencyObject cur)
		{
			if (cur == null) return null;
			if (cur is Visual) return VisualTreeHelper.GetParent (cur);
			if (cur is System.Windows.Media.Media3D.Visual3D) return VisualTreeHelper.GetParent (cur);
			return LogicalTreeHelper.GetParent (cur);
		}
	}
}

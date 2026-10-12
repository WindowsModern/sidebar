using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Sidebar
{
	public static class WindowPixelExtensions
	{
		/// <summary>
		/// 获取窗口的设备像素 X 坐标（左上角），通过 (int) 强制转换。
		/// </summary>
		public static int GetPixelLeft (this Window window)
		{
			if (window == null) throw new ArgumentNullException (nameof (window));
			var matrix = GetTransformToDevice (window);
			return (int)matrix.Transform (new System.Windows.Point (window.Left, 0)).X;
		}
		/// <summary>
		/// 获取窗口的设备像素 Y 坐标（左上角），通过 (int) 强制转换。
		/// </summary>
		public static int GetPixelTop (this Window window)
		{
			if (window == null) throw new ArgumentNullException (nameof (window));
			var matrix = GetTransformToDevice (window);
			return (int)matrix.Transform (new System.Windows.Point (0, window.Top)).Y;
		}
		/// <summary>
		/// 获取窗口的设备像素宽度（含非客户区），通过 (int) 强制转换。
		/// </summary>
		public static int GetPixelWidth (this Window window)
		{
			if (window == null) throw new ArgumentNullException (nameof (window));
			var matrix = GetTransformToDevice (window);
			return (int)matrix.Transform (new Vector (window.ActualWidth, 0)).X;
		}
		/// <summary>
		/// 获取窗口的设备像素高度（含非客户区），通过 (int) 强制转换。
		/// </summary>
		public static int GetPixelHeight (this Window window)
		{
			if (window == null) throw new ArgumentNullException (nameof (window));
			var matrix = GetTransformToDevice (window);
			return (int)matrix.Transform (new Vector (0, window.ActualHeight)).Y;
		}
		/// <summary>
		/// 获取窗口在屏幕上的像素位置（左上角），返回 System.Drawing.Point。
		/// </summary>
		public static System.Drawing.Point GetPixelPosition (this Window window)
		{
			if (window == null) throw new ArgumentNullException (nameof (window));
			var matrix = GetTransformToDevice (window);
			var wpfPoint = matrix.Transform (new System.Windows.Point (window.Left, window.Top));
			return new System.Drawing.Point ((int)wpfPoint.X, (int)wpfPoint.Y);
		}
		/// <summary>
		/// 获取窗口的像素尺寸（含非客户区），返回 System.Drawing.Size。
		/// </summary>
		public static System.Drawing.Size GetPixelSize (this Window window)
		{
			if (window == null) throw new ArgumentNullException (nameof (window));
			var matrix = GetTransformToDevice (window);
			var wpfVec = matrix.Transform (new Vector (window.ActualWidth, window.ActualHeight));
			return new System.Drawing.Size ((int)wpfVec.X, (int)wpfVec.Y);
		}
		/// <summary>
		/// 获取窗口在屏幕上的像素矩形（位置 + 尺寸，含非客户区），返回 System.Drawing.Rectangle。
		/// </summary>
		public static Rectangle GetPixelRect (this Window window)
		{
			if (window == null) throw new ArgumentNullException (nameof (window));
			var matrix = GetTransformToDevice (window);
			var wpfPos = matrix.Transform (new System.Windows.Point (window.Left, window.Top));
			var wpfSize = matrix.Transform (new Vector (window.ActualWidth, window.ActualHeight));
			return new Rectangle ((int)wpfPos.X, (int)wpfPos.Y, (int)wpfSize.X, (int)wpfSize.Y);
		}
		/// <summary>
		/// 将窗口移动到指定的屏幕像素矩形（同时改变位置和大小）。
		/// 内部自动根据当前 DPI 将像素值转换为 WPF 设备无关坐标。
		/// </summary>
		/// <param name="window">目标窗口</param>
		/// <param name="pixelRect">以屏幕像素为单位的矩形（X, Y, Width, Height）</param>
		public static void Move (this Window window, Rectangle pixelRect)
		{
			if (window == null) throw new ArgumentNullException (nameof (window));
			var inverse = GetTransformFromDevice (window);
			var wpfPos = inverse.Transform (new System.Windows.Point (pixelRect.X, pixelRect.Y));
			var wpfSize = inverse.Transform (new Vector (pixelRect.Width, pixelRect.Height));
			window.Left = wpfPos.X;
			window.Top = wpfPos.Y;
			window.Width = wpfSize.X;
			window.Height = wpfSize.Y;
		}
		public static void Move (this Window window, double ?left = null, double ?top = null, double ?width = null, double ?height = null)
		{
			if (window == null) throw new ArgumentNullException (nameof (window));
			if (left != null) window.Left = left ?? 0;
			if (top != null) window.Top = top ?? 0;
			if (width != null) window.Width = width ?? 0;
			if (height != null) window.Height = height ?? 0;
		}
		/// <summary>
		/// 将窗口移动到指定的屏幕像素位置（左上角），大小不变。
		/// </summary>
		/// <param name="window">目标窗口</param>
		/// <param name="x">屏幕像素 X 坐标</param>
		/// <param name="y">屏幕像素 Y 坐标</param>
		public static void MovePixel (this Window window, int x, int y)
		{
			if (window == null) throw new ArgumentNullException (nameof (window));
			var inverse = GetTransformFromDevice (window);
			var wpfPoint = inverse.Transform (new System.Windows.Point (x, y));
			window.Left = wpfPoint.X;
			window.Top = wpfPoint.Y;
		}
		/// <summary>
		/// 将窗口移动到指定的屏幕像素位置（左上角），大小不变。
		/// </summary>
		/// <param name="window">目标窗口</param>
		/// <param name="pixelPoint">以屏幕像素为单位的位置</param>
		public static void MovePixel (this Window window, System.Drawing.Point pixelPoint)
		{
			MovePixel (window, pixelPoint.X, pixelPoint.Y);
		}

		/// <summary>
		/// 将窗口移动到指定的屏幕像素坐标/尺寸。
		/// 可只传入需要修改的参数，未传入的参数保持当前值（设备无关坐标不变）。
		/// 内部自动根据当前 DPI 将像素值转换为 WPF 设备无关坐标。
		/// </summary>
		/// <param name="window">目标窗口</param>
		/// <param name="left">屏幕像素 X 坐标（左上角），为 null 则不改变水平位置</param>
		/// <param name="top">屏幕像素 Y 坐标（左上角），为 null 则不改变垂直位置</param>
		/// <param name="width">屏幕像素宽度，为 null 则不改变宽度</param>
		/// <param name="height">屏幕像素高度，为 null 则不改变高度</param>
		public static void MovePixel (this Window window, int? left = null, int? top = null, int? width = null, int? height = null)
		{
			if (window == null) throw new ArgumentNullException (nameof (window));
			var inverse = GetTransformFromDevice (window);
			if (left.HasValue)
			{
				var wpfPos = inverse.Transform (new System.Windows.Point (left.Value, 0));
				window.Left = wpfPos.X;
			}
			if (top.HasValue)
			{
				var wpfPos = inverse.Transform (new System.Windows.Point (0, top.Value));
				window.Top = wpfPos.Y;
			}
			if (width.HasValue)
			{
				var wpfSize = inverse.Transform (new Vector (width.Value, 0));
				window.Width = wpfSize.X;
			}
			if (height.HasValue)
			{
				var wpfSize = inverse.Transform (new Vector (0, height.Value));
				window.Height = wpfSize.Y;
			}
		}
		/// <summary>
		/// 获取从设备像素到 WPF 设备无关单位的逆变换矩阵。
		/// 若窗口尚未呈现（PresentationSource 为 null），则等同单位矩阵（假定 96 DPI）。
		/// </summary>
		private static Matrix GetTransformFromDevice (Window window)
		{
			var source = PresentationSource.FromVisual (window);
			if (source?.CompositionTarget != null)
			{
				var matrix = source.CompositionTarget.TransformToDevice;
				try
				{
					matrix.Invert ();
					return matrix;
				}
				catch (System.InvalidOperationException)
				{
					// 矩阵不可逆时回退为单位矩阵
				}
			}
			return Matrix.Identity;
		}
		private static Matrix GetTransformToDevice (Window window)
		{
			var source = PresentationSource.FromVisual (window);
			return source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
		}
	}
	/// <summary>
	/// SetTimeout 返回的句柄，用于 ClearTimeout 取消。
	/// </summary>
	public sealed class TimeoutHandle
	{
		private Dispatcher _dispatcher;
		private Action _action;
		private DispatcherTimer _timer;
		private DispatcherOperation _operation;
		private volatile bool _cancelled;

		internal TimeoutHandle (Dispatcher dispatcher, Action action)
		{
			_dispatcher = dispatcher;
			_action = action;
		}

		internal void Start (TimeSpan delay, DispatcherPriority priority)
		{
			if (delay <= TimeSpan.Zero)
			{
				// 排到指定优先级队列：默认 Loaded，等布局/渲染处理完再执行
				_operation = _dispatcher.BeginInvoke (priority, (Action)Execute);
			}
			else
			{
				// DispatcherTimer 绑定到指定 Dispatcher，Tick 一定在 UI 线程触发
				_timer = new DispatcherTimer (delay, priority, OnTick, _dispatcher);
				_timer.Start ();
			}
		}

		private void OnTick (object sender, EventArgs e)
		{
			Execute ();
		}

		private void Execute ()
		{
			if (_cancelled) return;
			_cancelled = true;

			var timer = _timer;
			_timer = null;
			if (timer != null)
			{
				timer.Stop ();
				timer.Tick -= OnTick;
			}
			_operation = null;

			var action = _action;
			_action = null;
			_dispatcher = null;

			if (action != null) action ();
		}

		/// <summary>
		/// 取消尚未执行的任务；重复调用无副作用。
		/// </summary>
		public void Cancel ()
		{
			if (_cancelled) return;
			_cancelled = true;

			var dispatcher = _dispatcher;
			if (dispatcher == null) return;

			if (dispatcher.CheckAccess ())
			{
				CancelCore ();
			}
			else
			{
				try { dispatcher.BeginInvoke (DispatcherPriority.Send, (Action)CancelCore); }
				catch (TaskCanceledException) { /* Dispatcher 正在关闭 */ }
			}
		}

		private void CancelCore ()
		{
			_action = null;

			var op = _operation;
			_operation = null;
			if (op != null && op.Status == DispatcherOperationStatus.Pending)
			{
				try { op.Abort (); }
				catch (InvalidOperationException) { /* 已经开始执行，忽略 */ }
			}

			var timer = _timer;
			_timer = null;
			if (timer != null)
			{
				timer.Stop ();
				timer.Tick -= OnTick;
			}

			_dispatcher = null;
		}

		/// <summary>是否仍在等待执行。</summary>
		public bool IsPending
		{
			get { return !_cancelled; }
		}
	}

	public static class TimeoutExtensions
	{
		/// <summary>
		/// 类似 JS 的 setTimeout。可对任意 DispatcherObject（Window、UserControl、控件等）调用。
		/// </summary>
		/// <param name="obj">作为计时上下文的 DispatcherObject。</param>
		/// <param name="action">要执行的委托。</param>
		/// <param name="delayMs">
		/// 延迟毫秒数。0（或负数）表示"布局/渲染完成后再执行"，即排到消息队列尾部。
		/// </param>
		/// <param name="priority">
		/// 队列优先级，默认 DispatcherPriority.Loaded —— 位于 Render 之后、Background 之前，
		/// 正好在布局与渲染完成之后触发。
		/// </param>
		public static TimeoutHandle SetTimeout (this DispatcherObject obj, Action action,
			int delayMs, DispatcherPriority priority = DispatcherPriority.Loaded)
		{
			if (obj == null) throw new ArgumentNullException ("obj");
			if (action == null) throw new ArgumentNullException ("action");

			var dispatcher = obj.Dispatcher;
			if (dispatcher == null)
				throw new InvalidOperationException ("DispatcherObject 尚未关联 Dispatcher。");

			return dispatcher.SetTimeout (action, delayMs, priority);
		}
		/// <summary>无延迟重载，等价于 SetTimeout(action, 0)。</summary>
		public static TimeoutHandle SetTimeout (this DispatcherObject obj, Action action)
		{
			return SetTimeout (obj, action, 0, DispatcherPriority.Loaded);
		}
		/// <summary>
		/// 直接对 Dispatcher 调用的版本（不依赖某个元素）。
		/// </summary>
		public static TimeoutHandle SetTimeout (this Dispatcher dispatcher, Action action,
			int delayMs, DispatcherPriority priority = DispatcherPriority.Loaded)
		{
			if (dispatcher == null) throw new ArgumentNullException ("dispatcher");
			if (action == null) throw new ArgumentNullException ("action");

			// 跨线程调用自动 marshal 回 UI 线程创建
			if (!dispatcher.CheckAccess ())
			{
				return (TimeoutHandle)dispatcher.Invoke (
					(Func<TimeoutHandle>)(() => dispatcher.SetTimeout (action, delayMs, priority)));
			}

			// Dispatcher 已关闭则不再排队
			if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
				return null;

			var handle = new TimeoutHandle (dispatcher, action);
			var delay = TimeSpan.FromMilliseconds (Math.Max (0, delayMs));
			handle.Start (delay, priority);
			return handle;
		}
		/// <summary>类似 JS 的 clearTimeout。</summary>
		public static void ClearTimeout (this DispatcherObject obj, TimeoutHandle handle)
		{
			if (handle != null) handle.Cancel ();
		}
		/// <summary>类似 JS 的 clearTimeout（Dispatcher 版本）。</summary>
		public static void ClearTimeout (this Dispatcher dispatcher, TimeoutHandle handle)
		{
			if (handle != null) handle.Cancel ();
		}
	}
	public static class DispatcherExtensions
	{
		/// <summary>
		/// 让出当前 Dispatcher 消息循环，等该优先级上的消息处理完后，返回的 Task 完成。
		/// 需要在 UI 线程上调用。
		/// </summary>
		public static Task Yield (this Dispatcher dispatcher, DispatcherPriority priority)
		{
			if (dispatcher == null) throw new ArgumentNullException ("dispatcher");
			var tcs = new TaskCompletionSource<object> ();
			dispatcher.BeginInvoke (priority, new Action (() => tcs.TrySetResult (null)));
			return tcs.Task;
		}
	}
	public static class RoomExtensions
	{
		public static double GetRemainingVertical (this StackPanel panel)
		{
			if (panel == null) return 0;
			double used = 0;
			foreach (UIElement child in panel.Children)
			{
				var fe = child as FrameworkElement;
				if (fe == null) continue;
				used += fe.ActualHeight + fe.Margin.Top + fe.Margin.Bottom;
			}
			return Math.Max (0, panel.ActualHeight - used);
		}
	}
}

using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

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
	public static class FrameworkElementAnimations
	{
		private const double MinAnimatableDelta = 5.0;
		private static readonly TimeSpan DefaultDuration = TimeSpan.FromMilliseconds (500);
		private sealed class AnimationToken { public int Value; }
		private static readonly ConditionalWeakTable<FrameworkElement, AnimationToken> Tokens = new ConditionalWeakTable<FrameworkElement, AnimationToken> ();
		public static Task<double> AnimeToNewHeight (this FrameworkElement fe, double targetHeight, double? originHeight = null, double? finalSetHeight = null, TimeSpan? duration = null)
		{
			if (fe == null) throw new ArgumentNullException ("fe");
			var tcs = new TaskCompletionSource<double> ();
			TimeSpan dur = duration ?? DefaultDuration;
			if (dur < TimeSpan.Zero) dur = TimeSpan.Zero;
			double currentHeight = fe.Height;
			double actualHeight = fe.ActualHeight;
			bool skipAnimation = IsCloseToTarget (currentHeight, targetHeight) || IsCloseToTarget (actualHeight, targetHeight);
			if (skipAnimation)
			{
				fe.BeginAnimation (FrameworkElement.HeightProperty, null);
				double finalValue = finalSetHeight ?? targetHeight;
				fe.Height = finalValue;
				tcs.TrySetResult (finalValue);
				return tcs.Task;
			}
			double? from = originHeight;
			if (!from.HasValue && double.IsNaN (currentHeight))
				from = double.IsNaN (actualHeight) ? 0d : actualHeight;
			int token = NextToken (fe);
			var animation = new DoubleAnimation { To = targetHeight, Duration = new Duration (dur), FillBehavior = FillBehavior.HoldEnd };
			if (from.HasValue) animation.From = from.Value;
			EventHandler completedHandler = null;
			completedHandler = (sender, args) => {
				animation.Completed -= completedHandler;
				if (!IsCurrentToken (fe, token)) return;
				if (finalSetHeight.HasValue)
				{
					fe.BeginAnimation (FrameworkElement.HeightProperty, null);
					fe.Height = finalSetHeight.Value;
					tcs.TrySetResult (finalSetHeight.Value);
				}
				else
				{
					tcs.TrySetResult (fe.Height);
				}
			};
			animation.Completed += completedHandler;
			fe.BeginAnimation (FrameworkElement.HeightProperty, animation, HandoffBehavior.SnapshotAndReplace);
			return tcs.Task;
		}
		private static bool IsCloseToTarget (double value, double target)
		{
			if (double.IsNaN (value) || double.IsNaN (target)) return false;
			return Math.Abs (value - target) <= MinAnimatableDelta;
		}
		private static int NextToken (FrameworkElement fe)
		{
			AnimationToken t;
			if (!Tokens.TryGetValue (fe, out t)) { t = new AnimationToken (); Tokens.Add (fe, t); }
			unchecked { t.Value++; }
			return t.Value;
		}
		private static bool IsCurrentToken (FrameworkElement fe, int token)
		{
			AnimationToken t;
			return Tokens.TryGetValue (fe, out t) && t.Value == token;
		}
		public static bool IsHeightAnimating (this FrameworkElement fe)
		{
			if (fe == null) throw new ArgumentNullException ("fe");
			var clock = fe.GetAnimationBaseValue (FrameworkElement.HeightProperty) as double?;
			var current = fe.Height;
			var baseValue = (double)fe.GetAnimationBaseValue (FrameworkElement.HeightProperty);
			if (double.IsNaN (current) || double.IsNaN (baseValue)) return true;
			return Math.Abs (current - baseValue) > double.Epsilon;
		}
	}
}

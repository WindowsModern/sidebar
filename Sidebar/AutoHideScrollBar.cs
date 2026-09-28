using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System.Windows.Threading;
namespace Sidebar
{
	/// <summary>
	/// ScrollViewer 自动隐藏滚动条附加行为。
	/// 相比 XAML 版：只有一个 DispatcherTimer，鼠标移动仅重置计时器，不创建对象。
	/// </summary>
	public static class AutoHideScrollBar
	{
		private const double VisibleOpacity = 0.8;
		private const double FadeInSeconds = 0.15;
		private const double FadeOutSeconds = 0.3;
		private const int IdleMs = 5000;
		public static readonly DependencyProperty IsEnabledProperty =
			DependencyProperty.RegisterAttached ("IsEnabled", typeof (bool), typeof (AutoHideScrollBar),
				new PropertyMetadata (false, OnIsEnabledChanged));
		public static void SetIsEnabled (DependencyObject o, bool v) => o.SetValue (IsEnabledProperty, v);
		public static bool GetIsEnabled (DependencyObject o) => (bool)o.GetValue (IsEnabledProperty);
		private static readonly DependencyProperty StateProperty =
			DependencyProperty.RegisterAttached ("State", typeof (State), typeof (AutoHideScrollBar));
		private sealed class State
		{
			public DispatcherTimer Timer;
			public ScrollViewer Viewer;
			public bool BarsVisible;
		}
		private static void OnIsEnabledChanged (DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var sv = d as ScrollViewer;
			if (sv == null) return;
			if ((bool)e.NewValue)
			{
				var state = new State { Viewer = sv };
				sv.SetValue (StateProperty, state);
				sv.MouseEnter += OnActivity;
				sv.MouseMove += OnActivity;
				sv.PreviewMouseWheel += OnActivity;
				sv.PreviewTouchMove += OnActivity;
				sv.MouseLeave += OnLeave;
				sv.TouchLeave += OnLeave;
				sv.Unloaded += OnUnloaded;
				var timer = new DispatcherTimer (DispatcherPriority.Background, sv.Dispatcher) {
					Interval = TimeSpan.FromMilliseconds (IdleMs)
				};
				timer.Tick += (s, ev) => {
					timer.Stop ();
					Hide (state);
				};
				state.Timer = timer;
			}
			else
			{
				var state = sv.GetValue (StateProperty) as State;
				if (state != null)
				{
					if (state.Timer != null) { state.Timer.Stop (); state.Timer = null; }
					sv.ClearValue (StateProperty);
				}
				sv.MouseEnter -= OnActivity;
				sv.MouseMove -= OnActivity;
				sv.PreviewMouseWheel -= OnActivity;
				sv.PreviewTouchMove -= OnActivity;
				sv.MouseLeave -= OnLeave;
				sv.TouchLeave -= OnLeave;
				sv.Unloaded -= OnUnloaded;
			}
		}
		private static void OnActivity (object sender, EventArgs e)
		{
			var sv = sender as ScrollViewer;
			if (sv == null) return;
			var state = sv.GetValue (StateProperty) as State;
			if (state == null) return;
			Show (state);
			var timer = state.Timer;
			if (timer != null)
			{
				timer.Stop ();
				timer.Start ();
			}
		}
		private static void OnLeave (object sender, EventArgs e)
		{
			var sv = sender as ScrollViewer;
			if (sv == null) return;
			var state = sv.GetValue (StateProperty) as State;
			if (state == null) return;
			if (state.Timer != null) state.Timer.Stop ();
			Hide (state);
		}
		private static void OnUnloaded (object sender, RoutedEventArgs e)
		{
			var sv = sender as ScrollViewer;
			if (sv == null) return;
			var state = sv.GetValue (StateProperty) as State;
			if (state != null && state.Timer != null)
				state.Timer.Stop ();
		}
		private static void Show (State state)
		{
			if (state.BarsVisible) return;
			state.BarsVisible = true;
			Animate (state.Viewer, "PART_VerticalScrollBar", VisibleOpacity, FadeInSeconds);
			Animate (state.Viewer, "PART_HorizontalScrollBar", VisibleOpacity, FadeInSeconds);
		}
		private static void Hide (State state)
		{
			if (!state.BarsVisible) return;
			state.BarsVisible = false;
			Animate (state.Viewer, "PART_VerticalScrollBar", 0, FadeOutSeconds);
			Animate (state.Viewer, "PART_HorizontalScrollBar", 0, FadeOutSeconds);
		}
		private static void Animate (ScrollViewer sv, string partName, double to, double seconds)
		{
			var template = sv.Template;
			if (template == null) return;
			var bar = template.FindName (partName, sv) as ScrollBar;
			if (bar == null) return;
			if (Math.Abs (bar.Opacity - to) < 0.01) return;
			var anim = new DoubleAnimation {
				To = to,
				Duration = TimeSpan.FromSeconds (seconds),
				FillBehavior = FillBehavior.HoldEnd,
			};
			bar.BeginAnimation (UIElement.OpacityProperty, anim, HandoffBehavior.SnapshotAndReplace);
		}
	}
}
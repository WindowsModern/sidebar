using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Sidebar;

namespace SGTheme.Sidebar
{
	/// <summary>
	/// Tile.xaml 的交互逻辑
	/// </summary>
	public partial class Tile: UserControl
	{
		public Tile ()
		{
			InitializeComponent ();
			showIconHandler = (s, ev) => ShowIcon ();
			hideIconHandler = (s, ev) => HideIcon ();
			this.AllowDrop = true;
		}
		private void UpdateMouseTrigger ()
		{
			bool headerVisible = TileHeader.Visibility == Visibility.Visible && TileHeader.ActualHeight > 0;

			if (headerVisible)
			{
				TileHeader.MouseEnter += showIconHandler;
				TileHeader.MouseLeave += hideIconHandler;
				// 关键：让图标也响应相同的显示/隐藏逻辑
				TileFlyoutIcon.MouseEnter += showIconHandler;
				TileFlyoutIcon.MouseLeave += hideIconHandler;
				// 移除磁贴自身的事件，避免重复
				this.MouseEnter -= Tile_MouseEnter;
				this.MouseLeave -= Tile_MouseLeave;
			}
			else
			{
				TileHeader.MouseEnter -= showIconHandler;
				TileHeader.MouseLeave -= hideIconHandler;
				TileFlyoutIcon.MouseEnter -= showIconHandler;
				TileFlyoutIcon.MouseLeave -= hideIconHandler;
				this.MouseEnter += Tile_MouseEnter;
				this.MouseLeave += Tile_MouseLeave;
			}
		}
		public static readonly DependencyProperty IsPinnedProperty =
			DependencyProperty.Register (
				"IsPinned",
				typeof (bool),
				typeof (Tile),
				new PropertyMetadata (false, OnIsPinnedChanged));
		public bool IsPinned
		{
			get { return (bool)GetValue (IsPinnedProperty); }
			set { SetValue (IsPinnedProperty, value); }
		}
		private static void OnIsPinnedChanged (DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var tile = (Tile)d;
			bool newValue = (bool)e.NewValue;
			tile.UpdatePinnedState (newValue);
		}
		private void UpdatePinnedState (bool pinned)
		{
			DockPanel.SetDock (Splitter, pinned ? Dock.Top : Dock.Bottom);
		}
		private MouseEventHandler showIconHandler;
		private MouseEventHandler hideIconHandler;
		private void Tile_MouseEnter (object sender, System.Windows.Input.MouseEventArgs e) => ShowIcon ();
		private void Tile_MouseLeave (object sender, System.Windows.Input.MouseEventArgs e) => HideIcon ();
		private void ShowIcon ()
		{
			var storyboard = Resources ["ShowIconStoryboard"] as Storyboard;
			storyboard?.Begin ();
		}
		private void HideIcon ()
		{
			var storyboard = Resources ["HideIconStoryboard"] as Storyboard;
			storyboard?.Begin ();
		}
		private double CalcHeightForAnime (double? contentHeight = null)
		{
			return TileHeader.ActualHeight + (contentHeight ?? TileContent.ActualHeight) + Splitter.ActualHeight;
		}
		private double GetTileContentSuggestHeight (double? customHeight = null)
		{
			return 80;
		}
		private DoubleAnimation _currentHeightAnimation = null;
		private bool _isHeightAnimating = false;
		private int _heightAnimationVersion = 0;
		private Task TransToNewHeight (FrameworkElement component, double elderHeight, double? newHeight = null, TimeSpan? timeout = null)
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
			_currentHeightAnimation = animation;
			component.BeginAnimation (
				FrameworkElement.HeightProperty,
				animation,
				HandoffBehavior.SnapshotAndReplace);
			return tcs.Task;
		}
		private void OnHeightAnimationCompleted (object sender, EventArgs e)
		{
		}
		private DoubleAnimation _currentOpacityAnimation = null;
		private int _opacityAnimationVersion = 0;
		private Task TransToNewOpacity (FrameworkElement component, double elderOpacity, double? newOpacity = null, TimeSpan? timeout = null)
		{
			var tcs = new TaskCompletionSource<bool> ();
			if (component == null)
			{
				tcs.SetResult (true);
				return tcs.Task;
			}
			if (!component.IsLoaded)
			{
				tcs.SetResult (true);
				return tcs.Task;
			}
			double targetOpacity = newOpacity ?? component.Opacity;
			if (
				double.IsNaN (targetOpacity) ||
				double.IsInfinity (targetOpacity) ||

				double.IsNaN (elderOpacity) ||
				double.IsInfinity (elderOpacity))
			{
				tcs.SetResult (true);
				return tcs.Task;
			}
			targetOpacity = Math.Max (0, Math.Min (1, targetOpacity));
			elderOpacity = Math.Max (0, Math.Min (1, elderOpacity));
			if (Math.Abs (targetOpacity - elderOpacity) < 0.001)
			{
				tcs.SetResult (true);
				return tcs.Task;
			}
			int version = ++_opacityAnimationVersion;
			double originalOpacity = component.Opacity;
			component.BeginAnimation (
				UIElement.OpacityProperty,
				null);
			component.Opacity = elderOpacity;
			var animation = new DoubleAnimation {
				From = elderOpacity,
				To = targetOpacity,
				Duration = timeout ?? TimeSpan.FromSeconds (0.4),
				FillBehavior = FillBehavior.Stop
			};
			EventHandler completedHandler = null;
			completedHandler = (s, e) => {
				animation.Completed -= completedHandler;
				if (version != _opacityAnimationVersion)
				{
					tcs.TrySetCanceled ();
					return;
				}
				component.BeginAnimation (
					UIElement.OpacityProperty,
					null);
				component.Opacity = originalOpacity;
				tcs.TrySetResult (true);
			};
			animation.Completed += completedHandler;
			_currentOpacityAnimation = animation;
			component.BeginAnimation (
				UIElement.OpacityProperty,
				animation,
				HandoffBehavior.SnapshotAndReplace);
			return tcs.Task;
		}
		private void OnOpacityAnimationCompleted (object sender, EventArgs e) { }
		public void CompleteAnimationImmediately (FrameworkElement target, DependencyProperty property, DoubleAnimation animation)
		{
			if (animation == null) return;
			double endValue = animation.To ?? (double)target.GetValue (property) + (animation.By ?? 0);
			target.BeginAnimation (property, null);
			target.SetValue (property, endValue);
		}
		internal Panel ApplyOverflowMode (TileOverflow overflow)
		{
			switch (overflow)
			{
				case TileOverflow.Auto:
				case TileOverflow.Hidden:
					// 直接返回 TileContent (它是一个 Grid)
					return TileContent;

				case TileOverflow.Scroll:
					var scrollViewer = new ScrollViewer {
						VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
						HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
					};
					scrollViewer.SetResourceReference (StyleProperty, "AutoHideScrollViewer");
					var scrollContent = new Grid ();   // 内容面板
					scrollViewer.Content = scrollContent;
					TileContent.Children.Add (scrollViewer);
					return scrollContent;

				case TileOverflow.Scale:
					var viewbox = new Viewbox { Stretch = Stretch.Uniform };
					var scaleContent = new Grid ();
					viewbox.Child = scaleContent;
					TileContent.Children.Add (viewbox);
					return scaleContent;

				default:
					return TileContent;
			}
		}
		private bool isSplitterDragging = false;
		private bool hasPlayedLoadAnimation = true;
		/// <summary>
		/// 更新右键菜单中所有菜单项的文字（多语言）
		/// </summary>
		public event EventHandler Disposed;
		public void SetSplitterVisible (bool visible)
		{
			if (visible)
			{
				Splitter.ClearValue (UIElement.VisibilityProperty);  // 恢复默认（样式或初始值）
				TileHighlight.ClearValue (UIElement.VisibilityProperty);  // 恢复默认（样式或初始值）
			}
			else
			{
				Splitter.Visibility = Visibility.Hidden;            // 隐藏但保留布局占位
				TileHighlight.Visibility = Visibility.Hidden;
			}
		}
	}
}

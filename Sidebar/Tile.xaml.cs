using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Sidebar
{
	/// <summary>
	/// Tile.xaml 的交互逻辑
	/// </summary>
	public partial class Tile: UserControl, IDisposable
	{
		public Tile ()
		{
			InitializeComponent ();
			Loaded += Tile_Loaded;
			Context = new TileContext (this, this.TileContent);
			UpdateMenuLocalization ();
			showIconHandler = (s, ev) => ShowIcon ();
			hideIconHandler = (s, ev) => HideIcon ();
			this.AllowDrop = true;
			infocache = new VisualInfo (this);
		}
		private void Tile_Loaded (object sender, RoutedEventArgs e)
		{
			UpdateMouseTrigger ();
			TileContent.VerticalAlignment = VerticalAlignment.Top;
			if (Instance?.Manifest.VisualElements.RailStyle.Overflow != TileOverflow.Auto && Instance?.Manifest.VisualElements.RailStyle.MaxHeight != null)
			{
				TileContent.MaxHeight = Instance?.Manifest.VisualElements.RailStyle.MaxHeight ?? 32767;
			}
			TileContent.MinHeight = Instance?.Manifest.VisualElements.RailStyle.MinHeight ?? 20;
			if (!hasPlayedLoadAnimation)
			{
				try
				{
					TileContent.Opacity = 0;
					hasPlayedLoadAnimation = true;
					var h = GetTileContentSuggestHeight ();
					TileContent.Height = h;
					if (Instance?.Manifest.VisualElements.RailStyle.Overflow == TileOverflow.Auto)
						TileContent.Height = double.NaN;
					UpdateLayout ();
					TileContent.Opacity = 1;
					TransToNewOpacity (TileContent, 0);
					//Task t;
					//if (double.IsNaN (h)) t = TransToNewHeight (TileContent, 0);
					//else t = TransToNewHeight (TileContent, 0, h);
					//t.ContinueWith (task =>
					//{
					//	TileContent.Opacity = 1;
					//	TransToNewOpacity (TileContent, 0);
					//	TileContent.Height = h;
					//	if (Instance?.Manifest.VisualElements.RailStyle.Overflow == TileOverflow.Auto)
					//		TileContent.Height = double.NaN;
					//}, TaskScheduler.FromCurrentSynchronizationContext ());
				}
				catch
				{
					TileContent.Opacity = 1;
					var h = GetTileContentSuggestHeight ();
					TileContent.Height = h;
					if (Instance?.Manifest.VisualElements.RailStyle.Overflow == TileOverflow.Auto)
						TileContent.Height = double.NaN;
				}
			}
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
			if (pinned)
			{
				TileHeader.SetResourceReference (StyleProperty, "PinnedTileHeader");
				Splitter.SetResourceReference (StyleProperty, "PinnedTileSplitterPanel");
				TileIcon.SetResourceReference (StyleProperty, "PinnedTileIcon");
				TileDisplayName.SetResourceReference (StyleProperty, "PinnedTileTitle");
				TileFlyoutIcon.SetResourceReference (StyleProperty, "PinnedTileFlyoutIcon");
				TileContent.SetResourceReference (StyleProperty, "PinnedTileContent");
				TileHighlight.SetResourceReference (StyleProperty, "PinnedTileHighlight");
				DockPanel.SetDock (Splitter, Dock.Top);
			}
			else
			{
				TileHeader.SetResourceReference (StyleProperty, "TileHeader");
				Splitter.SetResourceReference (StyleProperty, "TileSplitterPanel");
				TileIcon.SetResourceReference (StyleProperty, "TileIcon");
				TileDisplayName.SetResourceReference (StyleProperty, "TileTitle");
				TileFlyoutIcon.SetResourceReference (StyleProperty, "TileFlyoutIcon");
				TileContent.SetResourceReference (StyleProperty, "TileContent");
				TileHighlight.SetResourceReference (StyleProperty, "TileHighlight");
				DockPanel.SetDock (Splitter, Dock.Bottom);
			}
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
		protected TileInstance Instance { get; private set; } = null;
		[Browsable (false)]
		public TileInstance TileInstance => Instance;
		internal void Initialize (TileInstance ti)
		{
			if (ti == null) throw new ArgumentNullException ("Cannot init the tile control: tile instance is null.");
			Instance = ti;
			InitDisplayName ();
			InitIcon ();
			DataContext = Instance?.Config;
			Instance.Config.PropertyChanged += Config_PropertyChanged;
			IsPinned = Instance.Config.Pinned;
		}
		private void Config_PropertyChanged (object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case "Pinned":
					IsPinned = Instance.Config.Pinned; break;
				case "AutoSize":
					CompleteAnimationImmediately (TileContent, HeightProperty, _currentHeightAnimation);
					var elder = TileContent.ActualHeight;
					if (Instance.Config.AutoSize)
					{
						TileContent.ClearValue (Grid.HeightProperty);
						this.ClearValue (Grid.HeightProperty);
						//this.InvalidateMeasure ();
						TileContent.Height = double.NaN;
						this.Height = double.NaN;
					}
					else
					{
						TileContent.Height = GetTileContentSuggestHeight ();
					}
					TileContent.UpdateLayout ();
					this.UpdateLayout ();
					//TransToNewHeight (TileContent, elder);
					break;
				case "Height":
					TileContent.Height = Instance.Config.Height;
					break;
			}
		}
		internal void InitDisplayName ()
		{
			string dispName = "";
			try
			{
				string dispresname = Instance.Manifest.VisualElements.RailStyle.DisplayName;
				dispName = Instance.Region.StringResources [dispresname].SuitableValue ();
				if (string.IsNullOrEmpty (dispName)) dispName = dispresname;
			}
			catch
			{
				dispName = Instance.Manifest.VisualElements.RailStyle.DisplayName;
			}
			TileDisplayName.Text = dispName;
		}
		internal void InitIcon ()
		{
			string icon = "";
			try
			{
				string iconrespath = Instance.Manifest.VisualElements.RailStyle.Logo;
				icon = Instance.Region.FileResources [iconrespath].SuitableValue ();
				if (string.IsNullOrEmpty (icon)) icon = iconrespath;
			}
			catch
			{
				icon = Instance.Manifest.VisualElements.RailStyle.Logo;
			}
			string iconfullpath = "";
			iconfullpath = System.IO.Path.Combine (Instance.Region.FolderPath, icon);
			if (!System.IO.File.Exists (iconfullpath)) iconfullpath = System.IO.Path.Combine (Instance.Region.FolderPath, Instance.Manifest.VisualElements.RailStyle.Logo);
			if (!System.IO.File.Exists (iconfullpath)) iconfullpath = "";
			if (System.IO.File.Exists (iconfullpath))
			{
				try
				{
					var bm = new BitmapImage ();
					try
					{
						bm.BeginInit ();
						bm.UriSource = new Uri (iconfullpath, UriKind.RelativeOrAbsolute);
					}
					finally
					{
						bm.EndInit ();
						bm.Freeze ();
					}
					TileIcon.Source = bm;
				}
				catch { }
			}
		}
		internal string GetDisplayName () => TileDisplayName?.Text;
		internal ImageSource GetIcon () => TileIcon?.Source;
		private double CalcHeightForAnime (double ?contentHeight = null)
		{
			return TileHeader.ActualHeight + (contentHeight ?? TileContent.ActualHeight) + Splitter.ActualHeight;
		}
		private double GetTileContentSuggestHeight (double ?customHeight = null)
		{
			var configHeight = Instance?.Config?.Height ?? Instance?.Manifest?.VisualElements?.RailStyle?.DefaultHeight;
			if (configHeight == 0) configHeight = Instance?.Manifest?.VisualElements?.RailStyle?.DefaultHeight;
			double finalHeight = 0;
			if (configHeight == null || Instance?.Config?.AutoSize == true || Instance?.Manifest?.VisualElements?.RailStyle?.Overflow == TileOverflow.Auto)
			{
				TileContent.Height = double.NaN;
				TileContent.UpdateLayout ();
				finalHeight = TileContent.ActualHeight;
			}
			else finalHeight = customHeight ?? configHeight ?? 20;
			if (Instance?.Manifest?.VisualElements?.RailStyle?.Overflow == TileOverflow.Auto)
			{
				finalHeight = Math.Max (Instance?.Manifest?.VisualElements?.RailStyle?.MinHeight ?? 20, finalHeight);
			}
			else
			{
				finalHeight = Math.Min (finalHeight, Instance?.Manifest?.VisualElements?.RailStyle?.MaxHeight ?? 32767);
				finalHeight = Math.Max (Instance?.Manifest?.VisualElements?.RailStyle?.MinHeight ?? 20, finalHeight);
			}
			return finalHeight;
		}
		public void RequireSetTileContentHeight (double customHeight)
		{
			CompleteAnimationImmediately (TileContent, HeightProperty, _currentHeightAnimation);
			var s = GetTileContentSuggestHeight (customHeight);
			var elder = TileContent.ActualHeight;
			TileContent.Height = s;
			TransToNewHeight (TileContent, elder, s).ContinueWith (task => {
				if (TileContent == null) return;
				Dispatcher.Invoke (new Action (() =>
				{
					TileContent.Height = s;
				}));
			});
		}
		public void RequireSetTileContentHeightIgnoreAutoSize (double customHeight)
		{
			TileContent.BeginAnimation (FrameworkElement.HeightProperty, null);
			CompleteAnimationImmediately (TileContent, HeightProperty, _currentHeightAnimation);
			var elder = TileContent.ActualHeight;
			TileContent.Height = customHeight;
			TransToNewHeight (TileContent, elder, customHeight).ContinueWith (task => {
				if (TileContent == null) return;
				Dispatcher.Invoke (new Action (() =>
				{
					TileContent.Height = customHeight;
				}));
			});
		}
		public void RequireSetTileContentHeightAndSave (double customHeight)
		{
			CompleteAnimationImmediately (TileContent, HeightProperty, _currentHeightAnimation);
			try { Instance.Config.Height = customHeight; } catch { }
		}
		public void RequireLoadTileOpacityAnime (double ?target = null)
		{
			CompleteAnimationImmediately (TileContent, OpacityProperty, _currentOpacityAnimation);
			if (target == null) TransToNewOpacity (TileContent, 0);
			else TransToNewOpacity (TileContent, 0, null, TimeSpan.FromSeconds (target ?? 0.4));
		}
		private DoubleAnimation _currentHeightAnimation = null;
		private bool _isHeightAnimating = false;
		private int _heightAnimationVersion = 0;
		private double _lastTargetHeight = double.NaN;
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
			if (_isHeightAnimating)
			{
				component.Height = _lastTargetHeight;
				component.UpdateLayout ();
			}
			double originalHeight = component.Height;
			_lastTargetHeight = targetHeight;
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
			completedHandler = (s, e) =>
			{
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
			completedHandler = (s, e) =>
			{
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
		public static Tile GetTileComponent (TileStorage t, ISidebarFeatures sidebar)
		{
			Tile tileCtrl = new Tile ();
			try
			{
				TileInstance ti = new TileInstance (t, tileCtrl.TileContent, sidebar);
				ti.TileUI = tileCtrl.ApplyOverflowMode (t.Manifest.VisualElements.RailStyle.Overflow);
				ti.Initialize ();
				tileCtrl.Initialize (ti);
				if (t.Manifest.VisualElements.RailStyle.Overflow == TileOverflow.Auto)
					(ti.Config as TileConfig).AutoSize = true;
			}
			catch (Exception e)
			{
				if (tileCtrl != null) tileCtrl.Instance = null;
				tileCtrl = null;
				throw e;
			}
			return tileCtrl;
		}
		protected TileContext Context { get; private set; } = null;
		private void UserControl_Loaded (object sender, RoutedEventArgs e)
		{
			Instance?.Instance.OnLoad ();
		}
		private void UserControl_Unloaded (object sender, RoutedEventArgs e)
		{ 
			Instance?.Instance.OnUnload ();
		}
		private bool isSplitterDragging = false;
		private void Splitter_MouseLeftButtonDown (object sender, MouseButtonEventArgs e)
		{
			if (Instance.Config.AutoSize == true && Instance.Manifest.VisualElements.RailStyle.Overflow != TileOverflow.Auto)
			{
				(Instance.Config as TileConfig).AutoSize = false;
			}
			else if (Instance?.Config.AutoSize == true) return;
			if (e.ClickCount == 2)
			{
				(Instance.Config as TileConfig).AutoSize = true;
			}
			else if (e.ClickCount == 1)
			{
				if (!Instance.Config.AutoSize)
				{
					if (_currentHeightAnimation != null) _currentHeightAnimation.Duration = new Duration (new TimeSpan (0));
					_isHeightAnimating = false;
				}
			}
			Mouse.Capture (Splitter);
			isSplitterDragging = true;
			isResizing = true;
		}
		private void Splitter_MouseLeftButtonUp (object sender, MouseButtonEventArgs e)
		{
			Mouse.Capture (null);
			isSplitterDragging = false;
			isResizing = false;
		}
		private void Splitter_MouseMove (object sender, MouseEventArgs e)
		{
			if (_isHeightAnimating) return;
			if (!isSplitterDragging || Instance?.Config.AutoSize == true) return;
			double headerHeight = (TileHeader.Visibility == Visibility.Visible) ? TileHeader.ActualHeight : 0;
			double splitterHeight = Splitter.ActualHeight;
			double minContent = Instance?.Manifest.VisualElements.RailStyle.MinHeight ?? 20;   // 已由 Manifest 决定，至少20
			double maxContent = Instance?.Manifest.VisualElements.RailStyle.MaxHeight ?? 32767;   // 无限大或屏幕限制
			double newTotalHeight;
			if (!IsPinned)
			{
				// 普通磁贴：分隔条在底部，向下拖拽增加高度
				double mouseY = e.GetPosition (this).Y;
				// 不能小于 header + splitter + minContent
				double minTotal = headerHeight + splitterHeight + minContent;
				double maxTotal = headerHeight + splitterHeight + maxContent;
				newTotalHeight = Math.Max (minTotal, Math.Min (mouseY, maxTotal));
			}
			else
			{
				// 固定磁贴：分隔条在顶部，顶部可改变，底部固定
				// 获取父容器（DockPanel）并计算底部边距
				StackPanel parentStack = this.Parent as StackPanel;
				DockPanel parentDock = parentStack?.Parent as DockPanel;
				if (parentDock == null) return;
				// 当前磁贴在父容器中的位置
				Point tileOffset = this.TranslatePoint (new Point (0, 0), parentDock);
				double tileBottomInDock = tileOffset.Y + this.ActualHeight;
				// 鼠标在父容器中的 Y 坐标
				double mouseYInDock = e.GetPosition (parentDock).Y;
				// 新的总高度 = 原底部位置 - 新顶部位置
				double newTop = tileOffset.Y + (mouseYInDock - tileOffset.Y); // 实际上新顶部 = 鼠标位置
				newTop = Math.Max (0, newTop);  // 不能移出容器上方
				newTotalHeight = tileBottomInDock - newTop;
				// 同样受最小/最大内容高度限制
				double minTotal = headerHeight + splitterHeight + minContent;
				double maxTotal = headerHeight + splitterHeight + maxContent;
				newTotalHeight = Math.Max (minTotal, Math.Min (newTotalHeight, maxTotal));
			}
			// 从新的总高度反推内容高度并赋值，这会触发 Coerce 和 UpdateTotalHeight
			double newContentHeight = newTotalHeight - headerHeight - splitterHeight;
			Instance.Config.Height = newContentHeight;
		}
		private bool hasPlayedLoadAnimation = false;
		public Task UnloadTile ()
		{
			flyoutWnd?.Close ();
			propertiesWnd?.Close ();
			this.MinHeight = 0;
			TileContent.MinHeight = 0;
			return TaskExtra.WhenAll (TransToNewHeight (
				TileContent, TileContent.ActualHeight, 0, TimeSpan.FromSeconds (0.5)),
				TransToNewOpacity (TileContent, 1, 0, TimeSpan.FromSeconds (0.5))
			).ContinueWith (_ =>
			{
				Dispatcher.Invoke (new Action (() => {
					this.BeginAnimation (HeightProperty, null);
					(this.Parent as Panel)?.Children.Remove (this);
					Instance?.Instance.OnUnload ();
					Dispose ();
					App.ReleaseLargeResourcesAsync ();
				}));
			});
		}
		/// <summary>
		/// 更新右键菜单中所有菜单项的文字（多语言）
		/// </summary>
		internal void UpdateMenuLocalization ()
		{
			var loc = App.ProgramFolder.StringResources;
			AutoSizeMenuItem.Header = loc.SuitableResource ("TILE_CONTEXTMENU_AUTOSIZE", "Auto Size");
			PinBottomMenuItem.Header = loc.SuitableResource ("TILE_CONTEXTMENU_PIN", "Pin to Bottom");
			MoveUpMenuItem.Header = loc.SuitableResource ("TILE_CONTEXTMENU_MOVEUP", "Move Up");
			MoveDownMenuItem.Header = loc.SuitableResource ("TILE_CONTEXTMENU_MOVEDOWN", "Move Down");
			TilePropertiesMenuItem.Header = loc.SuitableResource ("TILE_CONTEXTMENU_PROP", "Properties");
			RemoveTileMenuItem.Header = loc.SuitableResource ("TILE_CONTEXTMENU_REMOVE", "Remove Tile");
		}
		private void TileContextMenu_Opened (object sender, RoutedEventArgs e)
		{
			var overflow = Instance?.Manifest?.VisualElements?.RailStyle?.Overflow;
			if (overflow == TileOverflow.Auto)
			{
				AutoSizeMenuItem.IsChecked = true;
				AutoSizeMenuItem.IsEnabled = false;
			}
			else
			{
				AutoSizeMenuItem.IsChecked = Instance?.Config?.AutoSize ?? false;
				AutoSizeMenuItem.IsEnabled = true;
			}
			var parentStack = this.Parent as StackPanel;
			if (parentStack != null)
			{
				int myIndex = parentStack.Children.IndexOf (this);
				MoveUpMenuItem.IsEnabled = myIndex > 0;
				MoveDownMenuItem.IsEnabled = myIndex < parentStack.Children.Count - 1;
			}
			else
			{
				MoveUpMenuItem.IsEnabled = false;
				MoveDownMenuItem.IsEnabled = false;
			}
			PinBottomMenuItem.IsChecked = Instance?.Config?.Pinned ?? false;
			bool canPin = Instance?.Manifest?.VisualElements?.RailStyle?.CanPinBottom ?? true;
			PinBottomMenuItem.IsEnabled = canPin;
			TilePropertiesMenuItem.IsEnabled = Instance?.Manifest?.VisualElements?.RailStyle?.TileHasProperties ?? false;
		}
		private void AutoSizeMenuItem_Click (object sender, RoutedEventArgs e)
		{
			var overflow = Instance?.Manifest?.VisualElements?.RailStyle?.Overflow;
			if (overflow == TileOverflow.Auto)
			{
				// 理论不会触发（会被禁掉），防御性处理
				AutoSizeMenuItem.IsChecked = true;
				return;
			}

			// 切换 AutoSize
			(Instance.Config as TileConfig).AutoSize = AutoSizeMenuItem.IsChecked;
		}
		private string TileFamilyName => Instance?.Storage?.Manifest?.Identity?.FamilyName;
		private bool IsInPinnedList => App.CurrentUserConfig.PinnedTiles.Contains (TileFamilyName);
		private void MoveUpMenuItem_Click (object sender, RoutedEventArgs e)
		{
			var main = Window.GetWindow (this) as MainWindow;
			if (main != null)
				main.MoveTile (TileFamilyName, true);
		}
		private void MoveDownMenuItem_Click (object sender, RoutedEventArgs e)
		{
			var main = Window.GetWindow (this) as MainWindow;
			if (main != null)
				main.MoveTile (TileFamilyName, false);
		}
		private void RemoveTileMenuItem_Click (object sender, RoutedEventArgs e)
		{
			if (!IsInPinnedList) return;
			string family = TileFamilyName;
			Dispatcher.BeginInvoke (new Action (() =>
			{
				App.CurrentUserConfig.PinnedTiles.Remove (family);
			}), System.Windows.Threading.DispatcherPriority.Background);
		}
		private void TilePropertiesMenuItem_Click (object sender, RoutedEventArgs e)
		{
			OpenPropertiesWindow ();
		}
		private void PinBottomMenuItem_Click (object sender, RoutedEventArgs e)
		{
			if (Instance?.Config == null) return;

			var overflow = Instance.Manifest.VisualElements.RailStyle.Overflow;
			if (!Instance.Manifest.VisualElements.RailStyle.CanPinBottom)
				return;
			bool newPinned = !Instance.Config.Pinned;
			(Instance.Config as TileConfig).Pinned = newPinned;   // 通过属性 setter 自动保存到 ini
			var main = Window.GetWindow (this) as MainWindow;
			if (main != null)
			{
				main.RefreshTileRegions ();
			}
		}
		public void Dispose ()
		{
			Instance.Config.PropertyChanged -= Config_PropertyChanged;
			try
			{
				Instance?.Instance?.OnDestroy ();
			}
			catch { }
			Instance = null;
			if (TileIcon.Source is BitmapImage)
			{
				BitmapImage bm = TileIcon.Source as BitmapImage;
				bm.StreamSource?.Dispose ();
			}
			TileIcon.Source = null;
			Context?.Dispose ();
			Context = null;
			Disposed?.Invoke (this, EventArgs.Empty);
			infocache = null;
		}
		public event EventHandler Disposed;
		private void UserControl_SizeChanged (object sender, System.Windows.SizeChangedEventArgs e)
		{
			Instance?.Instance.OnHostChanged (TileHostEvent.Resize, Context, sender);
			var sidebar = Window.GetWindow (this) as MainWindow;
			if (sidebar == null) return;
			sidebar?.OnTileHeightChanged (this, e);
		}
		private Flyout flyoutWnd = null;
		public Flyout CurrentFlyout => flyoutWnd;
		private void OpenFlyout ()
		{
			if (Instance?.Manifest?.VisualElements?.RailStyle?.TileHasFlyout != true) return;
			if (flyoutWnd != null)
			{
				flyoutWnd.Activate ();
				return;
			}
			flyoutWnd = new Flyout (this);
			Context.FlyoutWindow = flyoutWnd;
			Context.FlyoutUI = flyoutWnd.FlyoutContent;
			Context.IsFlyoutShow = false;
			RoutedEventHandler loadedHandler = null;
			SizeChangedEventHandler sizeChangedHandler = null;
			CancelEventHandler closingHandler = null;
			EventHandler closedHandler = null;
			EventHandler closedFinalHandler = null;
			loadedHandler = (sender, args) =>
			{
				Instance?.Instance.OnHostChanged (TileHostEvent.FlyoutShow, Context, sender);
			};
			sizeChangedHandler = (sender, e) =>
			{
				Instance?.Instance.OnHostChanged (TileHostEvent.FlyoutResize, Context, sender);
			};
			closingHandler = (sender, e) =>
			{
				Instance?.Instance.OnHostChanged (TileHostEvent.FlyoutClosing, Context, sender);
			};
			closedHandler = (sender, e) =>
			{
				try { flyoutWnd?.FlyoutContent?.Children?.Clear (); } catch { }
				if (flyoutWnd != null)
				{
					flyoutWnd.Loaded -= loadedHandler;
					flyoutWnd.SizeChanged -= sizeChangedHandler;
					flyoutWnd.Closing -= closingHandler;
					flyoutWnd.Closed -= closedHandler;
					flyoutWnd.Closed -= closedFinalHandler;
				}
				Context.FlyoutUI = null;
				Context.FlyoutWindow = null;
				flyoutWnd = null;
				Instance?.Instance.OnHostChanged (TileHostEvent.FlyoutClosed, Context, sender);
			};
			closedFinalHandler = (sender, e) =>
			{
				ContextMenuService.SetContextMenu (this, this.TileContextMenu);
				if (flyoutWnd?.HeaderOptions != null)
					flyoutWnd.HeaderOptions.ContextMenu = null;
				App.ReleaseLargeResourcesAsync ();
			};
			flyoutWnd.Loaded += loadedHandler;
			flyoutWnd.SizeChanged += sizeChangedHandler;
			flyoutWnd.Closing += closingHandler;
			flyoutWnd.Closed += closedHandler;
			flyoutWnd.Closed += closedFinalHandler;
			var header = flyoutWnd.HeaderOptions;
			header.ContextMenu = this.TileContextMenu;
			header.ContextMenu.PlacementTarget = header;
			header.MouseLeftButtonDown += (sender2, e2) =>
			{
				this.TileContextMenu.IsOpen = true;
				e2.Handled = true;
			};
			flyoutWnd.Width = Instance.Manifest.VisualElements.RailStyle.FlyoutWidth;
			flyoutWnd.Height = Instance.Manifest.VisualElements.RailStyle.FlyoutHeight;
			Instance.Instance.OnHostChanged (TileHostEvent.FlyoutInit, Context);
			flyoutWnd.Show ();
		}
		private ushort flyoutIconClickStatus = 0;
		private void TileFlyoutIcon_MouseDown (object sender, MouseButtonEventArgs e)
		{
			flyoutIconClickStatus |= 1;
		}
		private void TileFlyoutIcon_MouseUp (object sender, MouseButtonEventArgs e)
		{
			flyoutIconClickStatus |= 2;
			if ((flyoutIconClickStatus & 15) != 0)
			{
				flyoutIconClickStatus = 0;
				OnFlyoutIconClicked ();
			}
		}
		private void TileFlyoutIcon_TouchDown (object sender, TouchEventArgs e)
		{
			flyoutIconClickStatus |= 4;
		}
		private void TileFlyoutIcon_TouchUp (object sender, TouchEventArgs e)
		{
			flyoutIconClickStatus |= 8;
			if ((flyoutIconClickStatus & 15) != 0)
			{
				flyoutIconClickStatus = 0;
				OnFlyoutIconClicked ();
			}
		}
		private void OnFlyoutIconClicked ()
		{
			OpenFlyout ();
		}
		private TilePropertiesForm propertiesWnd = null;
		private void OpenPropertiesWindow ()
		{
			if (propertiesWnd != null)
			{
				propertiesWnd.Activate ();
				return;
			}
			propertiesWnd = new TilePropertiesForm ();
			Context.PropertiesWindow = propertiesWnd;
			Context.PropertiesContent = propertiesWnd.TilePropertiesContent;
			propertiesWnd.Title = string.Format (
				App.ProgramFolder.StringResources.SuitableResource ("TILEPROPERTIES_TITLE"),
				Instance.Storage.TileFolder.StringResources.SuitableResource (
					Instance.Instance.Manifest.Properties.DisplayName,
					Instance.Instance.Manifest.Properties.DisplayName
				)
			);
			RoutedEventHandler loadedHandler = null;
			CancelEventHandler closingHandler = null;
			EventHandler closedHandler = null;
			RoutedEventHandler okClickHandler = null;
			RoutedEventHandler cancelClickHandler = null;
			loadedHandler = (sender, e) =>
			{
				Instance.Instance.OnHostChanged (TileHostEvent.PropertiesLoad, Context, sender);
			};
			closingHandler = (sender, e) =>
			{
				Instance.Instance.OnHostChanged (TileHostEvent.PropertiesClosing, Context, sender);
			};
			closedHandler = (sender, e) =>
			{
				try { propertiesWnd?.TilePropertiesContent?.Children?.Clear (); } catch { }
				if (propertiesWnd != null)
				{
					propertiesWnd.Loaded -= loadedHandler;
					propertiesWnd.Closing -= closingHandler;
					propertiesWnd.Closed -= closedHandler;
					propertiesWnd.TilePropertiesOkButton.Click -= okClickHandler;
					propertiesWnd.TilePropertiesCancelButton.Click -= cancelClickHandler;
				}
				propertiesWnd = null;
				Context.PropertiesWindow = null;
				Context.PropertiesContent = null;
				Instance.Instance.OnHostChanged (TileHostEvent.PropertiesClosed, Context, sender);
				App.ReleaseLargeResourcesAsync ();
			};
			okClickHandler = (sender, e) =>
			{
				try
				{
					Instance.Instance.OnHostChanged (TileHostEvent.PropertiesClickOkButton, Context, sender);
					propertiesWnd.Close ();
				}
				catch (Exception ex)
				{
					MessageBox.Show (propertiesWnd, ex.Message, ex.GetType ().ToString (), MessageBoxButton.OK, MessageBoxImage.Error);
				}
			};
			cancelClickHandler = (sender, e) =>
			{
				try
				{
					Instance.Instance.OnHostChanged (TileHostEvent.PropertiesClickCancelButton, Context, sender);
					propertiesWnd.Close ();
				}
				catch (Exception ex)
				{
					MessageBox.Show (propertiesWnd, ex.Message, ex.GetType ().ToString (), MessageBoxButton.OK, MessageBoxImage.Error);
				}
			};
			propertiesWnd.Loaded += loadedHandler;
			propertiesWnd.Closing += closingHandler;
			propertiesWnd.Closed += closedHandler;
			propertiesWnd.TilePropertiesOkButton.Click += okClickHandler;
			propertiesWnd.TilePropertiesCancelButton.Click += cancelClickHandler;
			Instance.Instance.OnHostChanged (TileHostEvent.PropertiesInit, Context);
			propertiesWnd.Show ();
		}
		private Point _dragStartPoint;
		private bool _dragInProgress = false;
		private DragWindow _dragWindow;
		private double _dragWidth;
		private double _dragHeight;
		private double _dpiScaleX = 1.0;
		private double _dpiScaleY = 1.0;
		private Thickness _dragMargin;
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
		private DependencyObject _mouseDownSource;
		protected override void OnPreviewMouseLeftButtonDown (MouseButtonEventArgs e)
		{
			base.OnPreviewMouseLeftButtonDown (e);
			_dragStartPoint = e.GetPosition (this);
		}
		protected override void OnMouseMove (MouseEventArgs e)
		{
			base.OnMouseMove (e);
			if (_dragInProgress)
				return;
			if (e.LeftButton != MouseButtonState.Pressed)
				return;
			if (Mouse.Captured != null)
				return;
			var mainWindow = Window.GetWindow (this) as MainWindow;
			if (mainWindow != null)
			{
				Point mousePosInWindow = e.GetPosition (mainWindow);
				double edgeThreshold = 5.0; 
				var direction = App.CurrentUserConfig.Direction;
				bool isOnEdge = false;
				if (direction == SidebarDirection.Left)
				{
					if (mousePosInWindow.X >= mainWindow.ActualWidth - edgeThreshold)
						isOnEdge = true;
				}
				else if (direction == SidebarDirection.Right)
				{
					if (mousePosInWindow.X <= edgeThreshold)
						isOnEdge = true;
				}
				if (isOnEdge) return; 
			}
			Point pt = e.GetPosition (this);
			if (Math.Abs (pt.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
				Math.Abs (pt.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
				return;
			StartTileDrag ();
		}
		private void StartTileDrag ()
		{
			// 0. 先保存所有需要的数据（仍在可视化树中）
			MainWindow mainWin = Window.GetWindow (this) as MainWindow;
			_dragWidth = this.ActualWidth;
			_dragHeight = this.ActualHeight;

			// 获取 DPI 缩放比例
			var source = PresentationSource.FromVisual (this);
			if (source != null && source.CompositionTarget != null)
			{
				_dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
				_dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
			}
			else
			{
				_dpiScaleX = 1.0;
				_dpiScaleY = 1.0;
			}

			// 1. 获取磁贴左上角的物理坐标 → 转为逻辑坐标
			var screenPos = this.PointToScreen (new Point (0, 0));
			double logicalLeft = screenPos.X / _dpiScaleX;
			double logicalTop = screenPos.Y / _dpiScaleY;

			SetSplitterVisible (false);

			// 2. 创建虚影窗口
			_dragWindow = new DragWindow ();
			// 在 StartTileDrag 中，创建 _dragWindow 后立刻添加：
			_dragWindow.SourceInitialized += (o, args) =>
			{
				var hwnd = new WindowInteropHelper (_dragWindow).Handle;
				int extendedStyle = Win32WindowNative.GetWindowLong (hwnd, -20);
				extendedStyle |= 0x000000020;
				Win32WindowNative.SetWindowLong (hwnd, -20, extendedStyle);
			};
			_dragWindow.Owner = mainWin;

			// 读取内边距（来自 TileTemplateContainer 的 Margin）
			var container = _dragWindow.FindName ("TileTemplateContainer") as FrameworkElement;
			if (container != null)
				_dragMargin = container.Margin;
			else
				_dragMargin = new Thickness (0);

			// 窗口总尺寸 = 内容 + 内外边距
			double totalWidth = _dragWidth + _dragMargin.Left + _dragMargin.Right;
			double totalHeight = _dragHeight + _dragMargin.Top + _dragMargin.Bottom;

			// 窗口左上角 = 原磁贴逻辑左上角 - 内边距
			_dragWindow.Width = totalWidth;
			_dragWindow.Height = totalHeight;
			_dragWindow.Left = logicalLeft - _dragMargin.Left;
			_dragWindow.Top = logicalTop - _dragMargin.Top;
			_dragWindow.Topmost = true;

			var vb = new VisualBrush (this) {
				Stretch = Stretch.None,
				AlignmentX = AlignmentX.Left,
				AlignmentY = AlignmentY.Top
			};
			var rect = new Rectangle {
				Width = _dragWidth,
				Height = _dragHeight,
				Fill = vb,
				Opacity = 0.7
			};
			_dragWindow.TileTemplateContainer.Children.Clear ();
			_dragWindow.TileTemplateContainer.Children.Add (rect);
			_dragWindow.Show ();

			// 3. 移除磁贴，插入占位符
			var parentPanel = this.Parent as StackPanel;
			int originalIndex = -1;
			DragPlaceholder placeholder = null;

			if (parentPanel != null)
			{
				originalIndex = parentPanel.Children.IndexOf (this);
				parentPanel.Children.Remove (this);

				placeholder = new DragPlaceholder (_dragHeight);
				placeholder.Width = _dragWidth;
				placeholder.Tag = this;
				placeholder.IsPinned = this.IsPinned;
				parentPanel.Children.Insert (originalIndex, placeholder);

				if (mainWin != null)
					mainWin.SetDragPlaceholder (placeholder);
			}

			// 4. 启动拖放
			DataObject data = new DataObject ("SidebarTile", this);
			DragDrop.DoDragDrop (this, data, DragDropEffects.Move);

			// 5. 清理
			_dragWindow.Close ();
			_dragWindow = null;
			_dragInProgress = false;

			if (placeholder != null)
			{
				Panel p = placeholder.Parent as Panel;
				if (p != null)
					p.Children.Remove (placeholder);
			}

			SetSplitterVisible (true);
			this.Visibility = Visibility.Visible;

			if (mainWin != null)
				mainWin.RefreshTileRegions ();
		}
		protected override void OnGiveFeedback (GiveFeedbackEventArgs e)
		{
			base.OnGiveFeedback (e);
			if (_dragWindow != null)
			{
				// 获取鼠标物理坐标 → 转为逻辑坐标
				var pt = System.Windows.Forms.Control.MousePosition;
				double logicalMouseX = pt.X / _dpiScaleX;
				double logicalMouseY = pt.Y / _dpiScaleY;

				// 让虚影内容中心对齐鼠标
				_dragWindow.Left = logicalMouseX - (_dragWidth / 2.0) - _dragMargin.Left;
				_dragWindow.Top = logicalMouseY - (_dragHeight / 2.0) - _dragMargin.Top;
			}
			e.UseDefaultCursors = false;
		}
		private bool IsGestureConsumer (DependencyObject src)
		{
			while (src != null)
			{
				if (src == Splitter || src == SplitterLine1 || src == SplitterLine2)
					return false;
				// Thumb 是所有“拖动控件”的祖先：Splitter / Slider / ScrollBar
				if (src is System.Windows.Controls.Primitives.Thumb)
					return true;

				if (src is TextBoxBase ||
					src is PasswordBox ||
					src is Selector ||
					src is ButtonBase ||
					src is ComboBox)
					return true;

				src = VisualTreeHelper.GetParent (src);
			}
			return false;
		}
		private double ?_lastTileContentHeight = null;
		private bool isResizing = false;
		private void TileContent_SizeChanged (object sender, System.Windows.SizeChangedEventArgs e)
		{
			if (_lastTileContentHeight == null)
			{
				_lastTileContentHeight = TileContent.ActualHeight;
				return;
			}
			if (_isHeightAnimating)
			{
				_lastTileContentHeight = TileContent.ActualHeight;
				return;
			}
			if (isResizing)
			{
				_lastTileContentHeight = TileContent.ActualHeight;
				return;
			}
			if (isSplitterDragging)
			{
				_lastTileContentHeight = TileContent.ActualHeight;
				return;
			}
			//TileContent.BeginAnimation (FrameworkElement.HeightProperty, null);
			if (_currentHeightAnimation != null) _currentHeightAnimation.Duration = new Duration (new TimeSpan (0));
			_isHeightAnimating = false;
			TransToNewHeight (TileContent, _lastTileContentHeight ?? 0);
			_lastTileContentHeight = TileContent.ActualHeight;
		}
		private void Splitter_MouseEnter (object sender, MouseEventArgs e)
		{
			try
			{
				SplitterLine1.SetResourceReference (StyleProperty, "TileSplitterLineTopLighting");
				SplitterLine2.SetResourceReference (StyleProperty, "TileSplitterLineBottomLighting");
				SplitterLineTopFill.SetResourceReference (StyleProperty, "TileSplitterTopBlankFillLighting");
				SplitterLineBottomFill.SetResourceReference (StyleProperty, "TileSplitterBottomBlankFillLighting");
			}
			catch { }
		}
		private void Splitter_MouseLeave (object sender, MouseEventArgs e)
		{
			try
			{
				SplitterLine1.SetResourceReference (StyleProperty, "TileSplitterLineTop");
				SplitterLine2.SetResourceReference (StyleProperty, "TileSplitterLineBottom");
				SplitterLineTopFill.SetResourceReference (StyleProperty, "TileSplitterTopBlankFill");
				SplitterLineBottomFill.SetResourceReference (StyleProperty, "TileSplitterBottomBlankFill");
			}
			catch { }
		}
		public override string ToString ()
		{
			return Instance?.Manifest?.Identity?.FullName ?? base.ToString ();
		}
		private class VisualInfo: TileVisualInfo
		{
			Tile te = null;
			public VisualInfo (Tile t)
			{
				te = t;
			}
			public override UIElement TileElement => te;
			public override ImageSource TileLogo => te?.TileIcon?.Source;
			public override string TileTitle => te?.TileDisplayName?.Text;
		}
		private VisualInfo infocache = null;
		public TileVisualInfo TileVisual => infocache;
		public void RequireOpenFlyout ()
		{
			if (Instance?.Manifest?.VisualElements?.RailStyle?.TileHasFlyout == true)
			{
				OpenFlyout ();
			}
			else
			{
			}
		}
	}
}

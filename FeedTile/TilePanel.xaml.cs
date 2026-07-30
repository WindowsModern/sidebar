using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sidebar;
using WpfAnimatedGif;

namespace WindowsModern.FeedTile
{
	public partial class TilePanel: UserControl
	{
		private DispatcherTimer _timer;
		private int _currentStartIndex = 0;
		private int _itemsPerPage = 4;
		private bool _isAnimating = false;
		private bool _isLoad = false;
		private bool _dataChanged = false;
		private bool _fadeOutDone = false;
		private bool _fadeInDone = false;
		private bool _isFirstDataReceived = false;
		private DispatcherTimer _loadingTimeoutTimer;
		private bool _isTransitioning = false;

		public TilePanel ()
		{
			InitializeComponent ();
			var image = new BitmapImage ();
			image.BeginInit ();
			image.UriSource = new Uri (System.IO.Path.Combine (Tile.TileFolder.FolderPath, "Images\\Ring.gif"));
			image.EndInit ();
			ImageBehavior.SetAnimatedSource (LoadingImage, image);
			LoadingText.Text = Tile.TileFolder.StringResources.SuitableResource ("TILE_LOADING", "Loading...");
			NoItemsText.Text = Tile.TileFolder.StringResources.SuitableResource ("TILE_NOITEMS", "There's no items.");
		}

		private void UserControl_Loaded (object sender, RoutedEventArgs e)
		{
			_isLoad = true;

			// 初始状态：显示 Loading，隐藏内容容器和 NoItems
			LoadingPanel.Visibility = Visibility.Visible;
			LoadingPanel.Opacity = 1;
			NoItemsPanel.Visibility = Visibility.Collapsed;
			NoItemsPanel.Opacity = 1;
			FeedItemsContainerFront.Visibility = Visibility.Collapsed;
			FeedItemsContainerBehind.Visibility = Visibility.Collapsed;
			FeedItemsContainerFront.Opacity = 1;
			FeedItemsContainerBehind.Opacity = 0;

			Panel.SetZIndex (FeedItemsContainerFront, 1);
			Panel.SetZIndex (FeedItemsContainerBehind, 0);

			Tile.AllItems.CollectionChanged += AllItems_CollectionChanged;
			Tile.Options.TileConfig.PropertyChanged += TileConfig_PropertyChanged;
			Tile.Options.PropertyChanged += Options_PropertyChanged;

			_loadingTimeoutTimer = new DispatcherTimer ();
			_loadingTimeoutTimer.Interval = TimeSpan.FromSeconds (30);
			_loadingTimeoutTimer.Tick += LoadingTimeoutTimer_Tick;
			_loadingTimeoutTimer.Start ();


			DependencyPropertyDescriptor.FromProperty (UIElement.OpacityProperty, typeof (UIElement))
				.AddValueChanged (FeedItemsContainerFront, OnOpacityChanged);
			DependencyPropertyDescriptor.FromProperty (UIElement.OpacityProperty, typeof (UIElement))
				.AddValueChanged (FeedItemsContainerBehind, OnOpacityChanged);
			DependencyPropertyDescriptor.FromProperty (UIElement.OpacityProperty, typeof (UIElement))
				.AddValueChanged (LoadingPanel, OnOpacityChanged);
			DependencyPropertyDescriptor.FromProperty (UIElement.OpacityProperty, typeof (UIElement))
				.AddValueChanged (NoItemsPanel, OnOpacityChanged);

			ApplyAutoSizeItemsPerPage ();
			StartTimer ();


			if (Tile.Options.TileConfig.AutoSize)
			{
				UpdateLayout ();
				Tile.SidebarFeatures.Request (new SidebarRequest (Tile.TileInstance) {
					RequestName = "ResizeIgnoreAutoSize",
					RequestDatas = Tile.Options.AutoSizeTile ? double.NaN : (Tile.TileInstance?.Manifest?.VisualElements?.RailStyle?.DefaultHeight ?? 97.0)
				});
			}
			RefreshDisplay (false);
		}

		private void LoadingTimeoutTimer_Tick (object sender, EventArgs e)
		{
			_loadingTimeoutTimer?.Stop ();
			if (!_isFirstDataReceived && (Tile.AllItems == null || Tile.AllItems.Count == 0))
			{
				LoadingPanel.Visibility = Visibility.Collapsed;
				NoItemsPanel.Visibility = Visibility.Visible;
			}
		}

		private void UpdateVisibility ()
		{
			if (!_isLoad || _isTransitioning) return;

			bool hasItems = (Tile.AllItems != null && Tile.AllItems.Count > 0);
			Panel targetStatePanel = null;
			bool showContent = false;

			if (hasItems)
			{
				showContent = true;
				targetStatePanel = null;
			}
			else
			{
				targetStatePanel = !_isFirstDataReceived ? LoadingPanel : NoItemsPanel;
				showContent = false;
			}

			Panel currentStatePanel = null;
			if (LoadingPanel.Visibility == Visibility.Visible && LoadingPanel.Opacity > 0)
				currentStatePanel = LoadingPanel;
			else if (NoItemsPanel.Visibility == Visibility.Visible && NoItemsPanel.Opacity > 0)
				currentStatePanel = NoItemsPanel;

			if (showContent)
			{
				if (FeedItemsContainerFront.Visibility != Visibility.Visible)
				{
					FeedItemsContainerFront.Visibility = Visibility.Visible;
					FeedItemsContainerBehind.Visibility = Visibility.Visible;
					FeedItemsContainerFront.Opacity = 0;
					FeedItemsContainerBehind.Opacity = 0;
				}
				AnimateToContent (currentStatePanel);
			}
			else
			{
				if (targetStatePanel != null)
				{
					if (currentStatePanel != null && currentStatePanel != targetStatePanel)
					{
						AnimateStatePanelSwitch (currentStatePanel, targetStatePanel);
					}
					else if (currentStatePanel == null && (FeedItemsContainerFront.Visibility == Visibility.Visible && FeedItemsContainerFront.Opacity > 0))
					{
						AnimateContentToStatePanel (targetStatePanel);
					}
					else if (currentStatePanel == null && targetStatePanel != null)
					{
						targetStatePanel.Visibility = Visibility.Visible;
						targetStatePanel.Opacity = 1;
						_loadingTimeoutTimer?.Stop ();
					}
				}
			}
		}

		private void AnimateToContent (Panel statePanelToFadeOut)
		{
			_isTransitioning = true;
			var storyboard = new Storyboard ();

			if (statePanelToFadeOut != null && statePanelToFadeOut.Visibility == Visibility.Visible)
			{
				var fadeOutState = new DoubleAnimation (1, 0, new Duration (TimeSpan.FromSeconds (0.3)));
				Storyboard.SetTarget (fadeOutState, statePanelToFadeOut);
				Storyboard.SetTargetProperty (fadeOutState, new PropertyPath (UIElement.OpacityProperty));
				storyboard.Children.Add (fadeOutState);
			}

			var fadeInContentFront = new DoubleAnimation (0, 1, new Duration (TimeSpan.FromSeconds (0.3)));
			Storyboard.SetTarget (fadeInContentFront, FeedItemsContainerFront);
			Storyboard.SetTargetProperty (fadeInContentFront, new PropertyPath (UIElement.OpacityProperty));
			storyboard.Children.Add (fadeInContentFront);

			var fadeInContentBehind = new DoubleAnimation (0, 0, new Duration (TimeSpan.FromSeconds (0.3)));
			Storyboard.SetTarget (fadeInContentBehind, FeedItemsContainerBehind);
			Storyboard.SetTargetProperty (fadeInContentBehind, new PropertyPath (UIElement.OpacityProperty));
			storyboard.Children.Add (fadeInContentBehind);

			storyboard.Completed += (s, e) => {
				if (statePanelToFadeOut != null)
					statePanelToFadeOut.Visibility = Visibility.Collapsed;
				FeedItemsContainerFront.Visibility = Visibility.Visible;
				FeedItemsContainerBehind.Visibility = Visibility.Visible;
				FeedItemsContainerFront.Opacity = 1;
				FeedItemsContainerBehind.Opacity = 0;
				_isTransitioning = false;
				_loadingTimeoutTimer?.Stop ();
			};
			storyboard.Begin ();
		}

		private void AnimateStatePanelSwitch (Panel from, Panel to)
		{
			_isTransitioning = true;
			var storyboard = new Storyboard ();

			var fadeOut = new DoubleAnimation (1, 0, new Duration (TimeSpan.FromSeconds (0.3)));
			Storyboard.SetTarget (fadeOut, from);
			Storyboard.SetTargetProperty (fadeOut, new PropertyPath (UIElement.OpacityProperty));
			storyboard.Children.Add (fadeOut);

			to.Visibility = Visibility.Visible;
			to.Opacity = 0;
			var fadeIn = new DoubleAnimation (0, 1, new Duration (TimeSpan.FromSeconds (0.3)));
			Storyboard.SetTarget (fadeIn, to);
			Storyboard.SetTargetProperty (fadeIn, new PropertyPath (UIElement.OpacityProperty));
			storyboard.Children.Add (fadeIn);

			storyboard.Completed += (s, e) => {
				from.Visibility = Visibility.Collapsed;
				from.Opacity = 1;
				_isTransitioning = false;
				if (to == LoadingPanel) _loadingTimeoutTimer?.Start ();
				else _loadingTimeoutTimer?.Stop ();
			};
			storyboard.Begin ();
		}

		private void AnimateContentToStatePanel (Panel targetStatePanel)
		{
			_isTransitioning = true;
			var storyboard = new Storyboard ();

			var fadeOutFront = new DoubleAnimation (1, 0, new Duration (TimeSpan.FromSeconds (0.3)));
			Storyboard.SetTarget (fadeOutFront, FeedItemsContainerFront);
			Storyboard.SetTargetProperty (fadeOutFront, new PropertyPath (UIElement.OpacityProperty));
			storyboard.Children.Add (fadeOutFront);

			var fadeOutBehind = new DoubleAnimation (0, 0, new Duration (TimeSpan.FromSeconds (0.3)));
			Storyboard.SetTarget (fadeOutBehind, FeedItemsContainerBehind);
			Storyboard.SetTargetProperty (fadeOutBehind, new PropertyPath (UIElement.OpacityProperty));
			storyboard.Children.Add (fadeOutBehind);

			targetStatePanel.Visibility = Visibility.Visible;
			targetStatePanel.Opacity = 0;
			var fadeIn = new DoubleAnimation (0, 1, new Duration (TimeSpan.FromSeconds (0.3)));
			Storyboard.SetTarget (fadeIn, targetStatePanel);
			Storyboard.SetTargetProperty (fadeIn, new PropertyPath (UIElement.OpacityProperty));
			storyboard.Children.Add (fadeIn);

			storyboard.Completed += (s, e) => {
				FeedItemsContainerFront.Visibility = Visibility.Collapsed;
				FeedItemsContainerBehind.Visibility = Visibility.Collapsed;
				FeedItemsContainerFront.Opacity = 1;
				FeedItemsContainerBehind.Opacity = 0;
				_isTransitioning = false;
				if (targetStatePanel == LoadingPanel) _loadingTimeoutTimer?.Start ();
				else _loadingTimeoutTimer?.Stop ();
			};
			storyboard.Begin ();
		}

		private void Options_PropertyChanged (object sender, PropertyChangedEventArgs e)
		{
			// 使用 Dispatcher.BeginInvoke 异步执行，确保在 UI 线程上运行
			Dispatcher.BeginInvoke (new Action (() =>
			{
				switch (e.PropertyName)
				{
					case nameof (Tile.Options.AutoSizeTile):
						ApplyAutoSizeItemsPerPage ();
						UpdateLayout ();
						if (Tile.Options.TileConfig.AutoSize)
						{
							Tile.SidebarFeatures.Request (new SidebarRequest (Tile.TileInstance) {
								RequestName = "ResizeIgnoreAutoSize",
								RequestDatas = Tile.Options.AutoSizeTile ? double.NaN : (Tile.TileInstance?.Manifest?.VisualElements?.RailStyle?.DefaultHeight ?? 97.0)
							});
						}
						break;
					case nameof (Tile.Options.ShowItemsCountWhenAutoSize):
						UpdateLayout ();
						if (Tile.Options.TileConfig.AutoSize && Tile.Options.AutoSizeTile)
						{
							Tile.SidebarFeatures.Request (new SidebarRequest (Tile.TileInstance) {
								RequestName = "ResizeIgnoreAutoSize",
								RequestDatas = Tile.Options.AutoSizeTile ? double.NaN : (Tile.TileInstance?.Manifest?.VisualElements?.RailStyle?.DefaultHeight ?? 97.0)
							});
						}
						ApplyAutoSizeItemsPerPage ();
						break;
				}
			}));
		}

		private void OnOpacityChanged (object sender, EventArgs e)
		{
			var panel = sender as Panel;
			if (panel == null) return;
			if (panel.Opacity <= 0)
				panel.Visibility = Visibility.Collapsed;
			else
				panel.Visibility = Visibility.Visible;
		}

		private void TileConfig_PropertyChanged (object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof (Tile.Options.TileConfig.AutoSize))
			{
				ApplyAutoSizeItemsPerPage ();
				UpdateLayout ();
				if (Tile.Options.TileConfig.AutoSize)
				{
					Tile.SidebarFeatures.Request (new SidebarRequest (Tile.TileInstance) {
						RequestName = "ResizeIgnoreAutoSize",
						RequestDatas = Tile.Options.AutoSizeTile ? double.NaN : (Tile.TileInstance?.Manifest?.VisualElements?.RailStyle?.DefaultHeight ?? 97.0)
					});
				}
			}
		}

		private void UserControl_Unloaded (object sender, RoutedEventArgs e)
		{
			_isLoad = false;
			StopTimer ();
			Tile.AllItems.CollectionChanged -= AllItems_CollectionChanged;
			_loadingTimeoutTimer?.Stop ();
			var descriptor = DependencyPropertyDescriptor.FromProperty (UIElement.OpacityProperty, typeof (UIElement));
			descriptor.RemoveValueChanged (FeedItemsContainerFront, OnOpacityChanged);
			descriptor.RemoveValueChanged (FeedItemsContainerBehind, OnOpacityChanged);
			descriptor.RemoveValueChanged (LoadingPanel, OnOpacityChanged);
			descriptor.RemoveValueChanged (NoItemsPanel, OnOpacityChanged);
		}

		private Panel GetVisibleContainer ()
		{
			return FeedItemsContainerFront.Opacity >= 1 ? FeedItemsContainerFront : FeedItemsContainerBehind;
		}

		private Panel GetHiddenContainer ()
		{
			return FeedItemsContainerFront.Opacity < 1 ? FeedItemsContainerFront : FeedItemsContainerBehind;
		}

		private void StartTimer ()
		{
			if (_timer == null)
			{
				_timer = new DispatcherTimer ();
				_timer.Interval = TimeSpan.FromSeconds (10);
				_timer.Tick += OnTimerTick;
			}
			_timer.Start ();
		}

		private void StopTimer ()
		{
			if (_timer != null)
				_timer.Stop ();
		}
		private int count = 0;
		private void OnTimerTick (object sender, EventArgs e)
		{
			if (_isAnimating) return;
			GoToNextPage ();
			if ((count++) % 10 == 10)
			{
				count = 0;
				Utilities.ReleaseLargeResourcesAsync ();
			}
		}

		private void GoToNextPage ()
		{
			if (Tile.AllItems == null) return;
			int total = Tile.AllItems.Count;
			if (total <= _itemsPerPage) return;

			if (_dataChanged)
				_dataChanged = false;

			_currentStartIndex = (_currentStartIndex + _itemsPerPage) % total;
			PrepareNextPageAndAnimate ();
		}

		private void GoToPrevPage ()
		{
			if (Tile.AllItems == null) return;
			int total = Tile.AllItems.Count;
			if (total <= _itemsPerPage) return;

			if (_dataChanged)
				_dataChanged = false;

			_currentStartIndex = (_currentStartIndex - _itemsPerPage + total) % total;
			PrepareNextPageAndAnimate ();
		}

		private void PrepareNextPageAndAnimate ()
		{
			var items = BuildDisplayItems (_currentStartIndex);
			var hidden = GetHiddenContainer ();
			PopulateContainer (hidden, items);
			StartSwitchAnimation ();
		}

		private void StartSwitchAnimation ()
		{
			if (_isAnimating) return;
			_isAnimating = true;
			_fadeOutDone = false;
			_fadeInDone = false;

			var oldVisible = GetVisibleContainer ();
			var newVisible = GetHiddenContainer ();

			oldVisible.Visibility = Visibility.Visible;
			newVisible.Visibility = Visibility.Visible;

			Panel.SetZIndex (newVisible, 1);
			Panel.SetZIndex (oldVisible, 0);

			newVisible.Opacity = 0;
			oldVisible.Opacity = 1;
			EventHandler fadeOutComp = null;
			var fadeOut = new DoubleAnimation (1, 0, new Duration (TimeSpan.FromSeconds (0.3)));
			fadeOut.Completed += OnFadeOutCompleted;
			fadeOutComp = (s, e) => {
				//oldVisible.Visibility = Visibility.Collapsed;
				fadeOut.Completed -= fadeOutComp;
			};
			fadeOut.Completed += fadeOutComp;
			var fadeIn = new DoubleAnimation (0, 1, new Duration (TimeSpan.FromSeconds (0.3)));
			fadeIn.Completed += OnFadeInCompleted;

			oldVisible.BeginAnimation (OpacityProperty, fadeOut);
			newVisible.BeginAnimation (OpacityProperty, fadeIn);
		}

		private void OnFadeOutCompleted (object sender, EventArgs e) { _fadeOutDone = true; CheckAnimationsComplete (); }
		private void OnFadeInCompleted (object sender, EventArgs e) { _fadeInDone = true; CheckAnimationsComplete (); }

		private void CheckAnimationsComplete ()
		{
			if (!_fadeInDone || !_fadeOutDone)
				return;

			var oldVisible = GetVisibleContainer ();
			var newVisible = GetHiddenContainer ();

			oldVisible.Opacity = 0;
			newVisible.Opacity = 1;
			Panel.SetZIndex (newVisible, 1);
			Panel.SetZIndex (oldVisible, 0);

			_isAnimating = false;
		}

		private void PopulateContainer (Panel container, List<FeedItemData> items)
		{
			container.Children.Clear ();
			if (items != null)
			{
				foreach (var item in items)
				{
					if (item == null) continue;
					var feedItem = new TileFeedItem ();
					feedItem.SetItemValue (item);
					container.Children.Add (feedItem);
				}
			}
		}

		private List<FeedItemData> BuildDisplayItems (int startIndex)
		{
			var result = new List<FeedItemData> ();
			if (Tile.AllItems == null) return result;
			int total = Math.Min (Tile.AllItems.Count, Tile.Options.ShowCountLimit);
			if (total == 0) return result;
			for (int i = 0; i < _itemsPerPage; i++)
			{
				int idx = (startIndex + i) % total;
				result.Add (Tile.AllItems [idx]);
			}
			return result;
		}

		private void AdjustCurrentIndexAfterDataChange ()
		{
			if (Tile.AllItems == null || Tile.AllItems.Count == 0)
			{
				_currentStartIndex = 0;
				return;
			}
			var visible = GetVisibleContainer ();
			var displayedIds = visible.Children
				.OfType<TileFeedItem> ()
				.Select (x => x.GetLocalId ())
				.Where (x => !string.IsNullOrEmpty (x))
				.ToList ();

			if (displayedIds.Count == 0)
			{
				_currentStartIndex = 0;
				return;
			}

			int newIndex = -1;
			for (int i = 0; i < Tile.AllItems.Count; i++)
			{
				if (displayedIds.Contains (Tile.AllItems [i].LocalId))
				{
					newIndex = i;
					break;
				}
			}
			if (newIndex < 0)
				newIndex = 0;
			_currentStartIndex = newIndex;
		}

		private void AllItems_CollectionChanged (object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			try
			{
				if (!_isLoad) return;
				if (Tile.AllItems != null && Tile.AllItems.Count > 0 && !_isFirstDataReceived)
				{
					_isFirstDataReceived = true;
					// 立即刷新显示（无动画），确保内容容器被填充
					RefreshDisplay (false);
				}
				if (Tile.Options.TileConfig.AutoSize && !Tile.Options.AutoSizeTile)
				{
					UpdateLayout ();
					double targetHeight = Tile.TileInstance?.Manifest?.VisualElements?.RailStyle?.DefaultHeight ?? 97.0;
					Tile.SidebarFeatures.Request (new SidebarRequest (Tile.TileInstance) {
						RequestName = "ResizeIgnoreAutoSize",
						RequestDatas = targetHeight
					});
				}
				UpdateVisibility ();
				AdjustCurrentIndexAfterDataChange ();
				_dataChanged = true;
			}
			catch { }
		}

		public void RefreshDisplay (bool animate)
		{
			UpdateVisibility ();
			if (Tile.AllItems == null || Tile.AllItems.Count == 0)
			{
				FeedItemsContainerFront.Children.Clear ();
				FeedItemsContainerBehind.Children.Clear ();
				return;
			}

			FeedItemsContainerFront.Visibility = Visibility.Visible;
			FeedItemsContainerBehind.Visibility = Visibility.Visible;

			var items = BuildDisplayItems (_currentStartIndex);

			if (!animate)
			{
				var visible = GetVisibleContainer ();
				visible.Children.Clear ();
				PopulateContainer (visible, items);
				visible.Opacity = 1;
				var hidden = GetHiddenContainer ();
				hidden.Opacity = 0;
				hidden.Visibility = Visibility.Collapsed;
			}
			else
			{
				var hidden = GetHiddenContainer ();
				hidden.Children.Clear ();
				PopulateContainer (hidden, items);
				StartSwitchAnimation ();
			}
		}

		public void SetItemsPerPage (int count)
		{
			if (count <= 0) return;
			if (count == _itemsPerPage) return;

			string anchorId = null;
			var visible = GetVisibleContainer ();
			var first = visible.Children.OfType<TileFeedItem> ().FirstOrDefault ();
			if (first != null)
				anchorId = first.GetLocalId ();

			_itemsPerPage = count;

			if (!string.IsNullOrEmpty (anchorId) && Tile.AllItems != null)
			{
				for (int i = 0; i < Tile.AllItems.Count; i++)
				{
					if (Tile.AllItems [i].LocalId == anchorId)
					{
						_currentStartIndex = i;
						break;
					}
				}
			}
			else
			{
				_currentStartIndex = 0;
			}

			RefreshDisplay (false);
		}

		private void ApplyAutoSizeItemsPerPage ()
		{
			if (Tile.Options.TileConfig.AutoSize && Tile.Options.AutoSizeTile)
			{
				int fixedCount = Tile.Options.ShowItemsCountWhenAutoSize;
				if (fixedCount != _itemsPerPage)
				{
					_itemsPerPage = fixedCount;
					_currentStartIndex = 0;
					RefreshDisplay (false);
				}
			}
			else
			{
				UpdateItemsPerPageByHeight ();
			}
		}

		private void UpdateItemsPerPageByHeight ()
		{
			if (Tile.Options.TileConfig.AutoSize)
				return;

			if (double.IsNaN (this.ActualHeight) || this.ActualHeight <= 0)
				return;
			double itemHeight = GetItemEstimatedHeight ();
			int maxItems = Math.Max (1, (int)(this.ActualHeight / itemHeight));
			SetItemsPerPage (maxItems);
		}

		private double GetItemEstimatedHeight ()
		{
			var tempItem = new TileFeedItem ();
			tempItem.SetItemValue (new FeedItemData { Title = "Test" });
			tempItem.Measure (new Size (double.PositiveInfinity, double.PositiveInfinity));
			double desiredHeight = tempItem.DesiredSize.Height;
			if (desiredHeight <= 0)
				desiredHeight = 30;
			return desiredHeight;
		}

		private void UserControl_SizeChanged (object sender, System.Windows.SizeChangedEventArgs e)
		{
			if (e.NewSize.Height != e.PreviousSize.Height)
			{
				if (!(Tile.Options.TileConfig.AutoSize && Tile.Options.AutoSizeTile))
					UpdateItemsPerPageByHeight ();
			}
		}
	}
}
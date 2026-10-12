using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Media.Animation;
using System.Windows.Controls.Primitives;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Sidebar
{
	/// <summary>
	/// TileControl.xaml 的交互逻辑
	/// </summary>
	public partial class TileControl: UserControl, ITileElement
	{
		private TileContext tileContext = null;
		public TileInstance Instance { get; private set; }
		public TileApplicationStorage AppStorage { get; private set; }
		public string UserModelId => AppStorage?.UserModelId;
		#region Instance
		private void SetInstance (TileInstance inst)
		{
			if (Instance != null)
			{
				Instance.Config.PropertyChanged -= Config_PropertyChanged;
				TileContentContainer.Children.Clear ();
			}
			Instance = inst;
			if (Instance != null)
			{
				Instance.Config.PropertyChanged -= Config_PropertyChanged;
				Instance.Config.PropertyChanged += Config_PropertyChanged;
			}
		}
		private void Config_PropertyChanged (object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (!Dispatcher.CheckAccess ())
			{
				Dispatcher.BeginInvoke (System.Windows.Threading.DispatcherPriority.DataBind, new Action (() => Config_PropertyChanged (sender, e)));
				return;
			}
			switch (e.PropertyName)
			{
				case nameof (TileConfig.Pinned):
					IsPinned = Instance?.Config?.Pinned ?? false;
					break;
				case nameof (TileConfig.AutoSize):
				case nameof (TileConfig.Height):
					//if (_heightInitialized && !_isAnimating && !_isDragging)
						//PlayRuntimeHeightAnimation ();
					break;
			}
		}
		private void SetAppStorage (TileApplicationStorage tas)
		{
			AppStorage = tas;
			ContentContainer = InitTileContentContainer ();
			if (tas.Infomation.VisualElements.Tile.Overflow != TileOverflow.Auto)
			{
				TileContentContainerControl.MaxHeight = tas.Infomation.VisualElements.Tile.MaxHeight;
			}
			TileSplitterThumb.IsEnabled = tas.Infomation.VisualElements.Tile.Overflow != TileOverflow.Auto;
		}
		public static TileControl CreateTileControl (TileApplicationStorage tas, ISidebarFeatures isf = null)
		{
			var ctrl = new TileControl ();
			ctrl.SetAppStorage (tas);
			var inst = new TileInstance (tas, ctrl.ContentContainer, isf);
			ctrl.SetInstance (inst);
			inst.Initialize ();
			ctrl.DisplayName = tas.DisplayName;
			ctrl.Icon = tas.LogoImage;
			return ctrl;
		}
		#endregion
		public TileControl ()
		{
			InitializeComponent ();
			tileContext = new TileContext (this, TileContentContainerControl);
			VisualView = new TileVisualView (this);
		}
		public Task DestroyTileControl ()
		{
			var tcs = new TaskCompletionSource<object> ();
			this.SetTimeout (() => {
				Height = ActualHeight;
				var sb = Resources ["TileUnloadAnimation"] as Storyboard;
				if (sb == null)
				{
					tcs.TrySetResult (null);
					return;
				}
				EventHandler completed = null;
				completed = (s, e) => {
					sb.Completed -= completed;
					tcs.TrySetResult (null);
				};
				sb.Completed += completed;
				sb.Begin (this);
			}, 0);
			return tcs.Task.ContinueWith (_ => {
				(Parent as Panel)?.Children?.Remove (this);
				Instance?.Instance?.OnDestroy ();
				tileContext?.Dispose ();
				tileContext = null;
			}, TaskScheduler.FromCurrentSynchronizationContext ());
		}
		#region Public Properties
		public Panel ContentContainer { get; private set; }
		public string DisplayName
		{
			get { return TileDisplayNameControl?.Text; }
			set { TileDisplayNameControl.Text = value; }
		}
		public ImageSource Icon
		{
			get { return TileDisplayImageControl?.Source; }
			set { TileDisplayImageControl.Source = value; }
		}
		public Panel InitTileContentContainer ()
		{
			switch (AppStorage.Infomation.VisualElements.Tile.Overflow)
			{
				default:
				case TileOverflow.Auto:
				case TileOverflow.Hidden:
					return TileContentContainer;
				case TileOverflow.Scale:
					var vb = new Viewbox ();
					TileContentContainer.Children.Add (vb);
					var grid = new Grid ();
					vb.Child = grid;
					return grid;
				case TileOverflow.Scroll:
					var sv = new ScrollViewer ();
					sv.SetResourceReference (ScrollViewer.StyleProperty, "AutoHideScrollViewer");
					TileContentContainer.Children.Add (sv);
					var sg = new Grid ();
					sv.Content = sg;
					return sg;
			}
			return TileContentContainer;
		}
		public TileVisualView VisualView { get; set; }
		#endregion
		#region Pinned
		public static readonly DependencyProperty IsPinnedProperty = DependencyProperty.Register (nameof (IsPinned), typeof (bool), typeof (TileControl), new FrameworkPropertyMetadata (false, FrameworkPropertyMetadataOptions.AffectsRender));
		public bool IsPinned { get { return (bool)GetValue (IsPinnedProperty); } set { SetValue (IsPinnedProperty, value); } }
		#endregion
		#region Height
		private void TileFirstLoadStoryboard_Completed (object sender, EventArgs e)
		{
			Height = double.NaN;
			isfirstrunfinish = true;
			TileContentContainerControl.MinHeight = AppStorage.Infomation.VisualElements.Tile.MinHeight;
		}
		bool isfirstrun = false;
		bool isfirstrunfinish = false;
		private double InitContentHeight ()
		{
			var tc = Instance.Config;
			var ovfl = AppStorage.Infomation.VisualElements.Tile.Overflow;
			if (ovfl == TileOverflow.Auto || tc.AutoSize) return double.NaN;
			var ret = tc.Height;
			double minh = AppStorage.Infomation.VisualElements.Tile.MinHeight,
				maxh = AppStorage.Infomation.VisualElements.Tile.MaxHeight;
			if (ret < minh) ret = AppStorage.Infomation.VisualElements.Tile.DefaultHeight;
			if (ret > maxh) ret = maxh;
			return ret;
		}
		public bool IsOverflowAutoMode => AppStorage?.Infomation?.VisualElements?.Tile?.Overflow == TileOverflow.Auto;
		public bool IsAutoMode => IsOverflowAutoMode || (Instance?.Config?.AutoSize ?? false);
		private void InitHeight ()
		{
			if (isfirstrun) return;
			var ht = InitContentHeight ();
			TileContentContainerControl.Height = ht;
			if (!IsOverflowAutoMode)
			{
				var vet = AppStorage.Infomation.VisualElements.Tile;
				TileContentContainerControl.MinHeight = vet.MinHeight;
				TileContentControlContainer.MaxHeight = vet.MaxHeight;
			} 
			isfirstrun = true;
			UpdateLayout ();
			TimeoutExtensions.SetTimeout (this, () => {
				var sb = Resources ["TileFirstLoadAnimation"] as Storyboard;
				var da = sb.Children.First () as DoubleAnimation;
				da.To = ActualHeight;
				sb.Begin ();
				Visibility = Visibility.Visible;
			});
		}
		public void ResetFirstStatus ()
		{
			isfirstrun = false;
			isfirstrunfinish = false;
			Visibility = Visibility.Hidden;
		}
		private void TileContentContainerControl_SizeChanged (object sender, System.Windows.SizeChangedEventArgs e)
		{
			Instance?.Instance?.OnHostChanged (TileHostEvent.Resize, tileContext, sender);
		}
		private double lastTCCHeight = -1;
		private bool isanimating = false;
		private void TileContentContainer_SizeChanged (object sender, System.Windows.SizeChangedEventArgs e)
		{
			if (!isfirstrunfinish || !isfirstrun || isanimating || _isThumbDragging)
			{
				lastTCCHeight = TileContentContainer.ActualHeight;
				return;
			}
			if (lastTCCHeight < 0) lastTCCHeight = TileContentContainer.ActualHeight;
			if (IsAutoMode)
			{
				var sb = Resources ["TileHeightChangeAnimation"] as Storyboard;
				//sb.Stop ();
				var da = sb.Children.First () as DoubleAnimation;
				da.From = lastTCCHeight;
				da.To = TileContentContainerControl.ActualHeight;
				isanimating = true;
				sb.Begin ();
			}
			lastTCCHeight = TileContentContainer.ActualHeight;
		}
		private void TileHeightChangeAnimation_Completed (object sender, EventArgs e)
		{
			isanimating = false;
			if (IsAutoMode)
			{
				TileContentContainerControl.Height = double.NaN;
			}
			else
			{
				TileContentContainerControl.Height = InitContentHeight ();
			}
		}
		private void RootTileElement_SizeChanged (object sender, System.Windows.SizeChangedEventArgs e)
		{
			Instance?.Instance?.OnHostChanged (TileHostEvent.Resize, tileContext, sender);
		}
		private double _dragStartContentHeight;
		private bool _isThumbDragging;
		private bool _dragChangedAutoSize;
		private void TileSplitterThumb_DragStarted (object sender, DragStartedEventArgs e)
		{
			if (Instance?.Config != null) Instance.Config.AutoSize = false;
			_isThumbDragging = true;
			_dragChangedAutoSize = false;
			TileContentContainerControl.BeginAnimation (HeightProperty, null);
			isanimating = false;
			_dragStartContentHeight = TileContentContainerControl.ActualHeight;
			if (double.IsNaN (_dragStartContentHeight) || _dragStartContentHeight <= 0)
				_dragStartContentHeight = TileContentContainer.ActualHeight;
		}
		private void TileSplitterThumb_DragDelta (object sender, DragDeltaEventArgs e)
		{
			if (!_isThumbDragging) return;
			if (Instance?.Config?.AutoSize ?? false)
			{
				if (Instance?.Config != null) Instance.Config.AutoSize = false;
				_dragChangedAutoSize = true;
			}
			double currentHeight = TileContentContainerControl.ActualHeight;
			if (double.IsNaN (currentHeight) || currentHeight <= 0)
				currentHeight = TileContentContainer.ActualHeight;
			double delta = e.VerticalChange;
			var dock = DockPanel.GetDock (TileSplitterThumb);
			double newHeight;
			if (dock == Dock.Bottom)
				newHeight = currentHeight + delta;
			else if (dock == Dock.Top)
				newHeight = currentHeight - delta;
			else
				newHeight = currentHeight - delta;
			var ve = AppStorage.Infomation.VisualElements.Tile;
			double minH = (ve?.MinHeight ?? 10) > 0 ? (ve?.MinHeight ?? 10) : 0;
			double maxH = (ve?.MaxHeight ?? 32768) > 0 ? (ve?.MaxHeight ?? 32768) : double.PositiveInfinity;
			newHeight = Math.Max (minH, Math.Min (maxH, newHeight));
			if (newHeight < 0) newHeight = 0;
			TileContentContainerControl.Height = newHeight;
		}
		private void TileSplitterThumb_DragCompleted (object sender, DragCompletedEventArgs e)
		{
			_isThumbDragging = false;
		}
		private void TileSplitterThumb_PreviewMouseLeftButtonDown (object sender, MouseButtonEventArgs e)
		{
			if (e.ClickCount == 2)
			{
				ToggleAutoSize ();
				e.Handled = true;
			}
		}
		private void ToggleAutoSize ()
		{
			if (Instance == null || AppStorage == null) return;
			bool newAutoSize = !Instance.Config.AutoSize;
			Instance.Config.AutoSize = newAutoSize;
			if (newAutoSize)
			{
				TileContentContainerControl.BeginAnimation (HeightProperty, null);
				TileContentContainerControl.Height = double.NaN;
			}
			else
			{
				var sb = Resources ["TileHeightChangeAnimation"] as Storyboard;
				sb.Stop ();
				var da = sb.Children.First () as DoubleAnimation;
				da.From = lastTCCHeight;
				da.To = InitContentHeight ();
				isanimating = true;
				sb.Begin ();
			}
		}
		public double ContentActualHeight => TileContentContainerControl.ActualHeight;
		public double BorderActualHeight => ActualHeight - ContentActualHeight;
		public double? TargetHeightWhenAutoSize = null;
		public double ContentMinHeight => AppStorage?.Infomation?.VisualElements?.Tile?.MinHeight ?? 10;
		public double ContentMaxHeight => AppStorage?.Infomation?.VisualElements?.Tile?.MaxHeight ?? 32768;
		#endregion
		#region Context Menu
		private void MenuItemAutoSize_Click (object sender, RoutedEventArgs e)
		{
			ToggleAutoSize ();
		}
		private void MenuItemPinnedBottom_Click (object sender, RoutedEventArgs e)
		{
			var host = Window.GetWindow (this) as SidebarWindow;
			if (host == null) return;
			if (MenuItemPinnedBottom.IsChecked) host.PinTileToBottom (this);
			else host.PinTileToTop (this);
		}
		private void MenuItemMoveUp_Click (object sender, RoutedEventArgs e)
		{
			var host = Window.GetWindow (this) as SidebarWindow;
			host?.MoveTileUp (this);
		}
		private void MenuItemMoveDown_Click (object sender, RoutedEventArgs e)
		{
			var host = Window.GetWindow (this) as SidebarWindow;
			host?.MoveTileDown (this);
		}
		private void MenuItemProperties_Click (object sender, RoutedEventArgs e)
		{

		}
		private void MenuItemRemove_Click (object sender, RoutedEventArgs e)
		{
			var host = Window.GetWindow (this) as SidebarWindow;
			host?.RemoveTile (this);
		}
		private void TileContextMenu_Opened (object sender, RoutedEventArgs e)
		{
			if (Instance?.Config != null)
			{
				MenuItemAutoSize.IsChecked = Instance.Config.AutoSize;
				MenuItemPinnedBottom.IsChecked = Instance.Config.Pinned;
			}
			MenuItemProperties.IsEnabled = AppStorage?.Infomation?.HasProperties ?? false;
			var host = Window.GetWindow (this) as SidebarWindow;
			MenuItemMoveUp.IsEnabled = host?.CanMoveTileUp (this) == true;
			MenuItemMoveDown.IsEnabled = host?.CanMoveTileDown (this) == true;
		}
		#endregion
		#region Overflow (Hide Tile)
		public bool IsSizeHeightWhenOverflowed = false;
		#endregion
		private void RootTileElement_Loaded (object sender, RoutedEventArgs e)
		{
			Instance?.Instance?.OnLoad ();
			InitHeight ();


		}
		private void RootTileElement_Unloaded (object sender, RoutedEventArgs e)
		{
			Instance?.Instance?.OnUnload ();
		}

	}
	public struct TileVisualView: ITileElement
	{
		private ITileElement ite;
		public Panel ContentContainer => ite?.ContentContainer;
		public string DisplayName => ite?.DisplayName;
		public ImageSource Icon => ite?.Icon;
		public TileVisualView (ITileElement _ite) { ite = _ite; }
	}
}
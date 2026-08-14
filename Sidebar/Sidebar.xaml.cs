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
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using System.Diagnostics;
using System.Collections;

namespace Sidebar
{
	/// <summary>
	/// MainWindow.xaml 的交互逻辑
	/// </summary>
	public partial class MainWindow: Window, ISidebarFeatures
	{
		internal SidebarContext Context = new SidebarContext ();
		private HwndSource _source;
		private SidebarAppBar appbar;
		private TaskbarOverlapHelper tboHelper;
		private bool isClosed = false;
		private bool isSourceInitialized = false;
		protected IntPtr Handle { get; private set; } = IntPtr.Zero;
		protected View<string> PinnedTopTiles { get; set; }
		protected View<string> PinnedBottomTiles { get; set; }
		public MainWindow ()
		{
			InitializeComponent ();
			InitScreenChangeEvent ();
			UpdateTopOptionsContextMenuLocaleStrings ();
			TopOptionsPanel.MouseLeftButtonDown += (sender, e) => {
				var contextMenu = TopOptionsPanel.ContextMenu;
				if (contextMenu != null)
				{
					contextMenu.PlacementTarget = TopOptionsPanel;
					contextMenu.IsOpen = true;
				}
			};
			LoadTiles ();
			App.CurrentUserConfig.PinnedTiles.CollectionChanged += (s, e) =>
				Dispatcher.Invoke ((Action)(() => RefreshTileRegions ()));
			InitTrayIcon ();
			this.StateChanged += MainWindow_StateChanged;
			InitDragTileAbout ();
			ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;
			App.CurrentUserConfig.PropertyChanged += CurrentUserConfig_PropertyChanged;
			if (DWMAPI.IsElderWindows ()) AllowsTransparency = false;
			SidebarPipe.Mail += Pipe_OnMail;
			OverflowTiles.CollectionChanged += OverflowTiles_CollectionChanged;
			TileOverflowItems.ItemsSource = OverflowTiles;
			PinnedTopTiles = new View<string> (App.CurrentUserConfig.PinnedTiles, (item, index) => {
				var ts = App.TileMgr.GetByFamilyName (item);
				if (ts == null) return false;
				return !ts.TileCurrentUserFolder.InitConfig ["Settings"].GetKey ("Pinned").ReadBool (false);
			});
			PinnedBottomTiles = new View<string> (App.CurrentUserConfig.PinnedTiles, (item, index) => {
				var ts = App.TileMgr.GetByFamilyName (item);
				if (ts == null) return false;
				return ts.TileCurrentUserFolder.InitConfig ["Settings"].GetKey ("Pinned").ReadBool (false);
			});
			var i = 9;
		}
		private void OverflowTiles_CollectionChanged (object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			Dispatcher.Invoke (new Action (() => {
				if (OverflowTiles.Count == 0)
					OverflowTilesRegion.Height = 0;
				else
					OverflowTilesRegion.Height = double.NaN; // 自动
			}));
		}
		private void Pipe_OnMail (string name, object data, Type datatype)
		{
			if (isClosed) return;
			switch (name)
			{
				case "NotifyUnpinTile":
					{
						var familyName = data as string;
						if (string.IsNullOrWhiteSpace (familyName)) return;
						var pinnedTiles = App.CurrentUserConfig.PinnedTiles;
						if (pinnedTiles.Contains (familyName))
						{
							pinnedTiles.Remove (familyName);
						}
					}
					break;
				case "NotifyPinTile":
					{
						var familyName = data as string;
						if (string.IsNullOrWhiteSpace (familyName)) return;
						var pinnedTiles = App.CurrentUserConfig.PinnedTiles;
						if (!pinnedTiles.Contains (familyName))
						{
							pinnedTiles.Add (familyName);
						}
					}
					break;
			}
		}
		private void ApplyAllSettings ()
		{
			ApplySetting ("Direction");
			ApplySetting ("Width");
			ApplySetting ("Topmost");
			ApplySetting ("Screen");
			ApplySetting ("OccupyWorkingArea");
			ApplySetting ("OverlapTaskbar");
			ApplySetting (nameof (App.CurrentUserConfig.CanScroll));
		}
		private void ApplySetting (string propertyName)
		{
			var cc = App.CurrentUserConfig;
			if (cc == null) return;
			switch (propertyName)
			{
				case "Direction":
					Context.Direction = cc.Direction;
					foreach (var kv in tileCache)
						kv.Value?.TileInstance?.Instance?.OnHostChanged (TileHostEvent.SidebarDirectionChanged, Context);
					if (appbar != null && appbar.IsRegistered) appbar.Direction = cc.Direction;
					if (tboHelper != null) tboHelper.Direction = cc.Direction;
					break;
				case "Width":
					Width = Math.Max (MinWidth, Math.Min (cc.Width, MaxWidth));
					break;
				case "Topmost":
					Topmost = cc.Topmost;
					break;
				case "OccupyWorkingArea":
					if (!isSourceInitialized) return;
					if (cc.OccupyWorkingArea) appbar.Register ();
					else appbar.Unregister ();
					break;
				case "Screen":
					if (tboHelper != null) tboHelper.CurrentScreen = cc.CurrentScreen;
					break;
				case "Locked":
				case "ThemeName":
					break;
				case "AutoRun":
					if (StartupManager.IsEnabled () != cc.AutoRun)
					{
						if (cc.AutoRun) StartupManager.Enable ();
						else StartupManager.Disable ();
					}
					break;
				case "OverlapTaskbar":
					if (cc.OverlapTaskbar) tboHelper.Register ();
					else tboHelper.Unregister ();
					break;
				case nameof (cc.CanScroll):
					SwitchScrollMode (cc.CanScroll);
					DispatcherTimer timer = new DispatcherTimer ();
					timer.Interval = TimeSpan.FromMilliseconds (500);
					EventHandler eh = null;
					eh = (sender, e) => {
						timer.Stop ();
						OnTileHeightChanged (this, null);
						if (timer != null) timer.Tick -= eh;
					};
					timer.Tick -= eh;
					timer.Tick += eh;
					timer.Start ();
					break;
			}
			switch (propertyName)
			{
				case "Direction":
				case "Width":
				case "OccupyWorkingArea":
				case "Screen":
				case "OverlapTaskbar":
					UnionUpdatePosition ();
					break;
			}
		}
		private void CurrentUserConfig_PropertyChanged (object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			ApplySetting (e.PropertyName);
		}
		private void ThemeManager_ThemeChanged (Theme obj)
		{
			Context.Theme = obj;
			Context.ThemeName = obj?.ThemeName;
			foreach (var kv in tileCache)
				kv.Value?.TileInstance?.Instance?.OnHostChanged (TileHostEvent.ThemeChanged, Context);
			bool enableBlur = false;
			if (Application.Current.Resources.Contains ("EnableBlur"))
				enableBlur = (bool)Application.Current.Resources ["EnableBlur"];
			ChangeAeroStatus (enableBlur);
		}
		private Dictionary<string, Tile> tileCache = new Dictionary<string, Tile> ();
		private bool IsTilePinned (string familyName)
		{
			var ts = App.TileMgr.GetByFamilyName (familyName);
			if (ts == null) return false;
			var config = new TileConfig (ts.TileCurrentUserFolder);
			return config.Pinned;
		}
		private Tile GetOrCreateTile (string familyName)
		{
			Tile tile;
			if (tileCache.TryGetValue (familyName, out tile)) return tile;
			var ts = App.TileMgr.GetByFamilyName (familyName);
			if (ts == null) return null;
			tile = Tile.GetTileComponent (ts, this);
			tileCache [familyName] = tile;
			tile.Disposed += (s, e) => {
				tileCache.Remove (familyName);
			};
			return tile;
		}
		private void LoadTiles ()
		{
			RefreshTileRegions ();
		}
		public void RefreshTileRegions ()
		{
			var desiredList = App.CurrentUserConfig.PinnedTiles.Distinct ().ToList (); // ObservableCollection 转 List
			var toRemove = new List<string> ();
			var taskList = new List<Task> ();
			foreach (var pair in tileCache)
			{
				if (!desiredList.Contains (pair.Key))
					toRemove.Add (pair.Key);
			}
			foreach (var key in toRemove)
			{
				Tile tile;
				if (tileCache.TryGetValue (key, out tile))
				{
					OverflowTiles.Remove (tile.TileVisual);
					taskList.Add (tile.UnloadTile ());
					//tileCache.Remove (key);
				}
			}
			TaskExtra.WhenAll (taskList.ToArray ()).ContinueWith (_ => {
				ArrangeRegion (PinnedTilesRegion, desiredList, true);
				ArrangeRegion (TilesRegion, desiredList, false);
				OnTileHeightChanged (null, null);
				App.ReleaseLargeResourcesAsync ();
			}, TaskScheduler.FromCurrentSynchronizationContext ()); // 确保在 UI 线程执行
			PinnedTopTiles?.Refresh ();
			PinnedBottomTiles?.Refresh ();
		}
		private void ArrangeRegion (StackPanel panel, List<string> orderedFamilyNames, bool targetPinned)
		{
			var expected = orderedFamilyNames.Where (f => IsTilePinned (f) == targetPinned).ToList ();
			for (int i = panel.Children.Count - 1; i >= 0; i--)
			{
				var tile = panel.Children [i] as Tile;
				if (tile == null)
				{
					panel.Children.RemoveAt (i);
					continue;
				}
				string family = tile.TileInstance.Storage.Manifest.Identity.FamilyName;
				if (!expected.Contains (family))
				{
					panel.Children.RemoveAt (i);
				}
			}
			for (int idx = 0; idx < expected.Count; idx++)
			{
				string family = expected [idx];
				Tile tile = GetOrCreateTile (family);
				if (tile == null) continue;

				if (panel.Children.Count <= idx || panel.Children [idx] != tile)
				{
					(tile.Parent as Panel)?.Children.Remove (tile);
					SafeInsert (panel.Children, idx, tile);
				}
			}
			while (panel.Children.Count > expected.Count)
				panel.Children.RemoveAt (panel.Children.Count - 1);
		}
		public void MoveTile (string familyName, bool up)
		{
			var list = App.CurrentUserConfig.PinnedTiles;
			int idx = list.IndexOf (familyName);
			if (idx < 0) return;
			bool currentPinned = IsTilePinned (familyName);
			int step = up ? -1 : 1;
			int targetIdx = idx + step;
			while (targetIdx >= 0 && targetIdx < list.Count)
			{
				if (IsTilePinned (list [targetIdx]) == currentPinned)
					break;
				targetIdx += step;
			}
			if (targetIdx < 0 || targetIdx >= list.Count) return;
			list.Move (idx, targetIdx);
		}
		private void InitScreenChangeEvent ()
		{
			ScreenChangeNotifier.ScaleChanged += ScreenChangeNotifier_ScaleChanged;
			ScreenChangeNotifier.SizeChanged += ScreenChangeNotifier_SizeChanged;
			ScreenChangeNotifier.WorkingAreaHeightChanged += ScreenChangeNotifier_WorkingAreaHeightChanged;
			ScreenChangeNotifier.StartListening (this);
			UpdateMinMaxWidth ();
		}
		private void ScreenChangeNotifier_WorkingAreaHeightChanged (object sender, EventArgs e)
		{
			UnionUpdatePosition ();
		}
		private void UnionUpdatePosition ()
		{
			if (!isSourceInitialized) return;
			var cc = App.CurrentUserConfig;
			if (cc.OccupyWorkingArea && appbar.IsRegistered) appbar.SizeAppBar ();
			else appbar.SetPos ();
		}
		private void ScreenChangeNotifier_SizeChanged (object sender, EventArgs e)
		{
			UpdateMinMaxWidth ();
			UnionUpdatePosition ();
		}
		private void ScreenChangeNotifier_ScaleChanged (object sender, EventArgs e)
		{
			UpdateMinMaxWidth ();
			UnionUpdatePosition ();
		}
		private void Window_SizeChanged (object sender, System.Windows.SizeChangedEventArgs e)
		{

		}
		private void Window_Unloaded (object sender, RoutedEventArgs e)
		{
			ScreenChangeNotifier.StopListening ();
		}
		private void Window_Loaded (object sender, RoutedEventArgs e)
		{
			ApplyAllSettings ();
			overflowTilesContainer = new HideTilesContainWindow (this);
			overflowTilesContainer.Show ();
		}
		private void LoadAnimation_Completed (object sender, EventArgs e)
		{
			this.OpacityMask = null;
		}
		private void Window_ContentRendered (object sender, EventArgs e)
		{
			OpacityMaskGradStop.BeginAnimation (
	GradientStop.OffsetProperty,
	(DoubleAnimation)this.Resources ["LoadAnimOffset"]);
			OpacityMaskGradStop1.BeginAnimation (
				GradientStop.OffsetProperty,
				(DoubleAnimation)this.Resources ["LoadAnimOffset1"]);
			// 启动占位动画，用于等待动画完成
			this.BeginAnimation (
				UIElement.OpacityProperty,
				(DoubleAnimation)this.Resources ["DummyAnim"]);
		}
		private void DummyAnimation_Completed (object sender, EventArgs e)
		{

		}
		private void UpdateMinMaxWidth ()
		{
			if (App.CurrentUserConfig == null) return;
			var screen = App.CurrentUserConfig.CurrentScreen;
			if (screen == null) return;
			int screenWidthPx = screen.WorkingArea.Width;
			int maxWidthPx = Math.Max (200, (int)(screenWidthPx * 0.45));
			var source = PresentationSource.FromVisual (this);
			double dpiScale = 1.0;
			if (source?.CompositionTarget != null) dpiScale = source.CompositionTarget.TransformToDevice.M11;
			double minWidthDip = 100;
			double maxWidthDip = maxWidthPx / dpiScale;
			this.MinWidth = minWidthDip;
			this.MaxWidth = maxWidthDip;
		}
		private void UpdateTopOptionsContextMenuLocaleStrings ()
		{
			var lc = App.ProgramFolder.StringResources;
			var menu = this.Resources ["TopOptionsMenu"] as ContextMenu;
			if (menu == null) return;
			foreach (var i in menu.Items)
			{
				if (!(i is MenuItem)) continue;
				var item = i as MenuItem;
				switch (item.Name)
				{
					case "ItemMinSidebar":
						item.Header = lc.SuitableResource ("SIDEBAR_CONTEXTMENU_MINBAR", "Minimize bar");
						break;
					case "ItemProperties":
						item.Header = lc.SuitableResource ("SIDEBAR_CONTEXTMENU_PROP", "Properties");
						break;
					case "ItemAddTile":
						item.Header = lc.SuitableResource ("SIDEBAR_CONTEXTMENU_TILEMGR", "Manage tiles...");
						break;
					case "ItemCancelSidebar":
						item.Header = lc.SuitableResource ("SIDEBAR_CONTEXTMENU_CANCEL", "Cancel Sidebar");
						break;
					case "ItemManageTile":
						item.Header = lc.SuitableResource ("SIDEBAR_CONTEXTMENU_ADDTILE", "Add a tile...");
						break;
					case "ItemGetTiles":
						item.Header = lc.SuitableResource ("SIDEBAR_CONTEXTMENU_STORE", "Get more tiles");
						break;
				}
			}
		}
		private void MinimizeSidebar_Click (object sender, RoutedEventArgs e)
		{
			HideWindow ();
		}
		private void Properties_Click (object sender, RoutedEventArgs e)
		{
			OpenConfigWindow ();
		}
		private System.Windows.Forms.Form mgrform = null;
		private ManagerWindow mgrWindow;
		private void AddTile_Click (object sender, RoutedEventArgs e)
		{
			if (mgrWindow != null)
			{
				mgrWindow?.Activate ();
				return;
			}
			mgrWindow = new ManagerWindow ();
			mgrWindow.Closed += MgrWindow_Closed;
			mgrWindow.ShowDialog ();
			return;
			if (mgrform != null)
			{
				mgrform.Activate ();
				return;
			}
			mgrform = new TileManageForm ();
			mgrform.FormClosed += Mgrform_FormClosed;
			mgrform.Show (new WinFormWrapper (_source.Handle));
			return;
		}
		private void MgrWindow_Closed (object sender, EventArgs e)
		{
			mgrWindow.Closed -= MgrWindow_Closed;
			mgrWindow = null;
		}
		private void Mgrform_FormClosed (object sender, System.Windows.Forms.FormClosedEventArgs e)
		{
			mgrform.FormClosed -= Mgrform_FormClosed;
			mgrform = null;
		}
		private void CancelSidebar_Click (object sender, RoutedEventArgs e)
		{
			Application.Current.Shutdown ();
		}
		private System.Windows.Forms.NotifyIcon notifyIcon;
		private System.Windows.Forms.ContextMenuStrip trayMenu;
		private System.Windows.Forms.ToolStripMenuItem showItem;
		private System.Windows.Forms.ToolStripMenuItem exitItem;
		/// <summary>
		/// 初始化托盘图标及右键菜单
		/// </summary>
		private void InitTrayIcon ()
		{
			notifyIcon = new System.Windows.Forms.NotifyIcon ();
			notifyIcon.Icon = Properties.Resources.AppIcon;
			notifyIcon.Text = App.ProgramFolder.StringResources.SuitableResource ("SIDEBAR_TITLE");
			notifyIcon.Visible = true;
			trayMenu = new System.Windows.Forms.ContextMenuStrip ();
			showItem = new System.Windows.Forms.ToolStripMenuItem (App.ProgramFolder.StringResources.SuitableResource ("SIDEBAR_CONTEXTMENU_SHOW"));
			showItem.CheckOnClick = true;
			showItem.Checked = true;
			showItem.Click += (s, e) => {
				if (showItem.Checked) ShowWindow ();
				else HideWindow ();
			};
			var propertiesItem = new System.Windows.Forms.ToolStripMenuItem (App.ProgramFolder.StringResources.SuitableResource ("SIDEBAR_CONTEXTMENU_PROP"));
			propertiesItem.Click += (s, e) => {
				OpenConfigWindow ();
			};
			exitItem = new System.Windows.Forms.ToolStripMenuItem (App.ProgramFolder.StringResources.SuitableResource ("SIDEBAR_CONTEXTMENU_CANCEL"));
			exitItem.Click += (s, e) => {
				this.Close ();
			};
			trayMenu.Items.Add (showItem);
			trayMenu.Items.Add (propertiesItem);
			trayMenu.Items.Add (new System.Windows.Forms.ToolStripSeparator ());
			trayMenu.Items.Add (exitItem);
			notifyIcon.ContextMenuStrip = trayMenu;
			notifyIcon.MouseDoubleClick += notifyIcon_MouseDoubleClick;
		}
		/// <summary>
		/// 显示窗口（恢复）
		/// </summary>
		private void ShowWindow ()
		{
			this.Visibility = Visibility.Visible;
			this.WindowState = WindowState.Normal;
			showItem.Checked = true;
			var cc = App.CurrentUserConfig;
			if (cc.OccupyWorkingArea)
			{
				appbar.Register ();
			}
		}
		/// <summary>
		/// 隐藏窗口到托盘
		/// </summary>
		private void HideWindow ()
		{
			this.Visibility = Visibility.Collapsed;
			showItem.Checked = false;
			var cc = App.CurrentUserConfig;
			if (cc.OccupyWorkingArea)
			{
				appbar.Unregister ();
			}
		}
		/// <summary>
		/// 窗口状态改变时（如最小化）自动隐藏到托盘
		/// </summary>
		private void MainWindow_StateChanged (object sender, EventArgs e)
		{
			if (this.WindowState == WindowState.Minimized)
			{
				HideWindow ();
			}
		}
		/// <summary>
		/// 双击托盘图标恢复窗口
		/// </summary>
		private void notifyIcon_MouseDoubleClick (object sender, System.Windows.Forms.MouseEventArgs e)
		{
			if (e.Button == System.Windows.Forms.MouseButtons.Left)
			{
				ShowWindow ();
				showItem.Checked = true;
				Activate ();
			}
		}
		/// <summary>
		/// 属性窗口（留空实现）
		/// </summary>
		protected override void OnClosed (EventArgs e)
		{
			base.OnClosed (e);
			notifyIcon?.Dispose ();
		}
		private DragPlaceholder _dragPlaceholder = new DragPlaceholder ();
		public DragPlaceholder CurrentDragPlaceholder { get; set; }
		public ISidebarConfig Config => App.CurrentUserConfig;
		public void InitDragTileAbout ()
		{
			TilesRegion.AllowDrop = true;
			PinnedTilesRegion.AllowDrop = true;
			TilesRegion.PreviewDragOver += Region_DragOver;
			TilesRegion.PreviewDrop += Region_Drop;
			PinnedTilesRegion.PreviewDragOver += Region_DragOver;
			PinnedTilesRegion.PreviewDrop += Region_Drop;
		}
		private void Region_DragOver (object sender, DragEventArgs e)
		{
			if (!e.Data.GetDataPresent ("SidebarTile")) return;
			var region = sender as StackPanel;
			e.Effects = DragDropEffects.Move;
			e.Handled = true;

			Point mouse = e.GetPosition (region);

			// 1. 计算插入索引（排除占位符自身）
			int insertIndex = 0;
			for (int i = 0; i < region.Children.Count; i++)
			{
				var child = region.Children [i];
				if (child == _dragPlaceholder) continue; // 忽略占位符

				Tile tile = child as Tile;
				if (tile == null) continue;

				var pos = tile.TransformToAncestor (region).Transform (new Point (0, 0));
				double tileMidY = pos.Y + tile.ActualHeight / 2;
				if (mouse.Y < tileMidY)       // 鼠标在上半部，插在前面
				{
					insertIndex = i;
					break;
				}
				insertIndex = i + 1;           // 否则插在后面
			}
			// 如果占位符已经在区域中，需要剔除它对索引的影响
			int placeholderIndex = region.Children.IndexOf (_dragPlaceholder);
			if (placeholderIndex >= 0)
			{
				// 当占位符已经在列表中时，insertIndex 的指向应该是以“无占位符”的 Tile 列表为参照
				// 如果 insertIndex > placeholderIndex，说明目标位置在占位符之后，实际插入索引应减 1
				if (insertIndex > placeholderIndex)
					insertIndex--;
				// 如果 insertIndex == placeholderIndex，保持不变（插到占位符位置，即替换它）
			}

			// 2. 移动占位符到新位置（不闪烁）
			if (placeholderIndex != insertIndex)
			{
				if (placeholderIndex >= 0)
					region.Children.Remove (_dragPlaceholder);
				SafeInsert (region.Children, insertIndex, _dragPlaceholder);
				//region.Children.Insert (insertIndex, _dragPlaceholder);
			}
			UpdatePlaceholdersLastStatus ();
		}
		private void Region_Drop (object sender, DragEventArgs e)
		{
			if (!e.Data.GetDataPresent ("SidebarTile"))
				return;

			var region = sender as StackPanel;
			var tile = e.Data.GetData ("SidebarTile") as Tile;
			if (tile == null)
				return;

			// 1. 移除占位符
			int placeholderIndex = region.Children.IndexOf (_dragPlaceholder);
			if (placeholderIndex >= 0)
				region.Children.Remove (_dragPlaceholder);

			// 2. 恢复磁贴显示
			tile.Visibility = Visibility.Visible;

			// 3. 获取磁贴标识
			string familyName = tile.TileInstance != null
				? tile.TileInstance.Storage.Manifest.Identity.FamilyName
				: null;

			if (!string.IsNullOrEmpty (familyName))
			{
				View<string> view = null;
				int oldIndex = -1;

				oldIndex = PinnedTopTiles.IndexOf (familyName);
				if (oldIndex >= 0)
					view = PinnedTopTiles;
				else
				{
					oldIndex = PinnedBottomTiles.IndexOf (familyName);
					if (oldIndex >= 0)
						view = PinnedBottomTiles;
				}

				if (view != null)
				{
					int targetIndex = placeholderIndex >= 0 ? placeholderIndex : view.Count;

					if (targetIndex < 0)
						targetIndex = 0;
					if (targetIndex > view.Count)
						targetIndex = view.Count;

					if (oldIndex >= 0)
					{
						// View 自己负责源集合索引转换
						view.Move (oldIndex, targetIndex);
					}
					else
					{
						// View 自己负责插入到源集合正确位置
						view.Insert (targetIndex, familyName);
					}
				}
			}

			// 4. 重建布局
			RefreshTileRegions ();
			e.Handled = true;
		}
		public void SetDragPlaceholder (DragPlaceholder placeholder)
		{
			_dragPlaceholder = placeholder;
			UpdatePlaceholdersLastStatus ();
		}
		private void UpdatePlaceholdersLastStatus ()
		{
			// 更新 TilesRegion 中的占位符：最后一个元素为 IsLast=true
			for (int i = 0; i < TilesRegion.Children.Count; i++)
			{
				DragPlaceholder ph = TilesRegion.Children [i] as DragPlaceholder;
				if (ph != null)
				{
					bool isLast = (i == TilesRegion.Children.Count - 1);
					if (ph.IsLast != isLast)
						ph.IsLast = isLast;
				}
			}
			// 更新 PinnedTilesRegion 中的占位符：第一个元素为 IsLast=true
			for (int i = 0; i < PinnedTilesRegion.Children.Count; i++)
			{
				DragPlaceholder ph = PinnedTilesRegion.Children [i] as DragPlaceholder;
				if (ph != null)
				{
					bool isFirst = (i == 0);
					if (ph.IsLast != isFirst)
						ph.IsLast = isFirst;
				}
			}
		}
		private void Window_SourceInitialized (object sender, EventArgs e)
		{
			isSourceInitialized = true;
			Handle = new WindowInteropHelper (this).Handle;
			_source = HwndSource.FromHwnd (Handle);
			appbar = new SidebarAppBar (this);
			tboHelper = new TaskbarOverlapHelper (this);
			tboHelper.CurrentScreen = App.CurrentUserConfig.CurrentScreen;
			tboHelper.Direction = App.CurrentUserConfig.Direction;
			IntPtr hwnd = Handle;
			bool enableBlur = false;
			object val;
			if (Application.Current.Resources.Contains ("EnableBlur"))
				enableBlur = (bool)Application.Current.Resources ["EnableBlur"];
			ChangeAeroStatus (enableBlur);
			if (DWMAPI.IsDwmAvailable ())
			{
				try { DWMAPI.RemoveFromFlip3D (hwnd); } catch { }
				try
				{
					if (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Major != 0)
						DWMAPI.RemoveFromAeroPeek (hwnd);
				}
				catch { }
			}
			var h = (HWND)hwnd;
			DwmThemeProvider.Instance.StartListening (_source);
			//h.Styles &= ~Win32.WindowStyles.WS_POPUP;
			//h.Styles |= Win32.WindowStyles.WS_OVERLAPPED;
			h.StylesEx |= Win32.ExtendedWindowStyles.WS_EX_TOOLWINDOW;
			h.StylesEx |= Win32.ExtendedWindowStyles.WS_EX_LAYERED;
			UnionUpdatePosition ();

			IntPtr desktopHwnd = Win32WindowNative.FindWindow ("Progman", null);
			if (desktopHwnd == IntPtr.Zero)
			{
				IntPtr workerW = IntPtr.Zero;
				Win32WindowNative.EnumWindows (delegate (IntPtr hWnd, IntPtr lParam)
				{
					if (Win32WindowNative.FindWindowEx (hWnd, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
					{
						workerW = Win32WindowNative.FindWindowEx (IntPtr.Zero, hWnd, "WorkerW", null);
						return false; // 停止枚举
					}
					return true;
				}, IntPtr.Zero);
				desktopHwnd = workerW != IntPtr.Zero ? workerW : IntPtr.Zero;
			}
			if (desktopHwnd != IntPtr.Zero)
			{
				// GWL_HWNDPARENT = -8
				Win32WindowNative.SetWindowLong (Handle, -8, desktopHwnd.ToInt32 ());
			}
		}
		private void Window_Closed (object sender, EventArgs e)
		{
			isClosed = true;
			SidebarPipe.Mail -= Pipe_OnMail;
			DwmThemeProvider.Instance?.StopListening ();
			try { if (_configWnd != null) _configWnd?.Close (); } catch { }
			ThemeManager.ThemeChanged -= ThemeManager_ThemeChanged;
			App.CurrentUserConfig.PropertyChanged -= CurrentUserConfig_PropertyChanged;
			if (appbar.IsRegistered) appbar?.Unregister ();
			if (tboHelper.IsRegistered) tboHelper?.Unregister ();
			overflowTilesContainer?.Close ();
		}
		public ConfigWindow _configWnd = null;
		private void OpenConfigWindow ()
		{
			if (_configWnd != null) _configWnd.Activate ();
			else
			{
				_configWnd = new ConfigWindow ();
				_configWnd.Closed += (s, e) => {
					_configWnd = null;
				};
				_configWnd.ShowDialog ();
			}
		}
		private void Window_MouseMove (object sender, MouseEventArgs e)
		{
			var cc = App.CurrentUserConfig;
			var islock = cc.Locked;
			switch (cc.Direction)
			{
				case SidebarDirection.Right:
					if (e.GetPosition (this).X <= 5 && !islock)
					{
						base.Cursor = Cursors.SizeWE;
						if (e.LeftButton == MouseButtonState.Pressed)
						{
							Win32WindowNative.SendMessageW (Handle, 274, (IntPtr)61441, IntPtr.Zero);
							cc.Width = this.Width;
						}
					}
					else if (base.Cursor != Cursors.Arrow)
						base.Cursor = Cursors.Arrow;
					break;
				case SidebarDirection.Left:
					if (e.GetPosition (this).X >= this.Width - 5 && !islock)
					{
						base.Cursor = Cursors.SizeWE;
						if (e.LeftButton == MouseButtonState.Pressed)
						{
							Win32WindowNative.SendMessageW (Handle, 274, (IntPtr)61442, IntPtr.Zero);
							cc.Width = (int)this.Width;
						}
					}
					else if (base.Cursor != Cursors.Arrow)
						base.Cursor = Cursors.Arrow;
					break;
			}
		}
		private void Window_MouseDoubleClick (object sender, MouseButtonEventArgs e)
		{
			var cc = App.CurrentUserConfig;
			var islock = cc.Locked;
			switch (cc.Direction)
			{
				case SidebarDirection.Right:
					if (e.GetPosition (this).X <= 5 && !islock)
					{
						cc.Width = 150;
					}
					break;
				case SidebarDirection.Left:
					if (e.GetPosition (this).X >= this.Width - 5 && !islock)
					{
						cc.Width = 150;
					}
					break;
			}
		}
		// 针对 UIElementCollection 的安全插入
		private static void SafeInsert (UIElementCollection collection, int index, UIElement element)
		{
			if (collection == null) return;
			int count = collection.Count;
			if (count == 0)
				index = 0;
			else if (index < 0)
				index = 0;
			else if (index > count)
				index = count;
			collection.Insert (index, element);
		}
		private static void SafeInsert (ObservableCollection<string> collection, int index, string item)
		{
			if (collection == null) return;
			int count = collection.Count;
			if (count == 0)
				index = 0;
			else if (index < 0)
				index = 0;
			else if (index > count)
				index = count;
			collection.Insert (index, item);
		}
		public bool Request (ISidebarRequest request)
		{
			return Communicate (request);
		}
		public bool Communicate (ITileRequest request)
		{
			if (request.RequestTarget.NEquals ("Sidebar"))
			{
				if (request.RequestName.NEquals ("Notification"))
				{
					EventHandler handler = null;
					handler = (s, e) => {
						notifyIcon.BalloonTipClicked -= handler;
						foreach (var i in tileCache)
						{
							if (i.Key.NEquals (request.RequestSource))
							{
								var resp = new TileResponse (request);
								resp.Success = true;
								resp.ResponseName = "NotificationClick";
								Response (resp);
								break;
							}
						}
					};
					EventHandler closeHandler = null;
					closeHandler = (s, e) => {
						notifyIcon.BalloonTipClicked -= handler;
						notifyIcon.BalloonTipClosed -= closeHandler;
					};
					notifyIcon.BalloonTipClicked += handler;
					if (request.RequestDatas is string)
					{
						var ts = App.TileMgr.GetByFamilyName (request.RequestSource);
						var name = ts.TileFolder.StringResources.SuitableResource (ts.Manifest.Properties.DisplayName, ts.Manifest.Properties.DisplayName) ?? "Tile";
						notifyIcon.ShowBalloonTip (
							5000,
							String.Format (
								App.ProgramFolder.StringResources.SuitableResource ("SIDEBAR_NOTIFY_TITLE", "Notification from {0}"),
								name
							),
							request.RequestDatas as string,
							System.Windows.Forms.ToolTipIcon.None
						);
						return true;
					}
					else if (request.RequestDatas is NotifyIconNotification)
					{
						var ts = App.TileMgr.GetByFamilyName (request.RequestSource);
						var name = ts.TileFolder.StringResources.SuitableResource (ts.Manifest.Properties.DisplayName, ts.Manifest.Properties.DisplayName) ?? "Tile";
						var nin = request.RequestDatas as NotifyIconNotification;
						notifyIcon.ShowBalloonTip (nin.Timeout, nin.Title ?? String.Format (
								App.ProgramFolder.StringResources.SuitableResource ("SIDEBAR_NOTIFY_TITLE", "Notification from {0}"),
								name
							), nin.Content, nin.Icon);
						return true;
					}
					else
					{
						notifyIcon.BalloonTipClicked -= handler;
						notifyIcon.BalloonTipClosed -= closeHandler;
					}
				}
				else if (request.RequestName.NEquals ("Resize"))
				{
					foreach (var t in tileCache)
					{
						if (t.Key.NEquals (request.RequestSource))
						{
							if (request.RequestDatas is double)
							{
								double height = (double)request.RequestDatas;
								var tile = t.Value;
								if (tile != null)
								{
									tile.Dispatcher.Invoke (new Action (() => tile.RequireSetTileContentHeight (height)));
								}
							}
							return true;
						}
					}
				}
				else if (request.RequestName.NEquals ("ResizeIgnoreAutoSize"))
				{
					foreach (var t in tileCache)
					{
						if (t.Key.NEquals (request.RequestSource))
						{
							if (request.RequestDatas is double)
							{
								double height = (double)request.RequestDatas;
								var tile = t.Value;
								if (tile != null)
								{
									tile.Dispatcher.Invoke (new Action (() => tile.RequireSetTileContentHeightIgnoreAutoSize (height)));
								}
							}
							return true;
						}
					}
				}
				else if (request.RequestName.NEquals ("ResizeForever"))
				{
					foreach (var t in tileCache)
					{
						if (t.Key.NEquals (request.RequestSource))
						{
							if (request.RequestDatas is double)
							{
								double height = (double)request.RequestDatas;
								var tile = t.Value;
								if (tile != null)
								{
									tile.Dispatcher.Invoke (new Action (() => tile.RequireSetTileContentHeightAndSave (height)));
								}
							}
							return true;
						}
					}
				}
				else if (request.RequestName.NEquals ("OpacityAnime"))
				{
					foreach (var t in tileCache)
					{
						if (t.Key.NEquals (request.RequestSource))
						{
							var tile = t.Value;
							if (tile != null)
							{
								if (request.RequestDatas is double)
								{
									double opacity = (double)request.RequestDatas;
									tile.Dispatcher.Invoke (new Action (() => tile.RequireLoadTileOpacityAnime (opacity)));
								}
								else
								{
									tile.Dispatcher.Invoke (new Action (() => tile.RequireLoadTileOpacityAnime ()));
								}
							}
							return true;
						}
					}
				}
				else if (request.RequestName.NEquals ("FlyoutSizeToContent"))
				{
					foreach (var t in tileCache)
					{
						if (t.Key.NEquals (request.RequestSource))
						{
							if (request.RequestDatas is SizeToContent)
							{
								SizeToContent sizeMode = (SizeToContent)request.RequestDatas;
								var f = t.Value?.CurrentFlyout;
								if (f != null)
								{
									f.Dispatcher.Invoke (new Action (() => f.SizeToContent = sizeMode));
								}
							}
						}
					}
				}
				else if (request.RequestName.NEquals ("FlyoutUpdatePosition"))
				{
					foreach (var t in tileCache)
					{
						if (t.Key.NEquals (request.RequestSource))
						{
							var f = t.Value.CurrentFlyout;
							if (f != null)
							{
								f.Dispatcher.BeginInvoke (DispatcherPriority.Render, new Action (() => f.FixPosition ()));
								return true;
							}
						}
					}
				}
				else if (request.RequestName.NEquals ("RequireExtraFlyout"))
				{
					foreach (var t in tileCache)
					{
						if (t.Key.NEquals (request.RequestSource))
						{
							var tile = t.Value;
							if (tile != null)
							{
								var flyoutWnd = new Flyout (tile);
								var flyoutContent = flyoutWnd.FlyoutContent;
								Response (new TileResponse (request) {
									ResponseName = "ExtraFlyoutWindow",
									ResponseData = new FlyoutAboutEventArgs (flyoutContent, flyoutWnd)
								});
							}
							return true;
						}
					}
				}
				else if (request.RequestName.NEquals ("GetTitle"))
				{
					foreach (var t in tileCache)
					{
						if (t.Key.NEquals (request.RequestSource))
						{
							var tile = t.Value;
							if (tile != null)
							{
								Response (new TileResponse (request) {
									ResponseName = "ReturnGetTitle",
									ResponseData = tile.TileDisplayName.Text
								});
							}
							return true;
						}
					}
				}
				else if (request.RequestName.NEquals ("SetTitle"))
				{
					foreach (var t in tileCache)
					{
						if (t.Key.NEquals (request.RequestSource))
						{
							var tile = t.Value;
							tile.TileDisplayName.Text = request.RequestDatas as string;
							return true;
						}
					}
				}
				else if (request.RequestName.NEquals ("GetIcon"))
				{
					foreach (var t in tileCache)
					{
						if (t.Key.NEquals (request.RequestSource))
						{
							var tile = t.Value;
							if (tile != null)
							{
								Response (new TileResponse (request) {
									ResponseName = "ReturnGetIcon",
									ResponseData = tile.TileIcon.Source
								});
							}
							return true;
						}
					}
				}
				else if (request.RequestName.NEquals ("SetIcon"))
				{
					foreach (var t in tileCache)
					{
						if (t.Key.NEquals (request.RequestSource))
						{
							var tile = t.Value;
							tile.TileIcon.Source = request.RequestDatas as ImageSource;
							return true;
						}
					}
				}
				return false;
			}
			else
			{
				foreach (var t in tileCache)
				{
					if (t.Key.NEquals (request.RequestTarget))
					{
						return t.Value.TileInstance.Instance.OnRequest (request);
					}
				}
			}
			return false;
		}
		public bool Response (ITileResponse resp)
		{
			if (resp.ResponseTarget.NEquals ("Sidebar"))
			{
				return false;
			}
			else
			{
				foreach (var t in tileCache)
				{
					if (t.Key.NEquals (resp.ResponseTarget))
					{
						return t.Value.TileInstance.Instance.OnResponse (resp);
					}
				}
			}
			return false;
		}
		private bool? _lastAeroState = null;
		public void ChangeAeroStatus (bool state)
		{
			if (!DWMAPI.IsDwmAvailable ()) return;
			if (_lastAeroState.HasValue && _lastAeroState.Value == state) return;
			_lastAeroState = state;
			IntPtr hwnd = Handle;
			if (state)
			{
				DWMAPI.EnableBlur (ref hwnd, IntPtr.Zero);
				try { if (!AllowsTransparency) AllowsTransparency = true; } catch { }
			}
			else
			{
				DWMAPI.DisableBlur (ref hwnd);
			}
		}
		private void ItemManageTile_SubmenuOpened (object sender, RoutedEventArgs e)
		{
			var menuItem = sender as MenuItem;
			if (menuItem == null) return;
			menuItem.Items.Clear ();

			var allTiles = App.TileMgr.ValidTiles;
			if (allTiles == null) return;

			foreach (var tileStorage in allTiles)
			{
				string familyName = tileStorage.Manifest.Identity.FamilyName;
				string displayName = GetTileDisplayName (tileStorage);
				var subItem = new MenuItem {
					Header = displayName,
					IsCheckable = true,
					IsChecked = App.CurrentUserConfig.PinnedTiles.Contains (familyName),
					Tag = familyName
				};
				subItem.Click += OnManageTileClick;
				menuItem.Items.Add (subItem);
			}
		}
		private string GetTileDisplayName (TileStorage tileStorage)
		{
			try
			{
				string resId = tileStorage.Manifest.Properties.DisplayName;
				return tileStorage.TileFolder.StringResources.SuitableResource (resId, resId);
			}
			catch
			{
				return tileStorage.Manifest.Identity.FamilyName;
			}
		}
		private void OnManageTileClick (object sender, RoutedEventArgs e)
		{
			var item = sender as MenuItem;
			if (item == null) return;
			string familyName = item.Tag as string;
			if (string.IsNullOrEmpty (familyName)) return;
			var pinnedList = App.CurrentUserConfig.PinnedTiles;
			if (item.IsChecked)
			{
				if (!pinnedList.Contains (familyName))
					pinnedList.Add (familyName);
			}
			else
			{
				pinnedList.Remove (familyName);
			}
			//RefreshTileRegions ();
		}
		private void OverflowTileItem_Click (object sender, RoutedEventArgs e)
		{
			var btn = sender as ToggleButton;
			if (btn == null) return;
			var tvi = btn.Tag as TileVisualInfo;
			var t = tvi.TileElement as Tile;
			t.RequireOpenFlyout ();
		}
		public ObservableCollection<TileVisualInfo> OverflowTiles = new UniqueObservableCollection<TileVisualInfo> (o => o);
		private bool isTileHeightChanging = false;
		private HideTilesContainWindow overflowTilesContainer = null;
		public void OnTileHeightChanged (object sender, EventArgs e)
		{
			if (isTileHeightChanging) return;
			isTileHeightChanging = true;
			if (App.CurrentUserConfig.CanScroll)
			{
				try
				{
					if (OverflowTiles.Count > 0)
					{
						foreach (var ot in OverflowTiles)
						{
							var t = ot.TileElement as Tile;
							if (t != null) (t?.Parent as Panel)?.Children?.Remove (t);
							if (t.IsPinned)
							{
								if (t.Parent == null)
									PinnedTilesRegion.Children.Insert (0, t);
							}
							else
							{
								if (t.Parent == null)
									TilesRegion.Children.Add (t);
							}
						}
						OverflowTiles.Clear ();
					}
				}
				catch { }
				finally
				{
					isTileHeightChanging = false;
				}
				return;
			}
			try
			{
				var removeTileCollection = new List<Tile> ();
				foreach (FrameworkElement fe in PinnedTilesRegion.Children)
				{
					if (!(fe is Tile)) continue;
					var t = fe as Tile;
					if (CheckIsTileOverflow (t))
					{
						removeTileCollection.Add (t);
					}
				}
				foreach (FrameworkElement fe in TilesRegion.Children)
				{
					if (!(fe is Tile)) continue;
					var t = fe as Tile;
					if (CheckIsTileOverflow (t))
					{
						removeTileCollection.Add (t);
					}
				}
				foreach (var t in removeTileCollection)
				{
					(t?.Parent as Panel)?.Children?.Remove (t);
					overflowTilesContainer.OverflowTilesRegion.Children.Add (t);
					t.Width = App.CurrentUserConfig.Width;
					OverflowTiles.Add (t.TileVisual);
				}
				if (OverflowTiles.Count <= 0) return;
				ListCollectionView view = new ListCollectionView (OverflowTiles);
				view.CustomSort = new TileVisualInfoComparer ();
				Point pinnedTileRegionTopLeft = PinnedTilesRegion.TransformToAncestor (MainPanel).Transform (new Point (0, 0));
				var tth = pinnedTileRegionTopLeft.Y - OverflowTilesRegion.ActualHeight;
				var remain = tth;
				foreach (FrameworkElement t in TilesRegion.Children)
				{
					remain -= t.ActualHeight;
				}
				var willremove = new List<TileVisualInfo> ();
				foreach (TileVisualInfo tvi in view)
				{
					var t = tvi.TileElement as Tile;
					if (remain - t.ActualHeight <= 0) break;
					willremove.Add (tvi);
				}
				foreach (var tvi in willremove)
				{
					var t = tvi.TileElement as Tile;
					t.ClearValue (WidthProperty);
					if (t != null) (t?.Parent as Panel)?.Children?.Remove (t);
					OverflowTiles.Remove (tvi);
					if (t.Parent == null)
						(t.IsPinned ? PinnedTilesRegion : TilesRegion).Children.Add (t);
					continue;
					if (t.IsPinned)
					{
						if (t.Parent == null)
							PinnedTilesRegion.Children.Insert (0, t);
					}
					else
					{
						if (t.Parent == null)
							TilesRegion.Children.Add (t);
					}
				}
				willremove?.Clear ();
				PinnedTilesRegion.MaxHeight = MainPanel.ActualHeight - OverflowTilesRegionContent.ActualHeight;
			}
			finally
			{
				isTileHeightChanging = false;
			}
		}
		public void RefreshTileMaxHeight ()
		{
			Point pinnedTileRegionTopLeft = PinnedTilesRegion.TransformToAncestor (MainPanel).Transform (new Point (0, 0));
			var tth = pinnedTileRegionTopLeft.Y - OverflowTilesRegion.ActualHeight;
			foreach (FrameworkElement h in TilesRegion.Children)
			{
				if (h != null) continue;
				var maxheight = tth;
				if (h.MaxHeight != maxheight)
					h.MaxHeight = tth;
				if (tth - h.ActualHeight > 0)
					tth -= h.ActualHeight;
				else break;
			}
			//var oth = OverflowTilesRegion.ActualHeight;
			foreach (FrameworkElement ph in TilesRegion.Children)
			{
				if (ph != null) continue;
				var maxheight = tth + ph.ActualHeight;
				if (ph.MaxHeight != maxheight)
					ph.MaxHeight = maxheight;
				tth = maxheight;
			}
		}
		public bool CheckIsTileOverflow (Tile tile)
		{
			var parent = tile?.Parent as Panel;
			if (parent == TilesRegion)
			{
				Point tileTopLeft = tile.TransformToAncestor (TilesRegion).Transform (new Point (0, 0));
				Point pinnedTileRegionTopLeft = PinnedTilesRegion.TransformToAncestor (MainPanel).Transform (new Point (0, 0));
				var tth = pinnedTileRegionTopLeft.Y - OverflowTilesRegion.ActualHeight;
				return tileTopLeft.Y + tile.ActualHeight > tth;
			}
			else if (parent == PinnedTilesRegion)
			{
				Point tileTopLeft = tile.TransformToAncestor (MainPanel).Transform (new Point (0, 0));
				return tileTopLeft.Y - OverflowTilesRegion.ActualHeight > MainPanel.ActualHeight;
			}
			return false;
		}
		private void OverflowTileItem_ContextMenuOpening (object sender, ContextMenuEventArgs e)
		{
			var btn = sender as ToggleButton;
			if (btn == null) return;
			var tvi = btn.Tag as TileVisualInfo;
			var t = tvi.TileElement as Tile;
			var contextMenu = t?.ContextMenu;
			if (contextMenu != null)
			{
				contextMenu.IsOpen = true;
			}
		}
		private void GetTiles_Click (object sender, RoutedEventArgs e)
		{
			Process.Start (System.IO.Path.Combine (App.ProgramFolder.FolderPath, "SGStore.exe"));
		}
		private void SwitchScrollMode (bool canscroll)
		{
			if (canscroll)
			{
				if (PinnedTilesScrollRegion.Content != PinnedTilesRegion)
				{
					var parent = PinnedTilesScrollRegion.Parent as Panel;
					parent.Children.Remove (PinnedTilesRegion);
					PinnedTilesScrollRegion.Content = PinnedTilesRegion;
				}
				if (TilesScrollRegion.Content != TilesRegion)
				{
					var parent = TilesRegion.Parent as Panel;
					parent.Children.Remove (TilesRegion);
					TilesScrollRegion.Content = TilesRegion;
				}
				PinnedTilesScrollRegion.Visibility = TilesScrollRegion.Visibility = Visibility.Visible;
			}
			else
			{
				PinnedTilesScrollRegion.Visibility = TilesScrollRegion.Visibility = Visibility.Collapsed;
				UpdateLayout ();
				if (PinnedTilesScrollRegion.Content == PinnedTilesRegion)
				{
					PinnedTilesScrollRegion.Content = null;
					var parent = PinnedTilesScrollRegion.Parent as DockPanel;
					parent.Children.Insert (parent.Children.IndexOf (PinnedTilesScrollRegion), PinnedTilesRegion);
				}
				if (TilesScrollRegion.Content == TilesRegion)
				{
					TilesScrollRegion.Content = null;
					var parent = TilesScrollRegion.Parent as DockPanel;
					parent.Children.Insert (parent.Children.IndexOf (TilesScrollRegion), TilesRegion);
				}
			}
		}
	}
	public class TileVisualInfoComparer: IComparer<TileVisualInfo>, IComparer
	{
		public int Compare (object x, object y)
		{
			return Compare (x as TileVisualInfo, y as TileVisualInfo);
		}
		public int Compare (TileVisualInfo x, TileVisualInfo y)
		{
			if (x == null && y == null) return 0;
			if (x == null) return 1; 
			if (y == null) return -1;
			var tile_x = x?.TileElement as Tile;
			var tile_y = y?.TileElement as Tile;
			var fn_x = tile_x.TileInstance?.Manifest?.Identity?.FamilyName;
			var fn_y = tile_y.TileInstance?.Manifest?.Identity?.FamilyName;
			var tilepinned = App.CurrentUserConfig.PinnedTiles;
			var index_x = tilepinned.IndexOf (fn_x);
			var index_y = tilepinned.IndexOf (fn_y);
			if (index_x < 0) index_x = int.MaxValue;
			if (index_y < 0) index_y = int.MaxValue;
			return index_y - index_x;
		}
	}
}

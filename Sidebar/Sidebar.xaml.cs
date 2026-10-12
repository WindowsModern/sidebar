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
using System.Windows.Shapes;
using System.Windows.Threading;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using FormContextMenu = System.Windows.Forms.ContextMenu;
using FormMenuItem = System.Windows.Forms.MenuItem;
using Sidebar;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Controls.Primitives;

namespace Sidebar
{
	/// <summary>
	/// Sidebar.xaml 的交互逻辑
	/// </summary>
	public partial class SidebarWindow: Window, ISidebarFeatures, IWin32WindowInterop, IWindowInterop
	{
		public SidebarWindow ()
		{
			InitializeComponent ();
			InitTilesList ();
		}
		#region Tile to Create/Remove/Move
		private Dictionary<string, TileControl> tileCache = new Dictionary<string, TileControl> (NormalizedStringComparer.Instance);
		private ObservableCollection<TileControl> tileOverflowList = new ObservableCollection<TileControl> ();
		private ProjectedView<TileControl, ITileElement> tileOverflowVisualList;
		private bool _rebuildScheduled;
		private readonly HashSet<string> _pendingRemovals = new HashSet<string> (NormalizedStringComparer.Instance);
		private void InitTilesList ()
		{
			var cc = App.CurrentUserConfig;
			tileOverflowVisualList = new ProjectedView<TileControl, ITileElement> (tileOverflowList, tc => tc.VisualView);
			RebuildFromConfig ();
			cc.PinnedTiles.CollectionChanged += PinnedTiles_CollectionChanged;
			cc.PinnedBottomTiles.CollectionChanged += PinnedBottomTiles_CollectionChanged;
			App.TileMgr.Applications.CollectionChanged += Applications_CollectionChanged;
		}
		private void UninitTilesList ()
		{
			var cc = App.CurrentUserConfig;
			cc.PinnedTiles.CollectionChanged -= PinnedTiles_CollectionChanged;
			cc.PinnedBottomTiles.CollectionChanged -= PinnedBottomTiles_CollectionChanged;
			App.TileMgr.Applications.CollectionChanged -= Applications_CollectionChanged;
		}
		private void PinnedTiles_CollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
		{
			ScheduleRefresh ();
		}
		private void PinnedBottomTiles_CollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
		{
			ScheduleRefresh ();
		}
		private void Applications_CollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action == NotifyCollectionChangedAction.Remove || e.Action == NotifyCollectionChangedAction.Replace || e.Action == NotifyCollectionChangedAction.Reset)
			{
				PurgeDeadCache ();
			}
			ScheduleRefresh ();
		}
		private void ScheduleRefresh ()
		{
			if (_rebuildScheduled) return;
			_rebuildScheduled = true;
			Dispatcher.BeginInvoke (DispatcherPriority.Background, new Action (() => {
				_rebuildScheduled = false;
				RebuildFromConfig ();
			}));
		}
		private void RebuildFromConfig ()
		{
			var cc = App.CurrentUserConfig;
			if (cc == null) return;
			var topList = cc.PinnedTiles.ToList ();
			var bottomList = cc.PinnedBottomTiles.ToList ();
			ArrangePanel (TilesControl, topList, false);
			ArrangePanel (PinnedTilesControl, bottomList, true);
			RemoveOrphans (topList, bottomList);
		}
		private void ArrangePanel (StackPanel panel, List<string> orderedUids, bool pinnedBottom)
		{
			if (panel == null) return;
			for (int i = panel.Children.Count - 1; i >= 0; i--)
			{
				var tile = panel.Children [i] as TileControl;
				if (tile == null)
				{
					panel.Children.RemoveAt (i);
					continue;
				}
				string umid = tile.UserModelId;
				if (string.IsNullOrEmpty (umid) || !ContainsNormalized (orderedUids, umid))
				{
					if (_pendingRemovals.Contains (umid)) continue;
					panel.Children.RemoveAt (i);
				}
			}
			for (int i = 0; i < orderedUids.Count; i++)
			{
				string umid = orderedUids [i];
				if (_pendingRemovals.Contains (umid)) continue;
				var tile = GetOrCreateTile (umid);
				if (tile == null) continue;
				tile.IsPinned = pinnedBottom;
				if (tile?.Instance?.Config != null)
					(tile.Instance.Config as TileConfig)?.NotifyPinned ();
				if (i < panel.Children.Count && ReferenceEquals (panel.Children [i], tile)) continue;
				SafeInsert (panel, i, tile);
			}
		}
		private void RemoveOrphans (List<string> topList, List<string> bottomList)
		{
			if (tileCache.Count == 0) return;
			var toRemove = new List<string> ();
			foreach (var kv in tileCache)
			{
				string umid = kv.Key;
				if (ContainsNormalized (topList, umid)) continue;
				if (ContainsNormalized (bottomList, umid)) continue;
				toRemove.Add (umid);
			}
			foreach (var umid in toRemove)
			{
				BeginRemoveTileWithAnimation (umid);
			}
		}
		private void BeginRemoveTileWithAnimation (string umid)
		{
			if (string.IsNullOrWhiteSpace (umid)) return;
			if (!_pendingRemovals.Add (umid)) return;
			TileControl tc;
			if (!tileCache.TryGetValue (umid, out tc) || tc == null)
			{
				_pendingRemovals.Remove (umid);
				return;
			}
			tc.SizeChanged -= TileControl_SizeChanged;
			var task = tc.DestroyTileControl ();
			task.ContinueWith (_ => {
				var d = Dispatcher;
				if (d == null || d.HasShutdownStarted || d.HasShutdownFinished) return;
				try { d.BeginInvoke (new Action (() => FinishRemoveTile (umid))); }
				catch { }
			}, TaskScheduler.Default);
		}
		private void FinishRemoveTile (string umid)
		{
			_pendingRemovals.Remove (umid);
			RemoveFromPanel (TilesControl, umid);
			RemoveFromPanel (PinnedTilesControl, umid);
			tileCache.Remove (umid);
			ScheduleRefresh ();
		}
		private void RemoveFromPanel (StackPanel panel, string umid)
		{
			if (panel == null) return;
			int idx = IndexOfUserModelId (panel, umid);
			if (idx >= 0 && idx < panel.Children.Count) panel.Children.RemoveAt (idx);
		}
		private void FlushPendingRemovals ()
		{
			if (_pendingRemovals.Count == 0) return;
			var keys = new List<string> (_pendingRemovals);
			_pendingRemovals.Clear ();
			foreach (var umid in keys)
			{
				RemoveFromPanel (TilesControl, umid);
				RemoveFromPanel (PinnedTilesControl, umid);
				tileCache.Remove (umid);
			}
		}
		private TileControl GetOrCreateTile (string userModelId)
		{
			TileControl tc = null;
			if (tileCache.TryGetValue (userModelId, out tc)) return tc;
			var tas = App.TileMgr.GetApplicationByUserModelId (userModelId);
			if (tas == null) return null;
			tc = TileControl.CreateTileControl (tas, this);
			if (tc == null) return null;
			tc.SizeChanged -= TileControl_SizeChanged;
			tc.SizeChanged += TileControl_SizeChanged;
			tileCache [userModelId] = tc;
			return tc;
		}
		private TileControl GetOrCreateTile (TileApplicationStorage tas)
		{
			if (tas == null) return null;
			return GetOrCreateTile (tas.UserModelId);
		}
		private void PurgeDeadCache ()
		{
			var alive = new HashSet<string> (App.TileMgr.Applications.Select (a => a.UserModelId), NormalizedStringComparer.Instance);
			var dead = new List<string> ();
			foreach (var key in tileCache.Keys)
				if (!alive.Contains (key)) dead.Add (key);
			foreach (var key in dead)
				tileCache.Remove (key);
		}
		private static void DetachFromParent (UIElement element)
		{
			if (element == null) return;
			var panel = (element as FrameworkElement)?.Parent as Panel;
			if (panel != null) panel.Children.Remove (element);
		}
		private static void SafeInsert (Panel panel, int index, UIElement child)
		{
			if (panel == null || child == null) return;
			DetachFromParent (child);
			int count = panel.Children.Count;
			if (index < 0) index = 0;
			else if (index > count) index = count;
			panel.Children.Insert (index, child);
		}
		private static int IndexOfUserModelId (StackPanel panel, string userModelId)
		{
			if (panel == null || string.IsNullOrWhiteSpace (userModelId)) return -1;
			for (int i = 0; i < panel.Children.Count; i++)
			{
				var tc = panel.Children [i] as TileControl;
				if (tc != null && tc.UserModelId != null && tc.UserModelId.NEquals (userModelId)) return i;
			}
			return -1;
		}
		private static bool ContainsNormalized (IList<string> list, string userModelId)
		{
			if (list == null || string.IsNullOrWhiteSpace (userModelId)) return false;
			for (int i = 0; i < list.Count; i++)
			{
				if (list [i].NEquals (userModelId)) return true;
			}
			return false;
		}
		private static int IndexOfFamilyName (IList<string> list, string userModelId)
		{
			if (list == null || string.IsNullOrWhiteSpace (userModelId)) return -1;
			for (int i = 0; i < list.Count; i++)
			{
				if (list [i].NEquals (userModelId)) return i;
			}
			return -1;
		}
		private static ObservableCollection<string> FindPinnedList (SidebarConfig cc, string userModelId)
		{
			if (cc == null || string.IsNullOrWhiteSpace (userModelId)) return null;
			if (ContainsNormalized (cc.PinnedTiles, userModelId)) return cc.PinnedTiles;
			if (ContainsNormalized (cc.PinnedBottomTiles, userModelId)) return cc.PinnedBottomTiles;
			return null;
		}
		private double TilesRegionHeightLimit
		{
			get
			{
				var p = TilesContainer.Parent as Panel;
				var totalHeight = p.ActualHeight;
				var tilesContainerHeight = totalHeight - TilesContainer.Margin.Top - TilesContainer.Margin.Bottom;
				var nopinTilesHeight = tilesContainerHeight - PinnedTilesControl.ActualHeight - OverflowTilesControl.ActualHeight;
				return nopinTilesHeight < 0 ? 0 : nopinTilesHeight;
			}
		}
		public bool MoveTileUp (TileControl tile) => MoveTileUp (tile?.UserModelId);
		public bool MoveTileUp (string userModelId) => MoveTileCore (userModelId, -1);
		public bool MoveTileDown (TileControl tile) => MoveTileDown (tile?.UserModelId);
		public bool MoveTileDown (string userModelId) => MoveTileCore (userModelId, +1);
		public bool CanMoveTileUp (TileControl tile) => CanMoveTileUp (tile?.UserModelId);
		public bool CanMoveTileUp (string userModelId) => CanMoveTileCore (userModelId, -1);
		public bool CanMoveTileDown (TileControl tile) => CanMoveTileDown (tile?.UserModelId);
		public bool CanMoveTileDown (string userModelId) => CanMoveTileCore (userModelId, +1);
		public bool RemoveTile (TileControl tile) => RemoveTile (tile?.UserModelId);
		public bool RemoveTile (string userModelId)
		{
			if (string.IsNullOrWhiteSpace (userModelId)) return false;
			var cc = App.CurrentUserConfig;
			if (cc == null) return false;
			if (!cc.IsPinned (userModelId)) return false;
			cc.Unpin (userModelId);
			return true;
		}
		public bool PinTileToBottom (TileControl tile) => PinTileToBottom (tile?.UserModelId);
		public bool PinTileToBottom (string userModelId)
		{
			if (string.IsNullOrWhiteSpace (userModelId)) return false;
			var cc = App.CurrentUserConfig;
			if (cc == null) return false;
			cc.PinBottom (userModelId);
			return true;
		}
		public bool PinTileToTop (TileControl tile) => PinTileToTop (tile?.UserModelId);
		public bool PinTileToTop (string userModelId)
		{
			if (string.IsNullOrWhiteSpace (userModelId)) return false;
			var cc = App.CurrentUserConfig;
			if (cc == null) return false;
			cc.PinTop (userModelId);
			return true;
		}
		private bool MoveTileCore (string userModelId, int delta)
		{
			if (string.IsNullOrWhiteSpace (userModelId)) return false;
			var cc = App.CurrentUserConfig;
			if (cc == null) return false;
			var list = FindPinnedList (cc, userModelId);
			if (list == null) return false;
			int idx = IndexOfFamilyName (list, userModelId);
			if (idx < 0) return false;
			int target = idx + delta;
			if (target < 0 || target >= list.Count) return false;
			list.Move (idx, target);
			return true;
		}
		private bool CanMoveTileCore (string userModelId, int delta)
		{
			if (string.IsNullOrWhiteSpace (userModelId)) return false;
			var cc = App.CurrentUserConfig;
			if (cc == null) return false;
			var list = FindPinnedList (cc, userModelId);
			if (list == null) return false;
			int idx = IndexOfFamilyName (list, userModelId);
			if (idx < 0) return false;
			int target = idx + delta;
			return target >= 0 && target < list.Count;
		}
		#endregion
		#region Tile Overflow
		private HideTilesContainWindow hideTilesWindow;
		private void InitOverflowArea ()
		{
			hideTilesWindow?.Close ();
			hideTilesWindow = new HideTilesContainWindow (this);
		}
		private void DestroyOverflowArea ()
		{
			hideTilesWindow?.Close ();
			hideTilesWindow = null;
		}
		private void TileControl_SizeChanged (object sender, System.Windows.SizeChangedEventArgs e)
		{
			RebuildOverflowList ();
		}
		private DispatcherTimer overflowUpdateTimer;
		private void RebuildOverflowList ()
		{
			if (overflowUpdateTimer == null)
			{
				overflowUpdateTimer = new DispatcherTimer ();
				overflowUpdateTimer.Interval = TimeSpan.FromMilliseconds (15);
			}
			overflowUpdateTimer.Stop ();
			overflowUpdateTimer.Tick -= OverflowUpdateTimer_Tick;
			overflowUpdateTimer.Tick += OverflowUpdateTimer_Tick;
			overflowUpdateTimer.Start ();
		}
		private void OverflowUpdateTimer_Tick (object sender, EventArgs e)
		{
			overflowUpdateTimer.Stop ();
			overflowUpdateTimer.Tick -= OverflowUpdateTimer_Tick;
			OnTileHeightChanged ();
		}
		private void OnTileHeightChanged ()
		{
			var totalHeight = TilesRegionHeightLimit;
			foreach (FrameworkElement element in TilesControl.Children)
			{
				TileControl tc = null;
				if (element is TileControl) tc = element as TileControl;
				if (tc == null)
				{
					totalHeight -= element.ActualHeight;
					continue;
				}
				else
				{
					totalHeight -= tc.ActualHeight;
					if (totalHeight < 0)
					{
						tileOverflowList.Add (tc);
					}
				}
			}
			SortOverflowTiles ();
			HashSet<TileControl> willremove = new HashSet<TileControl> ();
			var remain = TilesControl.GetRemainingVertical ();
			if (remain > 0)
			{
				foreach (var tc in tileOverflowList)
				{
					remain -= tc.ActualHeight;
					if (remain >= 0)
					{
						(tc?.Parent as Panel)?.Children?.Remove (tc);
						TilesControl?.Children?.Add (tc);
						willremove.Add (tc);
					}
					else break;
				}
			}
			foreach (var tc in willremove)
			{
				tileOverflowList.Remove (tc);
			}
		}
		private bool IsTileOverflow (TileControl tc) => tileOverflowList?.Contains (tc) ?? false;
		private void SortOverflowTiles ()
		{
			if (tileOverflowList == null || tileOverflowList.Count <= 1) return;
			var cc = App.CurrentUserConfig;
			if (cc == null) return;
			var pinned = cc.PinnedTiles;
			var snapshot = tileOverflowList.ToList ();
			snapshot.Sort ((a, b) => {
				int ia = IndexOfPinned (pinned, a?.UserModelId);
				int ib = IndexOfPinned (pinned, b?.UserModelId);
				if (ia != ib) return ia.CompareTo (ib);
				return string.Compare (a?.UserModelId ?? "", b?.UserModelId ?? "", StringComparison.OrdinalIgnoreCase);
			});
			for (int i = 0; i < snapshot.Count; i++)
			{
				int current = tileOverflowList.IndexOf (snapshot [i]);
				if (current >= 0 && current != i) tileOverflowList.Move (current, i);
			}
		}
		private static int IndexOfPinned (IList<string> pinned, string umid)
		{
			if (pinned == null || string.IsNullOrWhiteSpace (umid)) return int.MaxValue;
			for (int i = 0; i < pinned.Count; i++)
			{
				if (pinned [i].NEquals (umid)) return i;
			}
			return int.MaxValue;
		}
		#endregion
		#region Features
		public ISidebarConfig Config => App.CurrentUserConfig;
		public bool Request (ISidebarRequest request) => Communicate (request);
		public bool Communicate (ITileRequest request)
		{
			return false;
		}
		public bool Response (ITileResponse resp)
		{
			return false;
		}
		#endregion
		#region Menu Item
		private void MenuItemMinBar_Click (object sender, RoutedEventArgs e)
		{
			MinimumSidebar ();
		}
		private ConfigWindow sidebarConfWnd = null;
		private void MenuItemProperties_Click (object sender, RoutedEventArgs e)
		{
			if (sidebarConfWnd != null) sidebarConfWnd.Activate ();
			else
			{
				sidebarConfWnd = new ConfigWindow ();
				sidebarConfWnd.Closed += SidebarConfWnd_Closed;
				sidebarConfWnd.Show ();
			}
		}
		private void SidebarConfWnd_Closed (object sender, EventArgs e)
		{
			sidebarConfWnd.Closed -= SidebarConfWnd_Closed;
			sidebarConfWnd = null;
		}
		private void MenuItemCancel_Click (object sender, RoutedEventArgs e)
		{
			try { Close (); } catch { }
		}
		private void MenuItemGetMoreTiles_Click (object sender, RoutedEventArgs e)
		{
			Process.Start (System.IO.Path.Combine (App.ProgramFolder.FolderPath, "SGStore.exe"));
		}
		private void TopOptionsButton_Click (object sender, RoutedEventArgs e)
		{
			var menu = Resources ["SidebarContextMenu"] as ContextMenu;
			if (menu == null) return;
			menu.PlacementTarget = (UIElement)sender ?? this;
			menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
			menu.IsOpen = true;
		}
		private void MenuItemAddTiles_SubmenuOpened (object sender, RoutedEventArgs e)
		{
			var menuItem = sender as MenuItem;
			if (menuItem == null) return;
			menuItem.Items.Clear ();
			var cc = App.CurrentUserConfig;
			var apps = App.TileMgr.Applications;
			if (apps == null || apps.Count == 0)
			{
				menuItem.Items.Add (new MenuItem {
					Header = App.ProgramFolder.StringResources.SuitableResource ("SIDEBAR_CONTEXTMENU_NOTILE", "(No tiles installed)"),
					IsEnabled = false
				});
				return;
			}
			foreach (var app in apps)
			{
				string umid = app.UserModelId;
				if (string.IsNullOrWhiteSpace (umid)) continue;
				bool isPinned = cc.IsPinned (umid);
				var subItem = new MenuItem {
					Header = app.DisplayName,
					IsCheckable = true,
					IsChecked = isPinned,
					Tag = umid
				};
				subItem.Click += MenuSubItemApp_Click;
				menuItem.Items.Add (subItem);
			}
		}
		private void MenuSubItemApp_Click (object sender, RoutedEventArgs e)
		{
			var item = sender as MenuItem;
			if (item == null) return;
			string umid = item.Tag as string;
			if (string.IsNullOrWhiteSpace (umid)) return;
			var cc = App.CurrentUserConfig;
			if (item.IsChecked) cc.PinTop (umid);
			else cc.Unpin (umid);
		}
		#endregion
		#region Handle & Aero
		public HWND HWnd => InteropHelper?.Handle ?? IntPtr.Zero;
		public WindowInteropHelper InteropHelper { get; private set; }
		public IntPtr Handle => InteropHelper?.Handle ?? IntPtr.Zero;
		public IntPtr WndOwner => InteropHelper?.Owner ?? IntPtr.Zero;
		private void InitInteropHelper ()
		{
			InteropHelper = null;
			InteropHelper = new WindowInteropHelper (this);
		}
		private bool ?_lastAeroState = null;
		private void ApplyAero (bool value)
		{
			if (!DWMAPI.IsDwmAvailable ()) return;
			if (_lastAeroState.HasValue && _lastAeroState.Value == value) return;
			_lastAeroState = value;
			IntPtr hwnd = Handle;
			if (value)
			{
				DWMAPI.EnableBlur (ref hwnd, IntPtr.Zero);
				try { if (!AllowsTransparency) AllowsTransparency = true; } catch { }
			}
			else DWMAPI.DisableBlur (ref hwnd);
			try { if (Environment.OSVersion.Version.Major > 6) WindowAccent.SetAccentPolicy (hwnd, value); } catch { }
		}
		private bool isSourceInitialized = false;
		private HwndSource hwndSource = null;
		private void InitHwndSource ()
		{
			hwndSource?.Dispose ();
			hwndSource = HwndSource.FromHwnd (HWnd);
		}
		private void DestoryHwndSource ()
		{
			hwndSource?.Dispose ();
			hwndSource = null;
		}
		private void Window_SourceInitialized (object sender, EventArgs e)
		{
			isSourceInitialized = true;
			InitInteropHelper ();
			InitHwndSource ();
			InitAppBar ();
			bool enableBlur = false;
			if (Application.Current.Resources.Contains ("EnableBlur"))
				enableBlur = (bool)Application.Current.Resources ["EnableBlur"];
			ApplyAero (enableBlur);
			var h = HWnd;
			Presentation.DwmThemeProvider.Instance.StartListening (hwndSource);
			h.StylesEx |= Win32.ExtendedWindowStyles.WS_EX_TOOLWINDOW;
			h.StylesEx |= Win32.ExtendedWindowStyles.WS_EX_LAYERED;
			#region Preview Desktop
			IntPtr desktopHwnd = Win32WindowNative.FindWindow ("Progman", null);
			if (desktopHwnd == IntPtr.Zero)
			{
				IntPtr workerW = IntPtr.Zero;
				Win32WindowNative.EnumWindows (delegate (IntPtr hWnd, IntPtr lParam)
				{
					if (Win32WindowNative.FindWindowEx (hWnd, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
					{
						workerW = Win32WindowNative.FindWindowEx (IntPtr.Zero, hWnd, "WorkerW", null);
						return false;
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
			#endregion
		}
		#endregion
		#region Appbar & Overlap Taskbar
		private SidebarAppBar appBar;
		private TaskbarOverlapHelper taskbarHelper;
		private void InitAppBar ()
		{
			var cc = App.CurrentUserConfig;
			appBar?.Dispose ();
			appBar = new SidebarAppBar (this);
			appBar.Direction = cc.Direction;
			taskbarHelper?.Dispose ();
			taskbarHelper = new TaskbarOverlapHelper (this);
			taskbarHelper.Direction = cc.Direction;
			taskbarHelper.CurrentScreen = cc.CurrentScreen;
		}
		private void MinimumSidebar ()
		{
			var cc = App.CurrentUserConfig;
			cc.IsMinimum = true;
			if (cc.OccupyWorkingArea) appBar.Unregister ();
			if (cc.OverlapTaskbar)
			{
				SidebarView.Visibility = Visibility.Collapsed;
				SizeToContent = SizeToContent.Width;
				if (TaskbarView.Visibility != Visibility.Visible)
				{
					TaskbarView.Visibility = Visibility.Visible;
					var sb = TryFindResource ("ShowTaskbarTiles") as Storyboard;
					sb?.Begin ();
				}
				Visibility = Visibility.Visible;
				if (TaskbarTiles.ItemsSource == null)
				{
					var tiles = TilesControl.Children.OfType<TileControl> ().Select (t => t.VisualView).ToList ();
					tiles.AddRange (tileOverflowList.Select (t => t.VisualView));
					tiles.AddRange (PinnedTilesControl.Children.OfType<TileControl> ().Select (t => t.VisualView));
					TaskbarTiles.ItemsSource = tiles;
				}
				RefreshWndPos ();
			}
			else
			{
				ClearValue (SizeToContentProperty);
				Width = cc.Width;
				SidebarView.Visibility = Visibility.Visible;
				TaskbarView.Visibility = Visibility.Collapsed;
				Visibility = Visibility.Hidden;
				TaskbarTiles.ItemsSource = null;
			}
		}
		private void RestoreSidebar ()
		{
			var cc = App.CurrentUserConfig;
			cc.IsMinimum = false;
			ClearValue (SizeToContentProperty);
			SidebarView.Visibility = Visibility.Visible;
			TaskbarView.Visibility = Visibility.Collapsed;
			Visibility = Visibility.Visible;
			Width = cc.Width;
			if (cc.OccupyWorkingArea) appBar.Register ();
			RefreshWndPos ();
		}
		#endregion
		#region Position
		private void RefreshWndPos ()
		{
			if (!isSourceInitialized) return;
			var cc = App.CurrentUserConfig;
			if (cc.IsMinimum)
			{
				var tbrect = ExplorerShell.GetTaskbarRect ();
				var tbdirect = ExplorerShell.GetTaskbarEdge ();
				switch (tbdirect)
				{
					case Win32.AppBarEdge.Bottom:
					case Win32.AppBarEdge.Top:
						var l = tbrect.Right - this.GetPixelWidth ();
						var t = tbrect.Top;
						HWnd.SetPosition (l, t, null, tbrect.Height);
						break;
				}
			}
			else
			{
				if (cc.OccupyWorkingArea && appBar.IsRegistered) appBar.SizeAppBar ();
				else appBar.SetPos ();
			}
		}
		private void RestoreButton_Click (object sender, RoutedEventArgs e)
		{
			RestoreSidebar ();
		}
		#endregion
		#region Scroll Tiles Container
		private void ApplyScrollMode (bool canscroll)
		{
			var PinnedTilesScrollRegion = ScrollPinnedTilesControl;
			var PinnedTilesRegion = PinnedTilesControl;
			var TilesScrollRegion = ScrollTilesControl;
			var TilesRegion = TilesControl;
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
		#endregion
		#region Settings
		internal SidebarContext Context = new SidebarContext ();
		private void CurrentUserConfig_PropertyChanged (object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			var cuc = App.CurrentUserConfig;
			switch (e.PropertyName)
			{
				case nameof (cuc.Direction):
					Context.Direction = cuc.Direction;
					foreach (var kv in tileCache)
						kv.Value?.Instance?.Instance?.OnHostChanged (TileHostEvent.SidebarDirectionChanged, Context);
					if (appBar != null && appBar.IsRegistered) appBar.Direction = cuc.Direction;
					if (taskbarHelper != null) taskbarHelper.Direction = cuc.Direction;
					RefreshWndPos ();
					break;
				case nameof (cuc.Width):
					Width = Math.Max (MinWidth, Math.Min (cuc.Width, MaxWidth));
					RefreshWndPos ();
					break;
				case nameof (cuc.AutoRun):
					if (StartupManager.IsEnabled () != cuc.AutoRun)
					{
						if (cuc.AutoRun) StartupManager.Enable ();
						else StartupManager.Disable ();
					}
					break;
				case nameof (cuc.CanScroll):
					ApplyScrollMode (cuc.CanScroll);
					break;
				case nameof (cuc.Screen):
					RefreshWndPos ();
					break;
				case nameof (cuc.OccupyWorkingArea):
					if (!isSourceInitialized) return;
					if (cuc.OccupyWorkingArea) appBar.Register ();
					else appBar.Unregister ();
					RefreshWndPos ();
					break;
				case nameof (cuc.OverlapTaskbar):
					if (!isSourceInitialized) return;
					if (cuc.OverlapTaskbar) taskbarHelper.Register ();
					else taskbarHelper.Unregister ();
					RefreshWndPos ();
					if (cuc.IsMinimum) MinimumSidebar ();
					break;
			}
		}
		private void InitSettings ()
		{
			var cc = App.CurrentUserConfig;
			Width = cc.Width;
			ApplyScrollMode (cc.CanScroll);
			if (cc.IsMinimum) MinimumSidebar ();
			UpdateLayout ();
			RefreshWndPos ();
			if (cc.OccupyWorkingArea & !cc.IsMinimum) appBar.Register ();
			if (cc.OverlapTaskbar) taskbarHelper.Register ();
			RefreshWndPos ();
		}
		#endregion
		#region Notify Icon
		private NotifyIcon notifyIcon = null;
		private FormContextMenu trayMenu = null;
		private void InitNotifyIcon ()
		{
			trayMenu = new FormContextMenu ();
			notifyIcon = new NotifyIcon ();
			notifyIcon.ContextMenu = trayMenu;
			#region Init Tray Menu
			var displayItem = new FormMenuItem ();
			var propItem = new FormMenuItem ();
			var exitItem = new FormMenuItem ();
			displayItem.Text = App.ProgramFolder.StringResources.SuitableResource ("SIDEBAR_CONTEXTMENU_SHOW");
			displayItem.Checked = !App.CurrentUserConfig.IsMinimum;
			displayItem.Click += TrayMenuItemDisplay_Click;
			propItem.Text = App.ProgramFolder.StringResources.SuitableResource ("SIDEBAR_CONTEXTMENU_PROP");
			propItem.Click += TrayMenuItemProperties_Click;
			exitItem.Text = App.ProgramFolder.StringResources.SuitableResource ("SIDEBAR_CONTEXTMENU_CANCEL");
			exitItem.Click += TrayMenuItemExit_Click;
			trayMenu.MenuItems.Add (displayItem);
			trayMenu.MenuItems.Add (propItem);
			trayMenu.MenuItems.Add ("-");
			trayMenu.MenuItems.Add (exitItem);
			#endregion
			notifyIcon.Icon = Properties.Resources.AppIcon;
			notifyIcon.Text = Title;
			notifyIcon.Visible = true;
			notifyIcon.MouseDoubleClick += NotifyIcon_MouseDoubleClick;
			notifyIcon.ContextMenu.Popup += TrayMenu_Popup;
		}
		private void TrayMenu_Popup (object sender, EventArgs e)
		{
			var displayItem = (sender as FormContextMenu).MenuItems [0];
			displayItem.Checked = !App.CurrentUserConfig.IsMinimum;
		}
		private void TrayMenuItemExit_Click (object sender, EventArgs e)
		{
			Close ();
		}
		private void TrayMenuItemProperties_Click (object sender, EventArgs e)
		{
			if (sidebarConfWnd != null) sidebarConfWnd.Activate ();
			else
			{
				sidebarConfWnd = new ConfigWindow ();
				sidebarConfWnd.Closed += SidebarConfWnd_Closed;
				sidebarConfWnd.Show ();
			}
		}
		private void TrayMenuItemDisplay_Click (object sender, EventArgs e)
		{
			if (App.CurrentUserConfig.IsMinimum) RestoreSidebar ();
			else MinimumSidebar ();
		}
		private void NotifyIcon_MouseDoubleClick (object sender, System.Windows.Forms.MouseEventArgs e)
		{
			RestoreSidebar ();
			Activate ();
		}
		#endregion
		#region Resize
		private const int WM_SYSCOMMAND = 0x0112;
		private const int SC_SIZE_LEFT = 0xF001;
		private const int SC_SIZE_RIGHT = 0xF002;
		private const double ResizeHotZone = 5.0;
		private const double DefaultWidth = 150;
		private void Window_MouseMove (object sender, MouseEventArgs e)
		{
			var cc = App.CurrentUserConfig;
			if (cc.IsMinimum) return;
			if (cc.Locked) return;
			double x = e.GetPosition (this).X;
			switch (cc.Direction)
			{
				case SidebarDirection.Right:
					if (x <= ResizeHotZone)
					{
						base.Cursor = Cursors.SizeWE;
						if (e.LeftButton == MouseButtonState.Pressed)
						{
							Win32WindowNative.SendMessageW (Handle, WM_SYSCOMMAND, (IntPtr)SC_SIZE_LEFT, IntPtr.Zero);
							cc.Width = (int)Math.Round (Width);
						}
					}
					else if (base.Cursor != Cursors.Arrow)
						base.Cursor = Cursors.Arrow;
					break;
				case SidebarDirection.Left:
					if (x >= this.Width - ResizeHotZone)
					{
						base.Cursor = Cursors.SizeWE;
						if (e.LeftButton == MouseButtonState.Pressed)
						{
							Win32WindowNative.SendMessageW (Handle, WM_SYSCOMMAND, (IntPtr)SC_SIZE_RIGHT, IntPtr.Zero);
							cc.Width = (int)Math.Round (Width);
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
			if (cc.IsMinimum || cc.Locked) return;
			double x = e.GetPosition (this).X;
			switch (cc.Direction)
			{
				case SidebarDirection.Right:
					if (x <= ResizeHotZone) cc.Width = (int)DefaultWidth;
					break;
				case SidebarDirection.Left:
					if (x >= this.Width - ResizeHotZone) cc.Width = (int)DefaultWidth;
					break;
			}
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
		#endregion
		#region System Event
		private void InitSystemEventHandlers ()
		{
			ScreenChangeNotifier.ScaleChanged += ScreenChangeNotifier_ScaleChanged;
			ScreenChangeNotifier.SizeChanged += ScreenChangeNotifier_SizeChanged;
			ScreenChangeNotifier.WorkingAreaHeightChanged += ScreenChangeNotifier_WorkingAreaHeightChanged;
			ScreenChangeNotifier.WorkingAreaChanged += ScreenChangeNotifier_WorkingAreaChanged;
			ScreenChangeNotifier.StartListening (this);
			ExplorerShell.TaskbarPositionChanged += ExplorerShell_TaskbarPositionChanged;
			ExplorerShell.StartTaskbarMonitoring ();
		}
		private void ExplorerShell_TaskbarPositionChanged (object sender, EventArgs e)
		{
			var cc = App.CurrentUserConfig;
			if (cc.OverlapTaskbar && cc.IsMinimum) RefreshWndPos ();
		}
		private void ScreenChangeNotifier_WorkingAreaChanged (object sender, EventArgs e)
		{
			var cc = App.CurrentUserConfig;
			if (!cc.OccupyWorkingArea) RefreshWndPos ();
		}
		private void ScreenChangeNotifier_WorkingAreaHeightChanged (object sender, EventArgs e)
		{
			var cc = App.CurrentUserConfig;
			if (cc.OccupyWorkingArea && !cc.OverlapTaskbar) RefreshWndPos ();
		}
		private void ScreenChangeNotifier_SizeChanged (object sender, EventArgs e)
		{
			RefreshWndPos ();
		}
		private void ScreenChangeNotifier_ScaleChanged (object sender, EventArgs e)
		{
			RefreshWndPos ();
		}
		private void DestroySystemEventHandlers ()
		{
			ScreenChangeNotifier.StopListening ();
			ScreenChangeNotifier.ScaleChanged -= ScreenChangeNotifier_ScaleChanged;
			ScreenChangeNotifier.SizeChanged -= ScreenChangeNotifier_SizeChanged;
			ScreenChangeNotifier.WorkingAreaHeightChanged -= ScreenChangeNotifier_WorkingAreaHeightChanged;
			ScreenChangeNotifier.WorkingAreaChanged -= ScreenChangeNotifier_WorkingAreaChanged;
			ExplorerShell.StopTaskbarMonitoring ();
			ExplorerShell.TaskbarPositionChanged -= ExplorerShell_TaskbarPositionChanged;
		}
		#endregion
		private void Window_Loaded (object sender, RoutedEventArgs e)
		{
			UpdateMinMaxWidth ();
			InitSettings ();
			InitNotifyIcon ();
			InitSystemEventHandlers ();
			InitOverflowArea ();
			ContextMenu = TryFindResource ("SidebarContextMenu") as ContextMenu;
			App.CurrentUserConfig.PropertyChanged -= CurrentUserConfig_PropertyChanged;
			App.CurrentUserConfig.PropertyChanged += CurrentUserConfig_PropertyChanged;
		}
		private void Window_Closed (object sender, EventArgs e)
		{
			DestroyOverflowArea ();
			DestroySystemEventHandlers ();
			UninitTilesList ();
			FlushPendingRemovals ();
			App.CurrentUserConfig.PropertyChanged -= CurrentUserConfig_PropertyChanged;
			var cc = App.CurrentUserConfig;
			if (cc.OverlapTaskbar) taskbarHelper.Unregister ();
			if (cc.OccupyWorkingArea) appBar.Unregister ();
			notifyIcon.Visible = false;
			notifyIcon?.Dispose ();
			notifyIcon = null;
			DestoryHwndSource ();
			App.Current.Shutdown ();
		}
		private void Window_SizeChanged (object sender, System.Windows.SizeChangedEventArgs e)
		{
			var cc = App.CurrentUserConfig;
			if (cc.IsMinimum & cc.OverlapTaskbar) RefreshWndPos ();
		}
	}
}

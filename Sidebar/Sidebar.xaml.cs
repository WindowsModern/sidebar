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
using Sidebar;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Sidebar
{
	/// <summary>
	/// Sidebar.xaml 的交互逻辑
	/// </summary>
	public partial class SidebarWindow: Window, ISidebarFeatures
	{
		public SidebarWindow ()
		{
			InitializeComponent ();
			InitTilesList ();
		}
		#region Tile to Create/Remove/Move
		private void InitTilesList ()
		{
			var cc = App.CurrentUserConfig;
			RebuildList (tilePinnedTopList, cc.PinnedTiles);
			RebuildList (tilePinnedBottomList, cc.PinnedBottomTiles);
			cc.PinnedTiles.CollectionChanged += PinnedTiles_CollectionChanged;
			cc.PinnedBottomTiles.CollectionChanged += PinnedBottomTiles_CollectionChanged;
			TilesControl.ItemsSource = tilePinnedTopList;
			PinnedTilesControl.ItemsSource = tilePinnedBottomList;
			App.TileMgr.Applications.CollectionChanged += Applications_CollectionChanged;
		}
		private Dictionary<string, TileControl> tileCache = new Dictionary<string, TileControl> (NormalizedStringComparer.Instance);
		private ObservableCollection<TileControl> tilePinnedTopList = new ObservableCollection<TileControl> ();
		private ObservableCollection<TileControl> tilePinnedBottomList = new ObservableCollection<TileControl> ();
		private bool _rebuildScheduled;
		private TileControl GetOrCreateTile (string userModelId)
		{
			TileControl tc = null;
			if (tileCache.TryGetValue (userModelId, out tc)) return tc;
			var tas = App.TileMgr.GetApplicationByUserModelId (userModelId);
			if (tas == null) return null;
			tc = TileControl.CreateTileControl (tas, this);
			if (tc == null) return null;
			tileCache [userModelId] = tc;
			return tc;
		}
		private TileControl GetOrCreateTile (TileApplicationStorage tas)
		{
			if (tas == null) return null;
			return GetOrCreateTile (tas.UserModelId);
		}
		private void PinnedTiles_CollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
		{
			ApplyPinnedChange (tilePinnedTopList, e);
		}
		private void PinnedBottomTiles_CollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
		{
			ApplyPinnedChange (tilePinnedBottomList, e);
		}
		private void ApplyPinnedChange (ObservableCollection<TileControl> target, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					InsertNewItems (target, e.NewItems, e.NewStartingIndex);
					break;
				case NotifyCollectionChangedAction.Remove:
					for (int i = 0; i < e.OldItems.Count; i++)
					{
						var umid = e.OldItems [i] as string;
						BeginRemoveTileWithAnimation (target, umid);
					}
					break;
				case NotifyCollectionChangedAction.Move:
					if (e.OldItems.Count == 1)
					{
						int oldIdx = IndexOfUserModelId (target, e.OldItems [0] as string);
						if (oldIdx >= 0)
						{
							int newIdx = e.NewStartingIndex;
							if (newIdx < 0) newIdx = 0;
							if (newIdx >= target.Count) newIdx = target.Count - 1;
							if (oldIdx != newIdx) target.Move (oldIdx, newIdx);
						}
					}
					break;
				case NotifyCollectionChangedAction.Replace:
					for (int i = 0; i < e.OldItems.Count; i++)
					{
						int idx = IndexOfUserModelId (target, e.OldItems [i] as string);
						if (idx >= 0) target.RemoveAt (idx);
					}
					InsertNewItems (target, e.NewItems, e.NewStartingIndex);
					break;
				case NotifyCollectionChangedAction.Reset:
					RebuildList (target, GetSourceList (target));
					break;
			}
		}
		private void InsertNewItems (ObservableCollection<TileControl> target, IList newItems, int startIndex)
		{
			if (newItems == null) return;
			for (int i = 0; i < newItems.Count; i++)
			{
				var umid = newItems [i] as string;
				if (string.IsNullOrWhiteSpace (umid)) continue;
				if (ContainsUserModelId (target, umid)) continue;
				var tc = GetOrCreateTile (umid);
				if (tc == null) continue;
				var ispinned = App.CurrentUserConfig.IsPinnedBottom (umid);
				tc.IsPinned = ispinned;
				if (tc?.Instance?.Config != null)
				{
					(tc.Instance.Config as TileConfig)?.NotifyPinned ();
				}
				int insertAt = startIndex + i;
				if (insertAt < 0) insertAt = 0;
				if (insertAt > target.Count) insertAt = target.Count;
				target.Insert (insertAt, tc);
			}
		}
		private static int IndexOfUserModelId (ObservableCollection<TileControl> list, string userModelId)
		{
			if (list == null || string.IsNullOrWhiteSpace (userModelId)) return -1;
			for (int i = 0; i < list.Count; i++)
			{
				var tc = list [i];
				if (tc != null && tc.UserModelId != null && tc.UserModelId.NEquals (userModelId)) return i;
			}
			return -1;
		}
		private static bool ContainsUserModelId (ObservableCollection<TileControl> list, string userModelId)
		{
			return IndexOfUserModelId (list, userModelId) >= 0;
		}
		private IList<string> GetSourceList (ObservableCollection<TileControl> target)
		{
			var cc = App.CurrentUserConfig;
			if (ReferenceEquals (target, tilePinnedTopList)) return cc.PinnedTiles;
			if (ReferenceEquals (target, tilePinnedBottomList)) return cc.PinnedBottomTiles;
			return null;
		}
		private void RebuildList (ObservableCollection<TileControl> target, IList<string> source)
		{
			target.Clear ();
			if (source == null) return;
			foreach (var umid in source)
			{
				if (_pendingRemovals.Contains (umid)) continue;
				var tc = GetOrCreateTile (umid);
				if (tc != null)
				{
					tc.IsPinned = App.CurrentUserConfig.IsPinnedBottom (umid);
					target.Add (tc);
				}
			}
		}
		private void Applications_CollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action == NotifyCollectionChangedAction.Remove || e.Action == NotifyCollectionChangedAction.Replace || e.Action == NotifyCollectionChangedAction.Reset)
			{
				PurgeDeadCache ();
			}
			if (_rebuildScheduled) return;
			_rebuildScheduled = true;
			Dispatcher.BeginInvoke (DispatcherPriority.Background, new Action (() => {
				_rebuildScheduled = false;
				var cc = App.CurrentUserConfig;
				RebuildList (tilePinnedTopList, cc.PinnedTiles);
				RebuildList (tilePinnedBottomList, cc.PinnedBottomTiles);
			}));
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
		private void UninitTilesList ()
		{
			var cc = App.CurrentUserConfig;
			cc.PinnedTiles.CollectionChanged -= PinnedTiles_CollectionChanged;
			cc.PinnedBottomTiles.CollectionChanged -= PinnedBottomTiles_CollectionChanged;
			App.TileMgr.Applications.CollectionChanged -= Applications_CollectionChanged;
		}
		#endregion
		#region Tile Remove
		private readonly HashSet<string> _pendingRemovals = new HashSet<string> (NormalizedStringComparer.Instance);
		private void BeginRemoveTileWithAnimation (ObservableCollection<TileControl> target, string userModelId)
		{
			if (string.IsNullOrWhiteSpace (userModelId)) return;
			if (!_pendingRemovals.Add (userModelId)) return;
			int idx = IndexOfUserModelId (target, userModelId);
			if (idx < 0)
			{
				_pendingRemovals.Remove (userModelId);
				return;
			}
			var tc = target [idx];
			if (tc == null)
			{
				_pendingRemovals.Remove (userModelId);
				return;
			}
			var task = tc.DestroyTileControl ();
			task.ContinueWith (_ => {
				var d = Dispatcher;
				if (d == null || d.HasShutdownStarted || d.HasShutdownFinished) return;
				try { d.BeginInvoke (new Action (() => FinishRemoveTile (target, userModelId))); }
				catch { }
			}, TaskScheduler.Default);
		}
		private void FinishRemoveTile (ObservableCollection<TileControl> target, string userModelId)
		{
			_pendingRemovals.Remove (userModelId);
			int idx = IndexOfUserModelId (target, userModelId);
			if (idx >= 0) target.RemoveAt (idx);
			tileCache.Remove (userModelId);
		}
		private void FlushPendingRemovals ()
		{
			if (_pendingRemovals.Count == 0) return;
			var keys = new List<string> (_pendingRemovals);
			_pendingRemovals.Clear ();
			foreach (var umid in keys)
			{
				int idx = IndexOfUserModelId (tilePinnedTopList, umid);
				if (idx >= 0) tilePinnedTopList.RemoveAt (idx);
				idx = IndexOfUserModelId (tilePinnedBottomList, umid);
				if (idx >= 0) tilePinnedBottomList.RemoveAt (idx);
				tileCache.Remove (umid);
			}
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
			App.Current.Shutdown ();
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
		private void Window_Loaded (object sender, RoutedEventArgs e)
		{
			ContextMenu = Resources ["SidebarContextMenu"] as ContextMenu;
			App.CurrentUserConfig.PropertyChanged -= CurrentUserConfig_PropertyChanged;
			App.CurrentUserConfig.PropertyChanged += CurrentUserConfig_PropertyChanged;
		}
		private void CurrentUserConfig_PropertyChanged (object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			var cuc = App.CurrentUserConfig;
			switch (e.PropertyName)
			{
				case nameof (cuc.Direction):
					break;
			}
		}
		private void Window_Closed (object sender, EventArgs e)
		{
			UninitTilesList ();
			FlushPendingRemovals ();
			App.CurrentUserConfig.PropertyChanged -= CurrentUserConfig_PropertyChanged;
		}
	}
}

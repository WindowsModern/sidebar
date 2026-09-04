using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualBasic.FileIO;

namespace Sidebar
{
	public partial class ManagerWindow: Window, INotifyPropertyChanged
	{
		private readonly TileManager _tileManager = App.TileMgr;
		private readonly ObservableCollection<string> _pinnedTiles = App.CurrentUserConfig.PinnedTiles;
		private TileMgrItem _selectedItem;
		private bool _isRefreshing = false;
		private DispatcherTimer _refreshTimer = new DispatcherTimer () {
			Interval = TimeSpan.FromSeconds (0.55)
		};
		public ObservableCollection<TileMgrItem> AvailableTiles { get; private set; }
		public ObservableCollection<TileMgrItem> PinnedTopTiles { get; private set; }
		public ObservableCollection<TileMgrItem> PinnedBottomTiles { get; private set; }

		public TileMgrItem SelectedItem
		{
			get { return _selectedItem; }
			set
			{
				if (object.Equals (_selectedItem, value)) return;
				_selectedItem = value;
				OnPropertyChanged ("SelectedItem");
				UpdateButtons ();
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged (string name)
		{
			PropertyChangedEventHandler handler = PropertyChanged;
			if (handler != null)
			{
				handler (this, new PropertyChangedEventArgs (name));
			}
		}

		public ManagerWindow ()
		{
			InitializeComponent ();
			DataContext = this;
			Title = App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_WINTITLE");
			AvailableTiles = new ObservableCollection<TileMgrItem> ();
			PinnedTopTiles = new ObservableCollection<TileMgrItem> ();
			PinnedBottomTiles = new ObservableCollection<TileMgrItem> ();

			LoadTiles ();
			_pinnedTiles.CollectionChanged += PinnedTiles_CollectionChanged;
			//App.TileMgr.ValidTilesObservable.CollectionChanged += PinnedTiles_CollectionChanged;
			//_refreshTimer.Tick += RefreshList_Timer;
		}

		private void RefreshList_Timer (object sender, EventArgs e)
		{
			Dispatcher.BeginInvoke (new Action (() => {
				if (_refreshPending) return;
				_refreshPending = true;
				try
				{
					RefreshLists ();
				}
				finally
				{
					_refreshPending = false;
				}
			}), System.Windows.Threading.DispatcherPriority.Background);
		}

		private void Window_Loaded (object sender, RoutedEventArgs e)
		{
			InitLocalization ();

			if (AvailableList.SelectedItem == null && PinnedTopList.SelectedItem == null && PinnedBottomList.SelectedItem == null)
			{
				SelectedItem = null;
			}
			UpdateButtons ();
			SetStatus (App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_STATUS_READY", "就绪"));
		}

		private void Window_Closed (object sender, EventArgs e)
		{
			_pinnedTiles.CollectionChanged -= PinnedTiles_CollectionChanged;
			App.TileMgr.ValidTilesObservable.CollectionChanged -= PinnedTiles_CollectionChanged;
			_refreshTimer.Tick -= RefreshList_Timer;
			App.ReleaseLargeResourcesAsync ();
		}

		private void InitLocalization ()
		{
			var loc = App.ProgramFolder.StringResources;

			this.Title = loc.SuitableResource ("TILEMGR_WINTITLE", "磁贴管理器");
			TitleText.Text = loc.SuitableResource ("TILEMGR_WINTITLE", "磁贴管理器");
			AvailableHeader.Text = loc.SuitableResource ("TILEMGR_VALIDTILES", "可用磁贴");
			PinnedTopHeader.Text = loc.SuitableResource ("TILEMGR_PINNEDTILES", "固定到顶部");
			PinnedBottomHeader.Text = loc.SuitableResource ("TILEMGR_PINNEDBOTTOMTILES", "固定到底部");
			AddButton.Content = loc.SuitableResource ("TILEMGR_ADDTO", "添加到边栏");
			UnpinButton.Content = loc.SuitableResource ("TILEMGR_REMOVE", "取消固定");
			DeleteButton.Content = loc.SuitableResource ("TILEMGR_DELETE", "删除小工具");
			EmptyHint.Text = loc.SuitableResource ("TILEMGR_SELECTAITEM", "请从左侧选择一个磁贴以查看详情");
		}
		private bool _refreshPending = false;
		private void PinnedTiles_CollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
		{
			RefreshLists ();
		}

		private void LoadTiles ()
		{
			RefreshLists ();
		}

		private void RefreshLists ()
		{
			_isRefreshing = true;
			try
			{
				AvailableTiles.Clear ();
				PinnedTopTiles.Clear ();
				PinnedBottomTiles.Clear ();

				var allValid = _tileManager.ValidTilesObservable;
				if (allValid == null) return;

				var pinnedSet = new HashSet<string> (_pinnedTiles);

				foreach (var storage in allValid)
				{
					var family = storage.Manifest.Identity.FamilyName;
					var item = new TileMgrItem (storage);

					if (pinnedSet.Contains (family))
					{
						var config = new TileConfig (storage.TileCurrentUserFolder);
						if (config.Pinned)
							PinnedBottomTiles.Add (item);
						else
							PinnedTopTiles.Add (item);
					}
					else
					{
						AvailableTiles.Add (item);
					}
				}

				RestoreSelection ();
				UpdateButtons ();
			}
			finally
			{
				_isRefreshing = false;
			}
		}

		private void RestoreSelection ()
		{
			if (SelectedItem == null) return;
			var family = SelectedItem.Storage.Manifest.Identity.FamilyName;

			TileMgrItem target = null;
			foreach (var t in AvailableTiles)
			{
				if (t.Storage.Manifest.Identity.FamilyName == family) { target = t; break; }
			}
			if (target != null) { SelectedItem = target; return; }

			foreach (var t in PinnedTopTiles)
			{
				if (t.Storage.Manifest.Identity.FamilyName == family) { target = t; break; }
			}
			if (target != null) { SelectedItem = target; return; }

			foreach (var t in PinnedBottomTiles)
			{
				if (t.Storage.Manifest.Identity.FamilyName == family) { target = t; break; }
			}
			if (target != null) { SelectedItem = target; return; }

			SelectedItem = null;
		}

		private void TileList_SelectionChanged (object sender, SelectionChangedEventArgs e)
		{
			// 刷新过程中不处理选中变化，避免意外清空 SelectedItem
			if (_isRefreshing) return;

			var list = sender as ListBox;
			if (list != null && list.SelectedItem is TileMgrItem)
			{
				SelectedItem = (TileMgrItem)list.SelectedItem;
			}
			else if (sender == AvailableList || sender == PinnedTopList || sender == PinnedBottomList)
			{
				if (SelectedItem != null && !IsItemInAnyList (SelectedItem))
					SelectedItem = null;
			}
			UpdateButtons ();
		}

		private bool IsItemInAnyList (TileMgrItem item)
		{
			if (item == null) return false;
			return AvailableTiles.Contains (item) || PinnedTopTiles.Contains (item) || PinnedBottomTiles.Contains (item);
		}

		private void UpdateButtons ()
		{
			bool hasSelected = SelectedItem != null;
			bool isAvailable = hasSelected && AvailableTiles.Contains (SelectedItem);
			bool isPinned = hasSelected && (PinnedTopTiles.Contains (SelectedItem) || PinnedBottomTiles.Contains (SelectedItem));

			AddButton.IsEnabled = isAvailable;
			UnpinButton.IsEnabled = isPinned;
			DeleteButton.IsEnabled = hasSelected;

			if (hasSelected)
			{
				string status = isAvailable ? App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_STATUS_AVAILABLE", "可用")
											: (isPinned ? App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_STATUS_PINNED", "已固定")
														: "");
				SetStatus (string.Format (App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_STATUS_SELECTED", "已选择: {0} ({1})"),
										SelectedItem.Title, status));
			}
			else
			{
				SetStatus (App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_SELECTAITEM", "请从左侧选择一个磁贴"));
			}
		}

		private void SetStatus (string text)
		{
			StatusText.Text = text;
		}

		private void AddButton_Click (object sender, RoutedEventArgs e)
		{
			if (SelectedItem == null) return;
			if (!AvailableTiles.Contains (SelectedItem)) return;

			string title = SelectedItem.Title;
			var family = SelectedItem.Storage.Manifest.Identity.FamilyName;
			if (_pinnedTiles.Contains (family))
			{
				MessageBox.Show (this,
					App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_PINNEDFAILED"),
					App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_PINNEDFAILED_TITLE"),
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			_pinnedTiles.Add (family);
			// 操作后状态会由 RefreshLists 更新，这里仅作为即时反馈（可选）
			SetStatus (string.Format (App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_STATUS_ADDED", "已添加: {0} (固定到顶部)"), title));
		}

		private void UnpinButton_Click (object sender, RoutedEventArgs e)
		{
			if (SelectedItem == null) return;
			string title = SelectedItem.Title;
			var family = SelectedItem.Storage.Manifest.Identity.FamilyName;
			if (_pinnedTiles.Contains (family))
			{
				_pinnedTiles.Remove (family);
				SetStatus (string.Format (App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_STATUS_UNPINNED", "已取消固定: {0}"), title));
			}
		}

		private void DeleteButton_Click (object sender, RoutedEventArgs e)
		{
			if (SelectedItem == null) return;
			var item = SelectedItem;
			string title = item.Title;
			var family = item.Storage.Manifest.Identity.FamilyName;
			var folder = item.Storage.FolderPath;

			var result = MessageBox.Show (this,
				App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_DELETEASK"),
				App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_DELETEASK_TITLE"),
				MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

			if (result != MessageBoxResult.Yes) return;

			if (_pinnedTiles.Contains (family))
			{
				_pinnedTiles.Remove (family);
				System.Windows.Forms.Application.DoEvents ();
				System.Threading.Thread.Sleep (300);
			}

			SelectedItem = null;
			var oldItem = item;

			AvailableTiles.Remove (oldItem);
			PinnedTopTiles.Remove (oldItem);
			PinnedBottomTiles.Remove (oldItem);

			try
			{
				if (Directory.Exists (folder))
				{
					FileSystem.DeleteDirectory (folder,
						UIOption.OnlyErrorDialogs,
						RecycleOption.SendToRecycleBin);
					SetStatus (string.Format (App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_STATUS_DELETED", "已删除: {0}"), title));
				}
			}
			catch (Exception)
			{
				try
				{
					string deletedRoot = Path.Combine (App.TileMgr.BaseDir, "Gadgets\\Deleted");
					if (!Directory.Exists (deletedRoot))
						Directory.CreateDirectory (deletedRoot);

					string targetPath = Path.Combine (deletedRoot, family);
					if (Directory.Exists (targetPath))
					{
						string ts = DateTime.Now.ToString ("yyyyMMdd_HHmmss");
						targetPath = Path.Combine (deletedRoot, string.Format ("{0}_{1}", family, ts));
					}

					MoveDirectory (folder, targetPath);
					SetStatus (string.Format (App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_STATUS_DELETED_TO_DELETED", "已删除: {0} (移至 Deleted)"), title));
				}
				catch (Exception moveEx)
				{
					MessageBox.Show (this,
						string.Format (App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_DELETEFAILED"), folder, moveEx.Message),
						App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_DELETEFAILED_TITLE"),
						MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}

			// 刷新列表（删除后需要完全刷新）
			RefreshLists ();
			App.ReleaseLargeResourcesAsync ();
		}

		private static void MoveDirectory (string sourceDir, string destDir)
		{
			try
			{
				Directory.Move (sourceDir, destDir);
			}
			catch (IOException)
			{
				CopyDirectory (sourceDir, destDir);
				Directory.Delete (sourceDir, true);
			}
		}

		private static void CopyDirectory (string sourceDir, string destDir)
		{
			Directory.CreateDirectory (destDir);
			foreach (string dirPath in Directory.GetDirectories (sourceDir, "*", System.IO.SearchOption.AllDirectories))
			{
				string relative = GetRelativePath (sourceDir, dirPath);
				Directory.CreateDirectory (Path.Combine (destDir, relative));
			}
			foreach (string filePath in Directory.GetFiles (sourceDir, "*", System.IO.SearchOption.AllDirectories))
			{
				string relative = GetRelativePath (sourceDir, filePath);
				File.Copy (filePath, Path.Combine (destDir, relative), true);
			}
		}

		private static string GetRelativePath (string baseDir, string fullPath)
		{
			if (!baseDir.EndsWith (Path.DirectorySeparatorChar.ToString ()))
				baseDir += Path.DirectorySeparatorChar;

			if (fullPath.StartsWith (baseDir, StringComparison.OrdinalIgnoreCase))
				return fullPath.Substring (baseDir.Length);

			Uri baseUri = new Uri (baseDir);
			Uri fullUri = new Uri (fullPath);
			Uri relativeUri = baseUri.MakeRelativeUri (fullUri);
			return Uri.UnescapeDataString (relativeUri.ToString ()).Replace ('/', '\\');
		}

		private void GridSplitter_PreviewMouseMove (object sender, MouseEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed)
				Cursor = Cursors.SizeWE;
			else
				Cursor = Cursors.Arrow;
		}

		private void Hyperlink_RequestNavigate (object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
		{
			Process.Start (System.IO.Path.Combine (App.ProgramFolder.FolderPath, "SGStore.exe"));
		}

		private void Hyperlink_Click (object sender, RoutedEventArgs e)
		{
			Process.Start (System.IO.Path.Combine (App.ProgramFolder.FolderPath, "SGStore.exe"));
		}
	}
}
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
using Sidebar;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Diagnostics;
using System.Threading;
using System.IO;

namespace TileManager
{
	public partial class MainWindow: Window
	{
		private CollectionViewSource canPinViewSource;
		private ObservableCollection<PinnedItem> pinnedList;
		private DispatcherTimer pinnedUpdateTimer;
		private object pinnedUpdateLock = new object ();
		// 每 3 秒从 config.xml 同步一次固定列表，用于捕捉 Sidebar 端（右键菜单等）修改后的变化
		private DispatcherTimer pinnedSyncTimer;

		public MainWindow ()
		{
			InitializeComponent ();
			pinnedUpdateTimer = new DispatcherTimer ();
			pinnedUpdateTimer.Interval = TimeSpan.FromSeconds (0.05);
			pinnedUpdateTimer.Tick += PinnedUpdateTimer_Tick;
			var cc = App.CurrentUserConfig;
			App.TileMgr.Applications.CollectionChanged += Applications_CollectionChanged;
			cc.PinnedTiles.CollectionChanged += PinnedTiles_CollectionChanged;
			cc.PinnedBottomTiles.CollectionChanged += PinnedBottomTiles_CollectionChanged;
			canPinViewSource = new CollectionViewSource { Source = App.TileMgr.Applications };
			canPinViewSource.Filter += (s, e) => {
				var app = (TileApplicationStorage)e.Item;
				e.Accepted = !App.CurrentUserConfig.IsPinned (app.UserModelId);
			};
			pinnedList = new ObservableCollection<PinnedItem> ();
			SelectPinTile.ItemsSource = canPinViewSource.View;
			ListPinnedTiles.ItemsSource = pinnedList;
			ListInstalledTiles.ItemsSource = App.TileMgr.Tiles;

			pinnedSyncTimer = new DispatcherTimer ();
			pinnedSyncTimer.Interval = TimeSpan.FromSeconds (3);
			pinnedSyncTimer.Tick += PinnedSyncTimer_Tick;
			pinnedSyncTimer.Start ();
		}

		protected override void OnClosed (EventArgs e)
		{
			if (pinnedSyncTimer != null)
			{
				pinnedSyncTimer.Stop ();
				pinnedSyncTimer.Tick -= PinnedSyncTimer_Tick;
				pinnedSyncTimer = null;
			}
			if (pinnedUpdateTimer != null)
			{
				pinnedUpdateTimer.Stop ();
				pinnedUpdateTimer.Tick -= PinnedUpdateTimer_Tick;
				pinnedUpdateTimer = null;
			}
			App.TileMgr.Applications.CollectionChanged -= Applications_CollectionChanged;
			App.CurrentUserConfig.PinnedTiles.CollectionChanged -= PinnedTiles_CollectionChanged;
			App.CurrentUserConfig.PinnedBottomTiles.CollectionChanged -= PinnedBottomTiles_CollectionChanged;
			base.OnClosed (e);
		}

		private void PinnedUpdateTimer_Tick (object sender, EventArgs e)
		{
			pinnedUpdateTimer?.Stop ();
			InitPinnedList ();
		}

		private void Applications_CollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
		{
			UpdatePinnedList ();
		}

		private void UpdatePinnedList ()
		{
			pinnedUpdateTimer?.Stop ();
			pinnedUpdateTimer?.Start ();
		}

		private void InitPinnedList ()
		{
			lock (pinnedUpdateLock)
			{
				if (pinnedList == null) return;
				var cc = App.CurrentUserConfig;
				pinnedList.Clear ();
				foreach (var pt in cc.PinnedTiles)
				{
					var tas = App.TileMgr.GetApplicationByUserModelId (pt);
					if (tas != null) pinnedList.Add (new PinnedItem (tas, PinnedLocation.Top));
					else pinnedList.Add (new PinnedItem (PinnedLocation.Top, pt));
				}
				foreach (var pt in cc.PinnedBottomTiles)
				{
					var tas = App.TileMgr.GetApplicationByUserModelId (pt);
					if (tas != null) pinnedList.Add (new PinnedItem (tas, PinnedLocation.Bottom));
					else pinnedList.Add (new PinnedItem (PinnedLocation.Bottom, pt));
				}
			}
		}

		private void PinnedBottomTiles_CollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
		{
			UpdatePinnedList ();
		}

		private void PinnedTiles_CollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
		{
			UpdatePinnedList ();
		}

		#region Pinned 定时同步（每 3 秒从 config.xml 读取一次）

		private void PinnedSyncTimer_Tick (object sender, EventArgs e)
		{
			SyncPinnedFromFile ();
		}

		/// <summary>
		/// 从 config.xml 读取最新固定列表，与内存中的 PinnedTiles / PinnedBottomTiles 比对，
		/// 有差异时用最小变更同步到内存（触发 UI 刷新并写回文件）。
		/// </summary>
		private void SyncPinnedFromFile ()
		{
			var xml = App.CurrentUserConfig?.Xml?.Global;
			if (xml == null) return;

			List<string> rawTop = null, rawBottom = null;
			try
			{
				rawTop = xml.Get<List<string>> ("PinnedTiles", null);
				rawBottom = xml.Get<List<string>> ("PinnedBottomTiles", null);
			}
			catch
			{
				return;
			}

			var fileTop = NormalizeAndDedupe (rawTop, null);
			var seen = new HashSet<string> (fileTop, StringComparer.OrdinalIgnoreCase);
			var fileBottom = NormalizeAndDedupe (rawBottom, seen);

			var memTop = App.CurrentUserConfig.PinnedTiles;
			var memBottom = App.CurrentUserConfig.PinnedBottomTiles;

			if (ListEquals (memTop, fileTop) && ListEquals (memBottom, fileBottom)) return;

			try
			{
				// 本地最小化变更：移除多余项、按顺序插入/移动，只触发必要事件
				SyncStringList (memTop, fileTop);
				SyncStringList (memBottom, fileBottom);
			}
			catch
			{
				// 忽略异常，下次 tick 再试
			}
		}

		private static List<string> NormalizeAndDedupe (List<string> src, HashSet<string> seen)
		{
			var result = new List<string> ();
			if (src == null) return result;
			if (seen == null) seen = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			foreach (var s in src)
			{
				if (string.IsNullOrWhiteSpace (s)) continue;
				var t = s.Trim ();
				if (!seen.Add (t)) continue;
				result.Add (t);
			}
			return result;
		}

		private static bool ListEquals (IList<string> a, IList<string> b)
		{
			if (ReferenceEquals (a, b)) return true;
			if (a == null || b == null) return false;
			if (a.Count != b.Count) return false;
			for (int i = 0; i < a.Count; i++)
			{
				if (!string.Equals (a [i], b [i], StringComparison.OrdinalIgnoreCase))
					return false;
			}
			return true;
		}

		/// <summary>
		/// 把 desired 列表同步到 target（有序、忽略大小写比较）。
		/// 采用最小变更策略：先移除不在 desired 中的项，再按位置逐个插入/移动，
		/// 避免清空重建导致的 UI 选中丢失与大量 CollectionChanged 事件。
		/// </summary>
		private static void SyncStringList (ObservableCollection<string> target, List<string> desired)
		{
			if (target == null) return;
			if (desired == null) desired = new List<string> ();

			// 1) 移除多余的
			var desiredSet = new HashSet<string> (desired, StringComparer.OrdinalIgnoreCase);
			for (int i = target.Count - 1; i >= 0; i--)
			{
				if (!desiredSet.Contains (target [i]))
					target.RemoveAt (i);
			}

			// 2) 逐位匹配：缺失的插入，错位的移动
			for (int i = 0; i < desired.Count; i++)
			{
				var item = desired [i];
				int existing = -1;
				for (int j = i; j < target.Count; j++)
				{
					if (string.Equals (target [j], item, StringComparison.OrdinalIgnoreCase))
					{
						existing = j;
						break;
					}
				}
				if (existing == -1)
				{
					target.Insert (i, item);
				}
				else if (existing != i)
				{
					target.Move (existing, i);
				}
			}
		}

		#endregion

		#region Pin / Unpin（与 Sidebar 端语义保持一致）

		private void ButtonAddToTop_Click (object sender, RoutedEventArgs e)
		{
			var tas = SelectPinTile.SelectedItem as TileApplicationStorage;
			if (tas == null) return;
			var umid = tas.UserModelId;
			if (string.IsNullOrWhiteSpace (umid)) return;
			// 与 Sidebar 端 cc.PinTop 一致：先跨表移除，再插入到顶部（首位）
			App.CurrentUserConfig.PinTop (umid);
			SidebarMail.Send ("NotifyPinTile", umid);
		}

		private void ButtonAddToBottom_Click (object sender, RoutedEventArgs e)
		{
			var tas = SelectPinTile.SelectedItem as TileApplicationStorage;
			if (tas == null) return;
			var umid = tas.UserModelId;
			if (string.IsNullOrWhiteSpace (umid)) return;
			App.CurrentUserConfig.PinBottom (umid);
			SidebarMail.Send ("NotifyPinTileBottom", umid);
		}

		private void ButtonUnpin_Click (object sender, RoutedEventArgs e)
		{
			var selected = ListPinnedTiles.SelectedItems;
			if (selected == null || selected.Count == 0) return;
			var cc = App.CurrentUserConfig;
			var items = new List<PinnedItem> ();
			foreach (var obj in selected)
				if (obj is PinnedItem) items.Add ((PinnedItem)obj);
			foreach (var item in items)
			{
				// Unpin 内部会从 top / bottom 两个表都移除
				cc.Unpin (item.UserModelId);
				SidebarMail.Send ("NotifyUnpinTile", item.UserModelId);
			}
		}

		private void ButtonPinToTop_Click (object sender, RoutedEventArgs e)
		{
			var selected = ListPinnedTiles.SelectedItems;
			if (selected == null || selected.Count == 0) return;
			var cc = App.CurrentUserConfig;
			var items = new List<PinnedItem> ();
			foreach (var obj in selected)
				if (obj is PinnedItem) items.Add ((PinnedItem)obj);
			foreach (var item in items)
			{
				if (item.PinnedLocation == PinnedLocation.Top) continue;
				// 与 Sidebar 端语义一致：从 bottom 移出并插入到顶部首位
				cc.PinTop (item.UserModelId);
				SidebarMail.Send ("NotifyPinTile", item.UserModelId);
			}
		}

		private void ButtonPinToBottom_Click (object sender, RoutedEventArgs e)
		{
			var selected = ListPinnedTiles.SelectedItems;
			if (selected == null || selected.Count == 0) return;
			var cc = App.CurrentUserConfig;
			var items = new List<PinnedItem> ();
			foreach (var obj in selected)
				if (obj is PinnedItem) items.Add ((PinnedItem)obj);
			foreach (var item in items)
			{
				if (item.PinnedLocation == PinnedLocation.Bottom) continue;
				cc.PinBottom (item.UserModelId);
				SidebarMail.Send ("NotifyPinTileBottom", item.UserModelId);
			}
		}

		private void ButtonPinnedMoveUp_Click (object sender, RoutedEventArgs e)
		{
			var selected = ListPinnedTiles.SelectedItems;
			if (selected == null || selected.Count == 0) return;
			var ids = new HashSet<string> ();
			foreach (var obj in selected)
				if (obj is PinnedItem) ids.Add (((PinnedItem)obj).UserModelId);
			var cc = App.CurrentUserConfig;
			var top = cc.PinnedTiles;
			for (int i = 1; i < top.Count; i++)
			{
				if (!ids.Contains (top [i])) continue;
				if (ids.Contains (top [i - 1])) continue;
				var tmp = top [i - 1];
				top [i - 1] = top [i];
				top [i] = tmp;
			}
			var bottom = cc.PinnedBottomTiles;
			for (int i = 1; i < bottom.Count; i++)
			{
				if (!ids.Contains (bottom [i])) continue;
				if (ids.Contains (bottom [i - 1])) continue;
				var tmp = bottom [i - 1];
				bottom [i - 1] = bottom [i];
				bottom [i] = tmp;
			}
			// 顺序变化写回文件后，Sidebar 会通过其自身的定时同步或下次启动读取；
			// 这里不额外发 Pin 消息，避免 Sidebar 端把项重新插到首位导致顺序被打破。
		}

		private void ButtonPinnedMoveDown_Click (object sender, RoutedEventArgs e)
		{
			var selected = ListPinnedTiles.SelectedItems;
			if (selected == null || selected.Count == 0) return;
			var ids = new HashSet<string> ();
			foreach (var obj in selected)
				if (obj is PinnedItem) ids.Add (((PinnedItem)obj).UserModelId);
			var cc = App.CurrentUserConfig;
			var top = cc.PinnedTiles;
			for (int i = top.Count - 2; i >= 0; i--)
			{
				if (!ids.Contains (top [i])) continue;
				if (ids.Contains (top [i + 1])) continue;
				var tmp = top [i + 1];
				top [i + 1] = top [i];
				top [i] = tmp;
			}
			var bottom = cc.PinnedBottomTiles;
			for (int i = bottom.Count - 2; i >= 0; i--)
			{
				if (!ids.Contains (bottom [i])) continue;
				if (ids.Contains (bottom [i + 1])) continue;
				var tmp = bottom [i + 1];
				bottom [i + 1] = bottom [i];
				bottom [i] = tmp;
			}
		}

		#endregion

		private void ButtonGetTiles_Click (object sender, RoutedEventArgs e)
		{
			try
			{
				Process.Start (System.IO.Path.Combine (App.ProgramFolder.FolderPath, "SGStore.exe"));
			}
			catch (Exception ex)
			{
				MessageBox.Show (ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void ButtonInstallTile_Click (object sender, RoutedEventArgs e)
		{
			var dlg = new Microsoft.Win32.OpenFileDialog ();
			dlg.Filter = App.S ("TILEMGR_FILTER_PKG");
			if (dlg.ShowDialog (this) != true) return;
			var filePath = dlg.FileName;

			TilePackageBase package;
			try
			{
				package = TilePackageReadManager.GetPackage (filePath);
			}
			catch (Exception ex)
			{
				MessageBox.Show (this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			string familyName = null;
			Sidebar.Version pkgVersion = default (Sidebar.Version);
			if (package is TilePackage)
			{
				var id = ((TilePackage)package).Manifest?.Identity;
				if (id != null) { familyName = id.FamilyName; pkgVersion = id.Version; }
			}
			else if (package is TilePackageBundle)
			{
				var id = ((TilePackageBundle)package).Manifest?.Identity;
				if (id != null) { familyName = id.FamilyName; pkgVersion = id.Version; }
			}
			if (string.IsNullOrEmpty (familyName))
			{
				MessageBox.Show (this, App.S ("TILEMGR_MSG_INVALID_PACKAGE"), Title, MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			var installed = App.TileMgr.GetByFamilyName (familyName);
			string operation;
			if (installed == null) operation = "install";
			else if (pkgVersion > installed.Manifest.Identity.Version) operation = "update";
			else operation = "reinstall";

			var allAumids = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			if (installed != null)
			{
				foreach (var a in installed.Items)
					allAumids.Add (a.UserModelId);
			}
			else if (package is TilePackage)
			{
				foreach (var app in ((TilePackage)package).Manifest.Applications)
					allAumids.Add (familyName + "!" + app.Id);
			}
			else if (package is TilePackageBundle)
			{
				var first = ((TilePackageBundle)package).Packages.FirstOrDefault ();
				if (first?.Manifest?.Applications != null)
					foreach (var app in first.Manifest.Applications)
						allAumids.Add (familyName + "!" + app.Id);
			}

			var toUnpinTop = new List<string> ();
			var toUnpinBottom = new List<string> ();
			foreach (var aumid in App.CurrentUserConfig.PinnedTiles)
				if (allAumids.Contains (aumid)) toUnpinTop.Add (aumid);
			foreach (var aumid in App.CurrentUserConfig.PinnedBottomTiles)
				if (allAumids.Contains (aumid)) toUnpinBottom.Add (aumid);
			bool isPinned = toUnpinTop.Count > 0 || toUnpinBottom.Count > 0;

			var prompt = operation == "install" ? App.S ("TILEMGR_CONFIRM_INSTALL")
					   : operation == "update" ? App.S ("TILEMGR_CONFIRM_UPDATE")
					   : App.S ("TILEMGR_CONFIRM_REINSTALL");
			if (isPinned) prompt += "\n\n" + App.S ("TILEMGR_CONFIRM_UNPIN_FIRST");
			if (MessageBox.Show (this, prompt, Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
				return;

			if (isPinned)
			{
				foreach (var aumid in toUnpinTop)
				{
					App.CurrentUserConfig.PinnedTiles.Remove (aumid);
					SidebarMail.Send ("NotifyUnpinTile", aumid);
				}
				foreach (var aumid in toUnpinBottom)
				{
					App.CurrentUserConfig.PinnedBottomTiles.Remove (aumid);
					SidebarMail.Send ("NotifyUnpinTile", aumid);
				}
				Thread.Sleep (500);
			}

			try
			{
				if (operation == "install")
					App.TileMgr.InstallPackage (filePath);
				else
					App.TileMgr.UpdatePackage (filePath);
				MessageBox.Show (this, App.S ("TILEMGR_MSG_OPERATION_SUCCESS"), Title, MessageBoxButton.OK, MessageBoxImage.Information);
			}
			catch (Exception ex)
			{
				MessageBox.Show (this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
			}
			finally
			{
				Utilities.ReleaseLargeResourcesAsync ();
			}
		}

		private void ButtonRemoveTile_Click (object sender, RoutedEventArgs e)
		{
			var selected = ListInstalledTiles.SelectedItem as TileStorage;
			if (selected == null)
			{
				MessageBox.Show (this, App.S ("TILEMGR_MSG_SELECT_REMOVE"), Title, MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}
			var familyName = selected.FamilyName;

			var allAumids = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			foreach (var a in selected.Items)
				allAumids.Add (a.UserModelId);

			var toUnpinTop = new List<string> ();
			var toUnpinBottom = new List<string> ();
			foreach (var aumid in App.CurrentUserConfig.PinnedTiles)
				if (allAumids.Contains (aumid)) toUnpinTop.Add (aumid);
			foreach (var aumid in App.CurrentUserConfig.PinnedBottomTiles)
				if (allAumids.Contains (aumid)) toUnpinBottom.Add (aumid);
			bool isPinned = toUnpinTop.Count > 0 || toUnpinBottom.Count > 0;

			var prompt = string.Format (App.S ("TILEMGR_CONFIRM_REMOVE"), selected.DisplayName ?? familyName);
			if (isPinned) prompt += "\n\n" + App.S ("TILEMGR_CONFIRM_UNPIN_FIRST");
			if (MessageBox.Show (this, prompt, Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
				return;

			if (isPinned)
			{
				foreach (var aumid in toUnpinTop)
				{
					App.CurrentUserConfig.PinnedTiles.Remove (aumid);
					SidebarMail.Send ("NotifyUnpinTile", aumid);
				}
				foreach (var aumid in toUnpinBottom)
				{
					App.CurrentUserConfig.PinnedBottomTiles.Remove (aumid);
					SidebarMail.Send ("NotifyUnpinTile", aumid);
				}
				Thread.Sleep (500);
			}

			try
			{
				var dir = selected.FolderPath;
				if (Directory.Exists (dir))
				{
					try
					{
						Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory (dir,
							Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
							Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
					}
					catch
					{
						foreach (var f in Directory.GetFiles (dir, "*", SearchOption.AllDirectories))
							File.SetAttributes (f, FileAttributes.Normal);
						Directory.Delete (dir, true);
					}
				}
				App.TileMgr.ForceRefresh ();
				MessageBox.Show (this, App.S ("TILEMGR_MSG_REMOVE_SUCCESS"), Title, MessageBoxButton.OK, MessageBoxImage.Information);
			}
			catch (Exception ex)
			{
				MessageBox.Show (this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
			}
			finally
			{
				Utilities.ReleaseLargeResourcesAsync ();
			}
		}

		private void ButtonRemoveInvalid_Click (object sender, RoutedEventArgs e)
		{
			var cc = App.CurrentUserConfig;
			var toRemoveTop = new List<string> ();
			var toRemoveBottom = new List<string> ();
			foreach (var item in pinnedList)
			{
				if (item.Exists) continue;
				if (item.PinnedLocation == PinnedLocation.Top)
					toRemoveTop.Add (item.UserModelId);
				else
					toRemoveBottom.Add (item.UserModelId);
			}
			if (toRemoveTop.Count == 0 && toRemoveBottom.Count == 0)
			{
				MessageBox.Show (this, App.S ("TILEMGR_MSG_NO_INVALID"), Title, MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}
			if (MessageBox.Show (this, App.S ("TILEMGR_CONFIRM_REMOVE_INVALID"), Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
				return;
			foreach (var aumid in toRemoveTop)
			{
				cc.PinnedTiles.Remove (aumid);
				SidebarMail.Send ("NotifyUnpinTile", aumid);
			}
			foreach (var aumid in toRemoveBottom)
			{
				cc.PinnedBottomTiles.Remove (aumid);
				SidebarMail.Send ("NotifyUnpinTile", aumid);
			}
		}
	}
}
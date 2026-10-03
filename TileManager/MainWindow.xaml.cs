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

		private void ButtonAddToTop_Click (object sender, RoutedEventArgs e)
		{
			var tas = SelectPinTile.SelectedItem as TileApplicationStorage;
			if (tas == null) return;
			App.CurrentUserConfig.PinnedTiles.Add (tas.UserModelId);
		}

		private void ButtonAddToBottom_Click (object sender, RoutedEventArgs e)
		{
			var tas = SelectPinTile.SelectedItem as TileApplicationStorage;
			if (tas == null) return;
			App.CurrentUserConfig.PinnedBottomTiles.Add (tas.UserModelId);
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
				if (item.PinnedLocation == PinnedLocation.Top)
					cc.PinnedTiles.Remove (item.UserModelId);
				else
					cc.PinnedBottomTiles.Remove (item.UserModelId);
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
				cc.PinnedBottomTiles.Remove (item.UserModelId);
				cc.PinnedTiles.Add (item.UserModelId);
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
				cc.PinnedTiles.Remove (item.UserModelId);
				cc.PinnedBottomTiles.Add (item.UserModelId);
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
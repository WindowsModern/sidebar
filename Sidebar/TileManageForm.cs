using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic.FileIO;
namespace Sidebar
{
	public partial class TileManageForm: Form
	{
		public TileManageForm ()
		{
			InitializeComponent ();
			var locale = App.ProgramFolder.StringResources;
			Text = locale.SuitableResource ("TILEMGR_WINTITLE", "Tile Manager") ?? "Tile Manager";
			addToButton.Text = locale.SuitableResource ("TILEMGR_ADDTO", "Add to Sidebar") ?? "Add to Sidebar";
			removeTileButton.Text = locale.SuitableResource ("TILEMGR_REMOVE", "Unpin Tile") ?? "Unpin Tile";
			deleteButton.Text = locale.SuitableResource ("TILEMGR_DELETE", "Delete Gadget") ?? "Delete Gadget";
			tileTitle.Text = locale.SuitableResource ("TILEMGR_SELECTAITEM");
			this.Font = FontHelper.GetFirstAvailableFont (FontHelper.preferredFonts);
			tileTitle.Font = FontHelper.GetFirstAvailableFont (FontHelper.preferredFonts, tileTitle.Font.Size);
			items.ListChanged += Items_ListChanged;
			tileListView.Resize += TileListView_Resize;
		}
		private BindingList<TileMgrItem> items = new BindingList<TileMgrItem> ();
		private TileItemControl currentSelectedTile = null;
		private readonly TileManager tileManager = App.TileMgr;
		private void TileManager_Load (object sender, EventArgs e)
		{
			var observable = tileManager.ValidTilesObservable;
			if (observable != null)
			{
				observable.CollectionChanged += OnTileCollectionChanged;
				RebuildItemsFromObservable ();
			}
			SetButtonsEnabled (false);
		}
		private void SetButtonsEnabled (bool enabled)
		{
			addToButton.Enabled = enabled;
			deleteButton.Enabled = enabled;
		}
		private void OnTileCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
		{
			if (this.InvokeRequired)
			{
				this.Invoke (new Action (() => OnTileCollectionChanged (sender, e)));
				return;
			}
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					foreach (TileStorage storage in e.NewItems)
						items.Add (new TileMgrItem (storage));
					break;
				case NotifyCollectionChangedAction.Remove:
					foreach (TileStorage storage in e.OldItems)
					{
						var itemToRemove = items.FirstOrDefault (i => i.Storage == storage);
						if (itemToRemove != null)
						{
							try
							{
								if (itemToRemove == currentSelectedTile as object)
									tileLogo.Image = null;
							}
							catch { }
							items.Remove (itemToRemove);
						}
					}
					break;
				case NotifyCollectionChangedAction.Reset:
					// 整个列表重建
					RebuildItemsFromObservable ();
					break;
			}
		}
		private void RebuildItemsFromObservable ()
		{
			items.RaiseListChangedEvents = false;
			items.Clear ();
			foreach (var storage in tileManager.ValidTilesObservable)
				items.Add (new TileMgrItem (storage));
			items.RaiseListChangedEvents = true;
			items.ResetBindings ();
		}
		private void Items_ListChanged (object sender, ListChangedEventArgs e)
		{
			if (this.InvokeRequired)
			{
				this.Invoke (new Action (() => Items_ListChanged (sender, e)));
				return;
			}
			switch (e.ListChangedType)
			{
				case ListChangedType.ItemAdded:
					var newItem = items [e.NewIndex];
					var newControl = CreateTileControl (newItem);
					tileListView.Controls.Add (newControl);
					if (e.NewIndex < tileListView.Controls.Count - 1)
					{
						tileListView.Controls.SetChildIndex (newControl, e.NewIndex);
					}
					break;

				case ListChangedType.ItemDeleted:
					if (e.NewIndex >= 0 && e.NewIndex < tileListView.Controls.Count)
					{
						var removedControl = tileListView.Controls [e.NewIndex] as TileItemControl;
						tileListView.Controls.RemoveAt (e.NewIndex);
						removedControl?.Dispose ();
						if (currentSelectedTile == removedControl)
						{
							currentSelectedTile = null;
							UpdateDetails (null);
						}
					}
					break;

				case ListChangedType.Reset:
					PopulateControlsAsync ();
					break;

				case ListChangedType.ItemChanged:
					if (e.NewIndex >= 0 && e.NewIndex < tileListView.Controls.Count)
					{
						var control = tileListView.Controls [e.NewIndex] as TileItemControl;
						control.Value = items [e.NewIndex];
					}
					break;
			}
		}
		private bool isAsyncPopulating = false;
		/// <summary>
		/// 异步分批添加控件，避免 UI 阻塞
		/// </summary>
		private void PopulateControlsAsync ()
		{
			if (isAsyncPopulating) return;
			isAsyncPopulating = true;
			tileListView.Controls.Clear ();
			currentSelectedTile = null;
			var itemsToAdd = new List<TileMgrItem> (items);
			//for (int i = 0; i < 10; i ++) foreach (var it in items) itemsToAdd.Add (it);
			int total = itemsToAdd.Count;
			Action<int> batchAdd = null;
			batchAdd = (index) =>
			{
				int batchSize = 5;  
				int end = Math.Min (index + batchSize, total);
				for (int i = index; i < end; i++)
				{
					var ctrl = CreateTileControl (itemsToAdd [i]);
					tileListView.Controls.Add (ctrl);
				}

				if (end < total)
				{
					this.BeginInvoke (new Action (() => batchAdd (end)));
				}
				else
				{
					isAsyncPopulating = false;
				}
			};
			if (total > 0)
				this.BeginInvoke (new Action (() => batchAdd (0)));
			else
				isAsyncPopulating = false;
		}
		private void AdjustTileWidths ()
		{
			int newWidth = tileListView.ClientSize.Width - 7; // 预留滚动条空间
			if (newWidth < 100) newWidth = 100;                // 不小于最小宽度
			foreach (Control ctrl in tileListView.Controls)
			{
				if (ctrl is TileItemControl)
				{
					ctrl.Width = newWidth;
				}
			}
		}
		private void TileListView_Resize (object sender, EventArgs e)
		{
			AdjustTileWidths ();
		}
		private TileItemControl CreateTileControl (TileMgrItem item)
		{
			var tileControl = new TileItemControl ();
			tileControl.Value = item;           // 自动填充标题、发布者、Logo
			tileControl.Width = tileListView.ClientSize.Width - 7;  // 预留滚动条空间
			tileControl.Height = 50;
			//tileControl.Margin = new Padding (3);
			tileControl.Click += TileControl_Click;   // 点击事件
			//tileControl.Dock = DockStyle.Top;
			return tileControl;
		}
		private void TileControl_Click (object sender, EventArgs e)
		{
			var clickedTile = sender as TileItemControl;
			if (clickedTile == null) return;
			SetSelectedTile (clickedTile);
		}
		private void SetSelectedTile (TileItemControl newSelected)
		{
			if (currentSelectedTile == newSelected) return;
			if (currentSelectedTile != null)
			{
				currentSelectedTile.Selected = false;
			}
			currentSelectedTile = newSelected;
			if (newSelected != null)
			{
				newSelected.Selected = true;
				UpdateDetails (newSelected.Value);
				SetButtonsEnabled (true);  
			}
			else
			{
				UpdateDetails (null);
				SetButtonsEnabled (false); 
			}
			addToButton.Enabled = !IsTileAdded (newSelected?.Value.Storage.Manifest.Identity.FamilyName);
			removeTileButton.Enabled = IsTileAdded (newSelected?.Value.Storage.Manifest.Identity.FamilyName);
		}
		private void UpdateDetails (TileMgrItem item)
		{
			if (item == null)
			{
				tileTitle.Text = tilePublisher.Text = tileVersion.Text = tileDiscription.Text = "";
				tileLogo.Image = null;
				return;
			}
			tileTitle.Text = item.Title;
			tilePublisher.Text = item.Publisher;
			tileVersion.Text = item.Version.ToString ();
			tileDiscription.Text = item.Description;
			if (!string.IsNullOrEmpty (item.Logo) && File.Exists (item.Logo))
			{
				try
				{
					tileLogo.Image = Image.FromFile (item.Logo);
				}
				catch { tileLogo.Image = null; }
			}
			else
				tileLogo.Image = null;
		}
		private void addToButton_Click (object sender, EventArgs e)
		{
			if (currentSelectedTile == null) return;
			var item = currentSelectedTile.Value;
			if (item == null) return;
			string familyName = item.Storage.Manifest.Identity.FamilyName;
			var pinnedTiles = App.CurrentUserConfig.PinnedTiles;
			if (pinnedTiles.Contains (familyName))
			{
				MessageBox.Show (
					this,
					App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_PINNEDFAILED"),
					App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_PINNEDFAILED_TITLE"),
					MessageBoxButtons.OK,
					MessageBoxIcon.Information);
				return;
			}
			pinnedTiles.Add (familyName);
			addToButton.Enabled = !IsTileAdded (item.Storage.Manifest.Identity.FamilyName);
			removeTileButton.Enabled = IsTileAdded (item.Storage.Manifest.Identity.FamilyName);
		}
		private void deleteButton_Click (object sender, EventArgs e)
		{
			if (currentSelectedTile == null) return;
			var item = currentSelectedTile.Value;
			if (item == null) return;

			string familyName = item.Storage.Manifest.Identity.FamilyName;
			string tileFolder = item.Storage.FolderPath;       // 磁贴程序的文件夹

			// 确认对话框
			var result = MessageBox.Show (
				this,
				App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_DELETEASK"),
				App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_DELETEASK_TITLE"),
				MessageBoxButtons.YesNo,
				MessageBoxIcon.Warning,
				MessageBoxDefaultButton.Button2);

			if (result != DialogResult.Yes) return;

			// 如果磁贴已添加到侧边栏，先移除（触发卸载动画和清理）
			var pinnedTiles = App.CurrentUserConfig.PinnedTiles;
			if (pinnedTiles.Contains (familyName))
			{
				pinnedTiles.Remove (familyName);
				// 给一点时间让 MainWindow 完成清理动画（如果需要）
				// 实际上卸载是异步的，但文件可能仍被占用，所以再稍作等待
				Application.DoEvents (); // 简单出让UI线程
				System.Threading.Thread.Sleep (500);
			}
			if (tileLogo.Image != null)
			{
				Image oldImage = tileLogo.Image;
				tileLogo.Image = null;
				oldImage.Dispose ();
			}
			var itemToRemove = items.FirstOrDefault (i => i.Storage.Manifest.Identity.FamilyName == familyName);
			if (itemToRemove != null)
			{
				items.Remove (itemToRemove);
			}

			Utilities.ReleaseLargeResources ();
			// 尝试移动文件夹到回收站
			try
			{
				if (Directory.Exists (tileFolder))
				{
					FileSystem.DeleteDirectory (tileFolder,
						UIOption.OnlyErrorDialogs,
						RecycleOption.SendToRecycleBin);
				}
			}
			catch (Exception)
			{
				// 回收站删除失败，改为移动到 Deleted 目录
				string deletedRoot = Path.Combine (App.TileMgr.BaseDir, "Gadgets\\Deleted");
				if (!Directory.Exists (deletedRoot))
					Directory.CreateDirectory (deletedRoot);

				string targetPath = Path.Combine (deletedRoot, familyName);
				// 如果目标已存在，添加时间戳后缀避免覆盖
				if (Directory.Exists (targetPath))
				{
					string timestamp = DateTime.Now.ToString ("yyyyMMdd_HHmmss");
					targetPath = Path.Combine (deletedRoot, $"{familyName}_{timestamp}");
				}

				try
				{
					MoveDirectory (tileFolder, targetPath);
					MessageBox.Show (this,
						string.Format (App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_MOVEDTODELETED"), targetPath),
						App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_DELETEASK_TITLE"),
						MessageBoxButtons.OK,
						MessageBoxIcon.Information);
				}
				catch (Exception moveEx)
				{
					MessageBox.Show (this,
						string.Format (App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_DELETEFAILED"), tileFolder, moveEx.Message),
						App.ProgramFolder.StringResources.SuitableResource ("TILEMGR_DELETEFAILED_TITLE"),
						MessageBoxButtons.OK,
						MessageBoxIcon.Error);
				}
			}

			SetSelectedTile (null);
		}
		/// <summary>
		/// 移动目录（支持跨卷，若跨卷则复制后删除）
		/// </summary>
		private static void MoveDirectory (string sourceDir, string destDir)
		{
			try
			{
				Directory.Move (sourceDir, destDir);
			}
			catch (IOException)
			{
				// 跨卷移动失败，改为复制后删除
				CopyDirectory (sourceDir, destDir);
				Directory.Delete (sourceDir, true);
			}
		}

		/// <summary>
		/// 递归复制目录内容
		/// </summary>
		private static void CopyDirectory (string sourceDir, string destDir)
		{
			// 创建目标根目录
			Directory.CreateDirectory (destDir);

			// 复制所有子目录
			foreach (string dirPath in Directory.GetDirectories (sourceDir, "*", System.IO.SearchOption.AllDirectories))
			{
				string relative = GetRelativePath (sourceDir, dirPath);
				Directory.CreateDirectory (Path.Combine (destDir, relative));
			}

			// 复制所有文件
			foreach (string filePath in Directory.GetFiles (sourceDir, "*", System.IO.SearchOption.AllDirectories))
			{
				string relative = GetRelativePath (sourceDir, filePath);
				string destFilePath = Path.Combine (destDir, relative);
				File.Copy (filePath, destFilePath, true);
			}
		}
		/// <summary>
		/// 获取相对路径（兼容 .NET 4.0）
		/// </summary>
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
		private bool IsTileAdded (string familyName)
		{
			return App.CurrentUserConfig.PinnedTiles.Contains (familyName);
		}
		private void TileManageForm_FormClosed (object sender, FormClosedEventArgs e)
		{
			var observable = tileManager.ValidTilesObservable;
			if (observable != null)
				observable.CollectionChanged -= OnTileCollectionChanged;
			App.ReleaseLargeResourcesAsync ();
		}

		private void removeTileButton_Click (object sender, EventArgs e)
		{
			if (currentSelectedTile == null) return;
			var item = currentSelectedTile.Value;
			if (item == null) return;
			string familyName = item.Storage.Manifest.Identity.FamilyName;
			var pinnedTiles = App.CurrentUserConfig.PinnedTiles;
			if (pinnedTiles.Contains (familyName))
			{
				pinnedTiles.Remove (familyName);
				// 给一点时间让 MainWindow 完成清理动画（如果需要）
				// 实际上卸载是异步的，但文件可能仍被占用，所以再稍作等待
				Application.DoEvents (); // 简单出让UI线程
				System.Threading.Thread.Sleep (500);
			}
			addToButton.Enabled = !IsTileAdded (item.Storage.Manifest.Identity.FamilyName);
			removeTileButton.Enabled = IsTileAdded (item.Storage.Manifest.Identity.FamilyName);
		}
	}
}

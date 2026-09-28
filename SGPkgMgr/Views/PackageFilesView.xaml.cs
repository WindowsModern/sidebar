using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SGPkgMgr.Localization;

namespace SGPkgMgr.Views
{
	/// <summary>
	/// 编辑一个 "路径 → 字节数组" 的文件集合。
	/// 直接持有传入字典的引用，任何增删改都作用于该字典。
	/// </summary>
	public partial class PackageFilesView: UserControl
	{
		private Dictionary<string, byte []> _files;

		public PackageFilesView ()
		{
			InitializeComponent ();
		}

		/// <summary>绑定要编辑的文件字典（引用传递，外部修改也生效）。</summary>
		public void SetFiles (Dictionary<string, byte []> files)
		{
			_files = files ?? new Dictionary<string, byte []> (StringComparer.OrdinalIgnoreCase);
			RebuildTree ();
		}

		public Dictionary<string, byte []> GetFiles () => _files;

		// ==================== 树构建 ====================

		private void RebuildTree ()
		{
			treeFiles.Items.Clear ();
			if (_files == null) return;

			var root = new TreeViewItem { Header = "/", Tag = "", IsExpanded = true };
			var dirMap = new Dictionary<string, TreeViewItem> (StringComparer.OrdinalIgnoreCase);
			dirMap [""] = root;

			foreach (var kv in _files.OrderBy (k => k.Key, StringComparer.OrdinalIgnoreCase))
			{
				var parts = kv.Key.Replace ('\\', '/').Split (new [] { '/' }, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length == 0) continue;

				string cur = "";
				TreeViewItem parent = root;
				for (int i = 0; i < parts.Length - 1; i++)
				{
					cur = cur.Length == 0 ? parts [i] : cur + "/" + parts [i];
					TreeViewItem dir;
					if (!dirMap.TryGetValue (cur, out dir))
					{
						dir = new TreeViewItem {
							Header = parts [i] + "/",
							Tag = cur, // 目录用前缀表示
							IsExpanded = true
						};
						dirMap [cur] = dir;
						parent.Items.Add (dir);
					}
					parent = dir;
				}
				var leaf = new TreeViewItem {
					Header = parts [parts.Length - 1],
					Tag = kv.Key // 完整 key
				};
				parent.Items.Add (leaf);
			}
			treeFiles.Items.Add (root);
		}

		// ==================== 选择状态 ====================

		private TreeViewItem SelectedNode => treeFiles.SelectedItem as TreeViewItem;

		private string SelectedFilePath
		{
			get
			{
				var n = SelectedNode;
				if (n == null) return null;
				var tag = n.Tag as string;
				if (string.IsNullOrEmpty (tag)) return null;
				return _files != null && _files.ContainsKey (tag) ? tag : null;
			}
		}

		private string SelectedDirPrefix
		{
			get
			{
				var n = SelectedNode;
				if (n == null) return null;
				var tag = n.Tag as string;
				if (string.IsNullOrEmpty (tag)) return ""; // 根
				return _files != null && !_files.ContainsKey (tag) ? tag : null;
			}
		}

		private void OnSelectedItemChanged (object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			var n = treeFiles.SelectedItem as TreeViewItem;
			if (n == null) { txtInfo.Text = ""; return; }
			var tag = n.Tag as string;
			if (string.IsNullOrEmpty (tag))
			{
				txtInfo.Text = "/ (" + (_files == null ? 0 : _files.Count) + " files)";
				return;
			}
			byte [] data;
			if (_files != null && _files.TryGetValue (tag, out data))
				txtInfo.Text = tag + " · " + data.Length + " bytes";
			else
				txtInfo.Text = tag + "/ (directory)";
		}

		// ==================== 添加 ====================

		private void OnAdd (object sender, RoutedEventArgs e)
		{
			if (_files == null) return;
			var dlg = new OpenFileDialog { Multiselect = true };
			if (dlg.ShowDialog () != true) return;

			string basePrefix = "";
			var dir = SelectedDirPrefix;
			if (!string.IsNullOrEmpty (dir)) basePrefix = dir.TrimEnd ('/') + "/";

			int added = 0, skipped = 0;
			foreach (var path in dlg.FileNames)
			{
				string name = Path.GetFileName (path);
				if (string.IsNullOrEmpty (name)) { skipped++; continue; }
				string key = basePrefix + name;

				byte [] content;
				try { content = File.ReadAllBytes (path); }
				catch (Exception ex)
				{
					MessageBox.Show (ex.Message, "SGPkgMgr", MessageBoxButton.OK, MessageBoxImage.Error);
					skipped++;
					continue;
				}
				if (_files.ContainsKey (key))
				{
					var r = MessageBox.Show (
						key + " already exists. Overwrite?",
						"Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
					if (r != MessageBoxResult.Yes) { skipped++; continue; }
				}
				_files [key] = content;
				added++;
			}
			RebuildTree ();
			txtInfo.Text = "Added: " + added + ", skipped: " + skipped;
		}

		// ==================== 替换 ====================

		private void OnReplace (object sender, RoutedEventArgs e)
		{
			if (_files == null) return;
			var file = SelectedFilePath;
			if (file == null)
			{
				MessageBox.Show ("Select a file to replace.", "SGPkgMgr",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			var dlg = new OpenFileDialog ();
			if (dlg.ShowDialog () != true) return;

			try { _files [file] = File.ReadAllBytes (dlg.FileName); }
			catch (Exception ex)
			{
				MessageBox.Show (ex.Message, "SGPkgMgr", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			RebuildTree ();
			SelectByTag (file);
			txtInfo.Text = file + " replaced · " + _files [file].Length + " bytes";
		}

		// ==================== 删除 ====================

		private void OnDelete (object sender, RoutedEventArgs e)
		{
			if (_files == null) return;

			var file = SelectedFilePath;
			if (file != null)
			{
				if (MessageBox.Show ("Delete '" + file + "'?",
					"Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
					return;
				_files.Remove (file);
				RebuildTree ();
				return;
			}

			var dir = SelectedDirPrefix;
			if (dir == null) return;
			var prefix = string.IsNullOrEmpty (dir) ? "" : dir.TrimEnd ('/') + "/";
			var toRemove = _files.Keys
				.Where (k => prefix.Length == 0 || k.StartsWith (prefix, StringComparison.OrdinalIgnoreCase))
				.ToList ();
			if (toRemove.Count == 0) return;
			if (MessageBox.Show ("Delete " + toRemove.Count + " file(s)?",
				"Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
				return;
			foreach (var k in toRemove) _files.Remove (k);
			RebuildTree ();
		}

		// ==================== 导出 ====================

		private void OnExtract (object sender, RoutedEventArgs e)
		{
			if (_files == null) return;
			var file = SelectedFilePath;
			if (file != null)
			{
				var dlg = new SaveFileDialog { FileName = Path.GetFileName (file) };
				if (dlg.ShowDialog () != true) return;
				try { File.WriteAllBytes (dlg.FileName, _files [file]); }
				catch (Exception ex)
				{
					MessageBox.Show (ex.Message, "SGPkgMgr", MessageBoxButton.OK, MessageBoxImage.Error);
				}
				return;
			}

			var dir = SelectedDirPrefix;
			if (dir == null) return;
			var fbd = new System.Windows.Forms.FolderBrowserDialog ();
			if (fbd.ShowDialog () != System.Windows.Forms.DialogResult.OK) return;

			var prefix = string.IsNullOrEmpty (dir) ? "" : dir.TrimEnd ('/') + "/";
			foreach (var kv in _files)
			{
				if (prefix.Length > 0 && !kv.Key.StartsWith (prefix, StringComparison.OrdinalIgnoreCase)) continue;
				var rel = kv.Key.Substring (prefix.Length).Replace ('/', Path.DirectorySeparatorChar);
				var outPath = Path.Combine (fbd.SelectedPath, rel);
				var outDir = Path.GetDirectoryName (outPath);
				if (!Directory.Exists (outDir)) Directory.CreateDirectory (outDir);
				File.WriteAllBytes (outPath, kv.Value);
			}
		}

		private void OnRefresh (object sender, RoutedEventArgs e) => RebuildTree ();

		// ==================== 拖放 ====================

		private void OnDragOver (object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent (DataFormats.FileDrop))
				e.Effects = DragDropEffects.Copy;
			else
				e.Effects = DragDropEffects.None;
			e.Handled = true;
		}

		private void OnDrop (object sender, DragEventArgs e)
		{
			if (_files == null) return;
			var paths = e.Data.GetData (DataFormats.FileDrop) as string [];
			if (paths == null) return;

			foreach (var p in paths)
			{
				if (!File.Exists (p)) continue;
				string name = Path.GetFileName (p);
				if (string.IsNullOrEmpty (name)) continue;
				if (_files.ContainsKey (name))
				{
					var r = MessageBox.Show (name + " already exists. Overwrite?",
						"Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
					if (r != MessageBoxResult.Yes) continue;
				}
				try { _files [name] = File.ReadAllBytes (p); } catch { }
			}
			RebuildTree ();
		}

		// ==================== 辅助 ====================

		private void SelectByTag (string tag)
		{
			foreach (var obj in treeFiles.Items)
			{
				var item = obj as TreeViewItem;
				if (item == null) continue;
				var found = FindByTag (item, tag);
				if (found != null)
				{
					found.IsSelected = true;
					found.BringIntoView ();
					return;
				}
			}
		}

		private static TreeViewItem FindByTag (TreeViewItem node, string tag)
		{
			if (string.Equals (node.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
				return node;
			foreach (var obj in node.Items)
			{
				var c = obj as TreeViewItem;
				if (c == null) continue;
				var r = FindByTag (c, tag);
				if (r != null) return r;
			}
			return null;
		}
	}
}
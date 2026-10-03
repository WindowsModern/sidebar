using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SGPkgMgr.Helpers;
using SGPkgMgr.Localization;

namespace SGPkgMgr.Views
{
	/// <summary>
	/// 编辑一个 "路径 → 字节数组" 的文件集合。
	/// 直接持有传入字典的引用，任何增删改都作用于该字典。
	///
	/// 路径 key 统一用 '/' 分隔，支持任意层级目录。
	/// 显式创建的空目录记录在 _emptyDirs 中（HashSet，路径不含末尾 /）。
	/// </summary>
	public partial class PackageFilesView: UserControl
	{
		private Dictionary<string, byte []> _files;
		private readonly HashSet<string> _emptyDirs =
			new HashSet<string> (StringComparer.OrdinalIgnoreCase);

		public PackageFilesView ()
		{
			InitializeComponent ();
		}

		public void SetFiles (Dictionary<string, byte []> files)
		{
			_files = files ?? new Dictionary<string, byte []> (StringComparer.OrdinalIgnoreCase);
			_emptyDirs.Clear ();
			RebuildTree ();
		}

		public Dictionary<string, byte []> GetFiles () => _files;

		// ============================================================
		//  路径辅助
		// ============================================================

		private static string NormalizePath (string p)
		{
			if (string.IsNullOrEmpty (p)) return "";
			return p.Replace ('\\', '/').Trim ('/');
		}

		private bool IsFile (string path)
			=> _files != null && _files.ContainsKey (path);

		private bool IsDir (string path)
		{
			if (path == null) return false;
			if (path.Length == 0) return true; // 根
			if (IsFile (path)) return false;

			if (_emptyDirs.Contains (path)) return true;

			// 有文件以此为前缀
			var prefix = path + "/";
			foreach (var k in _files.Keys)
				if (k.StartsWith (prefix, StringComparison.OrdinalIgnoreCase))
					return true;
			return false;
		}

		/// <summary>枚举包内所有目录（含根，路径不含末尾 /）。</summary>
		private IEnumerable<string> AllDirs ()
		{
			var set = new HashSet<string> (StringComparer.OrdinalIgnoreCase) { "" };

			if (_files != null)
				foreach (var k in _files.Keys)
					AddAncestors (set, k);

			foreach (var d in _emptyDirs)
			{
				set.Add (d);
				AddAncestors (set, d);
			}

			return set;
		}

		/// <summary>把 path 的所有上级目录加入 set（不含 path 自身）。</summary>
		private static void AddAncestors (HashSet<string> set, string path)
		{
			var parts = NormalizePath (path)
				.Split (new [] { '/' }, StringSplitOptions.RemoveEmptyEntries);
			string cur = "";
			for (int i = 0; i < parts.Length - 1; i++)
			{
				cur = cur.Length == 0 ? parts [i] : cur + "/" + parts [i];
				set.Add (cur);
			}
		}

		private static string ParentOf (string path)
		{
			if (string.IsNullOrEmpty (path)) return "";
			int i = path.LastIndexOf ('/');
			return i < 0 ? "" : path.Substring (0, i);
		}

		private static string NameOf (string path)
		{
			if (string.IsNullOrEmpty (path)) return "";
			int i = path.LastIndexOf ('/');
			return i < 0 ? path : path.Substring (i + 1);
		}

		private static string Join (string dir, string name)
		{
			if (string.IsNullOrEmpty (dir)) return name;
			return dir + "/" + name;
		}

		// ============================================================
		//  名称校验
		// ============================================================

		private static readonly char [] InvalidNameChars =
			{ '/', '\\', ':', '*', '?', '"', '<', '>', '|' };

		/// <summary>返回 null 表示合法，否则返回错误消息。</summary>
		private static string ValidateName (string name)
		{
			if (string.IsNullOrWhiteSpace (name)) return Strings.MsgNameEmpty;
			if (name != name.Trim ()) return Strings.MsgNameHasWhitespace;
			if (name == "." || name == "..") return Strings.MsgNameDotOrDotDot;
			if (name.IndexOfAny (InvalidNameChars) >= 0) return Strings.MsgNameInvalidChars;
			return null;
		}

		// ============================================================
		//  树构建
		// ============================================================

		private void RebuildTree ()
		{
			treeFiles.Items.Clear ();
			if (_files == null) return;

			var root = new TreeViewItem {
				Header = "/",
				Tag = "",
				IsExpanded = true
			};
			var dirMap = new Dictionary<string, TreeViewItem> (StringComparer.OrdinalIgnoreCase);
			dirMap [""] = root;

			// 收集所有需要显示的目录路径
			var dirs = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			foreach (var key in _files.Keys)
			{
				var parts = NormalizePath (key)
					.Split (new [] { '/' }, StringSplitOptions.RemoveEmptyEntries);
				string cur = "";
				for (int i = 0; i < parts.Length - 1; i++)
				{
					cur = cur.Length == 0 ? parts [i] : cur + "/" + parts [i];
					dirs.Add (cur);
				}
			}
			foreach (var d in _emptyDirs)
			{
				dirs.Add (d);
				var parts = d.Split (new [] { '/' }, StringSplitOptions.RemoveEmptyEntries);
				string cur = "";
				for (int i = 0; i < parts.Length - 1; i++)
				{
					cur = cur.Length == 0 ? parts [i] : cur + "/" + parts [i];
					dirs.Add (cur);
				}
			}

			// 建目录节点
			foreach (var d in dirs.OrderBy (x => x, StringComparer.OrdinalIgnoreCase))
			{
				var parts = d.Split (new [] { '/' }, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length == 0) continue;
				var parent = ParentOf (d);
				TreeViewItem parentNode;
				if (!dirMap.TryGetValue (parent, out parentNode))
					parentNode = root;

				var node = new TreeViewItem {
					Header = parts [parts.Length - 1] + "/",
					Tag = d,
					IsExpanded = true
				};
				dirMap [d] = node;
				parentNode.Items.Add (node);
			}

			// 建文件节点（按父目录聚合 + 排序）
			foreach (var kv in _files.OrderBy (k => k.Key, StringComparer.OrdinalIgnoreCase))
			{
				var parent = ParentOf (kv.Key);
				TreeViewItem parentNode;
				if (!dirMap.TryGetValue (parent, out parentNode))
					parentNode = root;

				parentNode.Items.Add (new TreeViewItem {
					Header = NameOf (kv.Key),
					Tag = kv.Key
				});
			}

			treeFiles.Items.Add (root);
		}

		// ============================================================
		//  选择状态
		// ============================================================

		private TreeViewItem SelectedNode => treeFiles.SelectedItem as TreeViewItem;

		private string SelectedPath
		{
			get
			{
				var n = SelectedNode;
				if (n == null) return null;
				return n.Tag as string;
			}
		}

		private string SelectedFilePath
		{
			get
			{
				var tag = SelectedPath;
				if (string.IsNullOrEmpty (tag)) return null;
				return IsFile (tag) ? tag : null;
			}
		}

		/// <summary>
		/// 当前选中目录的路径（"" 表示根）。
		/// 如果选中文件 → 返回该文件所在目录。
		/// 如果没有选中任何东西 → 返回根。
		/// </summary>
		private string SelectedDirPath
		{
			get
			{
				var tag = SelectedPath;
				if (string.IsNullOrEmpty (tag)) return "";
				if (IsFile (tag)) return ParentOf (tag);
				return tag;
			}
		}

		private void OnSelectedItemChanged (object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			var n = treeFiles.SelectedItem as TreeViewItem;
			if (n == null) { txtInfo.Text = ""; return; }
			var tag = n.Tag as string;
			if (string.IsNullOrEmpty (tag))
			{
				txtInfo.Text = string.Format (Strings.FmtRootFilesCount, _files?.Count ?? 0);
				return;
			}
			byte [] data;
			if (_files != null && _files.TryGetValue (tag, out data))
				txtInfo.Text = string.Format (Strings.FmtBytesSuffix, tag, data.Length);
			else
				txtInfo.Text = string.Format (Strings.FmtDirSuffix, tag);
		}

		// ============================================================
		//  添加文件
		// ============================================================

		private void OnAdd (object sender, RoutedEventArgs e)
		{
			if (_files == null) return;
			var dlg = new OpenFileDialog { Multiselect = true };
			if (dlg.ShowDialog () != true) return;

			string dir = SelectedDirPath;
			int added = 0, skipped = 0;
			var conflicts = new List<string> ();

			foreach (var path in dlg.FileNames)
			{
				string name = Path.GetFileName (path);
				if (string.IsNullOrEmpty (name)) { skipped++; continue; }
				string key = Join (dir, name);
				if (IsFile (key)) conflicts.Add (name);
			}

			bool overwrite = true;
			if (conflicts.Count > 0)
			{
				var list = string.Join ("\n", conflicts.Take (20));
				if (conflicts.Count > 20)
					list += "\n" + string.Format (Strings.FmtAndMoreCount, conflicts.Count);
				var msg = string.Format (Strings.FmtFilesExistOverwrite, list);
				var r = MessageBox.Show (msg, Strings.Confirm,
					MessageBoxButton.YesNo, MessageBoxImage.Question);
				overwrite = r == MessageBoxResult.Yes;
			}

			foreach (var path in dlg.FileNames)
			{
				string name = Path.GetFileName (path);
				if (string.IsNullOrEmpty (name)) { skipped++; continue; }
				string key = Join (dir, name);

				if (IsFile (key) && !overwrite) { skipped++; continue; }
				if (IsDir (key) && key.Length > 0) { skipped++; continue; }

				try { _files [key] = File.ReadAllBytes (path); added++; }
				catch { skipped++; }
			}

			RebuildTree ();
			txtInfo.Text = string.Format (Strings.FmtAddedSkipped, added, skipped);
		}

		// ============================================================
		//  添加目录（保留顶层目录名）
		// ============================================================

		private void OnAddFolder (object sender, RoutedEventArgs e)
		{
			if (_files == null) return;
			var fbd = new System.Windows.Forms.FolderBrowserDialog ();
			if (fbd.ShowDialog () != System.Windows.Forms.DialogResult.OK) return;

			AddDirectoryToCurrent (fbd.SelectedPath, keepTopFolderName: true);
			RebuildTree ();
		}

		// ============================================================
		//  导入目录（不保留顶层目录名，内容展开到当前目录）
		// ============================================================

		private void OnImportFolder (object sender, RoutedEventArgs e)
		{
			if (_files == null) return;
			var fbd = new System.Windows.Forms.FolderBrowserDialog ();
			if (fbd.ShowDialog () != System.Windows.Forms.DialogResult.OK) return;

			AddDirectoryToCurrent (fbd.SelectedPath, keepTopFolderName: false);
			RebuildTree ();
		}

		/// <summary>
		/// 把磁盘目录添加到 TreeView 当前选中目录下。
		/// keepTopFolderName:
		///   true  → 保留顶层目录名（选 D:\Icons → 成为 current/Icons/...）
		///   false → 内容展开（选 D:\Icons → 成为 current/...）
		/// </summary>
		private int AddDirectoryToCurrent (string rootDir, bool keepTopFolderName)
		{
			if (_files == null) return 0;
			if (string.IsNullOrEmpty (rootDir) || !Directory.Exists (rootDir)) return 0;

			string rootFull;
			try { rootFull = Path.GetFullPath (rootDir).TrimEnd ('\\', '/'); }
			catch { return 0; }

			string currentDir = SelectedDirPath;

			string prefix;
			if (keepTopFolderName)
			{
				string topName = Path.GetFileName (rootFull);
				prefix = string.IsNullOrEmpty (topName)
					? (string.IsNullOrEmpty (currentDir) ? "" : currentDir + "/")
					: Join (currentDir, topName) + "/";
			}
			else
			{
				prefix = string.IsNullOrEmpty (currentDir) ? "" : currentDir + "/";
			}

			// 收集所有文件
			var allFiles = EnumerateFilesSafe (rootFull);
			if (allFiles.Count == 0)
			{
				txtInfo.Text = string.Format (Strings.FmtFolderIsEmpty, rootFull);
				return 0;
			}

			var targets = new List<KeyValuePair<string, string>> ();
			foreach (var file in allFiles)
			{
				string full;
				try { full = Path.GetFullPath (file); }
				catch { continue; }
				if (!full.StartsWith (rootFull, StringComparison.OrdinalIgnoreCase)) continue;

				string rel = full.Substring (rootFull.Length)
					.TrimStart ('\\', '/')
					.Replace ('\\', '/');

				targets.Add (new KeyValuePair<string, string> (prefix + rel, file));
			}

			if (targets.Count == 0)
			{
				txtInfo.Text = string.Format (Strings.FmtFolderIsEmpty, rootFull);
				return 0;
			}

			// 冲突检测
			var conflicts = targets
				.Where (t => IsFile (t.Key) || (IsDir (t.Key) && t.Key.Length > 0))
				.Select (t => t.Key)
				.ToList ();

			bool overwrite = true;
			if (conflicts.Count > 0)
			{
				var list = string.Join ("\n", conflicts.Take (20));
				if (conflicts.Count > 20)
					list += "\n" + string.Format (Strings.FmtAndMoreCount, conflicts.Count);
				var msg = string.Format (Strings.FmtPathsExistOverwrite, list);
				var r = MessageBox.Show (msg, Strings.Confirm,
					MessageBoxButton.YesNo, MessageBoxImage.Question);
				overwrite = r == MessageBoxResult.Yes;
			}

			int added = 0, skipped = 0, failed = 0;
			foreach (var kv in targets)
			{
				bool exists = IsFile (kv.Key) || (IsDir (kv.Key) && kv.Key.Length > 0);
				if (exists && !overwrite) { skipped++; continue; }
				// 目录冲突：文件想放在与目录同名路径
				if (IsDir (kv.Key) && !IsFile (kv.Key)) { skipped++; continue; }

				try
				{
					_files [kv.Key] = File.ReadAllBytes (kv.Value);
					added++;
				}
				catch { failed++; }
			}

			txtInfo.Text = string.Format (Strings.FmtFolderOpDone, added, skipped, failed);
			return added;
		}

		private static List<string> EnumerateFilesSafe (string root)
		{
			var result = new List<string> ();
			var stack = new Stack<string> ();
			stack.Push (root);
			while (stack.Count > 0)
			{
				string dir = stack.Pop ();
				try { foreach (var f in Directory.GetFiles (dir)) result.Add (f); } catch { }
				try { foreach (var sub in Directory.GetDirectories (dir)) stack.Push (sub); } catch { }
			}
			return result;
		}

		// ============================================================
		//  创建目录
		// ============================================================

		private void OnCreateFolder (object sender, RoutedEventArgs e)
		{
			if (_files == null) return;

			string parent = SelectedDirPath;
			var name = InputDialog.Show (Window.GetWindow (this),
				Strings.CreateFolder, Strings.PromptFolderName, "");
			if (name == null) return;

			string err = ValidateName (name);
			if (err != null)
			{
				MessageBox.Show (err, Strings.Error,
					MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			string newPath = Join (parent, name);
			if (IsFile (newPath) || IsDir (newPath))
			{
				MessageBox.Show (string.Format (Strings.FmtPathAlreadyExists, newPath),
					Strings.Error, MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			_emptyDirs.Add (newPath);
			RebuildTree ();
			SelectByTag (newPath);
			txtInfo.Text = string.Format (Strings.FmtFolderCreated, newPath);
		}

		// ============================================================
		//  重命名
		// ============================================================

		private void OnRename (object sender, RoutedEventArgs e)
		{
			if (_files == null) return;
			var path = SelectedPath;
			if (string.IsNullOrEmpty (path)) return;

			bool isFile = IsFile (path);
			string oldName = NameOf (path);
			string parent = ParentOf (path);

			var newName = InputDialog.Show (Window.GetWindow (this),
				Strings.RenameItem, Strings.PromptNewName, oldName);
			if (newName == null) return;
			if (newName == oldName) return;

			string err = ValidateName (newName);
			if (err != null)
			{
				MessageBox.Show (err, Strings.Error,
					MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			string newPath = Join (parent, newName);
			if (IsFile (newPath) || IsDir (newPath))
			{
				MessageBox.Show (string.Format (Strings.FmtPathAlreadyExists, newPath),
					Strings.Error, MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			if (isFile)
				RenameFile (path, newPath);
			else
				RenameDirectory (path, newPath);

			RebuildTree ();
			SelectByTag (newPath);
			txtInfo.Text = string.Format (Strings.FmtRenamedTo, newPath);
		}

		private void RenameFile (string oldKey, string newKey)
		{
			var bytes = _files [oldKey];
			_files.Remove (oldKey);
			_files [newKey] = bytes;
		}

		private void RenameDirectory (string oldDir, string newDir)
		{
			// 更新 _emptyDirs
			var newEmpty = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			foreach (var d in _emptyDirs)
			{
				if (string.Equals (d, oldDir, StringComparison.OrdinalIgnoreCase))
					newEmpty.Add (newDir);
				else if (d.StartsWith (oldDir + "/", StringComparison.OrdinalIgnoreCase))
					newEmpty.Add (newDir + d.Substring (oldDir.Length));
				else
					newEmpty.Add (d);
			}
			_emptyDirs.Clear ();
			foreach (var d in newEmpty) _emptyDirs.Add (d);

			// 更新 _files
			var toMove = _files.Keys
				.Where (k => k.StartsWith (oldDir + "/", StringComparison.OrdinalIgnoreCase))
				.ToList ();
			var moved = new Dictionary<string, byte []> (StringComparer.OrdinalIgnoreCase);
			foreach (var k in toMove)
			{
				moved [newDir + k.Substring (oldDir.Length)] = _files [k];
				_files.Remove (k);
			}
			foreach (var kv in moved) _files [kv.Key] = kv.Value;
		}

		// ============================================================
		//  移动到...
		// ============================================================

		private void OnMove (object sender, RoutedEventArgs e)
		{
			if (_files == null) return;
			var path = SelectedPath;
			if (string.IsNullOrEmpty (path)) return;

			bool isFile = IsFile (path);
			string name = NameOf (path);
			string oldParent = ParentOf (path);

			// 候选目录：所有目录，但排除自身（如果是目录）和其子目录
			var candidates = AllDirs ()
				.Where (d => !string.Equals (d, oldParent, StringComparison.OrdinalIgnoreCase))
				.Where (d => {
					if (isFile) return true;
					if (string.Equals (d, path, StringComparison.OrdinalIgnoreCase)) return false;
					if (d.StartsWith (path + "/", StringComparison.OrdinalIgnoreCase)) return false;
					return true;
				})
				.OrderBy (d => d, StringComparer.OrdinalIgnoreCase)
				.ToList ();

			var target = DirectoryPickerDialog.Show (Window.GetWindow (this),
				Strings.MoveItem, Strings.PromptMoveTo, candidates);
			if (target == null) return;

			string newPath = Join (target, name);
			if (string.Equals (newPath, path, StringComparison.OrdinalIgnoreCase)) return;

			if (IsFile (newPath) || IsDir (newPath))
			{
				MessageBox.Show (string.Format (Strings.FmtPathAlreadyExists, newPath),
					Strings.Error, MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			if (isFile)
				RenameFile (path, newPath);
			else
				RenameDirectory (path, newPath);

			RebuildTree ();
			SelectByTag (newPath);
			txtInfo.Text = string.Format (Strings.FmtMovedTo, newPath);
		}

		// ============================================================
		//  替换
		// ============================================================

		private void OnReplace (object sender, RoutedEventArgs e)
		{
			if (_files == null) return;
			var file = SelectedFilePath;
			if (file == null)
			{
				MessageBox.Show (Strings.MsgSelectFileToReplace, Strings.AppTitle,
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			var dlg = new OpenFileDialog ();
			if (dlg.ShowDialog () != true) return;

			try { _files [file] = File.ReadAllBytes (dlg.FileName); }
			catch (Exception ex)
			{
				MessageBox.Show (ex.Message, Strings.AppTitle,
					MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			RebuildTree ();
			SelectByTag (file);
			txtInfo.Text = string.Format (Strings.FmtBytesSuffix, file, _files [file].Length);
		}

		// ============================================================
		//  删除
		// ============================================================

		private void OnDelete (object sender, RoutedEventArgs e)
		{
			if (_files == null) return;

			var file = SelectedFilePath;
			if (file != null)
			{
				var msg = string.Format (Strings.FmtDeleteFileConfirm, file);
				if (MessageBox.Show (msg, Strings.Confirm,
					MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
					return;
				_files.Remove (file);
				RebuildTree ();
				return;
			}

			var dir = SelectedPath;
			if (string.IsNullOrEmpty (dir)) return;
			var prefix = dir + "/";
			var filesToRemove = _files.Keys
				.Where (k => k.StartsWith (prefix, StringComparison.OrdinalIgnoreCase))
				.ToList ();
			var dirsToRemove = _emptyDirs
				.Where (d => string.Equals (d, dir, StringComparison.OrdinalIgnoreCase)
					|| d.StartsWith (prefix, StringComparison.OrdinalIgnoreCase))
				.ToList ();

			if (filesToRemove.Count == 0 && dirsToRemove.Count == 0) return;
			var folderMsg = string.Format (Strings.FmtDeleteFolderConfirm, dir, filesToRemove.Count);
			if (MessageBox.Show (folderMsg, Strings.Confirm,
				MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
				return;

			foreach (var k in filesToRemove) _files.Remove (k);
			foreach (var d in dirsToRemove) _emptyDirs.Remove (d);
			RebuildTree ();
		}

		// ============================================================
		//  导出
		// ============================================================

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
					MessageBox.Show (ex.Message, Strings.AppTitle,
						MessageBoxButton.OK, MessageBoxImage.Error);
				}
				return;
			}

			var dir = SelectedDirPath;
			var fbd = new System.Windows.Forms.FolderBrowserDialog ();
			if (fbd.ShowDialog () != System.Windows.Forms.DialogResult.OK) return;

			var prefix = string.IsNullOrEmpty (dir) ? "" : dir + "/";
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

		// ============================================================
		//  拖放
		// ============================================================

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

			string dir = SelectedDirPath;
			int addedFiles = 0, addedDirs = 0;

			foreach (var p in paths)
			{
				if (File.Exists (p))
				{
					string name = Path.GetFileName (p);
					if (string.IsNullOrEmpty (name)) continue;
					string key = Join (dir, name);
					if (IsFile (key))
					{
						var msg = string.Format (Strings.FmtFilesExistOverwrite, key);
						var r = MessageBox.Show (msg, Strings.Confirm,
							MessageBoxButton.YesNo, MessageBoxImage.Question);
						if (r != MessageBoxResult.Yes) continue;
					}
					try { _files [key] = File.ReadAllBytes (p); addedFiles++; } catch { }
				}
				else if (Directory.Exists (p))
				{
					AddDirectoryToCurrent (p, keepTopFolderName: true);
					addedDirs++;
				}
			}

			RebuildTree ();
			if (addedDirs > 0)
				txtInfo.Text = string.Format (Strings.FmtDroppedFilesAndDirs, addedFiles, addedDirs);
			else if (addedFiles > 0)
				txtInfo.Text = string.Format (Strings.FmtAddedCount, addedFiles);
		}

		// ============================================================
		//  辅助
		// ============================================================

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
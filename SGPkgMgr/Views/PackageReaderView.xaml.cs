using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Sidebar;
using SGPkgMgr.Localization;
using SGPkgMgr.Helpers;

namespace SGPkgMgr.Views
{
	public partial class PackageReaderView: UserControl
	{
		public PackageReaderView ()
		{
			InitializeComponent ();
		}

		private void OnBrowse (object sender, RoutedEventArgs e)
		{
			var dlg = new OpenFileDialog {
				Filter = "Package files (*.sgpkg;*.sgpkgbundle)|*.sgpkg;*.sgpkgbundle|All files (*.*)|*.*"
			};
			if (dlg.ShowDialog () == true)
				txtPackagePath.Text = dlg.FileName;
		}

		private void OnRead (object sender, RoutedEventArgs e)
		{
			var path = txtPackagePath.Text.Trim ();
			if (string.IsNullOrEmpty (path) || !File.Exists (path))
			{
				MessageBox.Show (Strings.Warning + ": " + Strings.Package, "SGPkgMgr",
					MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			treeReport.Items.Clear ();
			UiTask.Run<object> (
				progress,
				busy => { btnRead.IsEnabled = !busy; btnBrowse.IsEnabled = !busy; },
				report => {
			// 后台读取 + 解析
			using (var reader = TilePackageReadManager.GetPackage (path))
					{
						return BuildReportRoot (path, reader);
					}
				},
				(root, ex) => {
					if (ex != null)
					{
						MessageBox.Show (Strings.Error + ": " + ex.Message, "SGPkgMgr",
							MessageBoxButton.OK, MessageBoxImage.Error);
						return;
					}
					treeReport.Items.Add ((TreeViewItem)root);
				});
		}

		/// <summary>根据包内容构建整棵报告树（在后台线程执行）。</summary>
		private static TreeViewItem BuildReportRoot (string path, TilePackageBase reader)
		{
			var root = new TreeViewItem { Header = Path.GetFileName (path), IsExpanded = true };
			root.Items.Add (MakeItem ("Type", reader.PackageType.ToString ()));

			var bundle = reader as TilePackageBundle;
			if (bundle != null)
			{
				AddIdentity (root, bundle.Manifest.Identity);
				foreach (var sp in bundle.Packages)
				{
					var sub = new TreeViewItem {
						Header = "SubPackage: " + sp.FileName,
						IsExpanded = true
					};
					AddIdentity (sub, sp.Manifest.Identity);
					AddProperties (sub, sp.Manifest.Properties);
					AddPrerequisites (sub, sp.Manifest.Prerequisites);
					AddApplications (sub, sp.Manifest.Applications);
					root.Items.Add (sub);
				}
			}
			else
			{
				var single = reader as TilePackage;
				if (single != null)
				{
					AddIdentity (root, single.Manifest.Identity);
					AddProperties (root, single.Manifest.Properties);
					AddPrerequisites (root, single.Manifest.Prerequisites);
					AddApplications (root, single.Manifest.Applications);
				}
			}
			return root;
		}

		// ==================== 复制 ====================

		private void OnCopyNode (object sender, RoutedEventArgs e)
		{
			var item = treeReport.SelectedItem as TreeViewItem;
			if (item == null) return;
			CopyToClipboard (NodeToString (item, includeChildren: false));
		}

		private void OnCopySubtree (object sender, RoutedEventArgs e)
		{
			var item = treeReport.SelectedItem as TreeViewItem;
			if (item == null) return;
			CopyToClipboard (NodeToString (item, includeChildren: true));
		}

		private void OnCopyAll (object sender, RoutedEventArgs e)
		{
			var sb = new StringBuilder ();
			foreach (var obj in treeReport.Items)
			{
				var item = obj as TreeViewItem;
				if (item == null) continue;
				AppendNode (sb, item, 0);
			}
			CopyToClipboard (sb.ToString ());
		}

		private static string NodeToString (TreeViewItem item, bool includeChildren)
		{
			var sb = new StringBuilder ();
			if (includeChildren) AppendNode (sb, item, 0);
			else sb.AppendLine (item.Header?.ToString () ?? "");
			return sb.ToString ();
		}

		private static void AppendNode (StringBuilder sb, TreeViewItem item, int indent)
		{
			sb.Append (' ', indent * 2);
			sb.AppendLine (item.Header?.ToString () ?? "");
			foreach (var obj in item.Items)
			{
				var child = obj as TreeViewItem;
				if (child != null) AppendNode (sb, child, indent + 1);
			}
		}

		private static void CopyToClipboard (string text)
		{
			try { Clipboard.SetText (text); }
			catch { /* 剪贴板被占用时忽略 */ }
		}

		// ==================== 构建树 ====================

		private static void AddIdentity (TreeViewItem parent, TileIdentity id)
		{
			var node = new TreeViewItem { Header = "Identity", IsExpanded = true };
			node.Items.Add (MakeItem ("Name", id.Name));
			node.Items.Add (MakeItem ("Publisher", id.Publisher));
			node.Items.Add (MakeItem ("Version", id.Version != null ? id.Version.Expression : ""));
			node.Items.Add (MakeItem ("Architecture", id.ProcessorArchitecture.ToString ()));
			node.Items.Add (MakeItem ("PublisherId", id.PublisherId));
			node.Items.Add (MakeItem ("FamilyName", id.FamilyName));
			node.Items.Add (MakeItem ("FullName", id.FullName));
			parent.Items.Add (node);
		}
		private static void AddProperties (TreeViewItem parent, TileProperties p)
		{
			var node = new TreeViewItem { Header = "Properties" };
			node.Items.Add (MakeItem ("DisplayName", p.DisplayName));
			node.Items.Add (MakeItem ("PublisherDisplayName", p.PublisherDisplayName));
			node.Items.Add (MakeItem ("Description", p.Description));
			node.Items.Add (MakeItem ("Logo", p.Logo));
			parent.Items.Add (node);
		}
		private static void AddPrerequisites (TreeViewItem parent, TilePrerequisites p)
		{
			if (p == null) return;
			var node = new TreeViewItem { Header = "Prerequisites" };
			node.Items.Add (MakeItem ("OSMinVersion", p.OSMinVersion != null ? p.OSMinVersion.Expression : ""));
			node.Items.Add (MakeItem ("OSMaxVersion",
				p.OSMaxVersion != null ? p.OSMaxVersion?.Expression : "(unbounded)"));
			parent.Items.Add (node);
		}
		private static void AddApplications (TreeViewItem parent, System.Collections.Generic.List<TileApplication> apps)
		{
			var node = new TreeViewItem {
				Header = "Applications (" + (apps == null ? 0 : apps.Count) + ")",
				IsExpanded = true
			};
			if (apps != null)
			{
				foreach (var app in apps)
				{
					var an = new TreeViewItem { Header = "Application: " + app.Id };
					an.Items.Add (MakeItem ("Executable", app.Executable));
					an.Items.Add (MakeItem ("EntryPoint", app.EntryPoint));
					an.Items.Add (MakeItem ("HasFlyout", app.HasFlyout.ToString ()));
					an.Items.Add (MakeItem ("HasProperties", app.HasProperties.ToString ()));
					var ve = app.VisualElements;
					if (ve != null)
					{
						var vn = new TreeViewItem { Header = "VisualElements" };
						vn.Items.Add (MakeItem ("DisplayName", ve.DisplayName));
						vn.Items.Add (MakeItem ("Logo", ve.Logo));
						if (ve.Tile != null)
						{
							var tn = new TreeViewItem { Header = "Tile" };
							tn.Items.Add (MakeItem ("MinHeight", ve.Tile.MinHeight.ToString ()));
							tn.Items.Add (MakeItem ("MaxHeight", ve.Tile.MaxHeight.ToString ()));
							tn.Items.Add (MakeItem ("DefaultHeight", ve.Tile.DefaultHeight.ToString ()));
							tn.Items.Add (MakeItem ("CanPinBottom", ve.Tile.CanPinBottom.ToString ()));
							tn.Items.Add (MakeItem ("Overflow", ve.Tile.Overflow.ToString ()));
							vn.Items.Add (tn);
						}
						if (ve.Flyout != null)
						{
							var fn = new TreeViewItem { Header = "Flyout" };
							fn.Items.Add (MakeItem ("FlyoutWidth", ve.Flyout.FlyoutWidth.ToString ()));
							fn.Items.Add (MakeItem ("FlyoutHeight", ve.Flyout.FlyoutHeight.ToString ()));
							fn.Items.Add (MakeItem ("FlyoutCanResize", ve.Flyout.FlyoutCanResize.ToString ()));
							vn.Items.Add (fn);
						}
						an.Items.Add (vn);
					}
					node.Items.Add (an);
				}
			}
			parent.Items.Add (node);
		}
		private static TreeViewItem MakeItem (string key, string value)
		{
			return new TreeViewItem { Header = key + ": " + (value ?? "") };
		}
	}
}
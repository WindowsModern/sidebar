using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Sidebar;
using SGPkgMgr.Localization;
using SGPkgMgr.Helpers;
using SGPkgMgr.Services;

namespace SGPkgMgr.Views
{
	public partial class PackageReaderView: UserControl
	{
		public PackageReaderView ()
		{
			InitializeComponent ();
		}

		// ============================================================
		//  浏览 / 读取
		// ============================================================

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
				MessageBox.Show (Strings.Warning + ": " + Strings.Package, Strings.AppTitle,
					MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			treeReport.Items.Clear ();

			UiTask.Run<ReportNode> (
				progress,
				busy => { btnRead.IsEnabled = !busy; btnBrowse.IsEnabled = !busy; },
				report => {
					using (var reader = TilePackageReadManager.GetPackage (path))
					{
						return BuildReportRoot (path, reader);
					}
				},
				(root, ex) => {
					if (ex != null)
					{
						MessageBox.Show (Strings.Error + ": " + ex.Message, Strings.AppTitle,
							MessageBoxButton.OK, MessageBoxImage.Error);
						return;
					}
					treeReport.Items.Add (ToTreeViewItem (root));
				});
		}

		// ============================================================
		//  纯数据模型
		// ============================================================

		private sealed class ReportNode
		{
			public string Header { get; set; }
			public bool IsExpanded { get; set; }
			public List<ReportNode> Children { get; } = new List<ReportNode> ();

			public ReportNode Add (string header, bool expanded = false)
			{
				var n = new ReportNode { Header = header, IsExpanded = expanded };
				Children.Add (n);
				return n;
			}

			public void AddLeaf (string key, string value)
			{
				Add (string.Format (Strings.FmtKeyValue, key, value ?? ""));
			}
		}

		private static TreeViewItem ToTreeViewItem (ReportNode node)
		{
			if (node == null) return null;
			var item = new TreeViewItem {
				Header = node.Header,
				IsExpanded = node.IsExpanded
			};
			foreach (var child in node.Children)
			{
				var ci = ToTreeViewItem (child);
				if (ci != null) item.Items.Add (ci);
			}
			return item;
		}

		// ============================================================
		//  报告构建（后台线程，纯数据）
		// ============================================================

		private static ReportNode BuildReportRoot (string path, TilePackageBase reader)
		{
			var root = new ReportNode {
				Header = Path.GetFileName (path),
				IsExpanded = true
			};
			root.AddLeaf (Strings.Type, reader.PackageType.ToString ());

			var bundle = reader as TilePackageBundle;
			if (bundle != null)
			{
				AddIdentity (root, bundle.Manifest.Identity);
				foreach (var sp in bundle.Packages)
				{
					var sub = root.Add (
						string.Format (Strings.FmtSubPackageHeader, sp.FileName), true);
					AddIdentity (sub, sp.Manifest.Identity);
					AddProperties (sub, sp.Manifest.Properties);
					AddPrerequisites (sub, sp.Manifest.Prerequisites);
					AddApplications (sub, sp.Manifest.Applications);
					AddResources (sub, sp);
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
					AddResources (root, single);
				}
			}
			return root;
		}

		// ============================================================
		//  各节构建
		// ============================================================

		private static void AddIdentity (ReportNode parent, TileIdentity id)
		{
			var node = parent.Add (Strings.Identity, true);
			node.AddLeaf (Strings.Name, id.Name);
			node.AddLeaf (Strings.Publisher, id.Publisher);
			node.AddLeaf (Strings.Version, id.Version != null ? id.Version.ToString () : "");
			node.AddLeaf (Strings.Architecture, id.ProcessorArchitecture.ToString ());
			node.AddLeaf (Strings.PublisherId, id.PublisherId);
			node.AddLeaf (Strings.FamilyName, id.FamilyName);
			node.AddLeaf (Strings.FullName, id.FullName);
		}

		private static void AddProperties (ReportNode parent, TileProperties p)
		{
			var node = parent.Add (Strings.Properties);
			node.AddLeaf (Strings.DisplayName, p.DisplayName);
			node.AddLeaf (Strings.PublisherDisplayName, p.PublisherDisplayName);
			node.AddLeaf (Strings.Description, p.Description);
			node.AddLeaf (Strings.Logo, p.Logo);
		}

		private static void AddPrerequisites (ReportNode parent, TilePrerequisites p)
		{
			if (p == null) return;
			var node = parent.Add (Strings.Prerequisites);
			node.AddLeaf (Strings.OsMinVersion,
				p.OSMinVersion != null ? p.OSMinVersion.ToString () : "");
			node.AddLeaf (Strings.OsMaxVersion,
				p.OSMaxVersion != null ? p.OSMaxVersion.ToString () : Strings.UnboundedVersion);
		}

		private static void AddApplications (ReportNode parent, List<TileApplication> apps)
		{
			int count = apps == null ? 0 : apps.Count;
			var node = parent.Add (string.Format (Strings.FmtApplicationsCount, count), true);
			if (apps == null) return;

			foreach (var app in apps)
			{
				var an = node.Add (string.Format (Strings.FmtApplicationHeader, app.Id));
				an.AddLeaf (Strings.Executable, app.Executable);
				an.AddLeaf (Strings.EntryPoint, app.EntryPoint);
				an.AddLeaf (Strings.HasFlyout, app.HasFlyout.ToString ());
				an.AddLeaf (Strings.HasProperties, app.HasProperties.ToString ());

				var ve = app.VisualElements;
				if (ve == null) continue;

				var vn = an.Add (Strings.VisualElements);
				vn.AddLeaf (Strings.DisplayName, ve.DisplayName);
				vn.AddLeaf (Strings.Logo, ve.Logo);

				if (ve.Tile != null)
				{
					var tn = vn.Add (Strings.Tile);
					tn.AddLeaf (Strings.MinHeight, ve.Tile.MinHeight.ToString ());
					tn.AddLeaf (Strings.MaxHeight, ve.Tile.MaxHeight.ToString ());
					tn.AddLeaf (Strings.DefaultHeight, ve.Tile.DefaultHeight.ToString ());
					tn.AddLeaf (Strings.CanPinBottom, ve.Tile.CanPinBottom.ToString ());
					tn.AddLeaf (Strings.Overflow, ve.Tile.Overflow.ToString ());
				}

				if (ve.Flyout != null)
				{
					var fn = vn.Add (Strings.Flyout);
					fn.AddLeaf (Strings.FlyoutWidth, ve.Flyout.FlyoutWidth.ToString ());
					fn.AddLeaf (Strings.FlyoutHeight, ve.Flyout.FlyoutHeight.ToString ());
					fn.AddLeaf (Strings.FlyoutCanResize, ve.Flyout.FlyoutCanResize.ToString ());
				}
			}
		}

		/// <summary>
		/// 从包内容里解析 Locale.xml / Path.xml，作为报告树的两个子节点。
		/// 后台线程执行，只构造纯数据 ReportNode。
		/// </summary>
		private static void AddResources (ReportNode parent, TilePackageBase reader)
		{
			// ---- Locale.xml ----
			byte [] localeBytes = null;
			try { localeBytes = reader.ExtractFile ("Locale.xml"); }
			catch { }

			if (localeBytes != null && localeBytes.Length > 0)
			{
				List<LocaleResourceItem> locales = null;
				try { locales = ResourceFileParser.ParseLocale (localeBytes); }
				catch { }

				if (locales != null && locales.Count > 0)
				{
					var node = parent.Add (string.Format (
						Strings.FmtLocaleResourcesCount, locales.Count));
					foreach (var item in locales)
					{
						var rn = node.Add (item.ResourceName);
						foreach (var e in item.Entries)
							rn.AddLeaf (e.LocaleName, e.Value);
					}
				}
			}

			// ---- Path.xml ----
			byte [] pathBytes = null;
			try { pathBytes = reader.ExtractFile ("Path.xml"); }
			catch { }

			if (pathBytes != null && pathBytes.Length > 0)
			{
				List<PathResourceItem> paths = null;
				try { paths = ResourceFileParser.ParsePath (pathBytes); }
				catch { }

				if (paths != null && paths.Count > 0)
				{
					var node = parent.Add (string.Format (
						Strings.FmtPathResourcesCount, paths.Count));
					foreach (var item in paths)
					{
						var rn = node.Add (item.ResourceName);
						foreach (var e in item.Entries)
							rn.AddLeaf (e.Scale + "%", e.Path);
					}
				}
			}
		}

		// ============================================================
		//  复制
		// ============================================================

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
			catch { }
		}
	}
}
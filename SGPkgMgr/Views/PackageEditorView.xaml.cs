using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Sidebar;
using SGPkgMgr.Localization;
using SGPkgMgr.Services;
using SGPkgMgr.Helpers;

namespace SGPkgMgr.Views
{
	public partial class PackageEditorView: UserControl
	{
		private PackageEditorSession _session;

		/// <summary>当前在子包 Tab 里正在编辑的 slot（用于切换时提交资源）。</summary>
		private PackageEditorSession.PackageSlot _currentSubSlot;

		private sealed class SlotItem
		{
			public PackageEditorSession.PackageSlot Slot { get; set; }
			public string DisplayName { get; set; }
			public override string ToString () => DisplayName;
		}

		public PackageEditorView ()
		{
			InitializeComponent ();
			SetMode (isBundle: false);
		}

		private void SetMode (bool isBundle)
		{
			tabSingleManifest.Visibility = isBundle ? Visibility.Collapsed : Visibility.Visible;
			tabSingleFiles.Visibility = isBundle ? Visibility.Collapsed : Visibility.Visible;
			tabSingleLocale.Visibility = isBundle ? Visibility.Collapsed : Visibility.Visible;
			tabSinglePath.Visibility = isBundle ? Visibility.Collapsed : Visibility.Visible;

			tabBundleIdentity.Visibility = isBundle ? Visibility.Visible : Visibility.Collapsed;
			tabBundleFiles.Visibility = isBundle ? Visibility.Visible : Visibility.Collapsed;
			tabSubPackages.Visibility = isBundle ? Visibility.Visible : Visibility.Collapsed;

			tabRoot.SelectedItem = isBundle ? (object)tabBundleIdentity : tabSingleManifest;
		}

		private void OnBrowse (object sender, RoutedEventArgs e)
		{
			var dlg = new OpenFileDialog {
				Filter = "Package files (*.sgpkg;*.sgpkgbundle)|*.sgpkg;*.sgpkgbundle|All files (*.*)|*.*"
			};
			if (dlg.ShowDialog () == true)
				txtPackagePath.Text = dlg.FileName;
		}

		// ============================================================
		//  加载
		// ============================================================

		private void PopulateFromSession ()
		{
			SetMode (_session.IsBundle);

			if (_session.IsBundle && _session.BundleManifest != null)
			{
				var id = _session.BundleManifest.Identity;
				txtBundleName.Text = id.Name;
				txtBundlePublisher.Text = id.Publisher;
				txtBundleVerMajor.Text = id.Version.Major.ToString ();
				txtBundleVerMinor.Text = id.Version.Minor.ToString ();
				txtBundleVerBuild.Text = id.Version.Build.ToString ();
				txtBundleVerRev.Text = id.Version.Revision.ToString ();

				bundleFilesView.SetFiles (_session.BundleExtras);

				var items = new List<SlotItem> ();
				foreach (var slot in _session.Slots)
				{
					items.Add (new SlotItem {
						Slot = slot,
						DisplayName = string.IsNullOrEmpty (slot.EntryName)
							? slot.Manifest.Identity.Name
							: slot.EntryName
					});
				}
				lstSlots.ItemsSource = items;
				lstSlots.SelectedIndex = 0;
			}
			else
			{
				if (_session.Slots.Count > 0)
				{
					var slot = _session.Slots [0];
					singleManifestEditor.LoadManifest (slot.Manifest);
					singleFilesView.SetFiles (slot.Files);
					LoadLocaleInto (singleLocaleEditor, slot);
					LoadPathInto (singlePathEditor, slot);
				}
			}
		}

		private void OnSlotSelected (object sender, SelectionChangedEventArgs e)
		{
			// 先把当前编辑的子包资源提交
			CommitSubSlotResources ();

			var item = lstSlots.SelectedItem as SlotItem;
			if (item == null) return;

			_currentSubSlot = item.Slot;
			bundleManifestEditor.LoadManifest (item.Slot.Manifest);
			slotFilesView.SetFiles (item.Slot.Files);
			LoadLocaleInto (subLocaleEditor, item.Slot);
			LoadPathInto (subPathEditor, item.Slot);
		}

		// ============================================================
		//  Locale / Path 加载与提交
		// ============================================================

		private static void LoadLocaleInto (
			LocaleResourceEditorView editor, PackageEditorSession.PackageSlot slot)
		{
			var bytes = ResourceFileParser.GetFileBytes (slot.Files, "Locale.xml");
			editor.LoadFromBytes (bytes);
		}

		private static void LoadPathInto (
			PathResourceEditorView editor, PackageEditorSession.PackageSlot slot)
		{
			var bytes = ResourceFileParser.GetFileBytes (slot.Files, "Path.xml");
			editor.LoadFromBytes (bytes);
		}

		private static bool CommitLocaleFrom (
			LocaleResourceEditorView editor,
			PackageEditorSession.PackageSlot slot,
			out string error)
		{
			error = null;
			if (!editor.HasData) return true;

			error = editor.Validate ();
			if (error != null) return false;

			var bytes = editor.SaveToBytes ();
			ResourceFileParser.SetFileBytes (slot.Files, "Locale.xml", bytes);
			return true;
		}

		private static bool CommitPathFrom (
			PathResourceEditorView editor,
			PackageEditorSession.PackageSlot slot,
			out string error)
		{
			error = null;
			if (!editor.HasData) return true;

			error = editor.Validate ();
			if (error != null) return false;

			var bytes = editor.SaveToBytes ();
			ResourceFileParser.SetFileBytes (slot.Files, "Path.xml", bytes);
			return true;
		}

		/// <summary>提交当前正在编辑的子包（如果处于 bundle 模式）。</summary>
		private void CommitSubSlotResources ()
		{
			if (_currentSubSlot == null) return;

			string err;
			if (!CommitLocaleFrom (subLocaleEditor, _currentSubSlot, out err))
				throw new InvalidOperationException (Strings.Error + ": " + err);
			if (!CommitPathFrom (subPathEditor, _currentSubSlot, out err))
				throw new InvalidOperationException (Strings.Error + ": " + err);
		}

		// ============================================================
		//  提交所有编辑
		// ============================================================

		private bool CommitEditorChanges ()
		{
			if (_session.IsBundle)
			{
				var item = lstSlots.SelectedItem as SlotItem;
				if (item != null)
				{
					TileManifest manifest;
					string error;
					if (!bundleManifestEditor.TryBuildManifest (out manifest, out error))
					{
						MessageBox.Show (Strings.Error + ": " + error, Strings.AppTitle,
							MessageBoxButton.OK, MessageBoxImage.Error);
						return false;
					}
					item.Slot.Manifest = manifest;

					// 提交当前子包的资源
					if (!CommitLocaleFrom (subLocaleEditor, item.Slot, out error))
					{
						MessageBox.Show (Strings.Error + ": " + error, Strings.AppTitle,
							MessageBoxButton.OK, MessageBoxImage.Error);
						return false;
					}
					if (!CommitPathFrom (subPathEditor, item.Slot, out error))
					{
						MessageBox.Show (Strings.Error + ": " + error, Strings.AppTitle,
							MessageBoxButton.OK, MessageBoxImage.Error);
						return false;
					}
				}

				try
				{
					ushort major = ParseU16 (txtBundleVerMajor.Text);
					ushort minor = ParseU16 (txtBundleVerMinor.Text);
					ushort build = ParseU16 (txtBundleVerBuild.Text);
					ushort rev = ParseU16 (txtBundleVerRev.Text);

					var newIdentity = new TileIdentity (
						txtBundleName.Text.Trim (),
						txtBundlePublisher.Text.Trim (),
						new Sidebar.Version (major, minor, build, rev),
						ProcessorArchitecture.Neutral);

					_session.BundleManifest = new TileBundleManifest (
						newIdentity, _session.BundleManifest.Packages);
				}
				catch (Exception ex)
				{
					MessageBox.Show (Strings.Error + ": " + ex.Message, Strings.AppTitle,
						MessageBoxButton.OK, MessageBoxImage.Error);
					return false;
				}
			}
			else
			{
				TileManifest manifest;
				string error;
				if (!singleManifestEditor.TryBuildManifest (out manifest, out error))
				{
					MessageBox.Show (Strings.Error + ": " + error, Strings.AppTitle,
						MessageBoxButton.OK, MessageBoxImage.Error);
					return false;
				}
				_session.Slots [0].Manifest = manifest;

				if (!CommitLocaleFrom (singleLocaleEditor, _session.Slots [0], out error))
				{
					MessageBox.Show (Strings.Error + ": " + error, Strings.AppTitle,
						MessageBoxButton.OK, MessageBoxImage.Error);
					return false;
				}
				if (!CommitPathFrom (singlePathEditor, _session.Slots [0], out error))
				{
					MessageBox.Show (Strings.Error + ": " + error, Strings.AppTitle,
						MessageBoxButton.OK, MessageBoxImage.Error);
					return false;
				}
			}

			return true;
		}

		private static ushort ParseU16 (string s)
		{
			ushort v;
			return ushort.TryParse (s, out v) ? v : (ushort)0;
		}

		// ============================================================
		//  载入 / 保存 / 查找入口点
		// ============================================================

		private void OnLoad (object sender, RoutedEventArgs e)
		{
			var path = txtPackagePath.Text.Trim ();
			if (string.IsNullOrEmpty (path) || !File.Exists (path))
			{
				MessageBox.Show (Strings.Warning + ": " + Strings.Package, Strings.AppTitle,
					MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			UiTask.Run<PackageEditorSession> (
				progress,
				SetBusy,
				report => PackageEditorSession.Open (path),
				(session, ex) => {
					if (ex != null)
					{
						MessageBox.Show (Strings.Error + ": " + ex.Message, Strings.AppTitle,
							MessageBoxButton.OK, MessageBoxImage.Error);
						return;
					}
					_session = session;
					_currentSubSlot = null;
					PopulateFromSession ();
					txtStatus.Text = (_session.IsBundle ? "Bundle" : "Single")
						+ " · " + _session.Slots.Count + " package(s)";
				});
		}

		private void OnSaveAs (object sender, RoutedEventArgs e)
		{
			if (_session == null)
			{
				MessageBox.Show (Strings.Warning + ": " + Strings.LoadPackageFirst, Strings.AppTitle,
					MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			if (!CommitEditorChanges ()) return;

			var sfd = new SaveFileDialog {
				Filter = "Package files (*.sgpkg;*.sgpkgbundle)|*.sgpkg;*.sgpkgbundle|All files (*.*)|*.*",
				FileName = Path.GetFileName (_session.SourcePath)
			};
			if (sfd.ShowDialog () != true) return;

			var target = sfd.FileName;
			var session = _session;
			UiTask.Run<object> (
				progress,
				SetBusy,
				report => { session.SaveTo (target); return null; },
				(_, ex) => {
					if (ex != null)
					{
						MessageBox.Show (Strings.Error + ": " + ex.Message, Strings.AppTitle,
							MessageBoxButton.OK, MessageBoxImage.Error);
						return;
					}
					txtStatus.Text = Strings.Saved + ": " + target;
					MessageBox.Show (Strings.Success, Strings.AppTitle,
						MessageBoxButton.OK, MessageBoxImage.Information);
				});
		}

		private void OnFindEntryPoints (object sender, RoutedEventArgs e)
		{
			if (_session == null)
			{
				MessageBox.Show (Strings.Warning + ": " + Strings.LoadPackageFirst, Strings.AppTitle,
					MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			var session = _session;
			UiTask.Run<int> (
				progress,
				SetBusy,
				report => session.AutoFillEntryPoints (),
				(count, ex) => {
					if (ex != null)
					{
						MessageBox.Show (Strings.Error + ": " + ex.Message, Strings.AppTitle,
							MessageBoxButton.OK, MessageBoxImage.Error);
						return;
					}
					if (session.IsBundle)
					{
						var item = lstSlots.SelectedItem as SlotItem;
						if (item != null) bundleManifestEditor.LoadManifest (item.Slot.Manifest);
					}
					else if (session.Slots.Count > 0)
					{
						singleManifestEditor.LoadManifest (session.Slots [0].Manifest);
					}
					txtStatus.Text = Strings.FoundEntryPoints + ": " + count;
				});
		}

		private void SetBusy (bool busy)
		{
			tabRoot.IsEnabled = !busy;
		}
	}
}
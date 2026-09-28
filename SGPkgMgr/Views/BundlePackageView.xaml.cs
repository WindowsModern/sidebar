using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SGPkgMgr.Helpers;
using SGPkgMgr.Localization;
using Sidebar;

namespace SGPkgMgr.Views
{
	public partial class BundlePackageView: UserControl
	{
		private class PackageListItem
		{
			public string FilePath { get; set; }
			public string FileName
			{
				get
				{
					try { return Path.GetFileName (FilePath); }
					catch { return FilePath; }
				}
			}
		}
		private readonly ObservableCollection<PackageListItem> _items
			= new ObservableCollection<PackageListItem> ();

		public BundlePackageView ()
		{
			InitializeComponent ();
			gridPackages.ItemsSource = _items;
		}

		private void OnBrowseSave (object sender, RoutedEventArgs e)
		{
			var dlg = new System.Windows.Forms.FolderBrowserDialog ();
			if (dlg.ShowDialog () == System.Windows.Forms.DialogResult.OK)
				txtSaveDir.Text = dlg.SelectedPath;
		}

		private void OnAdd (object sender, RoutedEventArgs e)
		{
			var dlg = new System.Windows.Forms.OpenFileDialog {
				Multiselect = true,
				Filter = "Package files (*.sgpkg)|*.sgpkg|All files (*.*)|*.*"
			};
			if (dlg.ShowDialog () == System.Windows.Forms.DialogResult.OK)
			{
				foreach (var f in dlg.FileNames)
					AddOne (f);
			}
		}
		private void OnRemove (object sender, RoutedEventArgs e)
		{
			var sel = gridPackages.SelectedItem as PackageListItem;
			if (sel != null) _items.Remove (sel);
		}
		private void OnClear (object sender, RoutedEventArgs e) { _items.Clear (); }

		private void OnDragOver (object sender, System.Windows.DragEventArgs e)
		{
			if (e.Data.GetDataPresent (System.Windows.DataFormats.FileDrop))
				e.Effects = System.Windows.DragDropEffects.Copy;
			else
				e.Effects = System.Windows.DragDropEffects.None;
			e.Handled = true;
		}
		private void OnDrop (object sender, DragEventArgs e)
		{
			var files = e.Data.GetData (DataFormats.FileDrop) as string [];
			if (files == null) return;
			foreach (var f in files) AddOne (f);
		}
		private void AddOne (string path)
		{
			if (string.IsNullOrWhiteSpace (path)) return;
			string full;
			try { full = Path.GetFullPath (path); } catch { return; }
			if (_items.Any (x => string.Equals (x.FilePath, full, StringComparison.OrdinalIgnoreCase))) return;
			_items.Add (new PackageListItem { FilePath = full });
		}

		private void OnRun (object sender, RoutedEventArgs e)
		{
			var list = _items.Select (x => x.FilePath)
				.Where (p => !string.IsNullOrEmpty (p)).ToList ();
			if (list.Count < 2)
			{
				Log (Strings.Error + ": At least two packages are required.");
				return;
			}
			var dst = txtSaveDir.Text.Trim ();
			var name = txtBundleName.Text.Trim ();
			if (string.IsNullOrEmpty (dst) || string.IsNullOrEmpty (name))
			{
				Log (Strings.Error + ": " + Strings.SaveFolder);
				return;
			}

			Log ("Creating bundle package...");
			UiTask.Run<object> (
				progress,
				SetBusy,
				report => {
					using (var zip = TilePackageWriteManager.MakeBundlePackage (
						list, (curr, total, p) => report (curr, total, p)))
					{
						TilePackageWriteManager.SavePackageFile (zip, dst, name);
					}
					return null;
				},
				(_, ex) => {
					if (ex != null) Log (Strings.FailedPrefix + ex.Message);
					else Log (Strings.Done);
				});
		}

		private void SetBusy (bool busy)
		{
			btnRun.IsEnabled = !busy;
			gridPackages.IsEnabled = !busy;
			txtSaveDir.IsEnabled = !busy;
			txtBundleName.IsEnabled = !busy;
		}

		private void Log (string line)
		{
			txtLog.AppendText ("[" + DateTime.Now.ToString ("HH:mm:ss") + "] " + line + Environment.NewLine);
			txtLog.ScrollToEnd ();
		}
	}
}
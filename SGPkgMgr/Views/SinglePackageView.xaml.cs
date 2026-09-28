using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using SGPkgMgr.Helpers;
using SGPkgMgr.Localization;
using Sidebar;

namespace SGPkgMgr.Views
{
	public partial class SinglePackageView: UserControl
	{
		public SinglePackageView ()
		{
			InitializeComponent ();
		}

		private void OnBrowseSource (object sender, RoutedEventArgs e)
		{
			var dlg = new System.Windows.Forms.FolderBrowserDialog ();
			if (dlg.ShowDialog () == System.Windows.Forms.DialogResult.OK)
				txtSource.Text = dlg.SelectedPath;
		}
		private void OnBrowseSave (object sender, RoutedEventArgs e)
		{
			var dlg = new System.Windows.Forms.FolderBrowserDialog ();
			if (dlg.ShowDialog () == System.Windows.Forms.DialogResult.OK)
				txtSaveDir.Text = dlg.SelectedPath;
		}

		private void OnRun (object sender, RoutedEventArgs e)
		{
			var src = txtSource.Text.Trim ();
			var dst = txtSaveDir.Text.Trim ();
			var name = txtPkgName.Text.Trim ();
			if (string.IsNullOrEmpty (src) || string.IsNullOrEmpty (dst) || string.IsNullOrEmpty (name))
			{
				Log (Strings.Error + ": " + Strings.SourceFolder);
				return;
			}

			Log ("Creating single package...");
			UiTask.Run<object> (
				progress,
				SetBusy,
				report => {
					using (var zip = TilePackageWriteManager.MakeSinglePackage (
						src, (curr, total, p) => report (curr, total, p)))
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
			txtSource.IsEnabled = !busy;
			txtSaveDir.IsEnabled = !busy;
			txtPkgName.IsEnabled = !busy;
		}

		private void Log (string line)
		{
			txtLog.AppendText ("[" + DateTime.Now.ToString ("HH:mm:ss") + "] " + line + Environment.NewLine);
			txtLog.ScrollToEnd ();
		}
	}
}
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using SGPkgMgr.Helpers;
using SGPkgMgr.Localization;
using SGPkgMgr.Services;
using Sidebar;

namespace SGPkgMgr.Views
{
	public partial class PackageConverterView: UserControl
	{
		public PackageConverterView ()
		{
			InitializeComponent ();
		}

		private void OnBrowseSource (object sender, RoutedEventArgs e)
		{
			var dlg = new System.Windows.Forms.OpenFileDialog {
				Filter = "Package files (*.sgpkg;*.sgpkgbundle)|*.sgpkg;*.sgpkgbundle|All files (*.*)|*.*"
			};
			if (dlg.ShowDialog () == System.Windows.Forms.DialogResult.OK)
				txtSource.Text = dlg.FileName;
		}
		private void OnBrowseTarget (object sender, RoutedEventArgs e)
		{
			var dlg = new System.Windows.Forms.FolderBrowserDialog ();
			if (dlg.ShowDialog () == System.Windows.Forms.DialogResult.OK)
				txtTarget.Text = dlg.SelectedPath;
		}

		private void OnConvert (object sender, RoutedEventArgs e)
		{
			var src = txtSource.Text.Trim ();
			var dst = txtTarget.Text.Trim ();
			if (string.IsNullOrEmpty (src) || !File.Exists (src))
			{
				Log (Strings.Error + ": " + Strings.SourcePackage);
				return;
			}
			if (string.IsNullOrEmpty (dst))
			{
				Log (Strings.Error + ": " + Strings.TargetPackage);
				return;
			}

			Log (Strings.Converting);
			UiTask.Run<object> (
				progress,
				SetBusy,
				report => {
					uint ver = PackageConverter.DetectPackageVersion (src);
					if (ver == 0)
						throw new InvalidDataException ("Cannot detect manifest version.");
					if (ver >= 2)
						throw new InvalidOperationException (Strings.AlreadyV2);

					using (var zip = PackageConverter.Convert (src, (c, t, p) => report (c, t, p)))
					{
						string baseName = Path.GetFileNameWithoutExtension (src);
						TilePackageWriteManager.SavePackageFile (zip, dst, baseName);
						return (object)(Strings.ConversionSuccess + Path.Combine (dst, baseName));
					}
				},
				(result, ex) => {
					if (ex != null) Log (Strings.ConversionFailed + ex.Message);
					else Log ((string)result);
				});
		}

		private void SetBusy (bool busy)
		{
			btnConvert.IsEnabled = !busy;
			txtSource.IsEnabled = !busy;
			txtTarget.IsEnabled = !busy;
		}

		private void Log (string line)
		{
			txtLog.AppendText ("[" + DateTime.Now.ToString ("HH:mm:ss") + "] " + line + Environment.NewLine);
			txtLog.ScrollToEnd ();
		}
	}
}
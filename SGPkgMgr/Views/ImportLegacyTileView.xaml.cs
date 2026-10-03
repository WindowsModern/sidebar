using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Sidebar;
using SGPkgMgr.Helpers;
using SGPkgMgr.Localization;
using SGPkgMgr.Services;

namespace SGPkgMgr.Views
{
	public partial class ImportLegacyTileView: UserControl
	{
		private LegacyTileImportResult _imported;

		public ImportLegacyTileView ()
		{
			InitializeComponent ();
		}

		// ==================== 浏览 ====================

		private void OnBrowse (object sender, RoutedEventArgs e)
		{
			var dlg = new OpenFileDialog {
				Filter = Strings.LegacyTileSourceFilter
			};
			if (dlg.ShowDialog () == true)
				txtSource.Text = dlg.FileName;
		}

		// ==================== 分析 ====================

		private void OnAnalyze (object sender, RoutedEventArgs e)
		{
			var path = txtSource.Text.Trim ();
			if (string.IsNullOrEmpty (path) || !File.Exists (path))
			{
				Log (Strings.Error + ": " + Strings.LegacyTileSource);
				return;
			}

			Log (Strings.Analyzing + Path.GetFileName (path));

			UiTask.Run<LegacyTileImportResult> (
				progress,
				SetBusy,
				report => LegacyTileImporter.Import (
					path,
					(c, t, p) => report (c, t, p),
					msg => DispatchLog (msg)),        // ★ 诊断日志回 UI
				(result, ex) => {
					if (ex != null)
					{
						Log (Strings.FailedPrefix + ex.Message);
						return;
					}
					if (result == null)
					{
						txtInfo.Text = Strings.NoTileSubclassFound;
						Log (Strings.Error + ": " + Strings.NoTileSubclassFound);
						return;
					}

					_imported = result;

					manifestEditor.LoadManifest (result.Manifest);
					filesView.SetFiles (result.Files);

					txtInfo.Text = string.Format (
						Strings.LegacyTileInfoFormat,
						result.Analysis.SourceSdk,
						result.Analysis.EntryPointTypeName,
						result.Files.Count);

					Log (Strings.Done + " " + txtInfo.Text);
				});
		}

		// ==================== 保存 ====================

		private void OnSave (object sender, RoutedEventArgs e)
		{
			if (_imported == null)
			{
				Log (Strings.Error + ": " + Strings.LoadPackageFirst);
				return;
			}

			TileManifest manifest;
			string error;
			if (!manifestEditor.TryBuildManifest (out manifest, out error))
			{
				MessageBox.Show (Strings.Error + ": " + error, "SGPkgMgr",
					MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			var sfd = new SaveFileDialog {
				Filter = "Package files (*.sgpkg)|*.sgpkg|All files (*.*)|*.*",
				FileName = Path.GetFileNameWithoutExtension (_imported.SourcePath) + ".sgpkg"
			};
			if (sfd.ShowDialog () != true) return;

			string target = sfd.FileName;
			var files = filesView.GetFiles ();

			UiTask.Run<object> (
				progress,
				SetBusy,
				report => {
					// 用临时单槽 session 复用现有打包 / 签名逻辑
					var session = new PackageEditorSession ();
					var slot = new PackageEditorSession.PackageSlot {
						EntryName = null,
						Manifest = manifest
					};
					if (files != null)
					{
						foreach (var kv in files)
							slot.Files [kv.Key] = kv.Value;
					}
					session.Slots.Add (slot);
					session.SaveTo (target);
					return null;
				},
				(_, ex) => {
					if (ex != null)
					{
						Log (Strings.FailedPrefix + ex.Message);
					}
					else
					{
						Log (Strings.Saved + ": " + target);
						MessageBox.Show (Strings.Success, "SGPkgMgr",
							MessageBoxButton.OK, MessageBoxImage.Information);
					}
				});
		}

		// ==================== 忙状态 ====================

		private void SetBusy (bool busy)
		{
			btnAnalyze.IsEnabled = !busy;
			btnBrowse.IsEnabled = !busy;
			btnSave.IsEnabled = !busy;
			txtSource.IsEnabled = !busy;
			tabEdit.IsEnabled = !busy;
		}

		// ==================== 日志 ====================

		/// <summary>从后台线程安全地把日志推到 UI。</summary>
		private void DispatchLog (string line)
		{
			if (string.IsNullOrEmpty (line)) return;
			if (Dispatcher.CheckAccess ())
				Log (line);
			else
				Dispatcher.BeginInvoke (new Action (() => Log (line)));
		}

		private void Log (string line)
		{
			txtLog.AppendText ("[" + DateTime.Now.ToString ("HH:mm:ss") + "] "
				+ line + Environment.NewLine);
			txtLog.ScrollToEnd ();
		}
	}
}
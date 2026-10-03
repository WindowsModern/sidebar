using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SGPkgMgr.Localization;
using SGPkgMgr.Services;

namespace SGPkgMgr.Views
{
	public partial class ResourceFileEditorView: UserControl
	{
		/// <summary>载入 / 保存的“当前文件”类型。新建时由按钮决定。</summary>
		private ResourceFileKind _currentKind = ResourceFileKind.Unknown;

		public ResourceFileEditorView ()
		{
			InitializeComponent ();
			UpdateStatus ();
		}

		// ============================================================
		//  浏览 / 载入
		// ============================================================

		private void OnBrowse (object sender, RoutedEventArgs e)
		{
			var dlg = new OpenFileDialog {
				Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*"
			};

			try
			{
				var dir = Path.GetDirectoryName (txtPath.Text);
				if (!string.IsNullOrEmpty (dir) && Directory.Exists (dir))
					dlg.InitialDirectory = dir;
			}
			catch { }

			if (dlg.ShowDialog () == true)
				txtPath.Text = dlg.FileName;
		}

		private void OnLoad (object sender, RoutedEventArgs e)
		{
			var path = txtPath.Text.Trim ();
			if (string.IsNullOrEmpty (path) || !File.Exists (path))
			{
				MessageBox.Show (Strings.Error + ": " + Strings.ResourceFile,
					Strings.AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			byte [] bytes;
			try { bytes = File.ReadAllBytes (path); }
			catch (Exception ex)
			{
				MessageBox.Show (Strings.FmtLoadFailed + ex.Message, Strings.AppTitle,
					MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			var kind = ResourceFileParser.DetectKind (bytes);

			// 分别加载两份，由 DetectKind 决定显示哪个
			localeEditor.LoadFromBytes (kind == ResourceFileKind.Path ? null : bytes);
			pathEditor.LoadFromBytes (kind == ResourceFileKind.Path ? bytes : null);

			_currentKind = kind == ResourceFileKind.Unknown ? ResourceFileKind.Locale : kind;

			// 切到对应 Tab（切 Tab 会触发 OnTabChanged，不要在事件里递归）
			tabType.SelectedItem = _currentKind == ResourceFileKind.Path
				? (object)tabPath
				: tabLocale;

			UpdateStatus ();

			if (kind == ResourceFileKind.Unknown)
				MessageBox.Show (Strings.MsgResourceFileKindUnknown, Strings.AppTitle,
					MessageBoxButton.OK, MessageBoxImage.Information);
		}

		// ============================================================
		//  保存
		// ============================================================

		private void OnSave (object sender, RoutedEventArgs e)
		{
			var path = txtPath.Text.Trim ();
			if (string.IsNullOrEmpty (path))
			{
				OnSaveAs (sender, e);
				return;
			}
			SaveTo (path);
		}

		private void OnSaveAs (object sender, RoutedEventArgs e)
		{
			var kind = GetCurrentKind ();

			string suggestedName = kind == ResourceFileKind.Path ? "Path.xml" : "Locale.xml";

			var sfd = new SaveFileDialog {
				Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
				FileName = suggestedName
			};

			try
			{
				var dir = Path.GetDirectoryName (txtPath.Text);
				if (!string.IsNullOrEmpty (dir) && Directory.Exists (dir))
					sfd.InitialDirectory = dir;
			}
			catch { }

			if (sfd.ShowDialog () != true) return;

			txtPath.Text = sfd.FileName;
			SaveTo (sfd.FileName);
		}

		private void SaveTo (string path)
		{
			var kind = GetCurrentKind ();
			byte [] bytes;
			string error;

			if (kind == ResourceFileKind.Path)
			{
				if (!pathEditor.HasData)
				{
					MessageBox.Show (Strings.MsgNoDataToSave, Strings.AppTitle,
						MessageBoxButton.OK, MessageBoxImage.Warning);
					return;
				}
				error = pathEditor.Validate ();
				if (error != null)
				{
					MessageBox.Show (Strings.Error + ": " + error, Strings.AppTitle,
						MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}
				bytes = pathEditor.SaveToBytes ();
			}
			else
			{
				if (!localeEditor.HasData)
				{
					MessageBox.Show (Strings.MsgNoDataToSave, Strings.AppTitle,
						MessageBoxButton.OK, MessageBoxImage.Warning);
					return;
				}
				error = localeEditor.Validate ();
				if (error != null)
				{
					MessageBox.Show (Strings.Error + ": " + error, Strings.AppTitle,
						MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}
				bytes = localeEditor.SaveToBytes ();
			}

			try { File.WriteAllBytes (path, bytes); }
			catch (Exception ex)
			{
				MessageBox.Show (Strings.Error + ": " + ex.Message, Strings.AppTitle,
					MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			UpdateStatus ();
			MessageBox.Show (string.Format (Strings.MsgResourceFileSaved, path),
				Strings.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
		}

		// ============================================================
		//  新建
		// ============================================================

		private void OnNewLocale (object sender, RoutedEventArgs e)
		{
			if (!ConfirmDiscard ()) return;

			localeEditor.LoadFromBytes (null);
			pathEditor.LoadFromBytes (null);
			_currentKind = ResourceFileKind.Locale;
			txtPath.Text = "";
			tabType.SelectedItem = tabLocale;
			UpdateStatus ();
		}

		private void OnNewPath (object sender, RoutedEventArgs e)
		{
			if (!ConfirmDiscard ()) return;

			localeEditor.LoadFromBytes (null);
			pathEditor.LoadFromBytes (null);
			_currentKind = ResourceFileKind.Path;
			txtPath.Text = "";
			tabType.SelectedItem = tabPath;
			UpdateStatus ();
		}

		private bool ConfirmDiscard ()
		{
			bool hasLocale = localeEditor.HasData;
			bool hasPath = pathEditor.HasData;
			if (!hasLocale && !hasPath) return true;

			var r = MessageBox.Show (Strings.MsgDiscardChanges, Strings.Confirm,
				MessageBoxButton.YesNo, MessageBoxImage.Question);
			return r == MessageBoxResult.Yes;
		}

		// ============================================================
		//  Tab 切换
		// ============================================================

		private void OnTabChanged (object sender, SelectionChangedEventArgs e)
		{
			// 用户手动切到另一个 Tab：把 _currentKind 跟着切，
			// 相当于“我想以这种格式保存”。
			if (ReferenceEquals (tabType.SelectedItem, tabPath))
				_currentKind = ResourceFileKind.Path;
			else if (ReferenceEquals (tabType.SelectedItem, tabLocale))
				_currentKind = ResourceFileKind.Locale;

			UpdateStatus ();
		}

		private ResourceFileKind GetCurrentKind ()
		{
			if (ReferenceEquals (tabType.SelectedItem, tabPath))
				return ResourceFileKind.Path;
			return ResourceFileKind.Locale;
		}

		// ============================================================
		//  状态
		// ============================================================

		private void UpdateStatus ()
		{
			string kindText = _currentKind == ResourceFileKind.Path
				? Strings.TabPathResources
				: (_currentKind == ResourceFileKind.Locale
					? Strings.TabLocaleResources
					: Strings.Unsupported);

			txtStatus.Text = string.Format (Strings.FmtResourceFileStatus,
				kindText,
				localeEditor.Items.Count,
				pathEditor.Items.Count);
		}
	}
}
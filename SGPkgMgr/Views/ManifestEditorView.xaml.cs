using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SGPkgMgr.Localization;
using SGPkgMgr.Models;
using Sidebar;

namespace SGPkgMgr.Views
{
	public partial class ManifestEditorView: UserControl
	{
		private readonly ObservableCollection<ApplicationEntry> _applications
			= new ObservableCollection<ApplicationEntry> ();

		public ManifestEditorView ()
		{
			InitializeComponent ();

			// 枚举
			cmbArch.ItemsSource = Enum.GetValues (typeof (ProcessorArchitecture))
				.Cast<ProcessorArchitecture> ()
				.Where (a => a != ProcessorArchitecture.Unknown)
				.ToList ();
			cmbArch.SelectedItem = ProcessorArchitecture.Neutral;

			cmbOverflow.ItemsSource = Enum.GetValues (typeof (TileOverflow));
			cmbOsMin.ItemsSource = OSVersionCatalog.Items;
			cmbOsMax.ItemsSource = OSVersionCatalog.Items;
			cmbOsMin.SelectedItem = OSVersionCatalog.Custom;
			cmbOsMax.SelectedItem = OSVersionCatalog.Custom;
			chkOsMaxUnbounded.IsChecked = true;
			lstApplications.ItemsSource = _applications;
			AddApplication (); // 至少创建一个默认 Application
		}

		private void OnBrowse (object sender, RoutedEventArgs e)
		{
			var dlg = new OpenFileDialog {
				Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*"
			};
			if (dlg.ShowDialog () == true)
				txtManifestPath.Text = dlg.FileName;
		}

		private void OnLoad (object sender, RoutedEventArgs e)
		{
			var path = txtManifestPath.Text.Trim ();
			if (string.IsNullOrEmpty (path) || !File.Exists (path))
			{
				MessageBox.Show ("Please select a valid manifest file.", "Warning",
					MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			try
			{
				// 拒绝 V1
				var doc = System.Xml.Linq.XDocument.Load (path);
				uint ver = TileManifestV1ToV2Converter.DetectVersion (doc);
				if (ver < 2)
				{
					MessageBox.Show (
						"Manifest version " + ver + " is not supported. Only version 2 or newer is supported.",
						"Unsupported", MessageBoxButton.OK, MessageBoxImage.Warning);
					return;
				}

				var manifest = TileManifest.FromFile (path);
				LoadManifest (manifest);
				MessageBox.Show ("Loaded.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
			}
			catch (Exception ex)
			{
				MessageBox.Show ("Load failed: " + ex.Message, "Error",
					MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		public void LoadManifest (TileManifest m)
		{
			// Identity
			txtName.Text = m.Identity.Name;
			txtPublisher.Text = m.Identity.Publisher;
			txtVerMajor.Text = m.Identity.Version.Major.ToString ();
			txtVerMinor.Text = m.Identity.Version.Minor.ToString ();
			txtVerBuild.Text = m.Identity.Version.Build.ToString ();
			txtVerRev.Text = m.Identity.Version.Revision.ToString ();
			cmbArch.SelectedItem = m.Identity.ProcessorArchitecture;

			// Properties
			txtDisplayName.Text = m.Properties.DisplayName;
			txtPublisherDisplayName.Text = m.Properties.PublisherDisplayName;
			txtPropertiesLogo.Text = m.Properties.Logo;
			txtDescription.Text = m.Properties.Description;

			// Prerequisites
			// Prerequisites
			if (m.Prerequisites != null)
			{
				ApplyVersionToUI (m.Prerequisites.OSMinVersion,
					cmbOsMin, txtOsMinMajor, txtOsMinMinor, txtOsMinBuild, txtOsMinRev);

				if (m.Prerequisites.OSMaxVersion == null)
				{
					chkOsMaxUnbounded.IsChecked = true;
				}
				else
				{
					chkOsMaxUnbounded.IsChecked = false;
					ApplyVersionToUI (m.Prerequisites.OSMaxVersion,
						cmbOsMax, txtOsMaxMajor, txtOsMaxMinor, txtOsMaxBuild, txtOsMaxRev);
				}
			}
			else
			{
				cmbOsMin.SelectedItem = OSVersionCatalog.Custom;
				cmbOsMax.SelectedItem = OSVersionCatalog.Custom;
				chkOsMaxUnbounded.IsChecked = true;
			}

			// Applications
			_applications.Clear ();
			foreach (var app in m.Applications)
				_applications.Add (ApplicationEntry.FromModel (app));
			if (_applications.Count == 0)
				AddApplication ();
			lstApplications.SelectedIndex = 0;
		}
		/// <summary>从界面收集数据构建清单。失败返回 false 并输出原因。</summary>
		public bool TryBuildManifest (out TileManifest manifest, out string error)
		{
			manifest = null;
			error = null;
			try
			{
				ProcessorArchitecture arch = ProcessorArchitecture.Neutral;
				if (cmbArch.SelectedItem is ProcessorArchitecture)
					arch = (ProcessorArchitecture)cmbArch.SelectedItem;

				var identity = new TileIdentity (
					txtName.Text.Trim (),
					txtPublisher.Text.Trim (),
					ParseVersion (txtVerMajor.Text, txtVerMinor.Text, txtVerBuild.Text, txtVerRev.Text),
					arch);

				var properties = new TileProperties (
					txtDisplayName.Text.Trim (),
					txtPublisherDisplayName.Text.Trim (),
					txtDescription.Text,
					txtPropertiesLogo.Text.Trim ());

				Sidebar.Version osMin = ReadVersionFromUI (
	cmbOsMin, txtOsMinMajor, txtOsMinMinor, txtOsMinBuild, txtOsMinRev);

				Sidebar.Version? osMax = null;
				if (chkOsMaxUnbounded.IsChecked != true)
				{
					osMax = ReadVersionFromUI (
						cmbOsMax, txtOsMaxMajor, txtOsMaxMinor, txtOsMaxBuild, txtOsMaxRev);
				}

				var prereq = new TilePrerequisites (osMin, osMax);

				var apps = _applications.Select (a => a.ToModel ()).ToList ();

				manifest = new TileManifest (identity, properties, prereq, apps);
				return true;
			}
			catch (Exception ex)
			{
				error = ex.Message;
				return false;
			}
		}
		private void OnSave (object sender, RoutedEventArgs e)
		{
			var path = txtManifestPath.Text.Trim ();
			if (string.IsNullOrEmpty (path))
			{
				var dlg = new SaveFileDialog {
					Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
					FileName = "Manifest.xml"
				};
				if (dlg.ShowDialog () != true) return;
				path = dlg.FileName;
				txtManifestPath.Text = path;
			}

			TileManifest manifest;
			string error;
			if (!TryBuildManifest (out manifest, out error))
			{
				MessageBox.Show (Strings.Error + ": " + error, "SGPkgMgr",
					MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			try
			{
				manifest.ToFile (path);
				MessageBox.Show (Strings.Success, "SGPkgMgr",
					MessageBoxButton.OK, MessageBoxImage.Information);
			}
			catch (Exception ex)
			{
				MessageBox.Show (Strings.Error + ": " + ex.Message, "SGPkgMgr",
					MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void OnApplicationSelected (object sender, SelectionChangedEventArgs e) { }

		private void OnAddApplication (object sender, RoutedEventArgs e)
		{
			AddApplication ();
		}
		private void AddApplication ()
		{
			_applications.Add (new ApplicationEntry ());
			lstApplications.SelectedIndex = _applications.Count - 1;
		}
		private void OnRemoveApplication (object sender, RoutedEventArgs e)
		{
			var entry = lstApplications.SelectedItem as ApplicationEntry;
			if (entry != null)
				_applications.Remove (entry);
		}

		// -------- helpers --------
		private static Sidebar.Version ParseVersion (string major, string minor, string build, string rev)
		{
			return new Sidebar.Version (
				ParseU16 (major), ParseU16 (minor), ParseU16 (build), ParseU16 (rev));
		}
		private static Sidebar.Version ParseVersion (string expr)
		{
			if (string.IsNullOrWhiteSpace (expr)) return new Sidebar.Version ();
			try { return Sidebar.Version.Parse (expr); }
			catch { return new Sidebar.Version (); }
		}
		/// <summary>
		/// 解析可选的版本号：空白输入返回 null，表示"未来版本都支持"。
		/// </summary>
		private static Sidebar.Version? ParseOptionalVersion (string expr)
		{
			if (string.IsNullOrWhiteSpace (expr)) return null;
			try { return Sidebar.Version.Parse (expr); }
			catch { return null; }
		}
		private static ushort ParseU16 (string s)
		{
			ushort v;
			return ushort.TryParse (s, out v) ? v : (ushort)0;
		}
		private void OnConvertFromV1 (object sender, RoutedEventArgs e)
		{
			// 选择 V1 清单 → 转换 → 另存为 V2
			var ofd = new OpenFileDialog {
				Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
				Title = Strings.ConvertFromV1Title
			};
			if (ofd.ShowDialog () != true) return;

			try
			{
				var doc = System.Xml.Linq.XDocument.Load (ofd.FileName);
				uint ver = TileManifestV1ToV2Converter.DetectVersion (doc);
				if (ver >= 2)
				{
					MessageBox.Show (Strings.AlreadyV2, Strings.Warning,
						MessageBoxButton.OK, MessageBoxImage.Information);
					return;
				}

				var manifest = TileManifestV1ToV2Converter.Convert (doc);

				var sfd = new SaveFileDialog {
					Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
					FileName = "Manifest.xml"
				};
				if (sfd.ShowDialog () != true) return;

				manifest.ToFile (sfd.FileName);
				txtManifestPath.Text = sfd.FileName;
				LoadManifest (manifest);   // 顺便把转换结果填进界面
				MessageBox.Show (Strings.Success, Strings.Success,
					MessageBoxButton.OK, MessageBoxImage.Information);
			}
			catch (Exception ex)
			{
				MessageBox.Show (Strings.ConversionFailed + ex.Message, Strings.Error,
					MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}
		// -------- OS 版本 UI 辅助 --------

		/// <summary>
		/// 把版本号回填到 UI。
		/// v == null 时勾选"支持所有未来版本"并禁用全部相关控件（用于 OSMaxVersion）。
		/// unbounded 为 null 时表示不涉及"无上限"概念（用于 OSMinVersion）。
		/// </summary>
		private void ApplyVersionToUI (
			Sidebar.Version? v,
			ComboBox combo,
			TextBox major, TextBox minor, TextBox build, TextBox rev,
			CheckBox unbounded = null)
		{
			// ---------- 无上限 ----------
			if (v == null)
			{
				if (unbounded != null)
					unbounded.IsChecked = true;

				combo.SelectedItem = null;
				combo.IsEnabled = false;

				major.Text = "";
				minor.Text = "";
				build.Text = "";
				rev.Text = "";

				major.IsEnabled = false;
				minor.IsEnabled = false;
				build.IsEnabled = false;
				rev.IsEnabled = false;
				return;
			}

			// ---------- 有具体版本 ----------
			if (unbounded != null)
				unbounded.IsChecked = false;

			var value = v.Value;
			var match = OSVersionCatalog.Find (value);
			combo.SelectedItem = match;
			combo.IsEnabled = true;

			major.Text = value.Major.ToString ();
			minor.Text = value.Minor.ToString ();
			build.Text = value.Build.ToString ();
			rev.Text = value.Revision.ToString ();

			bool isCustom = match == null || match.IsCustom;
			major.IsEnabled = isCustom;
			minor.IsEnabled = isCustom;
			build.IsEnabled = isCustom;
			rev.IsEnabled = isCustom;
		}

		private static Sidebar.Version ReadVersionFromUI (
			ComboBox combo,
			TextBox major, TextBox minor, TextBox build, TextBox rev)
		{
			var item = combo.SelectedItem as StartupOS;
			if (item != null && !item.IsCustom)
				return item.Version;

			return new Sidebar.Version (
				ParseU16 (major.Text),
				ParseU16 (minor.Text),
				ParseU16 (build.Text),
				ParseU16 (rev.Text));
		}

		private void OnOsMinChanged (object sender, SelectionChangedEventArgs e)
		{
			var item = cmbOsMin.SelectedItem as StartupOS;
			bool isCustom = item == null || item.IsCustom;
			txtOsMinMajor.IsEnabled = isCustom;
			txtOsMinMinor.IsEnabled = isCustom;
			txtOsMinBuild.IsEnabled = isCustom;
			txtOsMinRev.IsEnabled = isCustom;

			if (!isCustom)
			{
				txtOsMinMajor.Text = item.Version.Major.ToString ();
				txtOsMinMinor.Text = item.Version.Minor.ToString ();
				txtOsMinBuild.Text = item.Version.Build.ToString ();
				txtOsMinRev.Text = item.Version.Revision.ToString ();
			}
		}

		private void OnOsMaxChanged (object sender, SelectionChangedEventArgs e)
		{
			// "无上限" 勾选时，max 控件应保持禁用；仅自定义选择时启用。
			UpdateOsMaxEnabledState ();

			var item = cmbOsMax.SelectedItem as StartupOS;
			if (item != null && !item.IsCustom && chkOsMaxUnbounded.IsChecked != true)
			{
				txtOsMaxMajor.Text = item.Version.Major.ToString ();
				txtOsMaxMinor.Text = item.Version.Minor.ToString ();
				txtOsMaxBuild.Text = item.Version.Build.ToString ();
				txtOsMaxRev.Text = item.Version.Revision.ToString ();
			}
		}

		private void OnOsMaxUnboundedChanged (object sender, RoutedEventArgs e)
		{
			UpdateOsMaxEnabledState ();
		}

		private void UpdateOsMaxEnabledState ()
		{
			bool unbounded = chkOsMaxUnbounded.IsChecked == true;

			cmbOsMax.IsEnabled = !unbounded;

			if (unbounded)
			{
				txtOsMaxMajor.IsEnabled = false;
				txtOsMaxMinor.IsEnabled = false;
				txtOsMaxBuild.IsEnabled = false;
				txtOsMaxRev.IsEnabled = false;
				return;
			}

			var item = cmbOsMax.SelectedItem as StartupOS;
			bool isCustom = item == null || item.IsCustom;
			txtOsMaxMajor.IsEnabled = isCustom;
			txtOsMaxMinor.IsEnabled = isCustom;
			txtOsMaxBuild.IsEnabled = isCustom;
			txtOsMaxRev.IsEnabled = isCustom;
		}
	}
}
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Sidebar;
namespace SGReader
{
	public partial class MainForm: Form
	{
		public MainForm ()
		{
			InitializeComponent ();
			webBrowser1.Url = new Uri (Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "HTML\\Report.html"));
		}
		private void button2_Click (object sender, EventArgs e)
		{
			openFileDialog1.Filter = String.Format (
				"{0}|*.sgpkg;*.sgpkgbundle|{1}|*.*",
				"Sidebar Gadget Package (*.sgpkg, *.sgpkgbundle)",
				"All Files (*.*)"
			);
			var dlgres = openFileDialog1.ShowDialog (this);
			if (dlgres == DialogResult.OK)
			{
				textBox1.Text = openFileDialog1.FileName;
			}
		}
		private void button1_Click (object sender, EventArgs e)
		{
			if (string.IsNullOrWhiteSpace (textBox1?.Text))
				button2.PerformClick ();
			ClearFormContent ();
			try
			{
				CurrentDocument.InvokeScript ("setElementTextByName", new [] { "file", textBox1.Text });
				using (var reader = TilePackageReadManager.GetPackage (textBox1?.Text))
				{
					CurrentDocument.InvokeScript ("setElementTextByName", new [] { "valid", "true" });
					switch (reader.PackageType)
					{
						case TilePackageType.SubPackage:
						case TilePackageType.Single:
							{
								CurrentDocument.InvokeScript ("setElementTextByName", new [] { "type", "Single" });
								var aread = reader as TilePackage;
								var m = aread?.Manifest;
								var i = m?.Identity;
								SetElementTextByName ("name", i?.Name);
								SetElementTextByName ("publisher", i?.Publisher);
								SetElementTextByName ("version", i?.Version.Expression);
								SetElementTextByName ("architecture", i?.ProcessorArchitecture.ToString ());
								SetElementTextByName ("package_family_name", i?.FamilyName);
								SetElementTextByName ("package_full_name", i?.FullName);
								SetElementTextByName ("publisher_id", i?.PublisherId);
								var p = m?.Properties;
								SetElementTextByName ("display_name", p?.DisplayName);
								SetElementTextByName ("publisher_display_name", p?.Publisher);
								SetElementTextByName ("description", p?.Description);
								SetElementTextByName ("logo", p?.Logo);
								var pre = m?.Prerequisites;
								SetElementTextByName ("os_min_version", pre?.OSMinVersion.Expression);
								SetElementTextByName ("os_max_version_tested", pre?.OSMaxVersionTested.Expression);
								var ve = m?.VisualElements;
								var rs = ve?.RailStyle;
								SetElementTextByName ("min_height", rs?.MinHeight);
								SetElementTextByName ("max_height", rs?.MaxHeight);
								SetElementTextByName ("default_height", rs?.DefaultHeight);
								SetElementTextByName ("can_pin_bottom", rs?.CanPinBottom ?? false ? "true" : "false");
								SetElementTextByName ("tile_has_flyout_window", rs?.TileHasFlyout ?? false ? "true" : "false");
								SetElementTextByName ("flyout_window_width", rs?.FlyoutWidth);
								SetElementTextByName ("flyout_window_height", rs?.FlyoutHeight);
								SetElementTextByName ("flyout_window_can_resize", rs?.FlyoutCanResize ?? false ? "true" : "false");
								SetElementTextByName ("overflow_mode", rs?.Overflow.ToString ());
								SetElementTextByName ("tile_display_name", rs?.DisplayName);
								SetElementTextByName ("tile_has_properties_window", rs?.TileHasProperties ?? false ? "true" : "false");
								SetElementTextByName ("tile_logo", rs?.Logo);
							}
							break;
						case TilePackageType.Bundle:
							{
								CurrentDocument.InvokeScript ("setElementTextByName", new [] { "type", "Bundle" });
								var bread = reader as TilePackageBundle;
								var m = bread?.Manifest;
								var i = m?.Identity;
								SetElementTextByName ("name", i?.Name);
								SetElementTextByName ("publisher", i?.Publisher);
								SetElementTextByName ("version", i?.Version.Expression);
								SetElementTextByName ("architecture", String.Join (", ", bread?.Manifest?.Items?.Select (d => d?.ProcessorArchitecture.ToString ())));
								SetElementTextByName ("package_family_name", i?.FamilyName);
								SetElementTextByName ("package_full_name", i?.FullName);
								SetElementTextByName ("publisher_id", i?.PublisherId);
								var sp = bread.Packages [0];
								var sm = sp?.Manifest;
								var p = sm?.Properties;
								SetElementTextByName ("display_name", p?.DisplayName);
								SetElementTextByName ("publisher_display_name", p?.Publisher);
								SetElementTextByName ("description", p?.Description);
								SetElementTextByName ("logo", p?.Logo);
								var pre = sm?.Prerequisites;
								SetElementTextByName ("os_min_version", pre?.OSMinVersion.Expression);
								SetElementTextByName ("os_max_version_tested", pre?.OSMaxVersionTested.Expression);
								var ve = sm?.VisualElements;
								var rs = ve?.RailStyle;
								SetElementTextByName ("min_height", rs?.MinHeight);
								SetElementTextByName ("max_height", rs?.MaxHeight);
								SetElementTextByName ("default_height", rs?.DefaultHeight);
								SetElementTextByName ("can_pin_bottom", rs?.CanPinBottom ?? false ? "true" : "false");
								SetElementTextByName ("tile_has_flyout_window", rs?.TileHasFlyout ?? false ? "true" : "false");
								SetElementTextByName ("flyout_window_width", rs?.FlyoutWidth);
								SetElementTextByName ("flyout_window_height", rs?.FlyoutHeight);
								SetElementTextByName ("flyout_window_can_resize", rs?.FlyoutCanResize ?? false ? "true" : "false");
								SetElementTextByName ("overflow_mode", rs?.Overflow.ToString ());
								SetElementTextByName ("tile_display_name", rs?.DisplayName);
								SetElementTextByName ("tile_has_properties_window", rs?.TileHasProperties ?? false ? "true" : "false");
								SetElementTextByName ("tile_logo", rs?.Logo);
							}
							break;
						default:
							CurrentDocument.InvokeScript ("setElementTextByName", new [] { "valid", "false" });
							break;
					}
				}
			}
			catch (Exception ex)
			{
				CurrentDocument.InvokeScript ("setElementTextById", new [] { "msg", ex?.InnerException?.Message ?? ex?.Message });
				CurrentDocument.InvokeScript ("setElementTextByName", new [] { "valid", "false" });
				MessageBox.Show (this, ex?.InnerException?.Message ?? ex?.Message, ex?.GetType ().ToString (), MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}
		private HtmlDocument CurrentDocument => webBrowser1.Document;
		private object HtmlDocumentInvokeScript (string funcName, params object [] args) => CurrentDocument.InvokeScript (funcName, args);
		private object HtmlDocumentInvokeScript (string funcName) => CurrentDocument.InvokeScript (funcName);
		private void ClearFormContent ()
		{
			HtmlDocumentInvokeScript ("setElementTextById", "msg", "");
			var namelist = new string [] {
				"file",
				"valid",
				"type",
				"name",
				"publisher",
				"version",
				"architecture",
				"package_family_name",
				"package_full_name",
				"publisher_id",
				"display_name",
				"publisher_display_name",
				"description",
				"os_min_version",
				"os_max_version_tested",
				"min_height",
				"max_height",
				"default_height",
				"can_pin_bottom",
				"tile_has_flyout_window",
				"flyout_window_width",
				"flyout_window_height",
				"flyout_window_can_resize",
				"overflow_mode",
				"tile_display_name",
				"tile_has_properties_window",
				"tile_logo"
			};
			foreach (var item in namelist)
				HtmlDocumentInvokeScript ("setElementTextByName", item, "");
		}
		private void SetElementTextByName (string name, object content) =>
			HtmlDocumentInvokeScript ("setElementTextByName", name, content);
		private void SetElementTextById (string id, object content) =>
			HtmlDocumentInvokeScript ("setElementTextById", id, content);
		private bool setOpenLoad = false;
		public void SetOpenedFile (string pkgFilePath)
		{
			textBox1.Text = pkgFilePath;
			setOpenLoad = true;
		}
		private void webBrowser1_DocumentCompleted (object sender, WebBrowserDocumentCompletedEventArgs e)
		{
			if (setOpenLoad)
			{
				setOpenLoad = false;
				button1?.PerformClick ();
			}
		}
	}
}

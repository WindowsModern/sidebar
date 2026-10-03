using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using SGPkgMgr.Localization;
using SGPkgMgr.Services;

namespace SGPkgMgr.Views
{
	public partial class LocaleResourceEditorView: UserControl
	{
		public ObservableCollection<LocaleResourceItem> Items { get; private set; }

		public LocaleResourceEditorView ()
		{
			InitializeComponent ();
			Items = new ObservableCollection<LocaleResourceItem> ();
			gridResources.ItemsSource = Items;
			UpdateInfo ();
		}

		// ============================================================
		//  对外接口
		// ============================================================

		/// <summary>从字节加载；bytes 为 null / 空时清空。</summary>
		public void LoadFromBytes (byte [] bytes)
		{
			Items.Clear ();
			var list = ResourceFileParser.ParseLocale (bytes);
			foreach (var item in list)
				Items.Add (item);

			if (Items.Count > 0)
				gridResources.SelectedIndex = 0;
			else
				gridEntries.ItemsSource = null;

			UpdateInfo ();
		}

		/// <summary>序列化为 Locale.xml 字节。</summary>
		public byte [] SaveToBytes ()
		{
			return ResourceFileParser.SerializeLocale (Items);
		}

		public bool HasData { get { return Items.Count > 0; } }

		/// <summary>返回 null 表示通过，否则返回错误消息。</summary>
		public string Validate ()
		{
			var resSet = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

			foreach (var res in Items)
			{
				if (string.IsNullOrWhiteSpace (res.ResourceName))
					return Strings.ResourceEmptyName;
				if (!resSet.Add (res.ResourceName))
					return string.Format (Strings.ResourceDupName, res.ResourceName);

				var locSet = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
				foreach (var entry in res.Entries)
				{
					if (string.IsNullOrWhiteSpace (entry.LocaleName))
						return string.Format (Strings.ResourceEmptyLanguage, res.ResourceName);
					if (!locSet.Add (entry.LocaleName))
						return string.Format (Strings.ResourceDupLanguage,
							res.ResourceName, entry.LocaleName);
				}
			}
			return null;
		}

		// ============================================================
		//  事件
		// ============================================================

		private LocaleResourceItem CurrentResource
		{
			get { return gridResources.SelectedItem as LocaleResourceItem; }
		}

		private void OnResourceSelected (object sender, SelectionChangedEventArgs e)
		{
			var item = CurrentResource;
			gridEntries.ItemsSource = item != null ? item.Entries : null;
			UpdateInfo ();
		}

		private void OnAddResource (object sender, RoutedEventArgs e)
		{
			var item = new LocaleResourceItem { ResourceName = "NewResource" };

			string preferred = GetPreferredLocale ();
			item.Entries.Add (new LocaleEntry { LocaleName = preferred, Value = "" });
			if (!string.Equals (preferred, "en-US", StringComparison.OrdinalIgnoreCase))
				item.Entries.Add (new LocaleEntry { LocaleName = "en-US", Value = "" });

			Items.Add (item);
			gridResources.SelectedItem = item;
			UpdateInfo ();
		}

		private void OnRemoveResource (object sender, RoutedEventArgs e)
		{
			var item = CurrentResource;
			if (item != null)
			{
				Items.Remove (item);
				UpdateInfo ();
			}
		}

		private void OnAddLanguage (object sender, RoutedEventArgs e)
		{
			var item = CurrentResource;
			if (item == null) return;
			item.Entries.Add (new LocaleEntry { LocaleName = "en-US", Value = "" });
		}

		private void OnRemoveLanguage (object sender, RoutedEventArgs e)
		{
			var item = CurrentResource;
			if (item == null) return;
			var entry = gridEntries.SelectedItem as LocaleEntry;
			if (entry != null) item.Entries.Remove (entry);
		}

		private void UpdateInfo ()
		{
			txtInfo.Text = string.Format (Strings.ResourceInfoLocale,
				Items.Count,
				CurrentResource != null ? CurrentResource.Entries.Count : 0);
		}

		private static string GetPreferredLocale ()
		{
			try
			{
				var c = Thread.CurrentThread.CurrentUICulture;
				if (!string.IsNullOrWhiteSpace (c.Name)) return c.Name;
			}
			catch { }
			try
			{
				var c = CultureInfo.InstalledUICulture;
				if (!string.IsNullOrWhiteSpace (c.Name)) return c.Name;
			}
			catch { }
			return "en-US";
		}
	}
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using SGPkgMgr.Localization;
using SGPkgMgr.Services;

namespace SGPkgMgr.Views
{
	public partial class PathResourceEditorView: UserControl
	{
		public ObservableCollection<PathResourceItem> Items { get; private set; }

		public PathResourceEditorView ()
		{
			InitializeComponent ();
			Items = new ObservableCollection<PathResourceItem> ();
			gridResources.ItemsSource = Items;
			UpdateInfo ();
		}

		// ============================================================
		//  对外接口
		// ============================================================

		public void LoadFromBytes (byte [] bytes)
		{
			Items.Clear ();
			var list = ResourceFileParser.ParsePath (bytes);
			foreach (var item in list)
				Items.Add (item);

			if (Items.Count > 0)
				gridResources.SelectedIndex = 0;
			else
				gridEntries.ItemsSource = null;

			UpdateInfo ();
		}

		public byte [] SaveToBytes ()
		{
			return ResourceFileParser.SerializePath (Items);
		}

		public bool HasData { get { return Items.Count > 0; } }

		public string Validate ()
		{
			var resSet = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

			foreach (var res in Items)
			{
				if (string.IsNullOrWhiteSpace (res.ResourceName))
					return Strings.ResourceEmptyName;
				if (!resSet.Add (res.ResourceName))
					return string.Format (Strings.ResourceDupName, res.ResourceName);

				var scaleSet = new HashSet<int> ();
				foreach (var entry in res.Entries)
				{
					if (entry.Scale <= 0)
						return string.Format (Strings.ResourceEmptyScale, res.ResourceName);
					if (!scaleSet.Add (entry.Scale))
						return string.Format (Strings.ResourceDupScale,
							res.ResourceName, entry.Scale);
				}
			}
			return null;
		}

		// ============================================================
		//  事件
		// ============================================================

		private PathResourceItem CurrentResource
		{
			get { return gridResources.SelectedItem as PathResourceItem; }
		}

		private void OnResourceSelected (object sender, SelectionChangedEventArgs e)
		{
			var item = CurrentResource;
			gridEntries.ItemsSource = item != null ? item.Entries : null;
			UpdateInfo ();
		}

		private void OnAddResource (object sender, RoutedEventArgs e)
		{
			var item = new PathResourceItem { ResourceName = "NewResource" };
			item.Entries.Add (new PathEntry { Scale = 100, Path = "" });

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

		private void OnAddScale (object sender, RoutedEventArgs e)
		{
			var item = CurrentResource;
			if (item == null) return;
			item.Entries.Add (new PathEntry { Scale = 100, Path = "" });
		}

		private void OnRemoveScale (object sender, RoutedEventArgs e)
		{
			var item = CurrentResource;
			if (item == null) return;
			var entry = gridEntries.SelectedItem as PathEntry;
			if (entry != null) item.Entries.Remove (entry);
		}

		private void UpdateInfo ()
		{
			txtInfo.Text = string.Format (Strings.ResourceInfoPath,
				Items.Count,
				CurrentResource != null ? CurrentResource.Entries.Count : 0);
		}
	}
}
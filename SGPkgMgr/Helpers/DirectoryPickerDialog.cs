using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SGPkgMgr.Localization;

namespace SGPkgMgr.Helpers
{
	/// <summary>
	/// 从一组候选目录里选一个。返回 "" 表示根目录；取消返回 null。
	/// </summary>
	public static class DirectoryPickerDialog
	{
		private sealed class Item
		{
			public string Path { get; set; }        // "" = 根
			public string Display { get; set; }
			public override string ToString () => Display;
		}

		public static string Show (
			Window owner, string title, string prompt, IEnumerable<string> directories)
		{
			var dlg = new Window {
				Title = title,
				Width = 460,
				Height = 420,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				Owner = owner,
				ResizeMode = ResizeMode.CanResize,
				ShowInTaskbar = false
			};

			var grid = new Grid { Margin = new Thickness (12) };
			grid.RowDefinitions.Add (new RowDefinition { Height = GridLength.Auto });
			grid.RowDefinitions.Add (new RowDefinition { Height = new GridLength (1, GridUnitType.Star) });
			grid.RowDefinitions.Add (new RowDefinition { Height = GridLength.Auto });

			var promptBlock = new TextBlock {
				Text = prompt,
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness (0, 0, 0, 8)
			};
			Grid.SetRow (promptBlock, 0);
			grid.Children.Add (promptBlock);

			var list = new ListBox { Margin = new Thickness (0, 0, 0, 8) };
			Grid.SetRow (list, 1);
			grid.Children.Add (list);

			var items = new List<Item> ();
			items.Add (new Item { Path = "", Display = Strings.RootDirDisplay });
			foreach (var d in directories)
			{
				if (string.IsNullOrEmpty (d)) continue;
				items.Add (new Item { Path = d, Display = "/" + d });
			}
			list.ItemsSource = items;
			list.SelectedIndex = 0;

			var buttons = new StackPanel {
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Right
			};
			var okBtn = new Button {
				Content = Strings.Ok,
				Width = 75,
				IsDefault = true
			};
			var cancelBtn = new Button {
				Content = Strings.Cancel,
				Width = 75,
				IsCancel = true,
				Margin = new Thickness (8, 0, 0, 0)
			};
			okBtn.Click += (s, e) => {
				var sel = list.SelectedItem as Item;
				if (sel == null) return;
				dlg.DialogResult = true;
			};
			buttons.Children.Add (okBtn);
			buttons.Children.Add (cancelBtn);

			Grid.SetRow (buttons, 2);
			grid.Children.Add (buttons);

			dlg.Content = grid;
			return dlg.ShowDialog () == true
				? (list.SelectedItem as Item)?.Path
				: null;
		}
	}
}
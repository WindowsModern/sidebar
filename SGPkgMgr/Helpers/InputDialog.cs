using System.Windows;
using System.Windows.Controls;
using SGPkgMgr.Localization;

namespace SGPkgMgr.Helpers
{
	/// <summary>
	/// 极简文本输入对话框。返回用户输入的字符串，取消返回 null。
	/// </summary>
	public static class InputDialog
	{
		public static string Show (Window owner, string title, string prompt, string defaultValue)
		{
			var dlg = new Window {
				Title = title,
				Width = 420,
				SizeToContent = SizeToContent.Height,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				Owner = owner,
				ResizeMode = ResizeMode.NoResize,
				ShowInTaskbar = false
			};

			var grid = new Grid { Margin = new Thickness (12) };
			grid.RowDefinitions.Add (new RowDefinition { Height = GridLength.Auto });
			grid.RowDefinitions.Add (new RowDefinition { Height = GridLength.Auto });
			grid.RowDefinitions.Add (new RowDefinition { Height = GridLength.Auto });

			var promptBlock = new TextBlock {
				Text = prompt,
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness (0, 0, 0, 8)
			};
			Grid.SetRow (promptBlock, 0);
			grid.Children.Add (promptBlock);

			var textBox = new TextBox {
				Text = defaultValue ?? "",
				Padding = new Thickness (4, 3, 4, 3)
			};
			Grid.SetRow (textBox, 1);
			grid.Children.Add (textBox);

			var buttons = new StackPanel {
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Right,
				Margin = new Thickness (0, 12, 0, 0)
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
			okBtn.Click += (s, e) => { dlg.DialogResult = true; };
			buttons.Children.Add (okBtn);
			buttons.Children.Add (cancelBtn);

			Grid.SetRow (buttons, 2);
			grid.Children.Add (buttons);

			dlg.Content = grid;
			dlg.Loaded += (s, e) => { textBox.Focus (); textBox.SelectAll (); };

			return dlg.ShowDialog () == true ? textBox.Text : null;
		}
	}
}
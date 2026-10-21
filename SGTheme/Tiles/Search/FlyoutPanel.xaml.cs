using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SGTheme.Tiles.Search
{
	/// <summary>
	/// FlyoutPanel.xaml 的交互逻辑
	/// </summary>
	public partial class FlyoutPanel: UserControl
	{
		public FlyoutPanel ()
		{
			InitializeComponent ();
			SearchProviderSelect.SelectedValuePath = "Key";
		}
		public event TextChangedEventHandler TextChanged;
		public event RoutedEventHandler SearchClick;
		private void SearchText_TextChanged (object sender, TextChangedEventArgs e)
		{
			TextChanged?.Invoke (sender, e);
		}
		public string Text
		{
			get { return SearchText.Text; }
			set { SearchText.Text = value; }
		}
		private void SearchProviderSelect_SelectionChanged (object sender, SelectionChangedEventArgs e)
		{
		}
		private void Button_Click (object sender, RoutedEventArgs e)
		{
			SearchClick?.Invoke (sender, e);
		}
		private void SearchText_KeyDown (object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				SearchClick?.Invoke (sender, e);
				e.Handled = true; // 防止“叮”一声
			}
		}
	}
}

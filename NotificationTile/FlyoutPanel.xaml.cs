using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace WindowsModern.NotificationHistoryTile
{
	/// <summary>
	/// FlyoutPanel.xaml 的交互逻辑
	/// </summary>
	public partial class FlyoutPanel: UserControl
	{
		public FlyoutPanel ()
		{
			InitializeComponent ();
		}
		private void GotoRulesAlertButton_Click (object sender, RoutedEventArgs e)
		{
			try
			{
				Process.Start ("explorer.exe", "shell:::{05d7b0f4-2121-4eff-bf6b-ed3f69b894d9}");
			}
			catch
			{
				try
				{
					Process.Start ("rundll32.exe", "shell32.dll,Options_RunDLL 1");
				}
				catch { }
			}
		}
		private void Button_Click (object sender, RoutedEventArgs e)
		{
			var button = sender as Button;
			if (button == null) return;
			var data = button.DataContext as NotificationStoreData;
			if (data == null) return;
			data.IsRead = true;
			NotificationClicked?.Invoke (this, data);
		}
		public event NotificationClickedHandler NotificationClicked;
	}
}

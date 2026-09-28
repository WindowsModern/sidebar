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
using System.Windows.Shapes;
using System.Windows.Threading;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using FormContextMenu = System.Windows.Forms.ContextMenu;
using Sidebar;
namespace Sidebar
{
	/// <summary>
	/// Sidebar.xaml 的交互逻辑
	/// </summary>
	public partial class SidebarWindow: Window
	{
		public SidebarWindow ()
		{
			InitializeComponent ();
			var cc = App.CurrentUserConfig;
			
			

		}
		private void Window_Loaded (object sender, RoutedEventArgs e)
		{
			App.CurrentUserConfig.PropertyChanged -= CurrentUserConfig_PropertyChanged;
			App.CurrentUserConfig.PropertyChanged += CurrentUserConfig_PropertyChanged;
		}
		private void CurrentUserConfig_PropertyChanged (object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			var cuc = App.CurrentUserConfig;
			switch (e.PropertyName)
			{
				case nameof (cuc.Direction):
					break;
			}
		}
		private void Window_Closed (object sender, EventArgs e)
		{
			App.CurrentUserConfig.PropertyChanged -= CurrentUserConfig_PropertyChanged;
		}
	}
}

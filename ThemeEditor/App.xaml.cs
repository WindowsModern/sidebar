using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using Sidebar;
namespace ThemeEditor
{
	/// <summary>
	/// App.xaml 的交互逻辑
	/// </summary>
	public partial class App: Application
	{
		private void Application_Startup (object sender, StartupEventArgs e)
		{
			Resources.Add ("GlobalConfig", SidebarConfig.Global);
			Resources.Add ("CurrentUserConfig", SidebarConfig.Global);
			Utilities.ReleaseLargeResourcesAsync ();
		}
	}
}

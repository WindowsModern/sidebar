using System.Windows;
using System.Windows.Data;
using Sidebar;
namespace SGTheme
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
			if (!Resources.Contains ("UtilSidebarIsReversedBinding"))
				Resources.Add ("UtilSidebarIsReversedBinding", new Binding ("IsReversed") { Source = SidebarConfig.Global });
			if (!Resources.Contains ("UtilSystemIsTaskbarTopBinding"))
				Resources.Add ("UtilSystemIsTaskbarTopBinding", new Binding ("IsTaskbarTop") { Source = SidebarConfig.Global });
			Utilities.ReleaseLargeResourcesAsync ();
		}
	}
}

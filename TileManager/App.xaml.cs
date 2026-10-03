using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Sidebar;

namespace TileManager
{
	/// <summary>
	/// App.xaml 的交互逻辑
	/// </summary>
	public partial class App: Application
	{
		internal static ProgramFolder ProgramFolder { get; } = ProgramFolder.GlobalFolder;
		internal static ProgramFolder CurrentUserFolder { get; } = ProgramFolder.CurrentUserFolder;
		internal static SidebarConfig GlobalConfig { get; } = new SidebarConfig (ProgramFolder);
		internal static SidebarConfig CurrentUserConfig { get; } = new SidebarConfig (CurrentUserFolder);
		internal static Sidebar.TileManager TileMgr { get; } = new Sidebar.TileManager ();
		internal static Mutex GlobalMutex = null;
		internal const string AppIdentity = "WindowsModern.Sidebar!TileManager";
		private void Application_Startup (object sender, StartupEventArgs e)
		{
			Directory.SetCurrentDirectory (AppDomain.CurrentDomain.BaseDirectory);
			bool createdNew = false;
			GlobalMutex = new Mutex (true, AppIdentity, out createdNew);
			if (!createdNew)
			{
				Shutdown ();
				return;
			}
			try
			{
				System.Windows.Forms.Application.EnableVisualStyles ();
				System.Windows.Forms.Application.SetCompatibleTextRenderingDefault (false);
			}
			catch { }
			ProgramFolder?.StringResources?.CleanRedundantValues ();
			ProgramFolder?.FileResources?.CleanRedundantValues ();
			CurrentUserFolder?.StringResources?.CleanRedundantValues ();
			CurrentUserFolder?.FileResources?.CleanRedundantValues ();
			Resources.Add ("GlobalConfig", GlobalConfig);
			Resources.Add ("CurrentUserConfig", CurrentUserConfig);
		}
		internal static string S (string key, string fallback = null)
		{
			try { return ProgramFolder?.StringResources?.SuitableResource (key, fallback) ?? fallback; }
			catch { return fallback; }
		}
	}
}

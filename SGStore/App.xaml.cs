using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace Sidebar
{
	/// <summary>
	/// App.xaml 的交互逻辑
	/// </summary>
	public partial class App: Application
	{
		public static ProgramFolder AppRoot { get; private set; }
		public static ProgramFolder AppData { get; private set; }
		internal static Mutex GlobalMutex = null;
		internal const string AppIdentity = "WindowsModern.Sidebar!Store";
		private void Application_Startup (object sender, StartupEventArgs e)
		{
			AppRoot = ProgramFolder.GlobalFolder;
			AppData = ProgramFolder.CurrentUserFolder;
			try
			{
				ServicePointManager.SecurityProtocol |= (SecurityProtocolType)192;   // TLS 1.0
				ServicePointManager.SecurityProtocol |= (SecurityProtocolType)768;   // TLS 1.1
				ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;  // TLS 1.2
			}
			catch (Exception ex)
			{
				MessageBox.Show (ex.Message, AppRoot.StringResources.SuitableResource ("STORE_ERRORTITLE"), MessageBoxButton.OK, MessageBoxImage.Error);
			}
			SetTheme ("aero", "normalcolor");
			bool createdNew = false;
			GlobalMutex = new Mutex (true, AppIdentity, out createdNew);
			if (!createdNew)
			{
				Shutdown ();
				return;
			}
		}
		private static void SetTheme (string themeName, string themeColor)
		{
			const BindingFlags staticNonPublic = BindingFlags.Static | BindingFlags.NonPublic;
			var presentationFrameworkAsm = Assembly.GetAssembly (typeof (Window));
			var themeWrapper = presentationFrameworkAsm?.GetType ("MS.Win32.UxThemeWrapper");
			if (themeWrapper == null) return;
			var isActiveField = themeWrapper.GetField ("_isActive", staticNonPublic);
			var themeColorField = themeWrapper.GetField ("_themeColor", staticNonPublic);
			var themeNameField = themeWrapper.GetField ("_themeName", staticNonPublic);
			isActiveField?.SetValue (null, true);
			themeColorField?.SetValue (null, themeColor);
			themeNameField?.SetValue (null, themeName);
		}
	}
}

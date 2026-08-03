using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
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

namespace WindowsModern.UserTile
{
	/// <summary>
	/// FlyoutPanel.xaml 的交互逻辑
	/// </summary>
	public partial class FlyoutPanel: UserControl
	{
		public FlyoutPanel ()
		{
			InitializeComponent ();
			InitStrings ();
		}
		private void InitStrings ()
		{
			var sr = Tile.Instance.Region.StringResources;
			UserFolderButton.Content = sr.SuitableResource ("FLYOUT_USERFOLDER");
			UserSettingsButton.Content = sr.SuitableResource ("FLYOUT_USERSETTINGS");
			UserSwitchButton.Content = sr.SuitableResource ("FLYOUT_SWITCHUSER");
		}
		private void UserFolderButton_Click (object sender, RoutedEventArgs e)
		{
			if (Environment.OSVersion.Version.Major < 6) Process.Start ("explorer.exe", "shell:Profile");
			else Process.Start ("explorer.exe", "shell:UsersFilesFolder");
		}
		private void UserSettingsButton_Click (object sender, RoutedEventArgs e)
		{
			System.Diagnostics.Process.Start ("control.exe", "userpasswords");
			//string environmentVariable = Environment.GetEnvironmentVariable ("windir");
			//Process process = new Process ();
			//process.StartInfo.FileName = environmentVariable + "\\explorer.exe";
			//process.StartInfo.Arguments = "shell:::{60632754-c523-4b62-b45c-4172da012619}";
			//process.Start ();
		}
		private void UserSwitchButton_Click (object sender, RoutedEventArgs e)
		{
			UserSwitchHelper.SwitchUser ();
			//SwitchUser ();
		}
		[DllImport ("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
		private static extern IntPtr GetProcAddress (IntPtr hModule, string lpProcName);
		[DllImport ("kernel32.dll", SetLastError = true)]
		private static extern IntPtr LoadLibrary (string lpFileName);
		private delegate bool WTSDisconnectSessionDelegate (IntPtr hServer, int sessionId, bool bWait);
		public static void SwitchUser ()
		{ 
			IntPtr hModule = LoadLibrary ("wtsapi32.dll");
			if (hModule == IntPtr.Zero) return;
			IntPtr pfnWTSDisconnectSession = GetProcAddress (hModule, "WTSDisconnectSession");
			if (pfnWTSDisconnectSession == IntPtr.Zero) return;
			WTSDisconnectSessionDelegate WTSDisconnectSession =
				(WTSDisconnectSessionDelegate)Marshal.GetDelegateForFunctionPointer (
					pfnWTSDisconnectSession, typeof (WTSDisconnectSessionDelegate));
			int sessionId = WTSGetActiveConsoleSessionId ();
			bool success = WTSDisconnectSession (IntPtr.Zero, sessionId, false);
		}
		[DllImport ("wtsapi32.dll", SetLastError = true)]
		private static extern bool WTSDisconnectSession (IntPtr hServer, int sessionId, bool bWait);
		[DllImport ("kernel32.dll")]
		private static extern int WTSGetActiveConsoleSessionId ();
	}
}

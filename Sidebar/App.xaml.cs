using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms.VisualStyles;
using Microsoft.Win32;

namespace Sidebar
{
	/// <summary>
	/// App.xaml 的交互逻辑
	/// </summary>
	public partial class App: Application
	{
		internal static ThemeManager ThemeMgr { get; } = new ThemeManager ();
		internal static ProgramFolder ProgramFolder { get; } = ProgramFolder.GlobalFolder;
		internal static ProgramFolder CurrentUserFolder { get; } = ProgramFolder.CurrentUserFolder;
		internal static SidebarConfig GlobalConfig { get; } = new SidebarConfig (ProgramFolder);
		internal static SidebarConfig CurrentUserConfig { get; } = new SidebarConfig (CurrentUserFolder);
		internal static TileManager TileMgr { get; } = new TileManager ();
		internal static Mutex GlobalMutex = null;
		internal const string AppIdentity = "WindowsModern.Sidebar!App";
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
			OnlyRemainProgramResources (ProgramFolder?.StringResources);
			OnlyRemainProgramResources (CurrentUserFolder?.StringResources);
			ProgramFolder?.StringResources?.CleanRedundantValues ();
			ProgramFolder?.FileResources?.CleanRedundantValues ();
			CurrentUserFolder?.StringResources?.CleanRedundantValues ();
			CurrentUserFolder?.FileResources?.CleanRedundantValues ();
			Resources.Add ("GlobalConfig", GlobalConfig);
			Resources.Add ("CurrentUserConfig", CurrentUserConfig);
			//BrowserEmulation.SetWebBrowserEmulation ();
			var theme = ThemeMgr.CurrentUserTheme;
			ThemeManager.Apply (theme);
			var mainwnd = new MainWindow ();
			this.MainWindow = mainwnd;
			mainwnd.Show ();
			SidebarPipe.StartServer ();
			UpdateTheme ();
			SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
			ReleaseLargeResourcesAsync ();
			Notification.ShowNotification (new NotifyIconNotification {
				Title = "测试用标题",
				Content = "测试用内容",
				Timeout = 100000
			}, null);
			Notification.ShowNotification (new NotifyIconNotification {
				Title = "测试用标题1",
				Content = "测试用内容1",
				Timeout = 5000
			}, null);
			Notification.ShowNotification (new NotifyIconNotification {
				Title = "测试用标题2",
				Content = "测试用内容2",
				Timeout = 301000
			}, null);
		}
		private void SystemEvents_UserPreferenceChanged (object sender, UserPreferenceChangedEventArgs e)
		{
			if (e.Category == UserPreferenceCategory.VisualStyle)
			{
				UpdateTheme ();
			}
		}
		private void Application_Exit (object sender, ExitEventArgs e)
		{
			SidebarPipe.StopServer ();
			SidebarPipe.Message = null;
			SidebarPipe.Mail = null;
		}
		private static void OnlyRemainProgramResources (ILocaleResources lr)
		{
			if (lr == null) return;
			var deletekeys = new List<string> ();
			foreach (var kv in lr)
			{
				var nord = kv.Key.Trim ().ToUpperInvariant ();
				if (nord.StartsWith ("SIDEBAR_") || nord.StartsWith ("TILE") || nord.StartsWith ("CONFIG_") || nord.StartsWith ("ABOUT_")) continue;
				else deletekeys.Add (kv.Key);
			}
			foreach (var i in deletekeys)
			{
				lr?.Remove (i);
			}
		}
		internal static void ReleaseLargeResourcesAsync () => Utilities.ReleaseLargeResourcesAsync ();
		private static void ReleaseLargeResources () => Utilities.ReleaseLargeResources ();
		private static void SetTheme (string themeName, string themeColor = "NormalColor")
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
		private static void SetCurrentBaseTheme (WpfTheme wpfTheme, string color = "normalcolor", string themeUri = null)
		{
			var appResources = Application.Current.Resources;
			var mergedDictionaries = appResources.MergedDictionaries;
			if (mergedDictionaries.Count < 2)
			{
				if (mergedDictionaries.Count == 1) mergedDictionaries.Add (new ResourceDictionary ());
			}
			switch (wpfTheme)
			{
				default:
				case WpfTheme.Default:
					mergedDictionaries [1] = new ResourceDictionary ();
					break;
				case WpfTheme.Classic:
					{
						var rd = new ResourceDictionary ();
						rd.Source = new Uri ("/PresentationFramework.Classic,Version=4.0.0.0,Culture=neutral,PublicKeyToken=31bf3856ad364e35;component/themes/classic.xaml", UriKind.RelativeOrAbsolute);
						mergedDictionaries [1] = rd;
					}
					break;
				case WpfTheme.Aero:
				case WpfTheme.Luna:
				case WpfTheme.Royale:
					{
						var themeStr = "Aero";
						switch (wpfTheme)
						{
							case WpfTheme.Aero: themeStr = "Aero"; break;
							case WpfTheme.Luna: themeStr = "Luna"; break;
							case WpfTheme.Royale: themeStr = "Royale"; break;
						}
						var rd = new ResourceDictionary ();
						rd.Source = new Uri ($"/PresentationFramework.{themeStr},Version=4.0.0.0,Culture=neutral,PublicKeyToken=31bf3856ad364e35;component/themes/{themeStr.ToLowerInvariant ()}.{color}.xaml", UriKind.RelativeOrAbsolute);
						mergedDictionaries [1] = rd;
					}
					break;
				case WpfTheme.Others:
					try
					{
						var rd = new ResourceDictionary ();
						rd.Source = new Uri (themeUri, UriKind.RelativeOrAbsolute);
						mergedDictionaries [1] = rd;
					}
					catch
					{
						mergedDictionaries [1] = new ResourceDictionary ();
					}
					break;
			}
		}
		private void UpdateTheme ()
		{
			if (Environment.OSVersion.Version.Major != 5) return;
			bool isThemed = VisualStyleInformation.IsEnabledByUser;
			string dispName = VisualStyleInformation.DisplayName;
			string colorScheme = VisualStyleInformation.ColorScheme;
			//MessageBox.Show ($"{isThemed}\n{dispName}\n{colorScheme}");
			WindowsXPVisualStyle style = WindowsXPVisualStyle.Others;
			var tld = dispName?.Trim ()?.ToLowerInvariant () ?? "";
			var colors = colorScheme?.Trim ()?.ToLowerInvariant () ?? "";
			if (!isThemed) style = WindowsXPVisualStyle.Classic;
			else if (tld.StartsWith ("windows xp "))
			{
				if (!tld.EndsWith ("ultimate")) style = WindowsXPVisualStyle.Luna;
			}
			else if (tld.StartsWith ("media center") || tld == "royale")
			{
				style = WindowsXPVisualStyle.Royale;
			}
			else if (tld.StartsWith ("embedded"))
			{
				style = WindowsXPVisualStyle.Embedded;
			}
			else if (tld.StartsWith ("zune "))
			{
				style = WindowsXPVisualStyle.Zune;
			}
			else if (tld == "watercolor")
			{
				style = WindowsXPVisualStyle.Watercolor;
			}
			else if (tld.Contains ("slate"))
			{
				if (colors.Contains ("plex")) style = WindowsXPVisualStyle.Plex;
				else style = WindowsXPVisualStyle.Slate;
			}
			else if (tld.Contains ("plex")) style = WindowsXPVisualStyle.Plex;
			else if (tld.Contains ("jade"))
				style = WindowsXPVisualStyle.Jade;
			else if (tld.Contains ("aero")) style = WindowsXPVisualStyle.Aero;
			else style = WindowsXPVisualStyle.Others;
			switch (style)
			{
				default:
				case WindowsXPVisualStyle.Embedded:
				case WindowsXPVisualStyle.Others:
				case WindowsXPVisualStyle.Plex:
				case WindowsXPVisualStyle.Slate:
				case WindowsXPVisualStyle.Watercolor:
					SetCurrentBaseTheme (WpfTheme.Aero);
					SetTheme ("Luna");
					break;
				case WindowsXPVisualStyle.Luna:
					SetCurrentBaseTheme (WpfTheme.Luna, colorScheme);
					SetTheme ("Luna", colorScheme);
					break;
				case WindowsXPVisualStyle.Royale:
					SetCurrentBaseTheme (WpfTheme.Royale);
					SetTheme ("Royale");
					break;
				case WindowsXPVisualStyle.Classic:
					SetCurrentBaseTheme (WpfTheme.Default);
					SetTheme ("Classic");
					break;
				case WindowsXPVisualStyle.Jade:
				case WindowsXPVisualStyle.Aero:
					SetCurrentBaseTheme (WpfTheme.Aero);
					SetTheme ("Aero");
					break;
			}
		}
		enum WindowsXPVisualStyle
		{
			Others, // 其他主题
			Luna,
			Classic,
			Royale, // Media Center 偏白云蓝天的蓝色，较浅
			Zune, // 黑色+橙色
			Embedded, // 嵌入式 Windows XP 的主题，深色，偏青
			Watercolor, // 测试版主题，浅蓝
			Plex, // 网友分享的个性化主题，主要模仿 Longhorn Plex 主题（蓝色)
			Slate, // 网友分享的个性化主题，主要模仿 Longhorn Slate 主题（灰色)
			Jade, // 网友分享的个性化主题，主要模仿 Longhorn Jade 主题（白色)
			Aero // 网友分享的个性化主题
		}
		enum WpfTheme
		{
			Default,
			Luna,
			Royale,
			Classic,
			Aero,
			Others
		}
	}
}

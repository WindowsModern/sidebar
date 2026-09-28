using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Sidebar;

namespace SGInstaller
{
	public partial class App: Application
	{
		internal static ProgramFolder ProgramFolder { get; private set; }
		internal static ProgramFolder CurrentUserFolder { get; private set; }
		internal static ILocaleResources StringResources { get; private set; }
		internal const string AppIdentity = "WindowsModern.Sidebar!Installer";
		private static string GetExecutableDirectory ()
		{
			try
			{
				var asm = Assembly.GetEntryAssembly ();
				if (asm != null && !string.IsNullOrEmpty (asm.Location))
					return Path.GetDirectoryName (asm.Location);
			}
			catch { }
			return AppDomain.CurrentDomain.BaseDirectory;
		}
		private static ILocaleResources TryLoadProgramFolderResources ()
		{
			try { ProgramFolder = ProgramFolder.GlobalFolder; }
			catch { }
			try { CurrentUserFolder = ProgramFolder.CurrentUserFolder; }
			catch { }
			try
			{
				if (ProgramFolder != null) return ProgramFolder.StringResources;
			}
			catch { }
			return null;
		}
		private static ILocaleResources TryLoadLocalLocaleXml ()
		{
			var path = Path.Combine (GetExecutableDirectory (), "Locale.xml");
			if (!File.Exists (path)) return null;
			try { return LocaleResources.CreateFromFile (path); }
			catch { return null; }
		}
		private static void OnlyRemainInstallerResources (ILocaleResources lr)
		{
			if (lr == null) return;
			var deletekeys = new List<string> ();
			foreach (var kv in lr)
			{
				var nord = (kv.Key ?? "").Trim ().ToUpperInvariant ();
				if (!nord.StartsWith ("INSTALLER_")) deletekeys.Add (kv.Key);
			}
			foreach (var key in deletekeys)
			{
				try { lr.Remove (key); } catch { }
			}
		}
		protected override void OnStartup (StartupEventArgs e)
		{
			base.OnStartup (e);
			var res = TryLoadProgramFolderResources ();
			var local = TryLoadLocalLocaleXml ();
			if (local != null)
			{
				// 本地 Locale.xml 优先（它包含 INSTALLER_* 条目）
				if (res != null)
				{
					foreach (var kv in local) res [kv.Key] = kv.Value;
				}
				else
				{
					res = local;
				}
			}
			OnlyRemainInstallerResources (res);
			StringResources = res;
			DispatcherUnhandledException += (s, ex) => {
				MessageBox.Show (ex.Exception.Message, "SGInstaller", MessageBoxButton.OK, MessageBoxImage.Error);
				ex.Handled = true;
			};
			var opts = CommandLineOptions.Parse (Environment.GetCommandLineArgs ());
			InstallerContext.Current.IsSilent = opts.Silent;
			InstallerContext.Current.IsVerySilent = opts.VerySilent;
			if (opts.VerySilent && !string.IsNullOrEmpty (opts.PackagePath))
			{
				ShutdownMode = ShutdownMode.OnExplicitShutdown;
				Task.Factory.StartNew (() => {
					try
					{
						InstallerContext.Current.CurrentPackage = PackageService.Instance.LoadPackage (opts.PackagePath);
						PackageService.Instance.Install (opts.PackagePath);
						Dispatcher.Invoke (new Action (() => Shutdown (0)));
					}
					catch
					{
						Dispatcher.Invoke (new Action (() => Shutdown (1)));
					}
				});
				return;
			}
			var wnd = new MainWindow ();
			MainWindow = wnd;
			wnd.Show ();
		}
	}
}
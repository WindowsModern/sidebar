using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Sidebar;
namespace SGInstall
{
	static class Program
	{
		internal static ProgramFolder ProgramFolder { get; } = ProgramFolder.GlobalFolder;
		internal static ProgramFolder CurrentUserFolder { get; } = ProgramFolder.CurrentUserFolder;
		internal static TileManager TileMgr { get; } = new TileManager ();
		internal const string AppIdentity = "WindowsModern.Sidebar!Installer";
		internal static ILocaleResources StringResources => ProgramFolder.StringResources;
		internal static IPathResources FileResources => ProgramFolder.FileResources;
		/// <summary>
		/// 应用程序的主入口点。
		/// </summary>
		[STAThread]
		static void Main ()
		{
			OnlyRemainProgramResources (ProgramFolder?.StringResources);
			OnlyRemainProgramResources (CurrentUserFolder?.StringResources);
			ProgramFolder?.StringResources?.CleanRedundantValues ();
			ProgramFolder?.FileResources?.CleanRedundantValues ();
			CurrentUserFolder?.StringResources?.CleanRedundantValues ();
			CurrentUserFolder?.FileResources?.CleanRedundantValues ();
			ReleaseLargeResourcesAsync ();
			Application.EnableVisualStyles ();
			Application.SetCompatibleTextRenderingDefault (false);
			Application.Run (new MainForm ());
		}
		private static void OnlyRemainProgramResources (ILocaleResources lr)
		{
			if (lr == null) return;
			var deletekeys = new List<string> ();
			foreach (var kv in lr)
			{
				var nord = kv.Key.Trim ().ToUpperInvariant ();
				if (nord.StartsWith ("INSTALLER_")) continue;
				else deletekeys.Add (kv.Key);
			}
			foreach (var i in deletekeys)
			{
				lr?.Remove (i);
			}
		}

		internal static void ReleaseLargeResourcesAsync () => Utilities.ReleaseLargeResourcesAsync ();
		private static void ReleaseLargeResources () => Utilities.ReleaseLargeResources ();
	}
}

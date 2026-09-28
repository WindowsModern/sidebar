using System;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Sidebar;

namespace SGInstaller
{
	public enum GadgetStatus
	{
		NotExist,
		NeedUpdate,
		Reinstall
	}

	public static class StringExtensions
	{
		public static bool NEquals (this string left, string right)
		{
			return string.Equals ((left ?? "").Trim (), (right ?? "").Trim (), StringComparison.OrdinalIgnoreCase);
		}
	}

	public sealed class CommandLineOptions
	{
		public string PackagePath { get; private set; }
		public bool Silent { get; private set; }
		public bool VerySilent { get; private set; }
		public static CommandLineOptions Parse (string [] args)
		{
			var opt = new CommandLineOptions ();
			if (args == null) return opt;
			for (var i = 1; i < args.Length; i++)
			{
				var a = args [i];
				if (string.IsNullOrWhiteSpace (a)) continue;
				a = a.Trim ();
				if (a.NEquals ("-silent") || a.NEquals ("/silent"))
				{
					opt.Silent = true;
					continue;
				}
				if (a.NEquals ("-verysilent") || a.NEquals ("/verysilent"))
				{
					opt.Silent = true;
					opt.VerySilent = true;
					continue;
				}
				if (opt.PackagePath == null && File.Exists (a))
				{
					opt.PackagePath = a;
				}
			}
			return opt;
		}
	}

	public sealed class InstallerContext
	{
		private static readonly InstallerContext inst = new InstallerContext ();
		public static InstallerContext Current { get { return inst; } }
		private InstallerContext ()
		{
			LaunchWhenReady = true;
		}
		public TilePackageBase CurrentPackage { get; set; }
		public bool IsSilent { get; set; }
		public bool IsVerySilent { get; set; }
		public bool LaunchWhenReady { get; set; }
		public GadgetStatus GetNextOperation ()
		{
			if (CurrentPackage == null) return GadgetStatus.NotExist;
			var id = GetIdentity ();
			if (id == null) return GadgetStatus.NotExist;
			var installed = PackageService.Instance.GetInstalled (id.FamilyName);
			if (installed == null) return GadgetStatus.NotExist;
			if (id.Version > installed.Manifest.Identity.Version) return GadgetStatus.NeedUpdate;
			return GadgetStatus.Reinstall;
		}
		public string GetFamilyName ()
		{
			if (CurrentPackage == null) return "";
			var id = GetIdentity ();
			return id == null ? "" : id.FamilyName;
		}
		public string GetPublisher ()
		{
			var sp = GetSubPackage ();
			if (sp == null) return "";
			var p = sp.Manifest.Properties;
			var sr = sp.StringResources;
			if (sr != null)
			{
				var r = sr.SuitableResource (p.Publisher, p.Publisher);
				if (!string.IsNullOrEmpty (r)) return r;
			}
			return p.Publisher ?? "";
		}
		public string GetDisplayName ()
		{
			var sp = GetSubPackage ();
			if (sp != null)
			{
				var p = sp.Manifest.Properties;
				var sr = sp.StringResources;
				if (sr != null)
				{
					var r = sr.SuitableResource (p.DisplayName, p.DisplayName);
					if (!string.IsNullOrEmpty (r)) return r;
				}
				if (!string.IsNullOrEmpty (p.DisplayName)) return p.DisplayName;
			}
			try
			{
				if (CurrentPackage != null && !string.IsNullOrEmpty (CurrentPackage.FileName))
					return System.IO.Path.GetFileNameWithoutExtension (CurrentPackage.FileName);
			}
			catch { }
			return "";
		}
		public string GetVersion ()
		{
			if (CurrentPackage == null) return "";
			TileIdentity id = null;
			var tp = CurrentPackage as TilePackage;
			if (tp != null) id = tp.Manifest == null ? null : tp.Manifest.Identity;
			var tb = CurrentPackage as TilePackageBundle;
			if (tb != null) id = tb.Manifest == null ? null : tb.Manifest.Identity;
			return id == null || id.Version == null ? "" : id.Version.Expression;
		}
		public System.Windows.Media.ImageSource GetLogoImageSource ()
		{
			var sp = GetSubPackage ();
			if (sp == null) return null;
			try
			{
				var p = sp.Manifest.Properties;
				var logoPath = p.Logo;
				var fr = sp.FileResources;
				if (fr != null)
				{
					var r = fr.SuitableResource (logoPath, logoPath);
					if (!string.IsNullOrEmpty (r)) logoPath = r;
				}
				if (string.IsNullOrEmpty (logoPath)) return null;
				var bytes = sp.ExtractFile (logoPath);
				if (bytes == null || bytes.Length == 0) return null;
				using (var ms = new System.IO.MemoryStream (bytes, false))
				{
					var bmp = new System.Windows.Media.Imaging.BitmapImage ();
					bmp.BeginInit ();
					bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
					bmp.StreamSource = ms;
					bmp.EndInit ();
					bmp.Freeze ();
					return bmp;
				}
			}
			catch { return null; }
		}
		public string GetDescription ()
		{
			var sp = GetSubPackage ();
			if (sp == null) return "";
			var p = sp.Manifest.Properties;
			var sr = sp.StringResources;
			if (sr != null)
			{
				var r = sr.SuitableResource (p.Description, p.Description);
				if (!string.IsNullOrEmpty (r)) return r;
			}
			return p.Description ?? "";
		}
		private TilePackage GetSubPackage ()
		{
			var tp = CurrentPackage as TilePackage;
			if (tp != null) return tp;
			var tb = CurrentPackage as TilePackageBundle;
			if (tb != null && tb.Packages != null) return tb.Packages.FirstOrDefault ();
			return null;
		}
		private TileIdentity GetIdentity ()
		{
			var tp = CurrentPackage as TilePackage;
			if (tp != null) return tp.Manifest == null ? null : tp.Manifest.Identity;
			var tb = CurrentPackage as TilePackageBundle;
			if (tb != null) return tb.Manifest == null ? null : tb.Manifest.Identity;
			return null;
		}
	}

	public static class NotificationService
	{
		public static void NotifyPinTile (string familyName)
		{
			SidebarMail.Send ("NotifyPinTile", familyName);
		}
		public static void NotifyUnpinTile (string familyName)
		{
			SidebarMail.Send ("NotifyUnpinTile", familyName);
		}
		public static void NotifyLaunchApp (string familyName)
		{
			SidebarMail.Send ("NotifyLaunchApp", familyName);
		}
	}

	public sealed class PackageService
	{
		private static readonly PackageService inst = new PackageService ();
		public static PackageService Instance { get { return inst; } }
		private readonly TileManager mgr = new TileManager ();
		private PackageService () { }
		public event Action<int, int, double> ProgressChanged;
		public TilePackageBase LoadPackage (string filePath)
		{
			return TilePackageReadManager.GetPackage (filePath);
		}
		public TileStorage GetInstalled (string familyName)
		{
			return mgr.GetByFamilyName (familyName);
		}
		public void Install (string filePath)
		{
			mgr.InstallPackage (filePath, OnProgress);
		}
		public void Update (string filePath)
		{
			mgr.UpdatePackage (filePath, OnProgress);
		}
		private void OnProgress (int current, int total, double percent)
		{
			var h = ProgressChanged;
			if (h != null) h (current, total, percent);
		}
	}

	public sealed class RelayCommand: ICommand
	{
		private readonly Action execute;
		private readonly Func<bool> canExecute;
		public RelayCommand (Action execute) { this.execute = execute; }
		public RelayCommand (Action execute, Func<bool> canExecute) { this.execute = execute; this.canExecute = canExecute; }
		public bool CanExecute (object parameter)
		{
			return canExecute == null || canExecute ();
		}
		public void Execute (object parameter)
		{
			if (execute != null) execute ();
		}
		public event EventHandler CanExecuteChanged
		{
			add { CommandManager.RequerySuggested += value; }
			remove { CommandManager.RequerySuggested -= value; }
		}
	}
}
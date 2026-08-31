using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace Sidebar
{
	public class Theme: ITheme
	{
		public string FolderPath { get; }
		public string ThemeMainFile => Path.Combine (FolderPath, "Theme.xaml");
		public string ThemeName => Path.GetFileName (FolderPath.TrimEnd (Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
		public ResourceDictionary ResourceDictionary { get; } 
		public Theme (string fn)
		{
			if (string.IsNullOrWhiteSpace (fn) || !Directory.Exists (fn))
				throw new ArgumentException ($"Theme folder \"{fn}\" is invalid");
			FolderPath = fn;
			if (!File.Exists (ThemeMainFile))
				throw new ArgumentException ($"Theme file \"{ThemeMainFile}\" is invalid");
			ResourceDictionary = new ResourceDictionary ();
			var uri = new Uri (ThemeMainFile, UriKind.RelativeOrAbsolute);
			ResourceDictionary.Source = uri;
		}
	}
	public class ThemeManager
	{
		public ThemeManager (string baseDir) { BaseDir = baseDir; }
		public ThemeManager () : this (AppDomain.CurrentDomain.BaseDirectory) { }
		public string BaseDir { get; }
		public List<Tuple<bool, string, Theme>> Get ()
		{
			var programFolder = ProgramFolder.GlobalFolder;
			var tileFolder = Path.Combine (programFolder.FolderPath, "Themes");
			var ret = new List<Tuple<bool, string, Theme>> ();
			try
			{
				var subFolders = Directory.EnumerateDirectories (tileFolder);
				foreach (var s in subFolders)
				{
					try
					{
						ret.Add (new Tuple<bool, string, Theme> (true, "", new Theme (s)));
					}
					catch (Exception e)
					{
						ret.Add (new Tuple<bool, string, Theme> (false, e.Message, null));
					}
				}
			}
			catch { }
			return ret;
		}
		public List<Theme> ValidThemes
		{
			get
			{
				var ret = new List<Theme> ();
				foreach (var t in Get ())
				{
					if (t.Item1 && t.Item3 != null) ret.Add (t.Item3);
				}
				return ret;
			}
		}
		public Theme GetCurrentUserTheme (bool enableDefault = true)
		{
			var gf = ProgramFolder.GlobalFolder;
			var pf = ProgramFolder.CurrentUserFolder;
			var curr = pf.InitConfig ["Theme"].Get ("ThemeName", gf.InitConfig ["Theme"] ["ThemeName"]) as string;
			if (string.IsNullOrWhiteSpace (curr))
			{
				curr = "";
				if (enableDefault) curr = "Slate";
			}
			curr = curr?.Trim ()?.ToLowerInvariant ();
			foreach (var itemTheme in ValidThemes)
			{
				if (itemTheme.ThemeName?.Trim ()?.ToLowerInvariant () == curr)
					return itemTheme;
			}
			return null;
		}
		public Theme GetPublicTheme (bool enableDefault = true)
		{
			var gf = ProgramFolder.GlobalFolder;
			var curr = gf.InitConfig ["Theme"] ["ThemeName"] as string;
			if (string.IsNullOrWhiteSpace (curr))
			{
				curr = "";
				if (enableDefault) curr = "Slate";
			}
			curr = curr?.Trim ()?.ToLowerInvariant ();
			foreach (var itemTheme in ValidThemes)
			{
				if (itemTheme.ThemeName?.Trim ()?.ToLowerInvariant () == curr)
					return itemTheme;
			}
			return null;
		}
		public void SetCurrentUserTheme (string themeName)
		{
			var gf = ProgramFolder.GlobalFolder;
			var pf = ProgramFolder.CurrentUserFolder;
			pf.InitConfig ["Theme"] ["ThemeName"] = themeName;
		}
		public void SetCurrentUserTheme (Theme theme)
			=> SetCurrentUserTheme (theme.ThemeName);
		public void SetPublicTheme (string themeName)
		{
			var gf = ProgramFolder.GlobalFolder;
			var pf = ProgramFolder.CurrentUserFolder;
			gf.InitConfig ["Theme"] ["ThemeName"] = themeName;
		}
		public void SetPublicTheme (Theme theme)
			=> SetPublicTheme (theme.ThemeName);
		public Theme CurrentUserTheme
		{
			get { return GetCurrentUserTheme (); }
			set { SetCurrentUserTheme (value); }
		}
		public Theme PublicTheme
		{
			get { return GetPublicTheme (); }
			set { SetPublicTheme (value); }
		}
		public static void Apply (Theme newTheme)
		{
			try { OnThemeChanged (newTheme); } catch { }
			var appResources = Application.Current.Resources;
			var mergedDictionaries = appResources.MergedDictionaries;
			if (mergedDictionaries.Count < 3)
			{
				if (mergedDictionaries.Count == 1) mergedDictionaries.Add (new ResourceDictionary ());
				if (mergedDictionaries.Count == 2) mergedDictionaries.Add (new ResourceDictionary ());
			}
			mergedDictionaries [2] = newTheme?.ResourceDictionary ?? new ResourceDictionary ();
			return;
		}
		public static event Action<Theme> ThemeChanged;
		private static void OnThemeChanged (Theme t) { ThemeChanged?.Invoke (t); }
	}
}

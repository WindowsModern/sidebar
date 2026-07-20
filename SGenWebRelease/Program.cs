using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Sidebar;
using Newtonsoft.Json;
namespace SGenWebRelease
{
	class Program
	{
		static Dictionary<string, string> GetDefaultResourceDictionary (string value)
		{
			var dict = new Dictionary<string, string> ();
			dict ["root"] = value;
			return dict;
		}
		static void Main (string [] args)
		{
			var baseDir = AppDomain.CurrentDomain.BaseDirectory;
			if (args.Length <= 0)
			{
				Console.WriteLine (@"Usage:
	SGenWebRelease <src_sgpkg> <output_dir>");
				return;
			}
			else if (args.Length == 1)
			{
				Console.Error.WriteLine (@"Error: please provide the output directory path.");
				return;
			}
			else
			{
				var src = args [0];
				var outdir = args [1];
				if (!File.Exists (src))
					Console.Error.WriteLine ("Error: invalid file");
				if (!Directory.Exists (outdir)) Directory.CreateDirectory (outdir);
				const string logoName = "logo.img";
				const string jsonName = "info.json";
				var familyName = "";
				var fullName = "";
				Sidebar.Version ver = new Sidebar.Version (0);
				byte [] logoImg = new byte [0];
				object jsonobj = null;
				var finalFileName = "";
				using (var pkg = TilePackageReadManager.GetPackage (src))
				{
					object id = null;
					object prop = null;
					object prer = null;
					switch (pkg.PackageType)
					{
						case TilePackageType.Single:
							{
								var aread = pkg as TilePackage;
								var m = aread.Manifest;
								var i = m.Identity;
								id = new {
									name = i.Name,
									publisher = i.Publisher,
									version = i.Version.Expression,
									processorArchitecture = new [] { i.ProcessorArchitecture.ToString () },
									familyName = i.FamilyName,
									fullName = i.FullName
								};
								familyName = i.FamilyName;
								fullName = i.FullName;
								ver = i.Version;
								var pro = m.Properties;
								IDictionary<string, string> dispName = aread.StringResources.ContainsKey (pro.DisplayName) ? aread.StringResources [pro.DisplayName] as IDictionary<string, string> : GetDefaultResourceDictionary (pro.DisplayName);
								IDictionary<string, string> desc = aread.StringResources.ContainsKey (pro.Description) ? aread.StringResources [pro.Description] as IDictionary<string, string> : GetDefaultResourceDictionary (pro.Description);
								IDictionary<string, string> publisherDispName = aread.StringResources.ContainsKey (pro.Publisher) ? aread.StringResources [pro.PublisherDisplayName] as IDictionary<string, string> : GetDefaultResourceDictionary (pro.PublisherDisplayName);
								prop = new {
									displayName = dispName,
									publisherDisplayName = publisherDispName,
									description = desc,
									logo = logoName
								};
								var pre = m.Prerequisites;
								prer = new {
									osMinVersion = pre.OSMinVersion.Expression,
									osMaxVersionTested = pre.OSMaxVersionTested.Expression
								};
								var logo = aread.FileResources?.SuitableResource (pro.Logo, pro.Logo, 100) ?? pro.Logo;
								logoImg = aread.ExtractFile (logo);
							} break;
						case TilePackageType.Bundle:
							{
								var bread = pkg as TilePackageBundle;
								var m = bread.Manifest;
								var i = m.Identity;
								id = new {
									name = i.Name,
									publisher = i.Publisher,
									version = i.Version.Expression,
									processorArchitecture = bread.Packages.Select (p => p.Manifest.Identity.ProcessorArchitecture.ToString ()).ToList (),
									familyName = i.FamilyName,
									fullName = i.FullName
								};
								familyName = i.FamilyName;
								fullName = i.FullName;
								ver = i.Version;
								var subpkg = bread.Packages.FirstOrDefault ();
								var am = subpkg.Manifest;
								var pro = am.Properties;
								IDictionary<string, string> dispName = bread.StringResources.ContainsKey (pro.DisplayName) ? bread.StringResources [pro.DisplayName] as IDictionary<string, string> : GetDefaultResourceDictionary (pro.DisplayName);
								IDictionary<string, string> desc = bread.StringResources.ContainsKey (pro.Description) ? bread.StringResources [pro.Description] as IDictionary<string, string> : GetDefaultResourceDictionary (pro.Description);
								IDictionary<string, string> publisherDispName = bread.StringResources.ContainsKey (pro.Publisher) ? bread.StringResources [pro.PublisherDisplayName] as IDictionary<string, string> : GetDefaultResourceDictionary (pro.PublisherDisplayName);
								prop = new {
									displayName = dispName,
									publisherDisplayName = publisherDispName,
									description = desc,
									logo = logoName
								};
								var pre = am.Prerequisites;
								prer = new {
									osMinVersion = pre.OSMinVersion.Expression,
									osMaxVersionTested = pre.OSMaxVersionTested.Expression
								};
								var logo = bread.FileResources?.SuitableResource (pro.Logo, pro.Logo, 100) ?? pro.Logo;
								logoImg = subpkg.ExtractFile (logo);
							}
							break;
					}
					finalFileName = fullName.Trim () + "." + (pkg.PackageType == TilePackageType.Bundle ? "sgpkgbundle" : "sgpkg");
					jsonobj = new {
						filename = finalFileName,
						hash = FileHash.GenerateDigest (src),
						identity = id,
						properties = prop,
						prerequisites = prer
					};
				}
				if (!Directory.Exists (Path.Combine (outdir, familyName, ver.Expression))) Directory.CreateDirectory (Path.Combine (outdir, familyName, ver.Expression));
				using (var file = File.Create (Path.Combine (outdir, familyName, ver.Expression, logoName)))
				{
					file.Write (logoImg, 0, logoImg?.Length ?? 0);
				}
				using (var file = File.CreateText (Path.Combine (outdir, familyName, ver.Expression, jsonName)))
				{
					file.Write (JsonConvert.SerializeObject (jsonobj));
				}
				File.Copy (args [0], Path.Combine (outdir, familyName, ver.Expression, finalFileName));
				Console.Write ("Done!");
			}
		}
	}
}

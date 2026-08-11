using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using System.IO;

namespace Sidebar
{
	public class TileInfo
	{
		[JsonProperty ("filename")]
		public string FileName { get; set; }
		[JsonProperty ("hash")]
		public string Digest { get; set; }
		[JsonProperty ("identity")]
		public TileIdentityModel Identity { get; set; } = new TileIdentityModel ();
		[JsonProperty ("properties")]
		public TilePropertiesModel Properties { get; set; } = new TilePropertiesModel ();
		[JsonProperty ("prerequisites")]
		public TilePrerequisitesModel Prerequisites { get; set; } = new TilePrerequisitesModel ();
	}
	public class TileIdentityModel
	{
		[JsonProperty ("name")]
		public string Name { get; set; }
		[JsonProperty ("publisher")]
		public string Publisher { get; set; }
		[JsonProperty ("version")]
		public string Version { get; set; }
		[JsonProperty ("processorArchitecture")]
		public List<string> ProcessorArchitecture { get; set; } = new List<string> ();
		[JsonProperty ("familyName")]
		public string FamilyName { get; set; }
		[JsonProperty ("fullName")]
		public string FullName { get; set; }
	}
	public class TilePropertiesModel
	{
		[JsonProperty ("displayName")]
		public Dictionary<string, string> DisplayName { get; set; } = new Dictionary<string, string> ();
		[JsonProperty ("publisherDisplayName")]
		public Dictionary<string, string> PublisherDisplayName { get; set; } = new Dictionary<string, string> ();
		[JsonProperty ("description")]
		public Dictionary<string, string> Description { get; set; } = new Dictionary<string, string> ();
		[JsonProperty ("logo")]
		public string Logo { get; set; }
	}
	public class TilePrerequisitesModel
	{
		[JsonProperty ("osMinVersion")]
		public string OsMinVersion { get; set; }
		[JsonProperty ("osMaxVersionTested")]
		public string OsMaxVersionTested { get; set; }
	}
	public class TileSupport
	{
		[JsonProperty ("osMinVersion")]
		public string OsMinVersion { get; set; }
		[JsonProperty ("processorArchitecture")]
		public List<string> ProcessorArchitecture { get; set; } = new List<string> ();
	}
	public class TilePackageSubItem
	{
		[JsonProperty ("directory")]
		public string BaseDirectory { get; set; }
		[JsonProperty ("infoFile")]
		public string InfoFileName { get; set; }
		[JsonProperty ("logoFile")]
		public string LogoFileName { get; set; }
		[JsonProperty ("pkgFile")]
		public string PackageFileName { get; set; }
		[JsonProperty ("version")]
		public string Version { get; set; }
		[JsonProperty ("hash")]
		public string Digest { get; set; }
		[JsonProperty ("supportOs")]
		public TileSupport Support { get; set; } = new TileSupport ();
	}
	public class TilePackageItem
	{
		[JsonProperty ("directory")]
		public string BaseDirectory { get; set; }
		[JsonProperty ("properties")]
		public TilePropertiesModel Properties { get; set; }
		[JsonProperty ("items")]
		public List<TilePackageSubItem> Items { get; set; } = new List<TilePackageSubItem> ();
		private List<TilePackageSubItem> suitItems = null;
		[JsonIgnore]
		public List<TilePackageSubItem> SupportedItems
		{
			get
			{
				if (suitItems != null) return suitItems;
				else
				{
					var osVersion = new Version (Environment.OSVersion.Version.ToString ());
					var archiStr = ProcessorDetector.GetCurrentArchitecture ().ToString ();
					suitItems = Items.Where (i => {
						var osminver = new Version (i.Support.OsMinVersion);
						if (osminver > osVersion) return false;
						foreach (var a in i.Support.ProcessorArchitecture)
						{
							if (a.NEquals ("neutral")) return true;
							else if (a.NEquals (archiStr)) return true;
						}
						return false;
					}).ToList ();
					suitItems = suitItems.OrderByDescending (i => new Version (i.Version)).ToList ();
					return suitItems;
				}
			}
		}
		[JsonIgnore]
		public Uri RandomLogo
		{
			get
			{
				foreach (var i in Items)
				{
					if (string.IsNullOrWhiteSpace (i.LogoFileName)) continue;
					return new Uri ($"{WebAPI.BaseUri}/{BaseDirectory}/{i.BaseDirectory}/{i.LogoFileName}");
				}
				return new Uri (Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "Images\\Gadget.png"), UriKind.RelativeOrAbsolute);
			}
		}
		[JsonIgnore]
		public string DisplayName
		{
			get
			{
				return ResourceSelector.GetString (Properties.DisplayName);
			}
		}
		[JsonIgnore]
		public string Publisher
		{
			get
			{
				return ResourceSelector.GetString (Properties.PublisherDisplayName);
			}
		}
		[JsonIgnore]
		public string Description
		{
			get
			{
				return ResourceSelector.GetString (Properties.Description);
			}
		}
		[JsonIgnore]
		public string NewestVersion
		{
			get
			{
				if (Items == null || Items.Count == 0)
					return null;
				var validVersions = Items
					.Select (i => { return new Version (i.Version); })
					.Where (v => v != null);
				if (!validVersions.Any ())
					return null;
				var maxVersion = validVersions.Max ();
				return maxVersion.ToString ();
			}
		}
		[JsonIgnore]
		public string SupportedNewestVersion
		{
			get
			{
				return SupportedItems.FirstOrDefault ().Version ?? "";
			}
		} 
		[JsonIgnore] 
		public Uri SupportedVersionLogo
		{
			get
			{
				foreach (var i in SupportedItems)
				{
					if (string.IsNullOrWhiteSpace (i.LogoFileName)) continue;
					return new Uri ($"{WebAPI.BaseUri}/{BaseDirectory}/{i.BaseDirectory}/{i.LogoFileName}");
				}
				return new Uri (Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "Images\\Gadget.png"), UriKind.RelativeOrAbsolute);
			}
		}
		[JsonIgnore]
		public Uri SupportedNewestFileUri
		{
			get
			{
				foreach (var i in SupportedItems)
				{
					if (string.IsNullOrWhiteSpace (i.PackageFileName)) continue;
					return new Uri ($"{WebAPI.BaseUri}/{BaseDirectory}/{i.BaseDirectory}/{i.PackageFileName}");
				}
				return new Uri (Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "Images\\Gadget.png"), UriKind.RelativeOrAbsolute);
			}
		}
		[JsonIgnore]
		public string SupportedNewestFileName
		{
			get
			{
				foreach (var i in SupportedItems)
				{
					if (string.IsNullOrWhiteSpace (i.PackageFileName)) continue;
					return i.PackageFileName;
				}
				return null;
			}
		}
		[JsonIgnore]
		public string SupportedNewestFileNameWithoutExtension
		{
			get
			{
				foreach (var i in SupportedItems)
				{
					if (string.IsNullOrWhiteSpace (i.PackageFileName)) continue;
					return Path.GetFileNameWithoutExtension (i.PackageFileName);
				}
				return null;
			}
		}
		[JsonIgnore]
		public string SupportedNewestFileExtension
		{
			get
			{
				foreach (var i in SupportedItems)
				{
					if (string.IsNullOrWhiteSpace (i.PackageFileName)) continue;
					return Path.GetExtension (i.PackageFileName);
				}
				return null;
			}
		}
		[JsonIgnore]
		public string SupportedNewestFileDigest
		{
			get
			{
				foreach (var i in SupportedItems)
				{
					if (string.IsNullOrWhiteSpace (i.Digest)) continue;
					return i.Digest;
				}
				return null;
			}
		}
	}
	public class TileList
	{
		[JsonProperty ("list")]
		public List<TilePackageItem> List { get; set; } = new List<TilePackageItem> ();
	}
	public static class ResourceSelector
	{
		public static string GetString (IDictionary<string, string> dict, string fallback = null, string localeName = null)
		{
			if (string.IsNullOrWhiteSpace (localeName)) localeName = Locale.GetComputerLocaleCode ();
			var ret = "";
			ret = StringGetRequiredValue (dict, localeName);
			if (string.IsNullOrEmpty (ret))
			{
				if (localeName?.Trim ()?.ToLowerInvariant () != Locale.GetComputerLocaleCode ()?.Trim ()?.ToLowerInvariant ())
				{
					ret = StringGetRequiredValue (dict, Locale.GetComputerLocaleCode ());
				}
			}
			if (string.IsNullOrEmpty (ret)) ret = StringGetRequiredValue (dict, "en-US");
			if (string.IsNullOrEmpty (ret)) dict.TryGetValue ("root", out ret);
			if (string.IsNullOrEmpty (ret))
			{
				foreach (var kv in dict)
				{
					ret = kv.Value;
					break;
				}
			}
			if (string.IsNullOrEmpty (ret)) ret = null;
			return ret ?? fallback;
		}
		internal static string StringGetRequiredValue (IDictionary<string, string> dict, string localeName)
		{
			var ln = localeName?.Trim ()?.ToLowerInvariant () ?? "";
			var lid = Locale.ToLCID (ln);
			var ret = "";
			if (dict.TryGetValue (ln, out ret)) return ret;
			foreach (var kv in dict)
			{
				if ((kv.Key?.Trim ()?.ToLowerInvariant () ?? "") == ln) return kv.Value;
			}
			foreach (var kv in dict)
			{
				if (Locale.ToLCID (kv.Key) == lid) return kv.Value;
			}
			var restrict = Locale.GetLocaleRestrictedCode (ln)?.Trim ()?.ToLowerInvariant () ?? "";
			foreach (var kv in dict)
			{
				var kr = Locale.GetLocaleRestrictedCode (restrict)?.Trim ()?.ToLowerInvariant () ?? "";
				if (kr == restrict) return kv.Value;
			}
			var rid = Locale.ToLCID (restrict);
			foreach (var kv in dict)
			{
				var kr = Locale.GetLocaleRestrictedCode (restrict)?.Trim ()?.ToLowerInvariant () ?? "";
				var krid = Locale.ToLCID (kr);
				if (krid == rid) return kv.Value;
			}
			return "";
		}
	}
}

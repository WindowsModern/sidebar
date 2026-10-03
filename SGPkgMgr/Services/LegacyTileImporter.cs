using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ICSharpCode.SharpZipLib.Zip;
using Sidebar;

namespace SGPkgMgr.Services
{
	/// <summary>
	/// 把旧 SDK 的磁贴（LongBar 安装包 / 单个 DLL）导入成
	/// "文件字典 + TileManifest"，交给用户在编辑器里修改后打包。
	/// </summary>
	public static class LegacyTileImporter
	{
		public static LegacyTileImportResult Import (
			string sourcePath,
			Action<int, int, double> progress = null,
			Action<string> log = null)
		{
			if (string.IsNullOrEmpty (sourcePath) || !File.Exists (sourcePath))
			{
				log?.Invoke ("Import: file not found: " + sourcePath);
				return null;
			}

			string ext = Path.GetExtension (sourcePath).ToLowerInvariant ();
			if (ext == ".dll")
				return ImportFromDll (sourcePath, progress, log);
			if (ext == ".zip" || ext == ".sgpkg" || ext == ".sgpkgbundle")
				return ImportFromPackage (sourcePath, progress, log);

			throw new NotSupportedException ("Unsupported source: " + ext);
		}

		// ============================================================
		//  单 DLL
		// ============================================================

		private static LegacyTileImportResult ImportFromDll (
			string dllPath,
			Action<int, int, double> progress,
			Action<string> log)
		{
			byte [] dllBytes;
			try { dllBytes = File.ReadAllBytes (dllPath); }
			catch (Exception ex)
			{
				log?.Invoke ("Import: read dll failed: " + ex.Message);
				return null;
			}

			progress?.Invoke (0, 1, 0.0);

			var analysis = LegacyTileAnalyzer.Analyze (dllPath, log);
			if (analysis == null) return null;

			progress?.Invoke (1, 1, 1.0);

			string fileName = Path.GetFileName (dllPath);
			var files = new Dictionary<string, byte []> (StringComparer.OrdinalIgnoreCase) {
				[fileName] = dllBytes
			};

			var manifest = BuildManifest (
				analysis,
				new List<KeyValuePair<string, LegacyTileAnalysis>> {
					new KeyValuePair<string, LegacyTileAnalysis> (fileName, analysis)
				});

			return new LegacyTileImportResult {
				SourcePath = dllPath,
				Analysis = analysis,
				Files = files,
				Manifest = manifest
			};
		}

		// ============================================================
		//  包导入
		// ============================================================

		private static LegacyTileImportResult ImportFromPackage (
			string packagePath,
			Action<int, int, double> progress,
			Action<string> log)
		{
			using (var zip = new ZipFile (packagePath))
			{
				var entries = zip.Cast<ZipEntry> ().Where (e => e.IsFile).ToList ();
				int total = Math.Max (entries.Count, 1);
				int current = 0;

				var files = new Dictionary<string, byte []> (StringComparer.OrdinalIgnoreCase);
				var analyses = new List<KeyValuePair<string, LegacyTileAnalysis>> ();

				foreach (var entry in entries)
				{
					byte [] bytes;
					using (var s = zip.GetInputStream (entry))
					using (var ms = new MemoryStream ())
					{
						s.CopyTo (ms);
						bytes = ms.ToArray ();
					}
					files [entry.Name] = bytes;

					if (entry.Name.EndsWith (".dll", StringComparison.OrdinalIgnoreCase))
					{
						log?.Invoke ("Import: analyzing " + entry.Name);
						var a = LegacyTileAnalyzer.Analyze (bytes, probeDir: null, log: log);
						if (a != null)
							analyses.Add (new KeyValuePair<string, LegacyTileAnalysis> (entry.Name, a));
					}

					current++;
					progress?.Invoke (current, total, (double)current / total);
				}

				if (analyses.Count == 0)
				{
					log?.Invoke ("Import: no legacy tile DLL found in package.");
					return null;
				}

				var manifest = BuildManifest (analyses [0].Value, analyses);

				return new LegacyTileImportResult {
					SourcePath = packagePath,
					Analysis = analyses [0].Value,
					Files = files,
					Manifest = manifest
				};
			}
		}

		// ============================================================
		//  Manifest 构建
		// ============================================================

		private static TileManifest BuildManifest (
			LegacyTileAnalysis primary,
			List<KeyValuePair<string, LegacyTileAnalysis>> analyses)
		{
			// ---- 统计 tile 类总数 ----
			int totalTileTypes = 0;
			foreach (var kv in analyses)
				totalTileTypes += (kv.Value.TileTypeNames?.Count ?? 0);
			if (totalTileTypes == 0) totalTileTypes = analyses.Count;
			bool singleApp = totalTileTypes == 1;

			string fallbackName = Path.GetFileNameWithoutExtension (
				analyses.Count > 0 ? analyses [0].Key : "App");

			// ---- Identity.Name ----
			// 只有一个 tile 类：直接用类全名（过滤不合规字符）
			// 否则回退到 Title → AssemblyTitle → AssemblyProduct → DLL 名
			string identityName = null;
			if (singleApp
				&& primary.TileTypeNames != null
				&& primary.TileTypeNames.Count == 1)
			{
				identityName = SanitizeIdentityName (primary.TileTypeNames [0]);
			}
			if (string.IsNullOrEmpty (identityName))
			{
				identityName = FirstNonEmpty (
					primary.Title,
					primary.AssemblyTitle,
					primary.AssemblyProduct,
					fallbackName) ?? fallbackName;
			}

			// ---- Version ----
			Sidebar.Version version = PickVersion (primary);

			// ---- Author / Publisher ----
			string authorPlain = FirstNonEmpty (
				primary.Author,
				primary.AssemblyCompany,
				"Unknown") ?? "Unknown";

			string identityPublisher = "CN=" + authorPlain;
			string propertiesPublisher = authorPlain;

			// ---- Display / Description ----
			string display = FirstNonEmpty (
				primary.Title,
				primary.AssemblyTitle,
				primary.AssemblyProduct,
				fallbackName) ?? fallbackName;
			string desc = primary.CombinedDescription ?? "";

			// ---- Architecture 聚合 ----
			Sidebar.ProcessorArchitecture arch = primary.Architecture;
			foreach (var kv in analyses)
			{
				if (kv.Value.Architecture != arch)
				{
					arch = Sidebar.ProcessorArchitecture.Neutral;
					break;
				}
			}

			var identity = new TileIdentity (identityName, identityPublisher, version, arch);
			var properties = new TileProperties (display, propertiesPublisher, desc, "Icon.ico");

			var prereq = new TilePrerequisites (
				new Sidebar.Version (6, 0, 6002, 0), null);

			var apps = new List<TileApplication> ();
			foreach (var kv in analyses)
				apps.Add (BuildApplication (kv.Value, kv.Key, singleApp));

			return new TileManifest (identity, properties, prereq, apps);
		}

		private static TileApplication BuildApplication (
			LegacyTileAnalysis a, string dllName, bool singleApp)
		{
			string dllBase = Path.GetFileNameWithoutExtension (dllName);
			string display = FirstNonEmpty (
				a.Title,
				a.AssemblyTitle,
				a.AssemblyProduct,
				dllBase) ?? dllBase;

			// ★ 只有一个 tile 类 → Id 固定为 "App"
			string id = singleApp
				? "App"
				: MakeSafeId (a.Title, dllBase);

			string entry = a.EntryPointTypeName ?? "";

			var ve = new TileVisualElements (
				display,
				"Icon.ico",
				new TileVisualElementTile (28, 300, 97, true, TileOverflow.Hidden),
				new TileVisualElementFlyout (378, 721, false));

			return new TileApplication (
				id, dllName, entry, a.HasFlyout, a.HasProperties, ve);
		}

		// ============================================================
		//  辅助
		// ============================================================

		private static Sidebar.Version PickVersion (LegacyTileAnalysis a)
		{
			// 1) SidebarTileInfo.Version（Karl 的 double）
			if (a.Version.HasValue)
			{
				try { return NormalizeVersion (a.Version.Value); }
				catch { }
			}

			// 2) 程序集版本
			if (!string.IsNullOrEmpty (a.AssemblyVersionString))
			{
				try
				{
					var v = System.Version.Parse (a.AssemblyVersionString);
					int major = v.Major; if (major <= 0) major = 1;
					return new Sidebar.Version (
						(ushort)Math.Max (0, Math.Min (65535, major)),
						(ushort)Math.Max (0, Math.Min (65535, v.Minor)),
						(ushort)Math.Max (0, Math.Min (65535, v.Build)),
						(ushort)Math.Max (0, Math.Min (65535, v.Revision)));
				}
				catch { }
			}

			return new Sidebar.Version (1, 0, 0, 0);
		}

		/// <summary>
		/// 把旧 SDK 的 double 版本号（如 0.2、1.5、2.0）规范成 Sidebar.Version。
		///   - Major 为 0 时自动抬到 1
		///   - 小数部分 × 100 作为 Minor
		///   - 分量夹到 [0, 65535]
		/// </summary>
		private static Sidebar.Version NormalizeVersion (double raw)
		{
			if (double.IsNaN (raw) || double.IsInfinity (raw))
				return new Sidebar.Version (1, 0, 0, 0);

			int major = (int)raw;
			int minor = (int)Math.Round ((raw - major) * 100);

			if (major <= 0) major = 1;
			if (major > 65535) major = 65535;
			if (minor < 0) minor = 0;
			if (minor > 65535) minor = 65535;

			return new Sidebar.Version ((ushort)major, (ushort)minor, 0, 0);
		}

		/// <summary>
		/// Identity.Name 过滤：保留字母数字（任意语言）以及 '.' 和 '-'。
		/// 其余字符直接丢弃。
		/// </summary>
		private static string SanitizeIdentityName (string raw)
		{
			if (string.IsNullOrEmpty (raw)) return null;
			var sb = new StringBuilder (raw.Length);
			foreach (var c in raw)
			{
				if (char.IsLetterOrDigit (c) || c == '.' || c == '-')
					sb.Append (c);
			}
			return sb.Length > 0 ? sb.ToString () : null;
		}

		/// <summary>
		/// 生成 Application Id（多 tile 类时才用）。
		/// preferred 清洗后有效就用它，否则用 fallback，都不行用 "Tile"。
		/// </summary>
		private static string MakeSafeId (string preferred, string fallback)
		{
			string result = SanitizeId (preferred);
			if (string.IsNullOrEmpty (result)) result = SanitizeId (fallback);
			if (string.IsNullOrEmpty (result)) result = "Tile";
			return result;
		}

		private static string SanitizeId (string text)
		{
			if (string.IsNullOrEmpty (text)) return null;
			var sb = new StringBuilder ();
			foreach (var c in text)
			{
				if (c < 128 && char.IsLetterOrDigit (c)) sb.Append (c);
				else if (c >= 128) { /* 非 ASCII 跳过 */ }
				else if (sb.Length > 0 && sb [sb.Length - 1] != '_') sb.Append ('_');
			}
			return sb.ToString ().Trim ('_');
		}

		private static string FirstNonEmpty (params string [] values)
		{
			foreach (var v in values)
				if (!string.IsNullOrWhiteSpace (v)) return v.Trim ();
			return null;
		}
	}

	public sealed class LegacyTileImportResult
	{
		public string SourcePath { get; set; }
		public LegacyTileAnalysis Analysis { get; set; }
		public Dictionary<string, byte []> Files { get; set; }
		public TileManifest Manifest { get; set; }
	}
}
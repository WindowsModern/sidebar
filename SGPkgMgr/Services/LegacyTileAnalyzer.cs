using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Sidebar;

namespace SGPkgMgr.Services
{
	/// <summary>
	/// 分析旧磁贴程序集（Karl's Sidebar / LongBar），提取元数据。
	///
	/// 依赖解析策略（优先级从高到低）：
	///   1. AppDomain 里已经加载的同名程序集（由 App.PreloadLegacySdks 保证）；
	///   2. Assembly.Load(simpleName) —— 走 CLR 默认解析；
	///   3. probeDir 向上扫描得到的 dll，才用 Assembly.LoadFrom。
	///
	/// 类型匹配一律用 FullName 字符串（"TileLib.BaseTile" 等）。
	/// </summary>
	public static class LegacyTileAnalyzer
	{
		private const string KarlTileFullName = "Applications.Sidebar.Tile";
		private const string KarlTileInfoFullName = "Applications.Sidebar.SidebarTileInfo";
		private const string KarlContactFullName = "Applications.Sidebar.SidebarDeveloperContactInfo";

		private const string LongBarTileFullName = "TileLib.BaseTile";
		private const string LongBarTileInfoFullName = "TileLib.TileInfo";

		private const int MaxUpLevels = 3;

		// ============================================================
		//  公开入口
		// ============================================================

		public static LegacyTileAnalysis Analyze (string dllPath, Action<string> log = null)
		{
			if (string.IsNullOrEmpty (dllPath) || !File.Exists (dllPath))
			{
				Emit (log, "Analyze: file not found: " + dllPath);
				return null;
			}

			string fullPath = Path.GetFullPath (dllPath);
			string probeDir = Path.GetDirectoryName (fullPath);

			Emit (log, "Analyze: loading " + fullPath);

			return AnalyzeWithResolver (
				() => Assembly.LoadFrom (fullPath),
				probeDir,
				log);
		}

		public static LegacyTileAnalysis Analyze (
			byte [] assemblyBytes, string probeDir = null, Action<string> log = null)
		{
			if (assemblyBytes == null || assemblyBytes.Length == 0)
			{
				Emit (log, "Analyze: empty bytes");
				return null;
			}

			return AnalyzeWithResolver (
				() => Assembly.Load (assemblyBytes),
				probeDir,
				log);
		}

		// ============================================================
		//  主流程
		// ============================================================

		private static LegacyTileAnalysis AnalyzeWithResolver (
			Func<Assembly> loader, string probeDir, Action<string> log)
		{
			var dllCache = BuildProbeCache (probeDir, MaxUpLevels, log);

			ResolveEventHandler handler = (sender, e) =>
				ResolveAssembly (e.Name, dllCache, log);

			AppDomain.CurrentDomain.AssemblyResolve += handler;
			try
			{
				return AnalyzeCore (loader, log);
			}
			finally
			{
				AppDomain.CurrentDomain.AssemblyResolve -= handler;
			}
		}

		private static Assembly ResolveAssembly (
			string fullName, Dictionary<string, string> dllCache, Action<string> log)
		{
			string simpleName;
			try { simpleName = new AssemblyName (fullName).Name; }
			catch { return null; }

			Emit (log, "Analyze: AssemblyResolve -> " + simpleName);

			foreach (var a in AppDomain.CurrentDomain.GetAssemblies ())
			{
				try
				{
					if (string.Equals (a.GetName ().Name, simpleName,
						StringComparison.OrdinalIgnoreCase))
					{
						Emit (log, "  using already-loaded " + a.FullName);
						return a;
					}
				}
				catch { }
			}

			try
			{
				var a = Assembly.Load (simpleName);
				if (a != null)
				{
					Emit (log, "  Assembly.Load('" + simpleName + "') -> " + a.FullName);
					return a;
				}
			}
			catch (Exception ex)
			{
				Emit (log, "  Assembly.Load('" + simpleName + "') failed: " + ex.Message);
			}

			string path;
			if (dllCache.TryGetValue (simpleName, out path) && File.Exists (path))
			{
				Emit (log, "  LoadFrom -> " + path);
				try { return Assembly.LoadFrom (path); }
				catch (Exception ex)
				{
					Emit (log, "  LoadFrom failed: " + ex.Message);
				}
			}

			Emit (log, "  NOT FOUND.");
			return null;
		}

		private static LegacyTileAnalysis AnalyzeCore (Func<Assembly> loader, Action<string> log)
		{
			Assembly asm;
			try
			{
				asm = loader ();
				Emit (log, "Analyze: loaded " + asm.FullName);
			}
			catch (Exception ex)
			{
				Emit (log, "Analyze: load failed: " + ex.GetType ().Name + ": " + ex.Message);
				return null;
			}

			Type [] types;
			try
			{
				types = asm.GetTypes ();
				Emit (log, "Analyze: GetTypes() => " + types.Length + " types");
			}
			catch (ReflectionTypeLoadException ex)
			{
				types = ex.Types.Where (t => t != null).ToArray ();
				Emit (log, "Analyze: ReflectionTypeLoadException, "
					+ types.Length + " / " + ex.Types.Length + " survived");

				if (ex.LoaderExceptions != null)
				{
					foreach (var le in ex.LoaderExceptions.Where (e => e != null).Take (10))
						Emit (log, "  loader: " + le.GetType ().Name + ": " + le.Message);
				}

				if (types.Length == 0)
				{
					Emit (log, "Analyze: no types survived, giving up.");
					return null;
				}
			}
			catch (Exception ex)
			{
				Emit (log, "Analyze: GetTypes failed: " + ex.GetType ().Name + ": " + ex.Message);
				return null;
			}

			Emit (log, "Analyze: sample types:");
			foreach (var t in types.Take (15))
				Emit (log, "  " + (t.FullName ?? t.Name));

			// 优先 LongBar，其次 KarlSidebar；收集所有子类
			List<Type> tileTypes;
			string sdk;
			var longBarTypes = AllConcreteSubclasses (types, LongBarTileFullName);
			if (longBarTypes.Count > 0)
			{
				tileTypes = longBarTypes;
				sdk = "LongBar";
			}
			else
			{
				var karlTypes = AllConcreteSubclasses (types, KarlTileFullName);
				if (karlTypes.Count > 0)
				{
					tileTypes = karlTypes;
					sdk = "KarlSidebar";
				}
				else
				{
					Emit (log, "Analyze: no concrete subclass of '"
						+ LongBarTileFullName + "' or '" + KarlTileFullName + "' found.");
					return null;
				}
			}

			foreach (var t in tileTypes)
				Emit (log, "Analyze: tile class = " + t.FullName + " (" + sdk + ")");

			var result = new LegacyTileAnalysis {
				SourceSdk = sdk,
				EntryPointTypeName = tileTypes [0].FullName,
				TileTypeNames = tileTypes.Select (t => t.FullName).ToList (),
				Architecture = MapArchitecture (asm)
			};
			Emit (log, "Analyze: architecture = " + result.Architecture);

			// 程序集级信息（用于回退）
			FillAssemblyInfo (result, asm, log);

			// 特性级信息
			if (sdk == "LongBar")
			{
				var info = FindAttribute (asm, LongBarTileInfoFullName, log);
				if (info != null)
				{
					result.Title = ReadString (info, "Name");
					result.HasFlyout = ReadBool (info, "hasflyout") ?? false;
					result.HasProperties = ReadBool (info, "hasOptions") ?? false;
				}
			}
			else
			{
				var info = FindAttribute (asm, KarlTileInfoFullName, log);
				if (info != null)
				{
					result.Title = ReadString (info, "Title");
					result.Author = ReadString (info, "Author");
					result.Copyright = ReadString (info, "Copyright");
					result.Description = ReadString (info, "Description");
					result.Version = ReadDouble (info, "Version");
				}

				var contact = FindAttribute (asm, KarlContactFullName, log);
				if (contact != null)
				{
					result.Email = ReadString (contact, "email");
					result.Website = ReadString (contact, "website");
				}

				result.HasFlyout = true;
				result.HasProperties = false;
			}

			return result;
		}

		// ============================================================
		//  程序集级信息
		// ============================================================

		private static void FillAssemblyInfo (LegacyTileAnalysis result, Assembly asm, Action<string> log)
		{
			try
			{
				var name = asm.GetName ();
				if (name != null && name.Version != null)
					result.AssemblyVersionString = name.Version.ToString ();
			}
			catch { }

			result.AssemblyTitle = GetAssemblyString<AssemblyTitleAttribute> (asm, a => a.Title);
			result.AssemblyProduct = GetAssemblyString<AssemblyProductAttribute> (asm, a => a.Product);
			result.AssemblyCompany = GetAssemblyString<AssemblyCompanyAttribute> (asm, a => a.Company);
			result.AssemblyCopyright = GetAssemblyString<AssemblyCopyrightAttribute> (asm, a => a.Copyright);
			result.AssemblyDescription = GetAssemblyString<AssemblyDescriptionAttribute> (asm, a => a.Description);

			Emit (log, "Analyze: asm title='" + (result.AssemblyTitle ?? "")
				+ "', company='" + (result.AssemblyCompany ?? "")
				+ "', version='" + (result.AssemblyVersionString ?? "") + "'");
		}

		private static string GetAssemblyString<TAttr> (Assembly asm, Func<TAttr, string> pick)
			where TAttr : Attribute
		{
			try
			{
				var attrs = asm.GetCustomAttributes (typeof (TAttr), false);
				if (attrs == null || attrs.Length == 0) return null;
				var a = attrs [0] as TAttr;
				return a != null ? pick (a) : null;
			}
			catch { return null; }
		}

		// ============================================================
		//  架构检测
		// ============================================================

		private static Sidebar.ProcessorArchitecture MapArchitecture (Assembly asm)
		{
			System.Reflection.ProcessorArchitecture clrArch;
			try { clrArch = asm.GetName ().ProcessorArchitecture; }
			catch { return Sidebar.ProcessorArchitecture.Neutral; }

			switch (clrArch)
			{
				case System.Reflection.ProcessorArchitecture.X86:
					return Sidebar.ProcessorArchitecture.X86;

				case System.Reflection.ProcessorArchitecture.Amd64:
					return Sidebar.ProcessorArchitecture.X64;

				case System.Reflection.ProcessorArchitecture.IA64:
					return Sidebar.ProcessorArchitecture.IA64;

				case System.Reflection.ProcessorArchitecture.MSIL:
					return Sidebar.ProcessorArchitecture.Neutral;

				default:
					return Sidebar.ProcessorArchitecture.Neutral;
			}
		}

		// ============================================================
		//  Fallback：probe 缓存
		// ============================================================

		private static Dictionary<string, string> BuildProbeCache (
			string startDir, int maxUpLevels, Action<string> log)
		{
			var cache = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
			if (string.IsNullOrEmpty (startDir)) return cache;

			DirectoryInfo dir;
			try { dir = new DirectoryInfo (startDir); }
			catch { return cache; }

			for (int level = 0; level <= maxUpLevels && dir != null; level++)
			{
				int added = AddDllsFromDir (dir, cache);
				try
				{
					foreach (var sub in dir.GetDirectories ())
						added += AddDllsFromDir (sub, cache);
				}
				catch { }

				Emit (log, "Analyze: probe[" + level + "] " + dir.FullName
					+ " -> +" + added + " (cache=" + cache.Count + ")");

				try { dir = dir.Parent; }
				catch { break; }
			}

			return cache;
		}

		private static int AddDllsFromDir (DirectoryInfo dir, Dictionary<string, string> cache)
		{
			int added = 0;
			try
			{
				foreach (var f in dir.GetFiles ("*.dll"))
				{
					var name = Path.GetFileNameWithoutExtension (f.Name);
					if (!cache.ContainsKey (name))
					{
						cache [name] = f.FullName;
						added++;
					}
				}
			}
			catch { }
			return added;
		}

		// ============================================================
		//  反射辅助
		// ============================================================

		private static List<Type> AllConcreteSubclasses (Type [] types, string baseFullName)
		{
			var list = new List<Type> ();
			foreach (var t in types)
			{
				if (t == null || t.IsAbstract) continue;
				if (IsSubclassOf (t, baseFullName)) list.Add (t);
			}
			return list;
		}

		private static bool IsSubclassOf (Type t, string baseFullName)
		{
			var cur = t.BaseType;
			while (cur != null)
			{
				if (string.Equals (cur.FullName, baseFullName, StringComparison.Ordinal))
					return true;
				try { cur = cur.BaseType; }
				catch { return false; }
			}
			return false;
		}

		private static Attribute FindAttribute (Assembly asm, string fullName, Action<string> log)
		{
			try
			{
				foreach (var a in asm.GetCustomAttributes (true))
				{
					var attr = a as Attribute;
					if (attr != null
						&& string.Equals (attr.GetType ().FullName, fullName, StringComparison.Ordinal))
						return attr;
				}
				Emit (log, "Analyze: attribute '" + fullName + "' not found.");
			}
			catch (Exception ex)
			{
				Emit (log, "Analyze: GetCustomAttributes failed: " + ex.Message);
			}
			return null;
		}

		private static string ReadString (object attr, string field)
		{
			var f = attr.GetType ().GetField (field, BindingFlags.Public | BindingFlags.Instance);
			return f != null && f.FieldType == typeof (string) ? (string)f.GetValue (attr) : null;
		}

		private static bool? ReadBool (object obj, string field)
		{
			var f = obj.GetType ().GetField (field, BindingFlags.Public | BindingFlags.Instance);
			return f != null && f.FieldType == typeof (bool) ? (bool?)f.GetValue (obj) : null;
		}

		private static double? ReadDouble (object obj, string field)
		{
			var f = obj.GetType ().GetField (field, BindingFlags.Public | BindingFlags.Instance);
			return f != null && f.FieldType == typeof (double) ? (double?)f.GetValue (obj) : null;
		}

		private static void Emit (Action<string> log, string msg)
		{
			if (msg == null) return;
			System.Diagnostics.Debug.WriteLine (msg);
			if (log != null)
			{
				try { log (msg); } catch { }
			}
		}
	}

	public sealed class LegacyTileAnalysis
	{
		public string SourceSdk { get; set; }
		public string EntryPointTypeName { get; set; }

		/// <summary>该 DLL 里所有继承 BaseTile 的非抽象类全名。</summary>
		public List<string> TileTypeNames { get; set; }

		public string Title { get; set; }
		public string Author { get; set; }
		public string Copyright { get; set; }
		public string Description { get; set; }

		public string Email { get; set; }
		public string Website { get; set; }

		public bool HasFlyout { get; set; }
		public bool HasProperties { get; set; }

		public double? Version { get; set; }

		/// <summary>从 DLL 检测出的处理器架构。</summary>
		public Sidebar.ProcessorArchitecture Architecture { get; set; }

		// ---- 程序集级回退字段 ----
		public string AssemblyTitle { get; set; }
		public string AssemblyProduct { get; set; }
		public string AssemblyCompany { get; set; }
		public string AssemblyCopyright { get; set; }
		public string AssemblyDescription { get; set; }
		public string AssemblyVersionString { get; set; }

		/// <summary>
		/// 描述文本按 "&lt;Description&gt;[\n\n&lt;Copyright&gt;]" 拼接。
		/// 缺 SidebarTileInfo 时回退到 AssemblyDescription / AssemblyCopyright。
		/// </summary>
		public string CombinedDescription
		{
			get
			{
				string desc = FirstNonEmpty (Description, AssemblyDescription) ?? "";
				string copy = FirstNonEmpty (Copyright, AssemblyCopyright) ?? "";
				if (string.IsNullOrEmpty (copy)) return desc;
				if (string.IsNullOrEmpty (desc)) return copy;
				return desc + "\n\n" + copy;
			}
		}

		private static string FirstNonEmpty (params string [] values)
		{
			foreach (var v in values)
				if (!string.IsNullOrWhiteSpace (v)) return v.Trim ();
			return null;
		}
	}
}
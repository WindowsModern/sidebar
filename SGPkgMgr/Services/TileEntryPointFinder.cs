using System;
using System.Linq;
using System.Reflection;

namespace SGPkgMgr.Services
{
	/// <summary>
	/// 从磁贴程序集中查找入口点（第一个继承 TileBase 的非抽象类的全名）。
	///
	/// 说明：不使用 typeof(TileBase) 直接比较，而是按类型全名匹配
	/// （"Sidebar.TileBase"），避免被加载的磁贴引用了与工具不同版本的
	/// CoreLib / TileClient 时 IsSubclassOf 失败。
	/// </summary>
	public static class TileEntryPointFinder
	{
		private const string TileBaseFullName = "Sidebar.TileBase";

		/// <summary>
		/// 从程序集字节里查找第一个继承 TileBase 的非抽象类的全名。
		/// 失败（不是有效程序集 / 没有匹配类 / 反射异常）返回 null。
		/// </summary>
		public static string FindEntryPoint (byte [] assemblyBytes)
		{
			if (assemblyBytes == null || assemblyBytes.Length == 0) return null;
			Assembly asm;
			try
			{
				// Load(byte[]) 不会锁定源文件；同一份字节多次 Load 会产生多个副本，
				// 对本工具的一次性操作来说可以接受。
				asm = Assembly.Load (assemblyBytes);
			}
			catch
			{
				return null;
			}
			return FindEntryPoint (asm);
		}

		public static string FindEntryPoint (Assembly asm)
		{
			if (asm == null) return null;

			Type [] types;
			try
			{
				types = asm.GetTypes ();
			}
			catch (ReflectionTypeLoadException ex)
			{
				types = ex.Types.Where (t => t != null).ToArray ();
			}
			catch
			{
				return null;
			}

			foreach (var t in types)
			{
				if (t == null) continue;
				if (t.IsAbstract) continue;
				if (IsTileBaseSubclass (t))
					return t.FullName;
			}
			return null;
		}

		private static bool IsTileBaseSubclass (Type t)
		{
			var cur = t.BaseType;
			while (cur != null)
			{
				if (string.Equals (cur.FullName, TileBaseFullName, StringComparison.Ordinal))
					return true;
				cur = cur.BaseType;
			}
			return false;
		}
	}
}
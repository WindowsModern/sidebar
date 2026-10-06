using System;
using System.Collections.Generic;
using System.Reflection;

namespace Sidebar
{
	public static class SdkCompatible
	{
		public static TileBase GetTileInstance (Assembly lib, string entryPoint)
		{
			if (lib == null) return null;
			Type [] types = GetLoadableTypes (lib);
			if (types.Length == 0) return null;
			Type explicitType = FindByEntryPoint (types, entryPoint);
			Type nativeType = Pick (explicitType, types, IsNativeTileType);
			if (nativeType != null) return CreateInstance<TileBase> (nativeType);
			Type karlType = Pick (explicitType, types, IsKarlSidebarTileType);
			if (karlType != null)
			{
				Applications.Sidebar.BaseTile raw = CreateInstance<Applications.Sidebar.BaseTile> (karlType);
				if (raw != null) return new KarlSidebarTile (raw);
			}
			Type longBarType = Pick (explicitType, types, IsLongBarTileType);
			if (longBarType != null)
			{
				TileLib.BaseTile raw = CreateInstance<TileLib.BaseTile> (longBarType);
				if (raw != null) return new LongBarTile (raw);
			}
			return null;
		}
		private static Type [] GetLoadableTypes (Assembly lib)
		{
			try
			{
				return lib.GetTypes ();
			}
			catch (ReflectionTypeLoadException ex)
			{
				if (ex.Types == null) return new Type [0];
				List<Type> list = new List<Type> ();
				for (int i = 0; i < ex.Types.Length; i++)
					if (ex.Types [i] != null) list.Add (ex.Types [i]);
				return list.ToArray ();
			}
			catch
			{
				return new Type [0];
			}
		}
		private static Type FindByEntryPoint (Type [] types, string entryPoint)
		{
			if (string.IsNullOrEmpty (entryPoint) || types == null) return null;
			for (int i = 0; i < types.Length; i++)
			{
				Type t = types [i];
				if (t == null) continue;
				if (string.Equals (t.FullName, entryPoint, StringComparison.OrdinalIgnoreCase))
					return t;
				if (string.Equals (t.Name, entryPoint, StringComparison.OrdinalIgnoreCase))
					return t;
			}
			return null;
		}
		private static Type Pick (Type explicitType, Type [] types, Func<Type, bool> predicate)
		{
			if (explicitType != null && predicate (explicitType))
				return explicitType;
			if (types == null) return null;
			for (int i = 0; i < types.Length; i++)
			{
				Type t = types [i];
				if (t == null) continue;
				if (predicate (t)) return t;
			}
			return null;
		}
		private static T CreateInstance<T> (Type t) where T : class
		{
			return Activator.CreateInstance (t) as T;
		}
		private static bool IsNativeTileType (Type t)
		{
			if (t == null || !t.IsClass || t.IsAbstract) return false;
			if (t == typeof (TileBase)) return false;
			return typeof (TileBase).IsAssignableFrom (t);
		}
		private static bool IsKarlSidebarTileType (Type t)
		{
			if (t == null || !t.IsClass || t.IsAbstract) return false;
			Type baseType = typeof (Applications.Sidebar.BaseTile);
			if (t == baseType) return false;
			return baseType.IsAssignableFrom (t);
		}
		private static bool IsLongBarTileType (Type t)
		{
			if (t == null || !t.IsClass || t.IsAbstract) return false;
			Type baseType = typeof (TileLib.BaseTile);
			if (t == baseType) return false;
			return baseType.IsAssignableFrom (t);
		}
	}
}
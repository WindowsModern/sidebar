using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32.Init.NativeCompatible;
using Sidebar.NativeCompatible;

namespace Applications.Sidebar
{
	public class Settings
	{
		private static readonly string SettingsDir = Path.Combine (
			Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData),
			"Windows Modern\\Sidebar\\Tiles\\KarlSidebar");

		private static readonly object SyncRoot = new object ();
		private static readonly Dictionary<Guid, PerTileStorage> storages = new Dictionary<Guid, PerTileStorage> ();

		private class PerTileStorage
		{
			public InitConfig Ini;
			public InitSection IniSection;
			public XmlConfig Xml;
			public ConfigSection XmlSection;
		}

		private static PerTileStorage GetStorage (Guid key)
		{
			lock (SyncRoot)
			{
				PerTileStorage storage;
				if (storages.TryGetValue (key, out storage))
					return storage;
				try
				{
					if (!Directory.Exists (SettingsDir))
						Directory.CreateDirectory (SettingsDir);
					string keyName = key.ToString ("D");
					string iniPath = Path.Combine (SettingsDir, keyName + ".ini");
					string xmlPath = Path.Combine (SettingsDir, keyName + ".xml");
					InitConfig ini = new InitConfig (iniPath);
					XmlConfig xml = new XmlConfig (xmlPath);
					storage = new PerTileStorage {
						Ini = ini,
						IniSection = ini.GetSection ("Settings"),
						Xml = xml,
						XmlSection = xml.Global
					};
					storages [key] = storage;
					return storage;
				}
				catch
				{
					return null;
				}
			}
		}

		private static bool IsIniFriendlyType (Type t)
		{
			if (t == null) return false;
			if (t.IsPrimitive) return true;
			if (t == typeof (string)) return true;
			if (t == typeof (decimal)) return true;
			if (t == typeof (DateTime)) return true;
			if (t == typeof (TimeSpan)) return true;
			if (t == typeof (Guid)) return true;
			if (t.IsEnum) return true;
			return false;
		}

		public static object GetSetting (Guid key, string name)
		{
			if (string.IsNullOrEmpty (name)) return null;
			PerTileStorage storage = GetStorage (key);
			if (storage == null) return null;

			// 优先读 XML（保留原类型）
			object value = null;
			try { value = storage.XmlSection [name]; }
			catch { value = null; }
			if (value != null) return value;

			// XML 里没有时才回退到 INI（通常是人工写入的）
			try
			{
				string s = storage.IniSection.GetKey (name).ReadString ("");
				if (!string.IsNullOrEmpty (s)) return s;
			}
			catch { }
			return null;
		}

		public static bool StoreSetting (Guid key, string name, object value)
		{
			if (string.IsNullOrEmpty (name)) return false;
			PerTileStorage storage = GetStorage (key);
			if (storage == null) return false;

			// XML：始终写入，保留类型
			bool ok = true;
			try { storage.XmlSection [name] = value; }
			catch { ok = false; }

			// INI：简单类型额外镜像一份，便于人工查看 / 编辑
			if (value != null && IsIniFriendlyType (value.GetType ()))
			{
				try { storage.IniSection.GetKey (name).Write (value.ToString ()); }
				catch { }
			}
			else if (value == null)
			{
				// 值为 null 时清掉 INI 镜像，保持两边一致
				try { storage.IniSection.GetKey (name).DeleteKey (); }
				catch { }
			}

			return ok;
		}
	}
}
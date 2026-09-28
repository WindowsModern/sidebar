using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Xml;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
namespace Microsoft.Win32.Init.NativeCompatible
{
	internal static class IniFile
	{
		[DllImport ("kernel32.dll", CharSet = CharSet.Ansi)]
		private static extern uint GetPrivateProfileStringA (string lpAppName, string lpKeyName, string lpDefault, StringBuilder lpReturnedString, uint nSize, string lpFileName);
		[DllImport ("kernel32.dll", CharSet = CharSet.Unicode)]
		private static extern uint GetPrivateProfileStringW (string lpAppName, string lpKeyName, string lpDefault, StringBuilder lpReturnedString, uint nSize, string lpFileName);
		[DllImport ("kernel32.dll", CharSet = CharSet.Ansi)]
		private static extern uint GetPrivateProfileSectionA (string lpAppName, byte [] lpReturnedString, uint nSize, string lpFileName);
		[DllImport ("kernel32.dll", CharSet = CharSet.Unicode)]
		private static extern uint GetPrivateProfileSectionW (string lpAppName, char [] lpReturnedString, uint nSize, string lpFileName);
		[DllImport ("kernel32.dll", CharSet = CharSet.Ansi)]
		private static extern uint GetPrivateProfileSectionNamesA (byte [] lpszReturnBuffer, uint nSize, string lpFileName);
		[DllImport ("kernel32.dll", CharSet = CharSet.Unicode)]
		private static extern uint GetPrivateProfileSectionNamesW (char [] lpszReturnBuffer, uint nSize, string lpFileName);
		[DllImport ("kernel32.dll", CharSet = CharSet.Ansi)]
		private static extern uint GetPrivateProfileIntA (string lpAppName, string lpKeyName, int nDefault, string lpFileName);
		[DllImport ("kernel32.dll", CharSet = CharSet.Unicode)]
		private static extern uint GetPrivateProfileIntW (string lpAppName, string lpKeyName, int nDefault, string lpFileName);
		[DllImport ("kernel32.dll", CharSet = CharSet.Ansi)]
		private static extern bool WritePrivateProfileStringA (string lpAppName, string lpKeyName, string lpString, string lpFileName);
		[DllImport ("kernel32.dll", CharSet = CharSet.Unicode)]
		private static extern bool WritePrivateProfileStringW (string lpAppName, string lpKeyName, string lpString, string lpFileName);
		[DllImport ("kernel32.dll", CharSet = CharSet.Ansi)]
		private static extern bool WritePrivateProfileSectionA (string lpAppName, string lpString, string lpFileName);
		[DllImport ("kernel32.dll", CharSet = CharSet.Unicode)]
		private static extern bool WritePrivateProfileSectionW (string lpAppName, string lpString, string lpFileName);
		[DllImport ("kernel32.dll", CharSet = CharSet.Ansi)]
		private static extern bool GetPrivateProfileStructA (string lpAppName, string lpKeyName, IntPtr lpStruct, uint uSize, string lpFileName);
		[DllImport ("kernel32.dll", CharSet = CharSet.Unicode)]
		private static extern bool GetPrivateProfileStructW (string lpAppName, string lpKeyName, IntPtr lpStruct, uint uSize, string lpFileName);
		[DllImport ("kernel32.dll", CharSet = CharSet.Ansi)]
		private static extern bool WritePrivateProfileStructA (string lpAppName, string lpKeyName, IntPtr lpStruct, uint uSize, string lpFileName);
		[DllImport ("kernel32.dll", CharSet = CharSet.Unicode)]
		private static extern bool WritePrivateProfileStructW (string lpAppName, string lpKeyName, IntPtr lpStruct, uint uSize, string lpFileName);

		public static string GetPrivateProfileStringA (string filePath, string section, string key)
		{
			return GetPrivateProfileStringA (filePath, section, key, "");
		}
		public static string GetPrivateProfileStringA (string filePath, string section, string key, string defaultValue)
		{
			StringBuilder buf = new StringBuilder (32768);
			GetPrivateProfileStringA (section, key, defaultValue, buf, (uint)buf.Capacity, filePath);
			return buf.ToString ();
		}
		public static string GetPrivateProfileStringW (string filePath, string section, string key)
		{
			return GetPrivateProfileStringW (filePath, section, key, "");
		}
		public static string GetPrivateProfileStringW (string filePath, string section, string key, string defaultValue)
		{
			StringBuilder buf = new StringBuilder (32768);
			GetPrivateProfileStringW (section, key, defaultValue, buf, (uint)buf.Capacity, filePath);
			return buf.ToString ();
		}
		public static uint GetPrivateProfileIntA (string filePath, string section, string key)
		{
			return GetPrivateProfileIntA (filePath, section, key, 0);
		}
		public static uint GetPrivateProfileIntA (string filePath, string section, string key, int defaultValue)
		{
			return GetPrivateProfileIntA (section, key, defaultValue, filePath);
		}
		public static uint GetPrivateProfileIntW (string filePath, string section, string key)
		{
			return GetPrivateProfileIntW (filePath, section, key, 0);
		}
		public static uint GetPrivateProfileIntW (string filePath, string section, string key, int defaultValue)
		{
			return GetPrivateProfileIntW (section, key, defaultValue, filePath);
		}
		public static bool WritePrivateProfileString (string filePath, string section, string key, string value)
		{
			return WritePrivateProfileStringW (section, key, value, filePath);
		}
		public static int GetPrivateProfileSectionA (string filePath, string section, List<string> output)
		{
			byte [] buf = new byte [32768];
			uint len = GetPrivateProfileSectionA (section, buf, (uint)buf.Length, filePath);
			output.Clear ();
			if (len == 0) return 0;
			int i = 0;
			while (i < len)
			{
				int start = i;
				while (i < len && buf [i] != 0) i++;
				if (i > start) output.Add (Encoding.Default.GetString (buf, start, i - start));
				i++;
			}
			return output.Count;
		}
		public static int GetPrivateProfileSectionW (string filePath, string section, List<string> output)
		{
			char [] buf = new char [32768];
			uint len = GetPrivateProfileSectionW (section, buf, (uint)buf.Length, filePath);
			output.Clear ();
			if (len == 0) return 0;
			int i = 0;
			while (i < len)
			{
				int start = i;
				while (i < len && buf [i] != '\0') i++;
				if (i > start) output.Add (new string (buf, start, i - start));
				i++;
			}
			return output.Count;
		}
		public static int GetPrivateProfileSectionNamesA (string filePath, List<string> output)
		{
			byte [] buf = new byte [32768];
			uint len = GetPrivateProfileSectionNamesA (buf, (uint)buf.Length, filePath);
			output.Clear ();
			if (len == 0) return 0;
			int i = 0;
			while (i < len)
			{
				int start = i;
				while (i < len && buf [i] != 0) i++;
				if (i > start) output.Add (Encoding.Default.GetString (buf, start, i - start));
				i++;
			}
			return output.Count;
		}
		public static int GetPrivateProfileSectionNamesW (string filePath, List<string> output)
		{
			char [] buf = new char [32768];
			uint len = GetPrivateProfileSectionNamesW (buf, (uint)buf.Length, filePath);
			output.Clear ();
			if (len == 0) return 0;
			int i = 0;
			while (i < len)
			{
				int start = i;
				while (i < len && buf [i] != '\0') i++;
				if (i > start) output.Add (new string (buf, start, i - start));
				i++;
			}
			return output.Count;
		}
		public static bool WritePrivateProfileSectionA (string filePath, string section, List<string> lines)
		{
			string buf = string.Join ("\0", lines.ToArray ()) + "\0\0";
			return WritePrivateProfileSectionA (section, buf, filePath);
		}
		public static bool WritePrivateProfileSectionW (string filePath, string section, List<string> lines)
		{
			string buf = string.Join ("\0", lines.ToArray ()) + "\0\0";
			return WritePrivateProfileSectionW (section, buf, filePath);
		}
		public static bool GetPrivateProfileStructA (string filePath, string section, string key, IntPtr output, uint size)
		{
			return GetPrivateProfileStructA (section, key, output, size, filePath);
		}
		public static bool GetPrivateProfileStructW (string filePath, string section, string key, IntPtr output, uint size)
		{
			return GetPrivateProfileStructW (section, key, output, size, filePath);
		}
		public static bool WritePrivateProfileStructA (string filePath, string section, string key, IntPtr data, uint size)
		{
			return WritePrivateProfileStructA (section, key, data, size, filePath);
		}
		public static bool WritePrivateProfileStructW (string filePath, string section, string key, IntPtr data, uint size)
		{
			return WritePrivateProfileStructW (section, key, data, size, filePath);
		}
		public static int GetPrivateProfileKeysA (string filePath, string section, List<string> keys)
		{
			List<string> lines = new List<string> ();
			GetPrivateProfileSectionA (filePath, section, lines);
			keys.Clear ();
			for (int i = 0; i < lines.Count; i++)
			{
				int pos = lines [i].IndexOf ('=');
				if (pos != -1) keys.Add (lines [i].Substring (0, pos));
			}
			return keys.Count;
		}
		public static int GetPrivateProfileKeysW (string filePath, string section, List<string> keys)
		{
			List<string> lines = new List<string> ();
			GetPrivateProfileSectionW (filePath, section, lines);
			keys.Clear ();
			for (int i = 0; i < lines.Count; i++)
			{
				int pos = lines [i].IndexOf ('=');
				if (pos != -1) keys.Add (lines [i].Substring (0, pos));
			}
			return keys.Count;
		}
		public static bool DeletePrivateProfileKeyA (string filePath, string section, string key)
		{
			return WritePrivateProfileStringA (section, key, null, filePath);
		}
		public static bool DeletePrivateProfileKeyW (string filePath, string section, string key)
		{
			return WritePrivateProfileStringW (section, key, null, filePath);
		}
		public static bool DeletePrivateProfileSectionA (string filePath, string section)
		{
			return WritePrivateProfileStringA (section, null, null, filePath);
		}
		public static bool DeletePrivateProfileSectionW (string filePath, string section)
		{
			return WritePrivateProfileStringW (section, null, null, filePath);
		}
	}

	internal static class BoolHelper
	{
		public static readonly string [] trueValue = { "true", "zhen", "yes", "真", "1" };
		public static readonly string [] falseValue = { "false", "jia", "no", "假", "0" };

		public static bool ConvertToBool (string str)
		{
			if (str == null) throw new ArgumentNullException ("str");
			str = str.Trim ().ToLowerInvariant ();
			int i;
			for (i = 0; i < trueValue.Length; i++)
				if (trueValue [i].ToLowerInvariant () == str) return true;
			for (i = 0; i < falseValue.Length; i++)
				if (falseValue [i].ToLowerInvariant () == str) return false;
			throw new FormatException (string.Format ("Cannot convert '{0}' to bool.", str));
		}

		public static bool TryParseBool (string str, out bool result)
		{
			result = false;
			if (str == null) return false;
			str = str.Trim ().ToLowerInvariant ();
			int i;
			for (i = 0; i < trueValue.Length; i++)
				if (trueValue [i].ToLowerInvariant () == str) { result = true; return true; }
			for (i = 0; i < falseValue.Length; i++)
				if (falseValue [i].ToLowerInvariant () == str) { result = false; return true; }
			return false;
		}
	}

	[ComVisible (true)]
	public class RefString
	{
		public string Value { get; set; }

		public RefString (string value) { Value = value; }

		public static RefString operator + (RefString a, RefString b)
		{
			if (ReferenceEquals (a, null)) return b;
			if (ReferenceEquals (b, null)) return a;
			return new RefString (a.Value + b.Value);
		}
		public static RefString operator + (RefString a, string b)
		{
			if (ReferenceEquals (a, null)) return new RefString (b);
			return new RefString (a.Value + b);
		}
		public static RefString operator + (string a, RefString b)
		{
			if (ReferenceEquals (b, null)) return new RefString (a);
			return new RefString (a + b.Value);
		}
		public static implicit operator RefString (string v) { return new RefString (v); }
		public static implicit operator string (RefString r)
		{
			if (r?.Equals (null) ?? true) return null;
			return r.Value;
		}
		public static bool operator == (RefString a, RefString b)
		{
			if (ReferenceEquals (a, b)) return true;
			if (ReferenceEquals (a, null) || ReferenceEquals (b, null)) return false;
			return a.Value == b.Value;
		}
		public static bool operator != (RefString a, RefString b) { return !(a == b); }
		public static bool operator == (RefString a, string b)
		{
			if (ReferenceEquals (a, null)) return b == null;
			return a.Value == b;
		}
		public static bool operator != (RefString a, string b) { return !(a == b); }
		public override bool Equals (object obj)
		{
			if (obj is RefString) return this == (RefString)obj;
			if (obj is string) return this == (string)obj;
			return false;
		}
		public override int GetHashCode ()
		{
			if (Value == null) return 0;
			return Value.GetHashCode ();
		}
		public override string ToString () { return Value; }
	}
	[ComVisible (true)]
	public class InitKey
	{
		private delegate T ConvertFunc<T> (string s);

		private RefString filepath = "";
		private RefString section = "";
		private RefString key = "";

		public string FilePath { get { return filepath.Value; } }
		public string Section { get { return section.Value; } }
		public string Key
		{
			get { return key.Value; }
			set { key.Value = value; }
		}

		public InitKey (RefString _fp, RefString _sect, RefString _key)
		{
			filepath = _fp;
			section = _sect;
			key = _key;
		}

		public string ReadString ()
		{
			return ReadString ("");
		}
		public string ReadString (string dflt)
		{
			return IniFile.GetPrivateProfileStringW (filepath, section, key, dflt);
		}
		private T ReadTo<T> (T dflt, ConvertFunc<T> convTo)
		{
			string str = ReadString (dflt != null ? dflt.ToString () : "");
			try { return convTo (str); }
			catch { return dflt; }
		}
		public short ReadShort () { return ReadShort (0); }
		public short ReadShort (short dflt) { return ReadTo (dflt, delegate (string s) { return Convert.ToInt16 (s); }); }
		public ushort ReadUShort () { return ReadUShort (0); }
		public ushort ReadUShort (ushort dflt) { return ReadTo (dflt, delegate (string s) { return Convert.ToUInt16 (s); }); }
		public int ReadInt () { return ReadInt (0); }
		public int ReadInt (int dflt) { return ReadTo (dflt, delegate (string s) { return Convert.ToInt32 (s); }); }
		public uint ReadUInt () { return ReadUInt (0); }
		public uint ReadUInt (uint dflt) { return ReadTo (dflt, delegate (string s) { return Convert.ToUInt32 (s); }); }
		public long ReadLong () { return ReadLong (0); }
		public long ReadLong (long dflt) { return ReadTo (dflt, delegate (string s) { return Convert.ToInt64 (s); }); }
		public ulong ReadULong () { return ReadULong (0); }
		public ulong ReadULong (ulong dflt) { return ReadTo (dflt, delegate (string s) { return Convert.ToUInt64 (s); }); }
		public Int16 ReadInt16 () { return ReadInt16 (0); }
		public Int16 ReadInt16 (Int16 dflt) { return ReadTo (dflt, delegate (string s) { return Convert.ToInt16 (s); }); }
		public UInt16 ReadUInt16 () { return ReadUInt16 (0); }
		public UInt16 ReadUInt16 (UInt16 dflt) { return ReadTo (dflt, delegate (string s) { return Convert.ToUInt16 (s); }); }
		public Int32 ReadInt32 () { return ReadInt32 (0); }
		public Int32 ReadInt32 (Int32 dflt) { return ReadTo (dflt, delegate (string s) { return Convert.ToInt32 (s); }); }
		public UInt32 ReadUInt32 () { return ReadUInt32 (0); }
		public UInt32 ReadUInt32 (UInt32 dflt) { return ReadTo (dflt, delegate (string s) { return Convert.ToUInt32 (s); }); }
		public Int64 ReadInt64 () { return ReadInt64 (0); }
		public Int64 ReadInt64 (Int64 dflt) { return ReadTo (dflt, delegate (string s) { return Convert.ToInt64 (s); }); }
		public UInt64 ReadUInt64 () { return ReadUInt64 (0); }
		public UInt64 ReadUInt64 (UInt64 dflt) { return ReadTo (dflt, delegate (string s) { return Convert.ToUInt64 (s); }); }
		public bool ReadBool () { return ReadBool (false); }
		public bool ReadBool (bool dflt) { return ReadTo (dflt, delegate (string s) { return BoolHelper.ConvertToBool (s); }); }
		public float ReadFloat () { return ReadFloat (0); }
		public float ReadFloat (float dflt) { return ReadTo (dflt, delegate (string s) { return Convert.ToSingle (s); }); }
		public double ReadDouble () { return ReadDouble (0); }
		public double ReadDouble (double dflt) { return ReadTo (dflt, delegate (string s) { return Convert.ToDouble (s); }); }
		public decimal ReadDecimal () { return ReadDecimal (0); }
		public decimal ReadDecimal (decimal dflt) { return ReadTo (dflt, delegate (string s) { return Convert.ToDecimal (s); }); }
		public sbyte ReadInt8 () { return ReadInt8 (0); }
		public sbyte ReadInt8 (sbyte dflt) { return ReadTo (dflt, delegate (string s) { return Convert.ToSByte (s); }); }
		public byte ReadUInt8 () { return ReadUInt8 (0); }
		public byte ReadUInt8 (byte dflt) { return ReadTo (dflt, delegate (string s) { return Convert.ToByte (s); }); }
		public byte ReadByte () { return ReadByte (0); }
		public byte ReadByte (byte dflt) { return ReadTo (dflt, delegate (string s) { return Convert.ToByte (s); }); }
		public sbyte ReadSByte () { return ReadSByte (0); }
		public sbyte ReadSByte (sbyte dflt) { return ReadTo (dflt, delegate (string s) { return Convert.ToSByte (s); }); }
		public DateTime ReadDateTime () { return ReadDateTime (default (DateTime)); }
		public DateTime ReadDateTime (DateTime dflt) { return ReadTo (dflt, delegate (string s) { return Convert.ToDateTime (s); }); }

		public object Get ()
		{
			return ReadString ();
		}
		public object Get (object dflt)
		{
			return ReadString (dflt != null ? dflt.ToString () : "");
		}

		public bool WriteString (string value)
		{
			return IniFile.WritePrivateProfileString (filepath, section, key, value);
		}
		public bool Write (string value) { return WriteString (value); }

		private bool WriteTo<T> (T value)
		{
			if (value == null) return WriteString (null);
			return WriteString (value.ToString ());
		}
		public bool Write (byte value) { return WriteTo (value); }
		public bool Write (sbyte value) { return WriteTo (value); }
		public bool Write (short value) { return WriteTo (value); }
		public bool Write (ushort value) { return WriteTo (value); }
		public bool Write (int value) { return WriteTo (value); }
		public bool Write (uint value) { return WriteTo (value); }
		public bool Write (long value) { return WriteTo (value); }
		public bool Write (ulong value) { return WriteTo (value); }
		public bool Write (bool value) { return WriteTo (value); }
		public bool Write (float value) { return WriteTo (value); }
		public bool Write (double value) { return WriteTo (value); }
		public bool Write (decimal value) { return WriteTo (value); }
		public bool Write (DateTime value) { return WriteTo (value); }

		public bool Set (object value)
		{
			if (value == null) return WriteString (null);
			return WriteString (value.ToString ());
		}

		public object Value
		{
			get { return Get (); }
			set { Set (value); }
		}

		public bool KeyExists
		{
			get
			{
				List<string> keys = new List<string> ();
				IniFile.GetPrivateProfileKeysW (filepath.Value, section.Value, keys);
				for (int i = 0; i < keys.Count; i++)
					if (keys [i] == key.Value) return true;
				return false;
			}
		}

		public bool ValueExists
		{
			get
			{
				if (!KeyExists) return false;
				string val = IniFile.GetPrivateProfileStringW (filepath, section, key, "");
				return !string.IsNullOrEmpty (val);
			}
		}

		public bool DeleteKey ()
		{
			return IniFile.WritePrivateProfileString (filepath, section, key, null);
		}
		public bool Clear () { return DeleteKey (); }
	}

	[ComVisible (true)]
	public class InitSection
	{
		private RefString filepath = "";
		private RefString section = "";

		public string FilePath { get { return filepath.Value; } }
		public string Section
		{
			get { return section.Value; }
			set { section.Value = value; }
		}

		public InitSection (RefString _fp, RefString _sect)
		{
			filepath = _fp;
			section = _sect;
		}

		public InitKey GetKey (string key)
		{
			return new InitKey (filepath, section, key);
		}
		public object Get (string key)
		{
			return GetKey (key).Get ();
		}
		public object Get (string key, object dflt)
		{
			return GetKey (key).Get (dflt);
		}
		public bool Set (string key, object value)
		{
			return GetKey (key).Set (value);
		}
		public object this [string key]
		{
			get { return Get (key); }
			set { Set (key, value); }
		}
		public string [] GetAllKeys ()
		{
			List<string> keys = new List<string> ();
			IniFile.GetPrivateProfileKeysW (filepath, section, keys);
			return keys.ToArray ();
		}
		public InitKey [] Keys
		{
			get
			{
				string [] allKeys = GetAllKeys ();
				InitKey [] result = new InitKey [allKeys.Length];
				for (int i = 0; i < allKeys.Length; i++)
					result [i] = new InitKey (filepath, section, allKeys [i]);
				return result;
			}
		}
		public bool DeleteSection ()
		{
			return IniFile.WritePrivateProfileString (filepath, section, null, null);
		}
		public bool Clear () { return DeleteSection (); }
	}

	[ComVisible (true)]
	public class InitConfig
	{
		private RefString filepath = "";
		public string FilePath
		{
			get { return filepath.Value; }
			set { filepath.Value = value; }
		}
		public InitConfig ()
		{
			FilePath = "";
		}
		public InitConfig (string _fp)
		{
			FilePath = _fp;
		}

		public InitSection GetSection (string section)
		{
			return new InitSection (filepath, section);
		}
		public InitKey GetKey (string section, string key)
		{
			return new InitKey (filepath, section, key);
		}
		public object Get (string section, string key)
		{
			return GetKey (section, key).Get ();
		}
		public object Get (string section, string key, object dflt)
		{
			return GetKey (section, key).Get (dflt);
		}
		public bool Set (string section, string key, object value)
		{
			return GetKey (section, key).Set (value);
		}
		public InitSection this [string key]
		{
			get { return GetSection (key); }
		}
		public string [] GetAllSections ()
		{
			List<string> sections = new List<string> ();
			IniFile.GetPrivateProfileSectionNamesW (filepath, sections);
			return sections.ToArray ();
		}
		public InitSection [] Sections
		{
			get
			{
				string [] allSections = GetAllSections ();
				InitSection [] result = new InitSection [allSections.Length];
				for (int i = 0; i < allSections.Length; i++)
					result [i] = new InitSection (filepath, allSections [i]);
				return result;
			}
		}
	}
}
namespace Sidebar.NativeCompatible
{
	public delegate void ConfigSaveHandler ();

	internal static class ConfigValueConverter
	{
		public const string EncodingString = "string";
		public const string EncodingBase64 = "base64";
		public const string EncodingNull = "null";

		public static bool IsSimpleType (Type t)
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

		public static string Serialize (object value, out string typeName, out string encoding)
		{
			if (value == null)
			{
				typeName = typeof (object).AssemblyQualifiedName;
				encoding = EncodingNull;
				return "";
			}
			Type t = value.GetType ();
			typeName = t.AssemblyQualifiedName;

			if (IsSimpleType (t))
			{
				TypeConverter conv = TypeDescriptor.GetConverter (t);
				if (conv != null && conv.CanConvertTo (typeof (string)))
				{
					try
					{
						encoding = EncodingString;
						return conv.ConvertToInvariantString (value);
					}
					catch
					{
					}
				}
			}

			encoding = EncodingBase64;
			BinaryFormatter bf = new BinaryFormatter ();
			using (MemoryStream ms = new MemoryStream ())
			{
				bf.Serialize (ms, value);
				return Convert.ToBase64String (ms.ToArray ());
			}
		}

		public static object Deserialize (string typeName, string encoding, string text)
		{
			if (string.IsNullOrEmpty (encoding)) encoding = EncodingString;
			if (encoding == EncodingNull) return null;
			if (string.IsNullOrEmpty (typeName)) return null;

			Type t = ResolveType (typeName);
			if (t == null) return null;

			if (encoding == EncodingString)
			{
				if (string.IsNullOrEmpty (text)) return null;
				try
				{
					TypeConverter conv = TypeDescriptor.GetConverter (t);
					if (conv != null && conv.CanConvertFrom (typeof (string)))
						return conv.ConvertFromInvariantString (text);
				}
				catch
				{
					return null;
				}
				return null;
			}

			if (encoding == EncodingBase64)
			{
				if (string.IsNullOrEmpty (text)) return null;
				try
				{
					byte [] data = Convert.FromBase64String (text);
					BinaryFormatter bf = new BinaryFormatter ();
					using (MemoryStream ms = new MemoryStream (data))
					{
						return bf.Deserialize (ms);
					}
				}
				catch
				{
					return null;
				}
			}

			return null;
		}

		private static Type ResolveType (string typeName)
		{
			if (string.IsNullOrEmpty (typeName)) return null;

			Type t = Type.GetType (typeName, false);
			if (t != null) return t;

			int comma = typeName.IndexOf (',');
			if (comma > 0)
			{
				string shortName = typeName.Substring (0, comma);
				t = Type.GetType (shortName, false);
				if (t != null) return t;

				System.Reflection.Assembly [] asms = AppDomain.CurrentDomain.GetAssemblies ();
				for (int i = 0; i < asms.Length; i++)
				{
					t = asms [i].GetType (shortName, false);
					if (t != null) return t;
				}
			}
			return null;
		}
	}

	[ComVisible (true)]
	public class ConfigSection
	{
		private XmlElement _root;
		private readonly object _lock;
		private readonly ConfigSaveHandler _saveHandler;

		internal ConfigSection (XmlElement root, object lockObj, ConfigSaveHandler saveHandler)
		{
			_root = root;
			_lock = lockObj;
			_saveHandler = saveHandler;
		}

		public XmlElement Element
		{
			get { return _root; }
		}

		// ---------------- Setting 访问 ----------------

		public object this [string key]
		{
			get
			{
				if (_root == null) return null;
				XmlElement elem = GetSettingElement (key);
				if (elem == null) return null;
				string typeName = elem.GetAttribute ("type");
				string encoding = elem.GetAttribute ("encoding");
				string text = elem.InnerText;
				return ConfigValueConverter.Deserialize (typeName, encoding, text);
			}
			set
			{
				if (_root == null) throw new InvalidOperationException ("Configuration section is not available.");
				XmlElement elem = GetSettingElement (key);

				string typeName;
				string encoding;
				string text;
				try
				{
					text = ConfigValueConverter.Serialize (value, out typeName, out encoding);
				}
				catch (Exception ex)
				{
					throw new InvalidOperationException ("Cannot serialize config value for key '" + key + "'.", ex);
				}

				lock (_lock)
				{
					if (elem == null)
					{
						elem = _root.OwnerDocument.CreateElement ("Setting");
						elem.SetAttribute ("name", key);
						elem.SetAttribute ("type", typeName);
						elem.SetAttribute ("encoding", encoding);
						elem.InnerText = text;
						_root.AppendChild (elem);
					}
					else
					{
						elem.SetAttribute ("type", typeName);
						elem.SetAttribute ("encoding", encoding);
						elem.InnerText = text;
					}
					if (_saveHandler != null) _saveHandler ();
				}
			}
		}

		public T Get<T> (string key)
		{
			return Get<T> (key, default (T));
		}

		public T Get<T> (string key, T defaultValue)
		{
			object val = this [key];
			if (val == null) return defaultValue;
			try { return (T)val; }
			catch { return defaultValue; }
		}

		public void Set<T> (string key, T value)
		{
			this [key] = value;
		}

		public bool ContainsKey (string key)
		{
			return GetSettingElement (key) != null;
		}

		public bool Remove (string key)
		{
			XmlElement elem = GetSettingElement (key);
			if (elem == null) return false;
			lock (_lock)
			{
				if (elem.ParentNode != null)
					elem.ParentNode.RemoveChild (elem);
				if (_saveHandler != null) _saveHandler ();
			}
			return true;
		}

		// ---------------- Section 嵌套 ----------------

		/// <summary>
		/// 获取或创建当前节下的直接子节（单层）。
		/// </summary>
		public ConfigSection GetSection (string name)
		{
			if (_root == null)
				throw new InvalidOperationException ("Configuration section is not available.");
			if (string.IsNullOrEmpty (name)) return this;

			XmlElement child = FindChildSection (_root, name);
			if (child == null)
			{
				lock (_lock)
				{
					child = FindChildSection (_root, name);
					if (child == null)
					{
						child = _root.OwnerDocument.CreateElement ("Section");
						child.SetAttribute ("name", name);
						_root.AppendChild (child);
						if (_saveHandler != null) _saveHandler ();
					}
				}
			}
			return new ConfigSection (child, _lock, _saveHandler);
		}

		/// <summary>
		/// 按相对路径获取或创建多层节。路径用 / 或 \ 分隔。
		/// 例：GetSectionPath("Appearance/Colors/Theme")
		/// </summary>
		public ConfigSection GetSectionPath (string path)
		{
			if (string.IsNullOrEmpty (path)) return this;
			string [] parts = path.Split (new char [] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
			ConfigSection current = this;
			for (int i = 0; i < parts.Length; i++)
				current = current.GetSection (parts [i]);
			return current;
		}

		/// <summary>
		/// 返回当前节下所有直接子节。
		/// </summary>
		public ConfigSection [] GetSections ()
		{
			if (_root == null) return new ConfigSection [0];
			List<ConfigSection> list = new List<ConfigSection> ();
			foreach (XmlNode node in _root.ChildNodes)
			{
				XmlElement e = node as XmlElement;
				if (e != null && e.Name == "Section")
					list.Add (new ConfigSection (e, _lock, _saveHandler));
			}
			return list.ToArray ();
		}

		/// <summary>
		/// 返回当前节下所有直接子节名称。
		/// </summary>
		public string [] GetSectionNames ()
		{
			if (_root == null) return new string [0];
			List<string> list = new List<string> ();
			foreach (XmlNode node in _root.ChildNodes)
			{
				XmlElement e = node as XmlElement;
				if (e == null || e.Name != "Section") continue;
				string name = e.GetAttribute ("name");
				if (!string.IsNullOrEmpty (name)) list.Add (name);
			}
			return list.ToArray ();
		}

		public bool HasSection (string name)
		{
			if (_root == null) return false;
			return FindChildSection (_root, name) != null;
		}

		public bool RemoveSection (string name)
		{
			XmlElement child = FindChildSection (_root, name);
			if (child == null) return false;
			lock (_lock)
			{
				if (child.ParentNode != null)
					child.ParentNode.RemoveChild (child);
				if (_saveHandler != null) _saveHandler ();
			}
			return true;
		}

		// ---------------- 内部辅助 ----------------

		private static XmlElement FindChildSection (XmlElement parent, string name)
		{
			if (parent == null) return null;
			foreach (XmlNode node in parent.ChildNodes)
			{
				XmlElement e = node as XmlElement;
				if (e == null || e.Name != "Section") continue;
				if (e.GetAttribute ("name") == name) return e;
			}
			return null;
		}

		private XmlElement GetSettingElement (string key)
		{
			if (_root == null) return null;
			foreach (XmlNode node in _root.ChildNodes)
			{
				XmlElement e = node as XmlElement;
				if (e == null || e.Name != "Setting") continue;
				if (e.GetAttribute ("name") == key) return e;
			}
			return null;
		}

		internal void SetRoot (XmlElement newRoot)
		{
			_root = newRoot;
		}
	}

	[ComVisible (true)]
	public class XmlConfig
	{
		private static readonly object _staticLock = new object ();
		private static XmlConfig _defaultInstance;
		private static string _currentUserName;

		private readonly object _instanceLock = new object ();
		private readonly string _filePath;
		private XmlDocument _doc;

		public string FilePath
		{
			get { return _filePath; }
		}

		// ---------------- 实例属性（兼容旧用法） ----------------

		public ConfigSection Global
		{
			get { return GetSection ("Global"); }
		}

		public ConfigSection CurrentUser
		{
			get
			{
				string user = _currentUserName;
				if (string.IsNullOrEmpty (user)) return null;
				return GetSection ("Users/" + user);
			}
		}

		// ---------------- 静态访问器 ----------------

		public static ConfigSection GlobalSettings
		{
			get
			{
				if (_defaultInstance == null)
					throw new InvalidOperationException ("XmlConfig not initialized. Call Initialize first.");
				return _defaultInstance.GetSection ("Global");
			}
		}

		public static ConfigSection CurrentUserSettings
		{
			get
			{
				if (_defaultInstance == null)
					throw new InvalidOperationException ("XmlConfig not initialized. Call Initialize first.");
				string user = _currentUserName;
				if (string.IsNullOrEmpty (user)) return null;
				return _defaultInstance.GetSection ("Users/" + user);
			}
		}

		public static string CurrentUserName
		{
			get { return _currentUserName; }
			set
			{
				if (_currentUserName == value) return;
				_currentUserName = value;
				if (_defaultInstance != null && !string.IsNullOrEmpty (value))
					_defaultInstance.GetSection ("Users/" + value);
			}
		}

		public static void Initialize (string path)
		{
			lock (_staticLock)
			{
				_defaultInstance = new XmlConfig (path);
				_currentUserName = Environment.UserName;
				_defaultInstance.GetSection ("Global");
				if (!string.IsNullOrEmpty (_currentUserName))
					_defaultInstance.GetSection ("Users/" + _currentUserName);
			}
		}

		// ---------------- 构造与持久化 ----------------

		public XmlConfig (string path)
		{
			if (Directory.Exists (path) || (!File.Exists (path) && !Path.HasExtension (path)))
				_filePath = Path.Combine (path, "Config.xml");
			else
				_filePath = path;

			LoadOrCreate ();
		}

		private void LoadOrCreate ()
		{
			string dir = Path.GetDirectoryName (_filePath);
			if (!string.IsNullOrEmpty (dir) && !Directory.Exists (dir))
				Directory.CreateDirectory (dir);

			if (File.Exists (_filePath))
			{
				_doc = new XmlDocument ();
				_doc.Load (_filePath);
			}
			else
			{
				_doc = new XmlDocument ();
				XmlElement root = _doc.CreateElement ("Config");
				_doc.AppendChild (root);

				XmlElement globalSection = _doc.CreateElement ("Section");
				globalSection.SetAttribute ("name", "Global");
				root.AppendChild (globalSection);

				XmlElement usersSection = _doc.CreateElement ("Section");
				usersSection.SetAttribute ("name", "Users");
				root.AppendChild (usersSection);

				Save ();
			}
		}

		private void Save ()
		{
			lock (_instanceLock)
			{
				_doc.Save (_filePath);
			}
		}

		// ---------------- GetSection 核心 ----------------

		/// <summary>
		/// 按路径获取或创建节。路径用 / 或 \ 分隔。
		/// 例："Global"、"Users/Bruce"、"Global/Appearance/Colors"
		/// 空路径返回根节。
		/// </summary>
		public ConfigSection GetSection (string path)
		{
			if (_doc == null || _doc.DocumentElement == null)
				throw new InvalidOperationException ("XmlConfig is not initialized.");

			XmlElement current = _doc.DocumentElement;
			if (!string.IsNullOrEmpty (path))
			{
				string [] parts = path.Split (new char [] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < parts.Length; i++)
				{
					XmlElement child = FindChildSection (current, parts [i]);
					if (child == null)
					{
						lock (_instanceLock)
						{
							child = FindChildSection (current, parts [i]);
							if (child == null)
							{
								child = _doc.CreateElement ("Section");
								child.SetAttribute ("name", parts [i]);
								current.AppendChild (child);
								Save ();
							}
						}
					}
					current = child;
				}
			}
			return new ConfigSection (current, _instanceLock, Save);
		}

		/// <summary>
		/// 判断某个路径的节是否存在（不创建）。
		/// </summary>
		public bool HasSection (string path)
		{
			if (_doc == null || _doc.DocumentElement == null) return false;
			XmlElement current = _doc.DocumentElement;
			if (!string.IsNullOrEmpty (path))
			{
				string [] parts = path.Split (new char [] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < parts.Length; i++)
				{
					XmlElement child = FindChildSection (current, parts [i]);
					if (child == null) return false;
					current = child;
				}
			}
			return true;
		}

		/// <summary>
		/// 顶层节名称列表。
		/// </summary>
		public string [] GetSectionNames ()
		{
			if (_doc == null || _doc.DocumentElement == null) return new string [0];
			List<string> list = new List<string> ();
			foreach (XmlNode node in _doc.DocumentElement.ChildNodes)
			{
				XmlElement e = node as XmlElement;
				if (e == null || e.Name != "Section") continue;
				string name = e.GetAttribute ("name");
				if (!string.IsNullOrEmpty (name)) list.Add (name);
			}
			return list.ToArray ();
		}

		public bool RemoveSection (string path)
		{
			if (_doc == null || _doc.DocumentElement == null || string.IsNullOrEmpty (path)) return false;
			string [] parts = path.Split (new char [] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length == 0) return false;

			XmlElement parent = _doc.DocumentElement;
			for (int i = 0; i < parts.Length - 1; i++)
			{
				XmlElement child = FindChildSection (parent, parts [i]);
				if (child == null) return false;
				parent = child;
			}

			XmlElement target = FindChildSection (parent, parts [parts.Length - 1]);
			if (target == null) return false;

			lock (_instanceLock)
			{
				parent.RemoveChild (target);
				Save ();
			}
			return true;
		}

		private static XmlElement FindChildSection (XmlElement parent, string name)
		{
			if (parent == null) return null;
			foreach (XmlNode node in parent.ChildNodes)
			{
				XmlElement e = node as XmlElement;
				if (e == null || e.Name != "Section") continue;
				if (e.GetAttribute ("name") == name) return e;
			}
			return null;
		}
	}
}
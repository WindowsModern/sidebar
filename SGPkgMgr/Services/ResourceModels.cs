using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace SGPkgMgr.Services
{
	/// <summary>资源文件类型。</summary>
	public enum ResourceFileKind
	{
		Unknown,
		Locale,
		Path
	}

	// ================================================================
	//  Locale 数据模型
	// ================================================================

	public class LocaleEntry: INotifyPropertyChanged
	{
		private string _localeName = "";
		private string _value = "";

		public string LocaleName
		{
			get { return _localeName; }
			set { _localeName = (value ?? "").Trim (); Raise ("LocaleName"); }
		}

		public string Value
		{
			get { return _value; }
			set { _value = value ?? ""; Raise ("Value"); }
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void Raise (string name)
		{
			var h = PropertyChanged;
			if (h != null) h (this, new PropertyChangedEventArgs (name));
		}
	}

	public class LocaleResourceItem: INotifyPropertyChanged
	{
		private string _resourceName = "";

		public string ResourceName
		{
			get { return _resourceName; }
			set { _resourceName = (value ?? "").Trim (); Raise ("ResourceName"); }
		}

		public ObservableCollection<LocaleEntry> Entries { get; private set; }

		public LocaleResourceItem ()
		{
			Entries = new ObservableCollection<LocaleEntry> ();
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void Raise (string name)
		{
			var h = PropertyChanged;
			if (h != null) h (this, new PropertyChangedEventArgs (name));
		}
	}

	// ================================================================
	//  Path 数据模型
	// ================================================================

	public class PathEntry: INotifyPropertyChanged
	{
		private int _scale = 100;
		private string _path = "";

		public int Scale
		{
			get { return _scale; }
			set { _scale = value; Raise ("Scale"); }
		}

		public string Path
		{
			get { return _path; }
			set { _path = value ?? ""; Raise ("Path"); }
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void Raise (string name)
		{
			var h = PropertyChanged;
			if (h != null) h (this, new PropertyChangedEventArgs (name));
		}
	}

	public class PathResourceItem: INotifyPropertyChanged
	{
		private string _resourceName = "";

		public string ResourceName
		{
			get { return _resourceName; }
			set { _resourceName = (value ?? "").Trim (); Raise ("ResourceName"); }
		}

		public ObservableCollection<PathEntry> Entries { get; private set; }

		public PathResourceItem ()
		{
			Entries = new ObservableCollection<PathEntry> ();
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void Raise (string name)
		{
			var h = PropertyChanged;
			if (h != null) h (this, new PropertyChangedEventArgs (name));
		}
	}

	// ================================================================
	//  解析 / 序列化
	// ================================================================

	/// <summary>
	/// 解析 / 序列化 Locale.xml 和 Path.xml。
	/// 与 Sidebar 侧的解析一致：只看 id / name / scale 属性，不看标签名。
	/// 输出统一为 &lt;resources&gt;&lt;res id=""&gt;&lt;locale name=""&gt; / &lt;file scale=""&gt;。
	/// </summary>
	public static class ResourceFileParser
	{
		// ============================================================
		//  Locale.xml
		// ============================================================

		public static List<LocaleResourceItem> ParseLocale (byte [] bytes)
		{
			var result = new List<LocaleResourceItem> ();
			if (bytes == null || bytes.Length == 0) return result;

			XmlDocument doc;
			try
			{
				doc = new XmlDocument ();
				using (var ms = new MemoryStream (bytes))
					doc.Load (ms);
			}
			catch { return result; }

			var root = doc.DocumentElement;
			if (root == null) return result;

			foreach (XmlNode resNode in root.ChildNodes)
			{
				if (resNode.NodeType != XmlNodeType.Element) continue;
				var idAttr = resNode.Attributes != null ? resNode.Attributes ["id"] : null;
				if (idAttr == null) continue;

				var item = new LocaleResourceItem { ResourceName = idAttr.Value };

				foreach (XmlNode locNode in resNode.ChildNodes)
				{
					if (locNode.NodeType != XmlNodeType.Element) continue;
					var nameAttr = locNode.Attributes != null ? locNode.Attributes ["name"] : null;
					if (nameAttr == null) continue;

					item.Entries.Add (new LocaleEntry {
						LocaleName = nameAttr.Value,
						Value = locNode.InnerText
					});
				}

				result.Add (item);
			}

			return result;
		}

		public static byte [] SerializeLocale (IEnumerable<LocaleResourceItem> items)
		{
			var doc = new XmlDocument ();
			doc.AppendChild (doc.CreateXmlDeclaration ("1.0", "utf-8", null));

			var root = doc.CreateElement ("resources");
			doc.AppendChild (root);

			if (items != null)
			{
				foreach (var item in items)
				{
					var resEl = doc.CreateElement ("res");
					resEl.SetAttribute ("id", item.ResourceName ?? "");

					foreach (var entry in item.Entries)
					{
						var locEl = doc.CreateElement ("locale");
						locEl.SetAttribute ("name", entry.LocaleName ?? "");
						locEl.InnerText = entry.Value ?? "";
						resEl.AppendChild (locEl);
					}

					root.AppendChild (resEl);
				}
			}

			return SaveXml (doc);
		}

		// ============================================================
		//  Path.xml
		// ============================================================

		public static List<PathResourceItem> ParsePath (byte [] bytes)
		{
			var result = new List<PathResourceItem> ();
			if (bytes == null || bytes.Length == 0) return result;

			XmlDocument doc;
			try
			{
				doc = new XmlDocument ();
				using (var ms = new MemoryStream (bytes))
					doc.Load (ms);
			}
			catch { return result; }

			var root = doc.DocumentElement;
			if (root == null) return result;

			foreach (XmlNode resNode in root.ChildNodes)
			{
				if (resNode.NodeType != XmlNodeType.Element) continue;
				var idAttr = resNode.Attributes != null ? resNode.Attributes ["id"] : null;
				if (idAttr == null) continue;

				var item = new PathResourceItem { ResourceName = idAttr.Value };

				foreach (XmlNode fileNode in resNode.ChildNodes)
				{
					if (fileNode.NodeType != XmlNodeType.Element) continue;
					var scaleAttr = fileNode.Attributes != null ? fileNode.Attributes ["scale"] : null;
					if (scaleAttr == null) continue;

					int scale;
					if (!int.TryParse (scaleAttr.Value, NumberStyles.Integer,
						CultureInfo.InvariantCulture, out scale))
						continue;

					item.Entries.Add (new PathEntry {
						Scale = scale,
						Path = fileNode.InnerText
					});
				}

				result.Add (item);
			}

			return result;
		}

		public static byte [] SerializePath (IEnumerable<PathResourceItem> items)
		{
			var doc = new XmlDocument ();
			doc.AppendChild (doc.CreateXmlDeclaration ("1.0", "utf-8", null));

			var root = doc.CreateElement ("resources");
			doc.AppendChild (root);

			if (items != null)
			{
				foreach (var item in items)
				{
					var resEl = doc.CreateElement ("res");
					resEl.SetAttribute ("id", item.ResourceName ?? "");

					foreach (var entry in item.Entries)
					{
						var fileEl = doc.CreateElement ("file");
						fileEl.SetAttribute ("scale",
							entry.Scale.ToString (CultureInfo.InvariantCulture));
						fileEl.InnerText = entry.Path ?? "";
						resEl.AppendChild (fileEl);
					}

					root.AppendChild (resEl);
				}
			}

			return SaveXml (doc);
		}

		// ============================================================
		//  自动识别
		// ============================================================

		/// <summary>
		/// 从字节识别是 Locale.xml 还是 Path.xml。
		/// 规则：子节点带 scale 属性的算 Path；带 name 属性的算 Locale；
		/// 两者都有或都没有时，凭出现次数多的一方判定，仍无法确定返回 Locale。
		/// </summary>
		public static ResourceFileKind DetectKind (byte [] bytes)
		{
			if (bytes == null || bytes.Length == 0) return ResourceFileKind.Unknown;

			XmlDocument doc;
			try
			{
				doc = new XmlDocument ();
				using (var ms = new MemoryStream (bytes))
					doc.Load (ms);
			}
			catch { return ResourceFileKind.Unknown; }

			var root = doc.DocumentElement;
			if (root == null) return ResourceFileKind.Unknown;

			int localeCount = 0;
			int pathCount = 0;

			foreach (XmlNode resNode in root.ChildNodes)
			{
				if (resNode.NodeType != XmlNodeType.Element) continue;
				foreach (XmlNode child in resNode.ChildNodes)
				{
					if (child.NodeType != XmlNodeType.Element) continue;
					if (child.Attributes != null && child.Attributes ["scale"] != null) pathCount++;
					if (child.Attributes != null && child.Attributes ["name"] != null) localeCount++;
				}
			}

			if (pathCount > localeCount) return ResourceFileKind.Path;
			if (localeCount > pathCount) return ResourceFileKind.Locale;

			// 都没记录或相等：默认 Locale
			return ResourceFileKind.Locale;
		}

		// ============================================================
		//  内部
		// ============================================================

		private static byte [] SaveXml (XmlDocument doc)
		{
			using (var ms = new MemoryStream ())
			{
				var settings = new XmlWriterSettings {
					Encoding = new UTF8Encoding (false),
					Indent = true,
					IndentChars = "  "
				};
				using (var writer = XmlWriter.Create (ms, settings))
					doc.Save (writer);
				return ms.ToArray ();
			}
		}

		/// <summary>从 files 里找出实际使用的 Locale.xml / Path.xml key。</summary>
		public static string FindExistingKey (
			IDictionary<string, byte []> files, string name)
		{
			if (files == null) return null;
			foreach (var k in files.Keys)
				if (string.Equals (k, name, StringComparison.OrdinalIgnoreCase))
					return k;
			return null;
		}

		public static byte [] GetFileBytes (
			IDictionary<string, byte []> files, string name)
		{
			var key = FindExistingKey (files, name);
			if (key == null) return null;
			byte [] data;
			return files.TryGetValue (key, out data) ? data : null;
		}

		public static void SetFileBytes (
			IDictionary<string, byte []> files, string name, byte [] bytes)
		{
			if (files == null) return;
			var key = FindExistingKey (files, name) ?? name;
			files [key] = bytes;
		}
	}
}
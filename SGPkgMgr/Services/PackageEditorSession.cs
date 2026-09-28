using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;
using ICSharpCode.SharpZipLib.Zip;
using Sidebar;

namespace SGPkgMgr.Services
{
	public sealed class PackageEditorSession
	{
		public string SourcePath { get; private set; }
		public bool IsBundle { get; private set; }

		/// <summary>Bundle 的清单，仅当 IsBundle 时有效。</summary>
		public TileBundleManifest BundleManifest { get; set; }

		/// <summary>所有子包。单包时只有一个 Slot，EntryName 为空。</summary>
		public List<PackageSlot> Slots { get; } = new List<PackageSlot> ();

		/// <summary>bundle 级别的其他条目。</summary>
		public Dictionary<string, byte []> BundleExtras { get; } =
			new Dictionary<string, byte []> (StringComparer.OrdinalIgnoreCase);

		public sealed class PackageSlot
		{
			public string EntryName { get; set; }
			public TileManifest Manifest { get; set; }
			public Dictionary<string, byte []> Files { get; } =
				new Dictionary<string, byte []> (StringComparer.OrdinalIgnoreCase);
		}

		public static PackageEditorSession Open (string filePath)
		{
			if (string.IsNullOrEmpty (filePath)) throw new ArgumentNullException (nameof (filePath));
			if (!File.Exists (filePath)) throw new FileNotFoundException ("Package file not found.", filePath);

			var s = new PackageEditorSession { SourcePath = filePath };
			using (var zip = new ZipFile (filePath))
			{
				bool isBundle = zip.GetEntry ("BundleManifest.xml") != null;
				if (isBundle) s.LoadBundle (zip);
				else s.LoadSingle (zip);
			}
			return s;
		}

		private void LoadSingle (ZipFile zip)
		{
			IsBundle = false;
			var manifestEntry = zip.GetEntry ("Manifest.xml");
			if (manifestEntry == null)
				throw new InvalidDataException ("Manifest.xml not found in package.");

			TileManifest manifest;
			using (var s = zip.GetInputStream (manifestEntry))
			{
				manifest = ReadManifestChecked (s, SourcePath);
			}

			var slot = new PackageSlot { EntryName = null, Manifest = manifest };
			foreach (var e in zip.Cast<ZipEntry> ())
			{
				if (!e.IsFile) continue;
				if (e.Name == "Manifest.xml"
					|| e.Name == "BlockMap.xml"
					|| e.Name == "Signature.bin") continue;
				slot.Files [e.Name] = ReadEntry (zip, e);
			}
			Slots.Add (slot);
		}

		private void LoadBundle (ZipFile zip)
		{
			IsBundle = true;
			var bundleEntry = zip.GetEntry ("BundleManifest.xml");
			using (var s = zip.GetInputStream (bundleEntry))
			{
				BundleManifest = TileBundleManifest.FromStream (s);
			}

			var subNames = new HashSet<string> (
				BundleManifest.Packages.Select (p => p.FileName).Where (n => !string.IsNullOrEmpty (n)),
				StringComparer.OrdinalIgnoreCase);

			foreach (var sub in BundleManifest.Packages)
			{
				var subEntry = zip.GetEntry (sub.FileName);
				if (subEntry == null || !subEntry.IsFile)
					throw new InvalidDataException ("SubPackage not found: " + sub.FileName);

				byte [] subBytes = ReadEntry (zip, subEntry);
				var slot = new PackageSlot { EntryName = sub.FileName };

				using (var subZip = new ZipFile (new MemoryStream (subBytes)))
				{
					var subManifestEntry = subZip.GetEntry ("Manifest.xml");
					if (subManifestEntry == null)
						throw new InvalidDataException ("Manifest.xml not found in " + sub.FileName + ".");

					using (var s = subZip.GetInputStream (subManifestEntry))
					{
						slot.Manifest = ReadManifestChecked (s, sub.FileName);
					}

					foreach (var e in subZip.Cast<ZipEntry> ())
					{
						if (!e.IsFile) continue;
						if (e.Name == "Manifest.xml"
							|| e.Name == "BlockMap.xml"
							|| e.Name == "Signature.bin") continue;
						slot.Files [e.Name] = ReadEntry (subZip, e);
					}
				}
				Slots.Add (slot);
			}

			foreach (var e in zip.Cast<ZipEntry> ())
			{
				if (!e.IsFile) continue;
				if (e.Name == "BundleManifest.xml"
					|| e.Name == "BlockMap.xml"
					|| e.Name == "Signature.bin") continue;
				if (subNames.Contains (e.Name)) continue;
				BundleExtras [e.Name] = ReadEntry (zip, e);
			}
		}

		public void SaveTo (string targetPath)
		{
			var bytes = BuildPackageBytes ();
			using (var zip = new ZipFile (new MemoryStream (bytes)))
			{
				string dir = Path.GetDirectoryName (Path.GetFullPath (targetPath));
				string name = Path.GetFileNameWithoutExtension (targetPath);
				TilePackageWriteManager.SavePackageFile (zip, dir, name);
			}
		}

		private byte [] BuildPackageBytes ()
		{
			var ms = new MemoryStream ();
			using (var zos = new ZipOutputStream (ms))
			{
				zos.SetLevel (9);

				if (IsBundle)
				{
					foreach (var slot in Slots)
					{
						byte [] subBytes = BuildSubPackageBytes (slot);
						var entry = new ZipEntry (slot.EntryName);
						entry.DateTime = DateTime.Now;
						zos.PutNextEntry (entry);
						zos.Write (subBytes, 0, subBytes.Length);
						zos.CloseEntry ();
					}

					var bundleEntry = new ZipEntry ("BundleManifest.xml");
					bundleEntry.DateTime = DateTime.Now;
					zos.PutNextEntry (bundleEntry);
					var ser = new XmlSerializer (typeof (TileBundleManifest));
					var ns = new XmlSerializerNamespaces ();
					ns.Add ("", "");
					ser.Serialize (zos, BundleManifest, ns);
					zos.CloseEntry ();

					foreach (var kv in BundleExtras)
					{
						var e = new ZipEntry (kv.Key);
						e.DateTime = DateTime.Now;
						zos.PutNextEntry (e);
						zos.Write (kv.Value, 0, kv.Value.Length);
						zos.CloseEntry ();
					}
				}
				else
				{
					var slot = Slots [0];
					byte [] subBytes = BuildSubPackageBytes (slot);
					using (var subZip = new ZipFile (new MemoryStream (subBytes)))
					{
						foreach (var e in subZip.Cast<ZipEntry> ())
						{
							if (!e.IsFile) continue;
							var ne = new ZipEntry (e.Name);
							ne.DateTime = e.DateTime;
							zos.PutNextEntry (ne);
							using (var s = subZip.GetInputStream (e))
								s.CopyTo (zos);
							zos.CloseEntry ();
						}
					}
				}
			}
			return ms.ToArray ();
		}

		private byte [] BuildSubPackageBytes (PackageSlot slot)
		{
			var ms = new MemoryStream ();
			using (var zos = new ZipOutputStream (ms))
			{
				zos.SetLevel (9);

				// 先把 Manifest.xml 序列化到独立的 MemoryStream，再一次性写入
				byte [] manifestBytes;
				using (var temp = new MemoryStream ())
				{
					using (var sw = new StreamWriter (temp, new UTF8Encoding (false)))
					{
						slot.Manifest.ToText (sw);
					}
					manifestBytes = temp.ToArray ();
				}

				var mEntry = new ZipEntry ("Manifest.xml");
				mEntry.DateTime = DateTime.Now;
				zos.PutNextEntry (mEntry);
				zos.Write (manifestBytes, 0, manifestBytes.Length);
				zos.CloseEntry ();

				foreach (var kv in slot.Files)
				{
					var e = new ZipEntry (kv.Key);
					e.DateTime = DateTime.Now;
					zos.PutNextEntry (e);
					zos.Write (kv.Value, 0, kv.Value.Length);
					zos.CloseEntry ();
				}
			}
			return ms.ToArray ();
		}

		private static TileManifest ReadManifestChecked (Stream stream, string source)
		{
			// 不再依赖 CoreLib 的 internal TileManifestSupport，直接在本地做版本检测。
			var doc = XDocument.Load (stream);
			uint ver = TileManifestV1ToV2Converter.DetectVersion (doc);
			if (ver < 2)
				throw new NotSupportedException (
					"Manifest version " + ver + " is not supported (source: " + source + "). " +
					"Only version 2 or newer is supported.");

			using (var ms = new MemoryStream ())
			{
				doc.Save (ms);
				ms.Position = 0;
				return TileManifest.FromStream (ms);
			}
		}

		private static byte [] ReadEntry (ZipFile zip, ZipEntry entry)
		{
			using (var s = zip.GetInputStream (entry))
			using (var ms = new MemoryStream ())
			{
				s.CopyTo (ms);
				return ms.ToArray ();
			}
		}
		/// <summary>
		/// 对每个子包里的每个 Application，如果 EntryPoint 为空，
		/// 就从该子包的文件字典里读 Executable，反射查找继承 TileBase 的类并填入。
		/// 返回成功填充的数量。
		/// </summary>
		public int AutoFillEntryPoints ()
		{
			int count = 0;
			foreach (var slot in Slots)
			{
				if (slot.Manifest?.Applications == null) continue;
				foreach (var app in slot.Manifest.Applications)
				{
					if (app == null) continue;
					if (!string.IsNullOrEmpty (app.EntryPoint)) continue;
					if (string.IsNullOrEmpty (app.Executable)) continue;

					byte [] bytes;
					if (!TryGetFileBytes (slot, app.Executable, out bytes)) continue;

					var typeName = TileEntryPointFinder.FindEntryPoint (bytes);
					if (!string.IsNullOrEmpty (typeName))
					{
						app.EntryPoint = typeName;
						count++;
					}
				}
			}
			return count;
		}

		private static bool TryGetFileBytes (PackageSlot slot, string name, out byte [] bytes)
		{
			bytes = null;
			if (slot.Files == null || string.IsNullOrEmpty (name)) return false;

			// 尝试几种常见的路径写法
			string [] candidates = {
		name,
		name.Replace ('\\', '/'),
		name.Replace ('/', '\\')
	};
			foreach (var c in candidates)
			{
				if (slot.Files.TryGetValue (c, out bytes) && bytes != null)
					return true;
			}
			return false;
		}
	}
}
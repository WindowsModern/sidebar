using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using ICSharpCode.SharpZipLib.Zip;
using Sidebar;

namespace SGPkgMgr.Services
{
	/// <summary>
	/// 把 V1 的 .sgpkg / .sgpkgbundle 转换成 V2。逻辑：
	/// <list type="bullet">
	/// <item>打开源 zip，读取 Manifest.xml，若是 V1 则调用
	/// <see cref="TileManifestV1ToV2Converter.Convert(XDocument)"/> 得到 V2 清单</item>
	/// <item>若源是 bundle，则递归处理每个子包</item>
	/// <item>把条目原样复制到新 zip，仅替换 Manifest.xml / BundleManifest.xml</item>
	/// <item>不写 BlockMap.xml / Signature.bin（让调用方再走一遍 SavePackageFile 统一签名）</item>
	/// </list>
	/// V1 转换产生的 Application 中 EntryPoint 为 null（见转换器内部），
	/// 运行时按 TileInstance 的方式反射查找 TileBase 子类，不依赖 EntryPoint。
	/// </summary>
	public static class PackageConverter
	{
		/// <summary>
		/// 检测源包内清单版本。0 表示无法解析；1 表示 V1；2+ 表示已经是 V2。
		/// 对 bundle，取第一个子包的版本。
		/// </summary>
		public static uint DetectPackageVersion (string filePath)
		{
			using (var zip = new ZipFile (filePath))
			{
				var single = zip.GetEntry ("Manifest.xml");
				if (single != null)
					return ReadManifestVersion (zip, single);

				var bundle = zip.GetEntry ("BundleManifest.xml");
				if (bundle == null) return 0;

				// bundle：读 BundleManifest 里第一个子包
				XDocument bundleDoc;
				using (var s = zip.GetInputStream (bundle))
					bundleDoc = XDocument.Load (s);

				var first = bundleDoc.Root?
					.Element ("Packages")?
					.Elements ("Package")
					.FirstOrDefault ();
				if (first == null) return 0;

				string subName = (string)first.Attribute ("FileName");
				if (string.IsNullOrEmpty (subName)) return 0;

				var subEntry = zip.GetEntry (subName);
				if (subEntry == null || !subEntry.IsFile) return 0;

				using (var subStream = zip.GetInputStream (subEntry))
				using (var ms = new MemoryStream ())
				{
					subStream.CopyTo (ms);
					ms.Position = 0;
					using (var subZip = new ZipFile (ms))
					{
						var subManifest = subZip.GetEntry ("Manifest.xml");
						if (subManifest == null) return 0;
						return ReadManifestVersion (subZip, subManifest);
					}
				}
			}
		}

		private static uint ReadManifestVersion (ZipFile zip, ZipEntry entry)
		{
			using (var s = zip.GetInputStream (entry))
				return TileManifestV1ToV2Converter.DetectVersion (s);
		}

		/// <summary>
		/// 转换单个包。返回转换后的 zip（未签名）；调用方用 SavePackageFile 落盘。
		/// </summary>
		/// <param name="sourcePath">源 .sgpkg 或 .sgpkgbundle</param>
		/// <param name="progress">可选进度回调</param>
		public static ZipFile Convert (string sourcePath, Action<int, int, double> progress = null)
		{
			using (var zip = new ZipFile (sourcePath))
			{
				bool isBundle = zip.GetEntry ("BundleManifest.xml") != null;
				if (isBundle)
					return ConvertBundle (zip, progress);
				else
					return ConvertSingle (zip, progress);
			}
		}

		private static ZipFile ConvertSingle (ZipFile zip, Action<int, int, double> progress)
		{
			var entries = zip.Cast<ZipEntry> ()
				.Where (e => e.IsFile
					&& e.Name != "Manifest.xml"
					&& e.Name != "BlockMap.xml"
					&& e.Name != "Signature.bin")
				.ToList ();

			int total = entries.Count + 1; // +1 是新的 Manifest.xml
			int current = 0;

			var ms = new MemoryStream ();
			using (var zos = new ZipOutputStream (ms))
			{
				zos.SetLevel (9);

				// 先写新的 Manifest.xml（已被调用方换成 V2 内容）
				byte [] newManifestBytes = ReadManifestConvertedToV2 (zip);
				var mEntry = new ZipEntry ("Manifest.xml");
				mEntry.DateTime = DateTime.Now;
				zos.PutNextEntry (mEntry);
				zos.Write (newManifestBytes, 0, newManifestBytes.Length);
				zos.CloseEntry ();
				current++;
				progress?.Invoke (current, total, (double)current / total);

				foreach (var entry in entries)
				{
					var ne = new ZipEntry (entry.Name);
					ne.DateTime = entry.DateTime;
					ne.Size = entry.Size;
					ne.ExternalFileAttributes = entry.ExternalFileAttributes;
					zos.PutNextEntry (ne);
					using (var s = zip.GetInputStream (entry))
						s.CopyTo (zos);
					zos.CloseEntry ();
					current++;
					progress?.Invoke (current, total, (double)current / total);
				}
			}
			return new ZipFile (new MemoryStream (ms.ToArray ()));
		}

		private static ZipFile ConvertBundle (ZipFile zip, Action<int, int, double> progress)
		{
			// 先读 BundleManifest，拿到子包名列表
			var bundleEntry = zip.GetEntry ("BundleManifest.xml");
			XDocument bundleDoc;
			using (var s = zip.GetInputStream (bundleEntry))
				bundleDoc = XDocument.Load (s);

			var packageNames = bundleDoc.Root?
				.Element ("Packages")?
				.Elements ("Package")
				.Select (e => (string)e.Attribute ("FileName"))
				.Where (n => !string.IsNullOrEmpty (n))
				.ToList () ?? new List<string> ();

			int total = zip.Cast<ZipEntry> ().Count (e => e.IsFile) + packageNames.Count;
			int current = 0;

			var ms = new MemoryStream ();
			using (var zos = new ZipOutputStream (ms))
			{
				zos.SetLevel (9);

				// 复制除子包之外的所有条目（BundleManifest.xml 等）
				foreach (var entry in zip.Cast<ZipEntry> ())
				{
					if (!entry.IsFile) continue;
					if (packageNames.Contains (entry.Name))
					{
						// 子包：递归转换
						byte [] subConverted = ConvertSubPackage (zip, entry, progress, ref current, total);
						var ne = new ZipEntry (entry.Name);
						ne.DateTime = DateTime.Now;
						zos.PutNextEntry (ne);
						zos.Write (subConverted, 0, subConverted.Length);
						zos.CloseEntry ();
						continue;
					}

					var copy = new ZipEntry (entry.Name);
					copy.DateTime = entry.DateTime;
					copy.Size = entry.Size;
					copy.ExternalFileAttributes = entry.ExternalFileAttributes;
					zos.PutNextEntry (copy);
					using (var s = zip.GetInputStream (entry))
						s.CopyTo (zos);
					zos.CloseEntry ();
					current++;
					progress?.Invoke (current, total, (double)current / total);
				}
			}
			return new ZipFile (new MemoryStream (ms.ToArray ()));
		}

		private static byte [] ConvertSubPackage (
			ZipFile parent,
			ZipEntry subEntry,
			Action<int, int, double> progress,
			ref int current,
			int total)
		{
			byte [] subBytes;
			using (var s = parent.GetInputStream (subEntry))
			using (var ms = new MemoryStream ())
			{
				s.CopyTo (ms);
				subBytes = ms.ToArray ();
			}

			using (var subZip = new ZipFile (new MemoryStream (subBytes)))
			{
				var ms2 = new MemoryStream ();
				using (var zos = new ZipOutputStream (ms2))
				{
					zos.SetLevel (9);

					// 新的 Manifest.xml
					byte [] newManifestBytes = ReadManifestConvertedToV2 (subZip);
					var mEntry = new ZipEntry ("Manifest.xml");
					mEntry.DateTime = DateTime.Now;
					zos.PutNextEntry (mEntry);
					zos.Write (newManifestBytes, 0, newManifestBytes.Length);
					zos.CloseEntry ();
					current++;
					progress?.Invoke (current, total, (double)current / total);

					// 其余条目
					foreach (var e in subZip.Cast<ZipEntry> ())
					{
						if (!e.IsFile) continue;
						if (e.Name == "Manifest.xml"
							|| e.Name == "BlockMap.xml"
							|| e.Name == "Signature.bin") continue;

						var copy = new ZipEntry (e.Name);
						copy.DateTime = e.DateTime;
						copy.Size = e.Size;
						copy.ExternalFileAttributes = e.ExternalFileAttributes;
						zos.PutNextEntry (copy);
						using (var s = subZip.GetInputStream (e))
							s.CopyTo (zos);
						zos.CloseEntry ();
						current++;
						progress?.Invoke (current, total, (double)current / total);
					}
				}
				return ms2.ToArray ();
			}
		}

		/// <summary>
		/// 读取源 zip 里的 Manifest.xml，转成 V2 并序列化成字节数组。
		/// </summary>
		private static byte [] ReadManifestConvertedToV2 (ZipFile zip)
		{
			var manifestEntry = zip.GetEntry ("Manifest.xml");
			if (manifestEntry == null)
				throw new InvalidDataException ("Manifest.xml not found in package.");

			XDocument doc;
			using (var s = zip.GetInputStream (manifestEntry))
				doc = XDocument.Load (s);

			uint ver = TileManifestV1ToV2Converter.DetectVersion (doc);
			TileManifest v2Manifest;
			if (ver <= 1)
				v2Manifest = TileManifestV1ToV2Converter.Convert (doc);
			else
			{
				using (var ms = new MemoryStream ())
				{
					doc.Save (ms);
					ms.Position = 0;
					v2Manifest = TileManifest.FromStream (ms);
				}
			}

			// 从包内 Executable 中导入磁贴，找出继承 TileBase 的入口点
			EnrichEntryPoints (v2Manifest, zip);

			using (var outMs = new MemoryStream ())
			using (var sw = new StreamWriter (outMs, new UTF8Encoding (false)))
			{
				v2Manifest.ToText (sw);
				sw.Flush ();
				return outMs.ToArray ();
			}
		}
		/// <summary>
		/// 对每个 Application，如果 EntryPoint 为空，就从包内的 Executable 里
		/// 反射查找第一个继承 TileBase 的非抽象类，填入 EntryPoint。
		/// 找不到或加载失败都保持原样。
		/// </summary>
		private static void EnrichEntryPoints (TileManifest manifest, ZipFile zip)
		{
			if (manifest?.Applications == null) return;

			foreach (var app in manifest.Applications)
			{
				if (app == null) continue;
				if (!string.IsNullOrEmpty (app.EntryPoint)) continue;
				if (string.IsNullOrEmpty (app.Executable)) continue;

				string entryName = app.Executable.Replace ('\\', '/');
				var entry = zip.GetEntry (entryName);
				if (entry == null || !entry.IsFile) continue;

				byte [] bytes;
				using (var s = zip.GetInputStream (entry))
				using (var ms = new MemoryStream ())
				{
					s.CopyTo (ms);
					bytes = ms.ToArray ();
				}

				var typeName = TileEntryPointFinder.FindEntryPoint (bytes);
				if (!string.IsNullOrEmpty (typeName))
					app.EntryPoint = typeName;
			}
		}
	}
}
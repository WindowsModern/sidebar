using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using ICSharpCode.SharpZipLib.Zip;
namespace Sidebar
{
	[XmlRoot ("Package")]
	public class TileBundleFileListItem: IXmlSerializable
	{
		[XmlAttribute ("FileName")]
		public string FileName { get; private set; }
		[XmlAttribute ("ProcessorArchitecture")]
		public ProcessorArchitecture ProcessorArchitecture { get; private set; }
		[XmlAttribute ("OSMinVersion")]
		public Version? OsMinVersion { get; private set; } = null;
		public override string ToString ()
			=> $"\"{FileName}\" {ProcessorArchitecture}";
		public TileBundleFileListItem (string fn, ProcessorArchitecture pa)
		{
			FileName = fn;
			ProcessorArchitecture = pa;
		}
		public TileBundleFileListItem (string fn, ProcessorArchitecture pa, Version osMin)
		{
			FileName = fn;
			ProcessorArchitecture = pa;
			OsMinVersion = osMin;
		}
		private TileBundleFileListItem () { }
		public XmlSchema GetSchema () => null;
		public void ReadXml (XmlReader reader)
		{
			reader.MoveToContent ();
			FileName = reader.GetAttribute ("FileName");

			string arch = reader.GetAttribute ("ProcessorArchitecture");
			ProcessorArchitecture pa;
			if (!Enum.TryParse (arch, out pa)) pa = ProcessorArchitecture.Unknown;
			ProcessorArchitecture = pa;
			string verStr = reader.GetAttribute ("OSMinVersion");
			if (!string.IsNullOrEmpty (verStr))
			{
				Version v;
				if (Version.TryParse (verStr, out v))
					OsMinVersion = v;
			}
			reader.ReadStartElement ("Package");
		}
		public void WriteXml (XmlWriter writer)
		{
			writer.WriteAttributeString ("FileName", FileName);
			writer.WriteAttributeString (
				"ProcessorArchitecture",
				ProcessorArchitecture.ToString ());
			if (OsMinVersion != null)
				writer.WriteAttributeString ("OSMinVersion", OsMinVersion.ToString ());
		}
	}
	[XmlRoot ("Bundle")]
	public class TileBundleManifest: IXmlSerializable
	{
		[XmlElement ("Identity")]
		public TileIdentity Identity { get; private set; }
		[XmlArray ("Packages")]
		[XmlArrayItem ("Package")]
		public List<TileBundleFileListItem> Packages { get; private set; } = new List<TileBundleFileListItem> ();
		[XmlIgnore]
		public List<TileBundleFileListItem> Items => Packages;
		public TileBundleManifest (TileIdentity identity, List<TileBundleFileListItem> packages)
		{
			if (identity == null) throw new ArgumentNullException (nameof (identity));
			Identity = identity;
			Packages = packages ?? new List<TileBundleFileListItem> ();
		}
		private TileBundleManifest () { }
		public static TileBundleManifest FromStream (Stream stream)
		{
			var serializer = new XmlSerializer (typeof (TileBundleManifest));
			return (TileBundleManifest) serializer.Deserialize (stream);
		}
		public static TileBundleManifest FromFile (string filePath)
		{
			using (var fs = File.OpenRead (filePath))
				return FromStream (fs);
		}
		public void ToStream (Stream stream)
		{
			var serializer = new XmlSerializer (typeof (TileBundleManifest));
			var ns = new XmlSerializerNamespaces ();
			ns.Add ("", "");
			serializer.Serialize (stream, this, ns);
		}
		public void ToFile (string filePath)
		{
			using (var fs = File.Create (filePath))
				ToStream (fs);
		}
		public XmlSchema GetSchema () => null;
		public void ReadXml (XmlReader reader)
		{
			reader.MoveToContent ();

			reader.ReadStartElement ("Bundle");

			while (reader.NodeType != XmlNodeType.EndElement)
			{
				if (reader.NodeType == XmlNodeType.Element)
				{
					if (reader.LocalName == "Identity")
					{
						var ser = new XmlSerializer (
							typeof (TileIdentity),
							new XmlRootAttribute ("Identity"));

						Identity =
							(TileIdentity)ser.Deserialize (reader);
					}
					else if (reader.LocalName == "Packages")
					{
						bool empty = reader.IsEmptyElement;

						reader.ReadStartElement ("Packages");

						if (!empty)
						{
							while (
								reader.NodeType != XmlNodeType.EndElement)
							{
								if (
									reader.NodeType ==
									XmlNodeType.Element &&
									reader.LocalName == "Package")
								{
									var ser = new XmlSerializer (
										typeof (
											TileBundleFileListItem),
										new XmlRootAttribute (
											"Package"));

									Packages.Add (
										(TileBundleFileListItem)
										ser.Deserialize (reader));
								}
								else
								{
									reader.Read ();
								}
							}

							reader.ReadEndElement ();
						}
					}
					else
					{
						reader.Skip ();
					}
				}
				else
				{
					reader.Read ();
				}
			}

			reader.ReadEndElement ();
		}
		public void WriteXml (XmlWriter writer)
		{
			var ns = new XmlSerializerNamespaces ();

			ns.Add ("", "");

			var idSer = new XmlSerializer (
				typeof (TileIdentity),
				new XmlRootAttribute ("Identity"));

			idSer.Serialize (writer, Identity, ns);

			writer.WriteStartElement ("Packages");

			var pkgSer = new XmlSerializer (
				typeof (TileBundleFileListItem),
				new XmlRootAttribute ("Package"));

			foreach (var p in Packages)
			{
				pkgSer.Serialize (writer, p, ns);
			}

			writer.WriteEndElement ();
		}
	}
	public enum TilePackageType
	{
		Single,
		Bundle,
		SubPackage
	};
	public abstract class TilePackageBase: IDisposable
	{
		public TilePackageType PackageType { get; internal set; } = TilePackageType.Single;
		public string FileName { get; internal set; }
		public ZipFile FileStream { get; internal set; }
		public virtual ILocaleResources StringResources { get; internal set; }
		public virtual IPathResources FileResources { get; internal set; }
		/// <summary>
		/// 从包中提取指定名称的文件内容（字节数组）。
		/// 支持路径格式：自动转换反斜杠为正斜杠，忽略首尾斜杠和空格，大小写不敏感匹配。
		/// </summary>
		/// <param name="entryName">要提取的条目名称（例如 "folder/file.txt"）</param>
		/// <returns>文件内容的字节数组，未找到则返回 null</returns>
		public virtual byte [] ExtractFile (string entryName)
		{
			if (string.IsNullOrEmpty (entryName)) return new byte [0];
			if (FileStream == null) return new byte [0];
			try
			{
				string normalized = entryName.Trim ();
				normalized = normalized.Replace ('\\', '/');
				normalized = normalized.TrimStart ('/');
				foreach (ZipEntry entry in FileStream)
				{
					if (!entry.IsFile) continue;
					string entryNormalized = entry.Name.Trim ();
					entryNormalized = entryNormalized.Replace ('\\', '/');
					entryNormalized = entryNormalized.TrimStart ('/');
					if (string.Equals (entryNormalized, normalized, StringComparison.OrdinalIgnoreCase))
					{
						using (Stream stream = FileStream.GetInputStream (entry))
						using (MemoryStream ms = new MemoryStream ())
						{
							stream.CopyTo (ms);
							return ms.ToArray ();
						}
					}
				}
			}
			catch
			{
			}
			return new byte [0];
		}
		public virtual void Dispose () { FileStream?.Close (); }
	}
	public class TilePackage: TilePackageBase
	{
		public TileManifest Manifest { get; internal set; }
		private void InitResources ()
		{
			try
			{
				byte [] localeResContent = ExtractFile ("Locale.xml");
				if (localeResContent != null && localeResContent.Length > 0)
				{
					using (var ms = new MemoryStream (localeResContent))
					{
						var xmldom = new XmlDocument ();
						xmldom.Load (ms);
						StringResources = LocaleResources.CreateFromXml (xmldom);
					}
				}
				else StringResources = new LocaleResources ();
				byte [] fileResContent = ExtractFile ("Path.xml");
				if (fileResContent != null && fileResContent.Length > 0)
				{
					using (var ms = new MemoryStream (fileResContent))
					{
						var xmldom = new XmlDocument ();
						xmldom.Load (ms);
						FileResources = PathResources.CreateFromXml (xmldom);
					}
				}
				else FileResources = new PathResources ();
			}
			catch
			{
				FileResources = new PathResources ();
				StringResources = new LocaleResources ();
			}
		}
		private void InitializeByFile ()
		{
			PackageType = TilePackageType.Single;
			var zip = new ZipFile (FileName);
			var isException = false;
			try
			{
				var manifestEntry = zip.GetEntry ("Manifest.xml");
				var tileEntry = zip.GetEntry ("Tile.dll");
				if (!manifestEntry.IsFile)
				{
					zip.Close ();
					throw new Exception ("Error: cannot find file Manifest.xml");
				}
				if (!tileEntry.IsFile)
				{
					zip.Close ();
					throw new Exception ("Error: cannot find file Manifest.xml");
				}
				using (Stream manifestStream = zip.GetInputStream (manifestEntry))
				{
					Manifest = TileManifest.FromStream (manifestStream);
				}
				FileStream = zip;
				InitResources ();
			}
			catch (Exception e)
			{
				isException = true;
				throw e;
			}
			finally
			{
				if (isException) zip?.Close ();
			}
		}
		private void InitializeByZipFile (ZipFile parentZip, ZipEntry subPkgEntry)
		{
			PackageType = TilePackageType.SubPackage;
			if (parentZip == null)
				throw new ArgumentNullException (nameof (parentZip));
			if (subPkgEntry == null)
				throw new ArgumentNullException (nameof (subPkgEntry));
			FileName = subPkgEntry.Name;
			using (Stream compressedStream = parentZip.GetInputStream (subPkgEntry))
			{
				var memoryStream = new MemoryStream ();
				compressedStream.CopyTo (memoryStream);
				memoryStream.Position = 0;
				try
				{
					var subZip = new ZipFile (memoryStream);
					var manifestEntry = subZip.GetEntry ("Manifest.xml");
					var tileEntry = subZip.GetEntry ("Tile.dll");
					if (manifestEntry == null || !manifestEntry.IsFile)
						throw new Exception ("Error: cannot find file Manifest.xml in subpackage");
					if (tileEntry == null || !tileEntry.IsFile)
						throw new Exception ("Error: cannot find file Tile.dll in subpackage");
					using (Stream manifestStream = subZip.GetInputStream (manifestEntry))
					{
						Manifest = TileManifest.FromStream (manifestStream);
					}
					FileStream = subZip;
					InitResources ();
				}
				catch
				{
					memoryStream.Dispose ();
					throw;
				}
			}
		}
		public TilePackage (string fileName)
		{
			FileName = fileName;
			InitializeByFile ();
		}
		public TilePackage (ZipFile parentZip, ZipEntry subPkgEntry)
		{
			InitializeByZipFile (parentZip, subPkgEntry);
		}
	}
	public class TilePackageBundle: TilePackageBase
	{
		public TileBundleManifest Manifest { get; internal set; }
		public List<TilePackage> Packages { get; } = new List<TilePackage> ();
		private ILocaleResources _blanksr = new LocaleResources ();
		private IPathResources _blankfr = new PathResources ();
		public override ILocaleResources StringResources
		{
			get
			{
				foreach (var p in Packages)
				{
					if (p == null) continue;
					return p.StringResources ?? _blanksr;
				}
				return _blanksr;
			}
			internal set { }
		}
		public override IPathResources FileResources
		{
			get
			{
				foreach (var p in Packages)
				{
					if (p == null) continue;
					return p.FileResources ?? _blankfr;
				}
				return _blankfr;
			}
			internal set { }
		}
		private void Initialize ()
		{
			PackageType = TilePackageType.Bundle;
			var zip = new ZipFile (FileName);
			var isException = false;
			try
			{
				var manifestEntry = zip.GetEntry ("BundleManifest.xml");
				if (!manifestEntry.IsFile)
				{
					zip.Close ();
					throw new Exception ("Error: cannot find file BundleManifest.xml");
				}
				using (Stream manifestStream = zip.GetInputStream (manifestEntry))
				{
					Manifest = TileBundleManifest.FromStream (manifestStream);
				}
				foreach (var m in Manifest.Items)
				{
					var subFn = m.FileName;
					var subEntry = zip.GetEntry (subFn);
					if (subEntry == null || !subEntry.IsFile)
						throw new FileNotFoundException ($"Error: cannot find package \"{subFn}\"");
					Packages.Add (new TilePackage (zip, subEntry));
				}
				FileStream = zip;
			}
			catch (Exception e)
			{
				isException = true;
				throw e;
			}
			finally
			{
				if (isException)
				{
					foreach (var p in Packages)
						p?.Dispose ();
				}
				if (isException) zip?.Close ();
			}
		}
		public TilePackageBundle (string fileName)
		{
			FileName = fileName;
			Initialize ();
		}
	}
	public static class TilePackageReadManager
	{
		public static TilePackageBase GetPackage (string filePath)
		{
			if (!TilePackageVerifier.Verify (filePath))
				throw new InvalidDataException ("Package integrity verification failed.");
			try
			{
				return new TilePackageBundle (filePath);
			}
			catch
			{
				return new TilePackage (filePath);
			}
			return null;
		}
	}
	public static class TilePackageWriteManager
	{
		public delegate void ProgressCallback (int curr, int total, double progress);
		public static ZipFile MakeSinglePackage (string folder, ProgressCallback callback = null)
		{
			if (folder == null) throw new ArgumentNullException (nameof (folder));
			if (!Directory.Exists (folder)) throw new DirectoryNotFoundException ($"Folder not found: {folder}");
			string manifestPath = Path.Combine (folder, "Manifest.xml");
			string tileDllPath = Path.Combine (folder, "Tile.dll");
			if (!File.Exists (manifestPath)) throw new FileNotFoundException ("Cannot find Manifest.xml");
			if (!File.Exists (tileDllPath)) throw new FileNotFoundException ("Cannot find Tile.dll");
			TileManifest manifest = TileManifest.FromFile (manifestPath);
			var allFiles = Directory.EnumerateFiles (folder, "*", SearchOption.AllDirectories).ToList ();
			int total = allFiles.Count;
			int current = 0;
			var ms = new MemoryStream ();
			using (var zipOutputStream = new ZipOutputStream (ms))
			{
				zipOutputStream.SetLevel (9); 
				foreach (string filePath in allFiles)
				{
					string relativePath = GetRelativePath (folder, filePath);
					relativePath = relativePath.Replace ('\\', '/');
					var entry = new ZipEntry (relativePath);
					entry.DateTime = File.GetLastWriteTime (filePath);
					zipOutputStream.PutNextEntry (entry);
					using (var fs = File.OpenRead (filePath))
					{
						fs.CopyTo (zipOutputStream);
					}
					zipOutputStream.CloseEntry ();
					current++;
					callback?.Invoke (current, total, (double)current / total);
				}
			}
			byte [] zipData = ms.ToArray ();
			var zipFile = new ZipFile (new MemoryStream (zipData));
			return zipFile;
		}
		public static ZipFile MakeBundlePackage (List<string> fileList, ProgressCallback callback = null)
		{
			if (fileList == null)
				throw new ArgumentNullException (nameof (fileList));
			if (fileList.Count <= 1)
				throw new InvalidOperationException (
					"Error: To create a bundle package, you must provide two or more packages.");

			TileManifest lastManifest = null;

			// 唯一性键：(架构, OSMinVersion)。允许不同架构、不同 OS 版本共存，只要组合不重复。
			// 注：System.Version 已正确重写 Equals/GetHashCode，可直接作为 Tuple 键的一部分。
			var packageMap = new Dictionary<Tuple<ProcessorArchitecture, Version>, string> ();

			foreach (var fp in fileList)
			{
				using (var p = TilePackageReadManager.GetPackage (fp))
				{
					if (p.PackageType == TilePackageType.Bundle)
						throw new InvalidOperationException (
							"Error: A bundle package cannot be packed into another bundle.");

					var sp = p as TilePackage;
					if (sp == null)
						throw new InvalidDataException ($"Error: \"{fp}\" is not a single package.");

					var arch = sp.Manifest.Identity.ProcessorArchitecture;
					if (arch == ProcessorArchitecture.Neutral ||
						arch == ProcessorArchitecture.Unknown)
						throw new InvalidOperationException (
							$"Error: Package \"{fp}\" has architecture {arch}, which cannot be bundled.");

					var osMin = sp.Manifest.Prerequisites.OSMinVersion;

					// 清单一致性：除架构与 Prerequisites 外，其余必须一致
					if (lastManifest == null)
					{
						lastManifest = sp.Manifest;
					}
					else if (!ManifestsConsistent (sp.Manifest, lastManifest))
					{
						throw new InvalidOperationException (
							$"Error: The manifest of \"{fp}\" is inconsistent with the first package " +
							"(only ProcessorArchitecture and Prerequisites may differ).");
					}

					var key = Tuple.Create (arch, osMin);
					if (packageMap.ContainsKey (key))
						throw new InvalidOperationException (
							$"Error: More than one package has the same architecture and OS version " +
							$"(arch: {arch}, osMin: {(osMin == null ? "<none>" : osMin.ToString ())}).");

					packageMap [key] = fp;
				}
			}

			var bundleIdentity = new TileIdentity (
				lastManifest.Identity.Name,
				lastManifest.Identity.Publisher,
				lastManifest.Identity.Version,
				ProcessorArchitecture.Neutral
			);

			string filenamePrefix = bundleIdentity.Name.Length >= 16
				? bundleIdentity.Name.Substring (0, 16)
				: bundleIdentity.Name;

			var ms = new MemoryStream ();
			using (var zipOutputStream = new ZipOutputStream (ms))
			{
				zipOutputStream.SetLevel (9);

				int total = packageMap.Count;
				int current = 0;
				var bundleItems = new List<TileBundleFileListItem> ();

				// 排序以保证 bundle 输出可复现
				var ordered = packageMap
					.OrderBy (kv => kv.Key.Item1.ToString (), StringComparer.OrdinalIgnoreCase)
					.ThenBy (kv => kv.Key.Item2.ToString () ?? "", StringComparer.Ordinal)
					.ToList ();

				foreach (var kv in ordered)
				{
					var arch = kv.Key.Item1;
					var osMin = kv.Key.Item2;

					// 内部文件名带上 OS 版本，避免同一架构下多版本冲突
					string internalFileName = osMin == null
						? $"{filenamePrefix}_{arch}.sgpkg"
						: $"{filenamePrefix}_{arch}_{osMin}.sgpkg";

					var entry = new ZipEntry (internalFileName);
					entry.DateTime = File.GetLastWriteTime (kv.Value);
					zipOutputStream.PutNextEntry (entry);
					using (var fs = File.OpenRead (kv.Value))
					{
						fs.CopyTo (zipOutputStream);
					}
					zipOutputStream.CloseEntry ();

					bundleItems.Add (new TileBundleFileListItem (internalFileName, arch, osMin));
					current++;
					callback?.Invoke (current, total, (double)current / total);
				}

				var bundleManifest = new TileBundleManifest (bundleIdentity, bundleItems);
				var manifestEntry = new ZipEntry ("BundleManifest.xml");
				manifestEntry.DateTime = DateTime.Now;
				zipOutputStream.PutNextEntry (manifestEntry);

				var serializer = new XmlSerializer (typeof (TileBundleManifest));
				var ns = new XmlSerializerNamespaces ();
				ns.Add ("", "");
				serializer.Serialize (zipOutputStream, bundleManifest, ns);
				zipOutputStream.CloseEntry ();
			}

			byte [] zipData = ms.ToArray ();
			return new ZipFile (new MemoryStream (zipData));
		}
		/// <summary>
		/// 判断两个 Manifest 是否“除架构与 OS 版本外”一致。
		/// 允许 Identity.ProcessorArchitecture 与 Prerequisites 不同。
		/// </summary>
		private static bool ManifestsConsistent (TileManifest a, TileManifest b)
		{
			if (a == null || b == null) return ReferenceEquals (a, b);

			// Identity: 架构允许不同，其余必须一致
			if (a.Identity == null || b.Identity == null) return ReferenceEquals (a.Identity, b.Identity);
			if (!string.Equals (a.Identity.Name, b.Identity.Name, StringComparison.OrdinalIgnoreCase)) return false;
			if (!string.Equals (a.Identity.Publisher, b.Identity.Publisher, StringComparison.OrdinalIgnoreCase)) return false;
			if (!Equals (a.Identity.Version, b.Identity.Version)) return false;

			// Properties
			if (a.Properties == null || b.Properties == null)
				return ReferenceEquals (a.Properties, b.Properties);
			if (!string.Equals (a.Properties.DisplayName, b.Properties.DisplayName, StringComparison.Ordinal)) return false;
			if (!string.Equals (a.Properties.PublisherDisplayName, b.Properties.PublisherDisplayName, StringComparison.Ordinal)) return false;
			if (!string.Equals (a.Properties.Description, b.Properties.Description, StringComparison.Ordinal)) return false;
			if (!string.Equals (a.Properties.Logo, b.Properties.Logo, StringComparison.Ordinal)) return false;
			if (a.Properties.Type != b.Properties.Type) return false;

			// Prerequisites: 明确跳过（允许不同 OS 版本）

			// VisualElements
			if (a.VisualElements == null || b.VisualElements == null)
				return ReferenceEquals (a.VisualElements, b.VisualElements);
			if (!RailStylesEqual (a.VisualElements.RailStyle, b.VisualElements.RailStyle)) return false;
			if (!GridStylesEqual (a.VisualElements.GridStyle, b.VisualElements.GridStyle)) return false;

			return true;
		}
		private static bool RailStylesEqual (TileRailStyle a, TileRailStyle b)
		{
			if (a == null || b == null) return ReferenceEquals (a, b);
			return a.CanPinBottom == b.CanPinBottom
				&& a.DefaultHeight == b.DefaultHeight
				&& string.Equals (a.DisplayName, b.DisplayName, StringComparison.Ordinal)
				&& a.FlyoutCanResize == b.FlyoutCanResize
				&& a.FlyoutHeight == b.FlyoutHeight
				&& a.FlyoutWidth == b.FlyoutWidth
				&& string.Equals (a.Logo, b.Logo, StringComparison.Ordinal)
				&& a.MaxHeight == b.MaxHeight
				&& a.MinHeight == b.MinHeight
				&& a.Overflow == b.Overflow
				&& a.TileHasFlyout == b.TileHasFlyout
				&& a.TileHasProperties == b.TileHasProperties;
		}
		private static bool GridStylesEqual (TileGridStyle a, TileGridStyle b)
		{
			return true;
		}
		private static string GetRelativePath (string baseDir, string fullPath)
		{
			if (!baseDir.EndsWith ("\\")) baseDir += "\\";
			if (fullPath.StartsWith (baseDir, StringComparison.OrdinalIgnoreCase))
			{
				return fullPath.Substring (baseDir.Length);
			}
			Uri baseUri = new Uri (baseDir);
			Uri fullUri = new Uri (fullPath);
			Uri relativeUri = baseUri.MakeRelativeUri (fullUri);
			return Uri.UnescapeDataString (relativeUri.ToString ()).Replace ('/', '\\');
		}
		/// <summary>
		/// 保存 ZipFile 到指定目录，添加完整性数据（TileBlockMap 和 Signature）。
		/// 扩展名根据包内容自动选择：.sgpkg（单包）或 .sgpkgbundle（捆绑包）。
		/// </summary>
		/// <param name="zip">要保存的 ZipFile 实例（必须可读，且不包含 BlockMap.xml 和 Signature.bin）</param>
		/// <param name="saveDir">目标目录</param>
		/// <param name="saveFileName">文件名（不含扩展名）</param>
		public static void SavePackageFile (ZipFile zip, string saveDir, string saveFileName)
		{
			if (zip == null) throw new ArgumentNullException (nameof (zip));
			if (string.IsNullOrEmpty (saveDir)) throw new ArgumentNullException (nameof (saveDir));
			if (string.IsNullOrEmpty (saveFileName)) throw new ArgumentNullException (nameof (saveFileName));
			if (!Directory.Exists (saveDir))
				Directory.CreateDirectory (saveDir);
			bool isBundle = zip.GetEntry ("BundleManifest.xml") != null;
			bool isSingle = zip.GetEntry ("Manifest.xml") != null;
			string extension = isBundle ? ".sgpkgbundle" : (isSingle ? ".sgpkg" : ".sgpkg");
			string outputPath = Path.Combine (saveDir, saveFileName + extension);
			var entries = zip.Cast<ZipEntry> ()
				.Where (e => e.Name != "BlockMap.xml" && e.Name != "Signature.bin" && e.IsFile)
				.ToList ();
			var fileHashes = new Dictionary<string, string> ();
			foreach (var entry in entries)
			{
				using (var stream = zip.GetInputStream (entry))
				{
					byte [] hash = ComputeSha256 (stream);
					fileHashes [entry.Name] = Convert.ToBase64String (hash);
				}
			}
			var blockMap = new TileBlockMap {
				Files = fileHashes.Select (kv => new TileBlockMapFile {
					Name = kv.Key,
					HashBase64 = kv.Value
				}).ToList ()
			};
			string blockMapXml = SerializeToXml (blockMap);
			byte [] blockMapBytes = Encoding.UTF8.GetBytes (blockMapXml);
			byte [] allHashBytes = fileHashes.Values
				.Select (Convert.FromBase64String)
				.SelectMany (b => b)
				.ToArray ();
			byte [] derivedKey = ComputeSha256 (allHashBytes);
			byte [] signature;
			using (var hmac = new HMACSHA256 (derivedKey))
			{
				signature = hmac.ComputeHash (blockMapBytes);
			}
			using (var outputStream = File.Create (outputPath))
			using (var zipOutputStream = new ZipOutputStream (outputStream))
			{
				zipOutputStream.SetLevel (9);
				foreach (var entry in entries)
				{
					var newEntry = new ZipEntry (entry.Name);
					newEntry.DateTime = entry.DateTime;
					newEntry.Size = entry.Size;
					newEntry.ExternalFileAttributes = entry.ExternalFileAttributes;
					zipOutputStream.PutNextEntry (newEntry);
					using (var entryStream = zip.GetInputStream (entry))
					{
						entryStream.CopyTo (zipOutputStream);
					}
					zipOutputStream.CloseEntry ();
				}
				var blockMapEntry = new ZipEntry ("BlockMap.xml");
				blockMapEntry.DateTime = DateTime.UtcNow;
				zipOutputStream.PutNextEntry (blockMapEntry);
				zipOutputStream.Write (blockMapBytes, 0, blockMapBytes.Length);
				zipOutputStream.CloseEntry ();
				var sigEntry = new ZipEntry ("Signature.bin");
				sigEntry.DateTime = DateTime.UtcNow;
				zipOutputStream.PutNextEntry (sigEntry);
				zipOutputStream.Write (signature, 0, signature.Length);
				zipOutputStream.CloseEntry ();
			}
		}
		private static byte [] ComputeSha256 (Stream stream)
		{
			using (var sha = SHA256.Create ())
				return sha.ComputeHash (stream);
		}
		private static byte [] ComputeSha256 (byte [] data)
		{
			using (var sha = SHA256.Create ())
				return sha.ComputeHash (data);
		}
		private static string SerializeToXml<T> (T obj)
		{
			var serializer = new XmlSerializer (typeof (T));
			using (var sw = new StringWriter ())
			{
				var ns = new XmlSerializerNamespaces ();
				ns.Add ("", "");
				serializer.Serialize (sw, obj, ns);
				return sw.ToString ();
			}
		}
	}
	[XmlRoot ("BlockMap")]
	public class TileBlockMap
	{
		[XmlElement ("File")]
		public List<TileBlockMapFile> Files { get; set; } = new List<TileBlockMapFile> ();
	}
	public class TileBlockMapFile
	{
		[XmlAttribute ("Name")]
		public string Name { get; set; }
		[XmlAttribute ("Hash")]
		public string HashBase64 { get; set; }
	}
	internal static class TilePackageVerifier
	{
		public static bool Verify (string filePath)
		{
			using (var zip = new ZipFile (filePath))
			{
				var blockMapEntry = zip.GetEntry ("BlockMap.xml");
				var sigEntry = zip.GetEntry ("Signature.bin");
				if (blockMapEntry == null || sigEntry == null) return false;
				TileBlockMap blockMap;
				using (var stream = zip.GetInputStream (blockMapEntry))
				using (var sr = new StreamReader (stream, Encoding.UTF8))
				{
					var serializer = new XmlSerializer (typeof (TileBlockMap));
					blockMap = (TileBlockMap)serializer.Deserialize (sr);
				}
				byte [] signature;
				using (var stream = zip.GetInputStream (sigEntry))
				using (var ms = new MemoryStream ())
				{
					stream.CopyTo (ms);
					signature = ms.ToArray ();
				}
				var entries = zip.Cast<ZipEntry> ()
					.Where (e => e.Name != "BlockMap.xml" && e.Name != "Signature.bin" && e.IsFile)
					.ToList ();
				var hashDict = new Dictionary<string, string> ();
				foreach (var entry in entries)
				{
					using (var stream = zip.GetInputStream (entry))
					{
						byte [] hash = ComputeSha256 (stream);
						hashDict [entry.Name] = Convert.ToBase64String (hash);
					}
				}
				if (blockMap.Files.Count != hashDict.Count) return false;
				foreach (var file in blockMap.Files)
				{
					string hash;
					if (!hashDict.TryGetValue (file.Name, out hash) || hash != file.HashBase64)
						return false;
				}

				byte [] allHashBytes = hashDict.Values
					.Select (Convert.FromBase64String)
					.SelectMany (b => b)
					.ToArray ();
				byte [] derivedKey = ComputeSha256 (allHashBytes);
				byte [] blockMapBytes = Encoding.UTF8.GetBytes (SerializeToXml (blockMap));
				byte [] computedSig;
				using (var hmac = new HMACSHA256 (derivedKey))
				{
					computedSig = hmac.ComputeHash (blockMapBytes);
				}
				return computedSig.SequenceEqual (signature);
			}
		}
		private static byte [] ComputeSha256 (Stream stream)
		{
			using (var sha = SHA256.Create ())
				return sha.ComputeHash (stream);
		}
		private static byte [] ComputeSha256 (byte [] data)
		{
			using (var sha = SHA256.Create ())
				return sha.ComputeHash (data);
		}
		private static string SerializeToXml<T> (T obj)
		{
			var serializer = new XmlSerializer (typeof (T));
			using (var sw = new StringWriter ())
			{
				var ns = new XmlSerializerNamespaces ();
				ns.Add ("", "");
				serializer.Serialize (sw, obj, ns);
				return sw.ToString ();
			}
		}
	}
}

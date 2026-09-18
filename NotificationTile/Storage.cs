using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Collections.Specialized;
using System.Collections;
using Newtonsoft.Json;
using System.Threading;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Globalization;
using Sidebar;

namespace WindowsModern.NotificationHistoryTile
{
	public class NotificationStoreData: INotifyPropertyChanged, IDisposable
	{
		// ---- backing fields ----
		private DateTime _timeStampUtc;
		private string _iconFileName;
		private string _infoTitle;
		private string _info;
		private int _timeout;
		private NotifyIconInfoType _type;
		private uint _callbackMessage;
		private bool _isRead;
		[JsonIgnore]
		private readonly HashSet<PropertyChangedEventHandler> handlers = new HashSet<PropertyChangedEventHandler> ();
		public event PropertyChangedEventHandler PropertyChanged
		{
			add
			{
				if (value == null) return;
				lock (handlers) { handlers.Add (value); }
			}
			remove
			{
				if (value == null) return;
				lock (handlers) { handlers.Remove (value); }
			}
		}
		public DateTime TimeStampUtc
		{
			get { return _timeStampUtc; }
			set
			{
				if (_timeStampUtc == value) return;
				_timeStampUtc = value;
				OnPropertyChanged (nameof (TimeStampUtc));
				OnPropertyChanged (nameof (ToolTip));
			}
		}
		public string IconFileName
		{
			get { return _iconFileName; }
			set
			{
				if (string.Equals (_iconFileName, value, StringComparison.Ordinal)) return;
				_iconFileName = value;
				OnPropertyChanged (nameof (IconFileName));
			}
		}
		public string InfoTitle
		{
			get { return _infoTitle; }
			set
			{
				if (string.Equals (_infoTitle, value, StringComparison.Ordinal)) return;
				_infoTitle = value;
				OnPropertyChanged (nameof (InfoTitle));
				OnPropertyChanged (nameof (ToolTip));
			}
		}
		public string Info
		{
			get { return _info; }
			set
			{
				if (string.Equals (_info, value, StringComparison.Ordinal)) return;
				_info = value;
				OnPropertyChanged (nameof (Info));
				OnPropertyChanged (nameof (ToolTip));
			}
		}
		public int Timeout
		{
			get { return _timeout; }
			set
			{
				if (_timeout == value) return;
				_timeout = value;
				OnPropertyChanged (nameof (Timeout));
			}
		}
		public NotifyIconInfoType Type
		{
			get { return _type; }
			set
			{
				if (_type == value) return;
				_type = value;
				OnPropertyChanged (nameof (Type));
			}
		}
		public uint CallbackMessage
		{
			get { return _callbackMessage; }
			set
			{
				if (_callbackMessage == value) return;
				_callbackMessage = value;
				OnPropertyChanged (nameof (CallbackMessage));
			}
		}
		public bool IsRead
		{
			get { return _isRead; }
			set
			{
				if (_isRead == value) return;
				_isRead = value;
				OnPropertyChanged (nameof (IsRead));
			}
		}
		private void OnPropertyChanged (string propName)
		{
			PropertyChangedEventHandler [] snapshot;
			lock (handlers)
			{
				if (handlers.Count == 0) return;
				snapshot = new PropertyChangedEventHandler [handlers.Count];
				handlers.CopyTo (snapshot);
			}

			var e = new PropertyChangedEventArgs (propName);
			foreach (var h in snapshot)
			{
				if (h != null) h (this, e);
			}
		}
		public void Dispose ()
		{
			handlers?.Clear ();
		}
		[JsonIgnore]
		public string ToolTip
		{
			get
			{
				string time = TimeStampUtc.ToLocalTime ().ToString ("G");
				if (!string.IsNullOrEmpty (InfoTitle)) return time + "\n" + InfoTitle;
				return time + "\n" + Info;
			}
		}
		[JsonIgnore]
		public Guid Id
		{
			get
			{
				var raw = string.Concat (
					_timeStampUtc.ToString ("O", CultureInfo.InvariantCulture), "\u001F",
					_iconFileName ?? string.Empty, "\u001F",
					_infoTitle ?? string.Empty, "\u001F",
					_info ?? string.Empty, "\u001F",
					_timeout.ToString (CultureInfo.InvariantCulture), "\u001F",
					((int)_type).ToString (CultureInfo.InvariantCulture), "\u001F",
					_callbackMessage.ToString (CultureInfo.InvariantCulture), "\u001F",
					_isRead ? "1" : "0"
				);
				using (var md5 = new MD5CryptoServiceProvider ())
				{
					var hash = md5.ComputeHash (Encoding.UTF8.GetBytes (raw));
					return new Guid (hash);
				}
			}
		}
		[JsonIgnore]
		public DateTime DateKey
		{
			get { return TimeStampUtc.ToLocalTime ().Date; }
		}
	}
	public class NotificationHistory: IEnumerable<NotificationStoreData>, ICollection<NotificationStoreData>, INotifyCollectionChanged, IDisposable
	{
		// ============== 配置 ==============
		private const int MaxItemsPerShard = 500;               // 每个分片的最大条目数
		private const string ShardPrefix = "shard_";
		private const string ShardSuffix = ".json";
		private const string TempSuffix = ".tmp";
		private const int StreamBufferSize = 8192;              // 8KB 流缓冲（SSD 友好）

		// 防抖窗口：最后一次修改后多久才真正写盘
		private static readonly TimeSpan FlushDelay = TimeSpan.FromMilliseconds (500);

		// ============== 状态 ==============
		private readonly string basedir;
		private readonly object sync = new object ();
		private readonly List<Shard> shards = new List<Shard> ();
		private readonly HashSet<NotifyCollectionChangedEventHandler> changedhandlers =
			new HashSet<NotifyCollectionChangedEventHandler> ();
		private readonly JsonSerializerSettings jsonSettings;

		private Timer flushTimer;      // 防抖用
		private int nextShardIndex;  // 下一个分片的编号
		private bool dirty;           // 是否有分片需要写盘
		private bool disposed;

		public string BaseImageDir => Path.Combine (BaseDir, "Images");
		// 一个分片 = 一个文件 + 内存中的条目列表
		private sealed class Shard
		{
			public string FileName;
			public List<NotificationStoreData> Items = new List<NotificationStoreData> ();
			public bool Dirty;
		}

		// ============== 事件 ==============
		public event NotifyCollectionChangedEventHandler CollectionChanged
		{
			add
			{
				if (value == null) return;
				lock (changedhandlers) { changedhandlers.Add (value); }
			}
			remove
			{
				if (value == null) return;
				lock (changedhandlers) { changedhandlers.Remove (value); }
			}
		}

		// ============== 构造 / 销毁 ==============
		public NotificationHistory (string basedir)
		{
			if (string.IsNullOrEmpty (basedir))
				throw new ArgumentException ("basedir 不能为空", "basedir");

			this.basedir = basedir;
			Directory.CreateDirectory (basedir);

			jsonSettings = new JsonSerializerSettings {
				DateFormatHandling = DateFormatHandling.IsoDateFormat,
				DateTimeZoneHandling = DateTimeZoneHandling.Utc,
				Formatting = Formatting.None,          // 紧凑，省磁盘
				NullValueHandling = NullValueHandling.Ignore,
				MissingMemberHandling = MissingMemberHandling.Ignore,
			};

			LoadFromDisk ();

			// 初始不启动，等第一次变更再 Change(...)
			flushTimer = new Timer (OnFlushTimer, null, Timeout.Infinite, Timeout.Infinite);
		}

		public void Dispose ()
		{
			Timer t;
			GroupedNotificationHistory g = null;

			lock (sync)
			{
				if (disposed) return;
				disposed = true;

				t = flushTimer;
				flushTimer = null;

				g = grouped;
				grouped = null;
			}

			if (t != null) t.Dispose ();
			if (g != null) g.Dispose ();

			try { FlushInternal (); } catch { }
		}

		// ============== 只读属性 ==============
		public string BaseDir { get { return basedir; } }

		public int Count
		{
			get { lock (sync) { return CountUnsafe (); } }
		}

		public bool IsReadOnly
		{
			get { return false; }
		}

		// ============== ICollection 实现 ==============
		public void Add (NotificationStoreData item)
		{
			if (item == null) throw new ArgumentNullException ("item");

			NotifyCollectionChangedEventArgs e;
			lock (sync)
			{
				ThrowIfDisposed ();

				int globalIndex = CountUnsafe ();

				Shard shard;
				if (shards.Count == 0 ||
					shards [shards.Count - 1].Items.Count >= MaxItemsPerShard)
				{
					shard = new Shard {
						FileName = MakeShardFileName (nextShardIndex++),
						Dirty = true,
					};
					shards.Add (shard);
				}
				else
				{
					shard = shards [shards.Count - 1];
				}

				shard.Items.Add (item);
				shard.Dirty = true;
				item.PropertyChanged += Item_PropertyChanged;

				ScheduleFlushLocked ();

				e = new NotifyCollectionChangedEventArgs (
						NotifyCollectionChangedAction.Add, item, globalIndex);
			}

			RaiseCollectionChanged (e);
		}

		public bool Remove (NotificationStoreData item)
		{
			if (item == null) return false;

			NotifyCollectionChangedEventArgs e = null;
			lock (sync)
			{
				ThrowIfDisposed ();

				for (int si = 0; si < shards.Count; si++)
				{
					int idx = shards [si].Items.IndexOf (item);
					if (idx < 0) continue;

					int globalIndex = GlobalIndexUnsafe (si, idx);
					shards [si].Items.RemoveAt (idx);
					shards [si].Dirty = true;

					item.PropertyChanged -= Item_PropertyChanged;

					ScheduleFlushLocked ();

					e = new NotifyCollectionChangedEventArgs (
							NotifyCollectionChangedAction.Remove, item, globalIndex);
					break;
				}
			}

			if (e == null) return false;
			RaiseCollectionChanged (e);
			return true;
		}

		public void Clear ()
		{
			NotifyCollectionChangedEventArgs e;
			lock (sync)
			{
				ThrowIfDisposed ();

				for (int i = 0; i < shards.Count; i++)
				{
					var s = shards [i];
					for (int j = 0; j < s.Items.Count; j++)
						s.Items [j].PropertyChanged -= Item_PropertyChanged;

					s.Items.Clear ();
					s.Dirty = true;
				}

				ScheduleFlushLocked ();
				e = new NotifyCollectionChangedEventArgs (NotifyCollectionChangedAction.Reset);
			}

			RaiseCollectionChanged (e);
		}

		public bool Contains (NotificationStoreData item)
		{
			if (item == null) return false;
			lock (sync)
			{
				for (int i = 0; i < shards.Count; i++)
					if (shards [i].Items.Contains (item)) return true;
			}
			return false;
		}

		public void CopyTo (NotificationStoreData [] array, int arrayIndex)
		{
			if (array == null) throw new ArgumentNullException ("array");
			if (arrayIndex < 0) throw new ArgumentOutOfRangeException ("arrayIndex");

			lock (sync)
			{
				int count = CountUnsafe ();
				if (array.Length - arrayIndex < count)
					throw new ArgumentException ("目标数组空间不足", "array");

				int pos = arrayIndex;
				for (int i = 0; i < shards.Count; i++)
				{
					var list = shards [i].Items;
					for (int j = 0; j < list.Count; j++)
						array [pos++] = list [j];
				}
			}
		}

		public IEnumerator<NotificationStoreData> GetEnumerator ()
		{
			return Snapshot ().GetEnumerator ();
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			return GetEnumerator ();
		}

		// ============== 显式 Flush ==============
		/// <summary>
		/// 强制把所有脏分片落盘。Dispose 时也会自动调用。
		/// </summary>
		public void Flush ()
		{
			FlushInternal ();
		}

		// ============== 内部：索引/快照工具 ==============
		private int CountUnsafe ()
		{
			int n = 0;
			for (int i = 0; i < shards.Count; i++) n += shards [i].Items.Count;
			return n;
		}

		private int GlobalIndexUnsafe (int shardIndex, int itemIndex)
		{
			int n = 0;
			for (int i = 0; i < shardIndex; i++) n += shards [i].Items.Count;
			return n + itemIndex;
		}

		private List<NotificationStoreData> Snapshot ()
		{
			lock (sync) { return SnapshotUnsafe (); }
		}

		private List<NotificationStoreData> SnapshotUnsafe ()
		{
			var result = new List<NotificationStoreData> (CountUnsafe ());
			for (int i = 0; i < shards.Count; i++)
				result.AddRange (shards [i].Items);
			return result;
		}

		// ============== 内部：防抖 / 写盘 ==============
		private void ScheduleFlushLocked ()
		{
			dirty = true;
			if (flushTimer == null) return;
			try
			{
				// 重置计时器：连续调用只会触发一次
				flushTimer.Change (FlushDelay.Milliseconds, Timeout.Infinite);
			}
			catch (ObjectDisposedException) { /* 已 Dispose */ }
		}

		private void OnFlushTimer (object state)
		{
			try { FlushInternal (); }
			catch { /* 后台线程吞掉异常，避免崩溃 */ }
		}

		private void FlushInternal ()
		{
			List<KeyValuePair<string, List<NotificationStoreData>>> toWrite;

			lock (sync)
			{
				if (!dirty) return;

				toWrite = new List<KeyValuePair<string, List<NotificationStoreData>>> ();
				for (int i = 0; i < shards.Count; i++)
				{
					var s = shards [i];
					if (!s.Dirty) continue;

					// 快照：写盘期间允许内存继续被修改
					toWrite.Add (new KeyValuePair<string, List<NotificationStoreData>> (
						s.FileName, new List<NotificationStoreData> (s.Items)));
					s.Dirty = false;
				}

				dirty = false;
			}

			for (int i = 0; i < toWrite.Count; i++)
				WriteShardFile (toWrite [i].Key, toWrite [i].Value);
		}

		private void WriteShardFile (string fileName, List<NotificationStoreData> items)
		{
			string targetPath = Path.Combine (basedir, fileName);
			string tempPath = targetPath + TempSuffix;

			string json = JsonConvert.SerializeObject (items, jsonSettings);
			byte [] bytes = Encoding.UTF8.GetBytes (json);

			// 写临时文件 + Flush(true) 保证数据真正落到磁盘介质
			using (var fs = new FileStream (tempPath, FileMode.Create, FileAccess.Write,
										   FileShare.None, StreamBufferSize,
										   FileOptions.SequentialScan))
			{
				fs.Write (bytes, 0, bytes.Length);
				fs.Flush (true);   // FlushFileBuffers
			}

			// 原子替换（同卷）
			if (File.Exists (targetPath))
			{
				try
				{
					File.Replace (tempPath, targetPath, null, true);
				}
				catch (IOException)
				{
					// 某些文件系统/杀毒软件会挡住 Replace，退化为 delete+move
					try { File.Delete (targetPath); } catch { }
					File.Move (tempPath, targetPath);
				}
			}
			else
			{
				File.Move (tempPath, targetPath);
			}
		}

		// ============== 内部：从磁盘加载 ==============
		private void LoadFromDisk ()
		{
			string [] files = Directory.GetFiles (basedir, ShardPrefix + "*" + ShardSuffix);
			Array.Sort (files, StringComparer.OrdinalIgnoreCase);

			int maxIndex = -1;

			for (int i = 0; i < files.Length; i++)
			{
				string name = Path.GetFileName (files [i]);

				int idx = ParseShardIndex (name);
				if (idx > maxIndex) maxIndex = idx;

				List<NotificationStoreData> items = null;
				try
				{
					string json = File.ReadAllText (files [i], Encoding.UTF8);
					if (!string.IsNullOrEmpty (json))
					{
						items = JsonConvert.DeserializeObject<List<NotificationStoreData>> (
									json, jsonSettings);
					}
				}
				catch
				{
					// 分片损坏：跳过，不阻塞其它分片
					continue;
				}

				if (items == null) items = new List<NotificationStoreData> ();

				var shard = new Shard {
					FileName = name,
					Items = items,
					Dirty = false,
				};

				for (int k = 0; k < items.Count; k++)
				{
					if (items [k] != null)
						items [k].PropertyChanged += Item_PropertyChanged;
				}

				shards.Add (shard);
			}

			nextShardIndex = maxIndex + 1;
		}

		private static int ParseShardIndex (string fileName)
		{
			if (!fileName.StartsWith (ShardPrefix, StringComparison.OrdinalIgnoreCase)) return -1;
			if (!fileName.EndsWith (ShardSuffix, StringComparison.OrdinalIgnoreCase)) return -1;

			int len = fileName.Length - ShardPrefix.Length - ShardSuffix.Length;
			if (len <= 0) return -1;

			string num = fileName.Substring (ShardPrefix.Length, len);
			int result;
			return int.TryParse (num, out result) ? result : -1;
		}

		private static string MakeShardFileName (int index)
		{
			return ShardPrefix + index.ToString ("D6") + ShardSuffix;
		}

		// ============== 内部：单个条目属性变化 ==============
		private void Item_PropertyChanged (object sender, PropertyChangedEventArgs e)
		{
			var item = sender as NotificationStoreData;
			if (item == null) return;

			lock (sync)
			{
				if (disposed) return;

				for (int i = 0; i < shards.Count; i++)
				{
					if (shards [i].Items.Contains (item))
					{
						shards [i].Dirty = true;
						ScheduleFlushLocked ();
						return;
					}
				}
			}
		}

		// ============== 内部：事件触发 ==============
		private void RaiseCollectionChanged (NotifyCollectionChangedEventArgs e)
		{
			NotifyCollectionChangedEventHandler [] snapshot;
			lock (changedhandlers)
			{
				if (changedhandlers.Count == 0) return;
				snapshot = new NotifyCollectionChangedEventHandler [changedhandlers.Count];
				changedhandlers.CopyTo (snapshot);
			}

			for (int i = 0; i < snapshot.Length; i++)
			{
				var h = snapshot [i];
				if (h != null) h (this, e);
			}
		}

		private void ThrowIfDisposed ()
		{
			if (disposed) throw new ObjectDisposedException ("NotificationHistory");
		}
		// ============== Add 重载：从 NotificationData 转换 ==============

		/// <summary>
		/// 将 NotificationData 转换为 NotificationStoreData 并添加到历史记录。
		/// 图标会以 PNG 格式保存到 basedir\Images 下，文件名为 "<md5>%<sha1>"。
		/// 若图标固化失败，IconFileName 为空字符串。
		/// </summary>
		/// <returns>转换后的 NotificationStoreData 对象。</returns>
		public NotificationStoreData Add (NotificationData data)
		{
			if (data == null) throw new ArgumentNullException (nameof (data));

			var storeData = new NotificationStoreData {
				TimeStampUtc = data.TimestampUtc,
				IconFileName = SaveIconToFile (data.HIcon),
				InfoTitle = data.InfoTitle,
				Info = data.Info,
				Timeout = (int)(data.Timeout ?? 0),
				Type = data.InfoType,
				CallbackMessage = data.CallbackMessage,
				IsRead = false,
			};

			// 调用已有的 Add(NotificationStoreData) 完成实际添加与事件触发
			Add (storeData);
			return storeData;
		}

		/// <summary>
		/// 将 HICON 固化为 PNG 文件，返回文件名（哈希）。失败返回空字符串。
		/// </summary>
		private string SaveIconToFile (IntPtr hIcon)
		{
			if (hIcon == IntPtr.Zero)
				return string.Empty;

			byte [] pngBytes;
			try
			{
				using (var icon = Icon.FromHandle (hIcon))
				using (var bitmap = icon.ToBitmap ())
				using (var ms = new MemoryStream ())
				{
					bitmap.Save (ms, ImageFormat.Png);
					pngBytes = ms.ToArray ();
				}
			}
			catch
			{
				return string.Empty;
			}

			if (pngBytes == null || pngBytes.Length == 0)
				return string.Empty;

			string imagesDir = Path.Combine (basedir, "Images");
			Directory.CreateDirectory (imagesDir);

			string hash = ComputeHash (pngBytes);
			string filePath = Path.Combine (imagesDir, hash);

			// 已存在相同图片则直接复用
			if (!File.Exists (filePath))
			{
				string tempPath = Path.Combine (imagesDir, Guid.NewGuid ().ToString ("N") + ".tmp");
				try
				{
					File.WriteAllBytes (tempPath, pngBytes);

					try
					{
						// 原子移动，避免并发写入冲突
						File.Move (tempPath, filePath);
					}
					catch (IOException)
					{
						if (File.Exists (filePath))
						{
							// 其他线程已写入，删除临时文件
							File.Delete (tempPath);
						}
						else
						{
							throw;
						}
					}
				}
				catch
				{
					// 写入失败，清理临时文件并返回空
					try { if (File.Exists (tempPath)) File.Delete (tempPath); } catch { }
					return string.Empty;
				}
			}

			return hash;
		}

		/// <summary>
		/// 计算数据的 MD5 和 SHA-1，返回 "<md5>%<sha1>" 格式的字符串（小写十六进制）。
		/// </summary>
		private static string ComputeHash (byte [] data)
		{
			using (var md5 = MD5.Create ())
			using (var sha1 = SHA1.Create ())
			{
				byte [] md5Hash = md5.ComputeHash (data);
				byte [] sha1Hash = sha1.ComputeHash (data);

				var sb = new StringBuilder (md5Hash.Length * 2 + 1 + sha1Hash.Length * 2);
				for (int i = 0; i < md5Hash.Length; i++)
					sb.Append (md5Hash [i].ToString ("x2"));
				sb.Append ('%');
				for (int i = 0; i < sha1Hash.Length; i++)
					sb.Append (sha1Hash [i].ToString ("x2"));
				return sb.ToString ();
			}
		}

		private GroupedNotificationHistory grouped;

		/// <summary>
		/// 按日期分组的视图。首次访问时创建，并与本历史保持同步。
		/// 需在 UI 线程访问。
		/// </summary>
		public GroupedNotificationHistory Grouped
		{
			get
			{
				lock (sync)
				{
					if (disposed)
						throw new ObjectDisposedException (nameof (NotificationHistory));

					if (grouped == null)
						grouped = new GroupedNotificationHistory (this);

					return grouped;
				}
			}
		}

		// ============== Add 重载：从 NotifyIconNotification2 转换 ==============

		/// <summary>
		/// 将 NotifyIconNotification2 转换为 NotificationStoreData 并添加到历史记录。
		/// 图标会以 PNG 格式保存到 basedir\Images 下，文件名为 "<md5>%<sha1>"。
		/// 若图标固化失败，IconFileName 为空字符串。
		/// </summary>
		/// <returns>转换后的 NotificationStoreData 对象。</returns>
		public NotificationStoreData Add (NotifyIconNotification2 notification)
		{
			if (notification == null) throw new ArgumentNullException (nameof (notification));

			var storeData = new NotificationStoreData {
				TimeStampUtc = DateTime.UtcNow,
				IconFileName = SaveIconImageToFile (notification.IconImage),
				InfoTitle = notification.Title,
				Info = notification.Content,
				Timeout = notification.Timeout,
				Type = ConvertToolTipIconToNotifyIconInfoType (notification.Icon),
				CallbackMessage = 0,
				IsRead = false,
			};

			Add (storeData);
			return storeData;
		}

		/// <summary>
		/// 将 WPF ImageSource 固化为 PNG 文件，返回文件名（哈希）。失败返回空字符串。
		/// </summary>
		private string SaveIconImageToFile (System.Windows.Media.ImageSource imageSource)
		{
			if (imageSource == null)
				return string.Empty;

			try
			{
				// ImageSource 不能直接交给 BitmapFrame.Create，先转成 BitmapSource
				var bitmapSource = ToBitmapSource (imageSource);
				if (bitmapSource == null)
					return string.Empty;

				var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder ();
				encoder.Frames.Add (System.Windows.Media.Imaging.BitmapFrame.Create (bitmapSource));

				byte [] pngBytes;
				using (var ms = new MemoryStream ())
				{
					encoder.Save (ms);
					pngBytes = ms.ToArray ();
				}

				if (pngBytes == null || pngBytes.Length == 0)
					return string.Empty;

				string imagesDir = Path.Combine (basedir, "Images");
				Directory.CreateDirectory (imagesDir);

				string hash = ComputeHash (pngBytes);
				string filePath = Path.Combine (imagesDir, hash);

				if (!File.Exists (filePath))
				{
					string tempPath = Path.Combine (imagesDir, Guid.NewGuid ().ToString ("N") + ".tmp");
					try
					{
						File.WriteAllBytes (tempPath, pngBytes);

						try
						{
							File.Move (tempPath, filePath);
						}
						catch (IOException)
						{
							if (File.Exists (filePath))
							{
								File.Delete (tempPath);
							}
							else
							{
								throw;
							}
						}
					}
					catch
					{
						try { if (File.Exists (tempPath)) File.Delete (tempPath); } catch { }
						return string.Empty;
					}
				}

				return hash;
			}
			catch
			{
				return string.Empty;
			}
		}

		/// <summary>
		/// 将任意 ImageSource 转换为 BitmapSource。
		/// 若本身已是 BitmapSource（含 BitmapImage），直接返回；
		/// 否则用 DrawingVisual + RenderTargetBitmap 渲染。
		/// </summary>
		private static System.Windows.Media.Imaging.BitmapSource ToBitmapSource (
			System.Windows.Media.ImageSource imageSource)
		{
			if (imageSource == null)
				return null;

			var bitmapSource = imageSource as System.Windows.Media.Imaging.BitmapSource;
			if (bitmapSource != null)
				return bitmapSource;

			// 非 BitmapSource（例如 DrawingImage），通过 DrawingVisual 渲染
			double width = imageSource.Width;
			double height = imageSource.Height;

			if (double.IsNaN (width) || width <= 0)
				width = 16;   // 回退尺寸
			if (double.IsNaN (height) || height <= 0)
				height = 16;

			int pixelWidth = (int)Math.Ceiling (width);
			int pixelHeight = (int)Math.Ceiling (height);

			var drawingVisual = new System.Windows.Media.DrawingVisual ();
			using (var dc = drawingVisual.RenderOpen ())
			{
				dc.DrawImage (imageSource, new System.Windows.Rect (0, 0, width, height));
			}

			var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap (
				pixelWidth, pixelHeight,
				96, 96,
				System.Windows.Media.PixelFormats.Pbgra32);
			rtb.Render (drawingVisual);

			return rtb;
		}

		/// <summary>
		/// 将 WinForms ToolTipIcon 映射为 NotifyIconInfoType。
		/// </summary>
		private static NotifyIconInfoType ConvertToolTipIconToNotifyIconInfoType (System.Windows.Forms.ToolTipIcon icon)
		{
			switch (icon)
			{
				case System.Windows.Forms.ToolTipIcon.Info:
					return NotifyIconInfoType.Info;
				case System.Windows.Forms.ToolTipIcon.Warning:
					return NotifyIconInfoType.Warning;
				case System.Windows.Forms.ToolTipIcon.Error:
					return NotifyIconInfoType.Error;
				default:
					return NotifyIconInfoType.None;
			}
		}
	}
	public struct NotificationDataPair
	{
		public NotificationData DataFromObject { get; set; }
		public NotificationStoreData DataFromStore { get; set; }
	}
}

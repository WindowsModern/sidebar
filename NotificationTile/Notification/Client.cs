using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace WindowsModern.NotificationHistoryTile
{
	public delegate void NotificationReceivedHandler (object sender, NotificationData notification);
	public delegate void NotificationPipeErrorHandler (object sender, NotificationPipeErrorEventArgs e);

	public class NotificationPipeClient: IDisposable
	{
		public const string DefaultPipeName = "SidebarNotifyIconPipe";

		private const int HeaderSize = 20;
		private const int MaxFrameSize = 1024 * 1024;
		private const int DefaultHistoryCapacity = 100;
		private const int StopTimeoutMs = 3000;
		private const int ReconnectDelayMs = 50;

		private static readonly DateTime UnixEpoch =
			new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		private readonly string _pipeName;
		private readonly object _stateLock = new object ();
		private readonly object _handlerLock = new object ();
		private readonly object _historyLock = new object ();
		private readonly HashSet<NotificationReceivedHandler> _handlers =
			new HashSet<NotificationReceivedHandler> ();
		private readonly HashSet<NotificationPipeErrorHandler> _errorHandlers =
			new HashSet<NotificationPipeErrorHandler> ();
		private readonly Queue<NotificationData> _history;

		private volatile bool _stopRequested;
		private Thread _serverThread;
		private volatile bool _started;
		private volatile bool _disposed;
		private volatile NamedPipeServerStream _activeServer;
		private NamedPipeServerStream _pendingServer;

		private int _historyCapacity;

		// ============================================================
		// 构造
		// ============================================================

		public NotificationPipeClient ()
			: this (DefaultPipeName, DefaultHistoryCapacity)
		{
		}

		public NotificationPipeClient (string pipeName)
			: this (pipeName, DefaultHistoryCapacity)
		{
		}

		public NotificationPipeClient (string pipeName, int historyCapacity)
		{
			if (string.IsNullOrEmpty (pipeName))
				throw new ArgumentNullException ("pipeName");
			if (historyCapacity < 0)
				historyCapacity = 0;

			_pipeName = pipeName;
			_historyCapacity = historyCapacity;
			_history = new Queue<NotificationData> (historyCapacity);
		}

		// ============================================================
		// 属性
		// ============================================================

		public string PipeName { get { return _pipeName; } }

		public bool IsRunning { get { return _started; } }

		public int HistoryCapacity
		{
			get { lock (_historyLock) { return _historyCapacity; } }
			set
			{
				if (value < 0) value = 0;
				lock (_historyLock)
				{
					_historyCapacity = value;
					TrimHistory ();
				}
			}
		}

		// ============================================================
		// 事件
		// ============================================================

		public event NotificationReceivedHandler NotificationReceived
		{
			add { if (value != null) lock (_handlerLock) { _handlers.Add (value); } }
			remove { if (value != null) lock (_handlerLock) { _handlers.Remove (value); } }
		}

		public event NotificationPipeErrorHandler PipeError
		{
			add { if (value != null) lock (_handlerLock) { _errorHandlers.Add (value); } }
			remove { if (value != null) lock (_handlerLock) { _errorHandlers.Remove (value); } }
		}

		// ============================================================
		// 历史记录
		// ============================================================

		public NotificationData [] GetHistory ()
		{
			lock (_historyLock) { return _history.ToArray (); }
		}

		public void ClearHistory ()
		{
			lock (_historyLock) { _history.Clear (); }
		}

		public void ReplayHistory ()
		{
			NotificationData [] snapshot;
			lock (_historyLock) { snapshot = _history.ToArray (); }
			for (int i = 0; i < snapshot.Length; i++)
				DispatchToHandlers (snapshot [i]);
		}

		// ============================================================
		// Start / Stop / Dispose
		// ============================================================

		public void Start ()
		{
			lock (_stateLock)
			{
				if (_disposed) throw new ObjectDisposedException (GetType ().Name);
				if (_started) return;

				// 同步创建初始管道实例，消除启动窗口
				NamedPipeServerStream initialServer;
				try
				{
					initialServer = CreateServer ();
				}
				catch (Exception ex)
				{
					RaiseError ("Start", ex);
					throw;
				}

				_pendingServer = initialServer;
				_stopRequested = false;
				_started = true;

				_serverThread = new Thread (ServerLoop);
				_serverThread.IsBackground = true;
				_serverThread.Name = "NotificationPipeClient";
				_serverThread.Start ();
			}
		}

		public void Stop ()
		{
			Thread thread;
			lock (_stateLock)
			{
				if (!_started) return;
				_started = false;
				_stopRequested = true;
				thread = _serverThread;
				_serverThread = null;
			}

			// 释放可能还没被线程接管的初始管道
			var pending = Interlocked.Exchange (ref _pendingServer, null);
			if (pending != null)
			{
				try { pending.Dispose (); } catch { }
			}

			// 中断当前正在等待/读取的服务器
			InterruptActiveServer ();

			if (thread != null && thread.IsAlive)
			{
				try { thread.Join (StopTimeoutMs); } catch { }
			}
		}

		public void Dispose ()
		{
			if (_disposed) return;
			_disposed = true;

			Stop ();

			lock (_handlerLock)
			{
				_handlers.Clear ();
				_errorHandlers.Clear ();
			}
			lock (_historyLock)
			{
				_history.Clear ();
			}
		}

		// ============================================================
		// 服务器循环：持久管道实例 + 内层连接循环
		// ============================================================

		private void ServerLoop ()
		{
			// 拿到 Start() 预先创建好的管道（如果没有，则自己创建）
			NamedPipeServerStream server = Interlocked.Exchange (ref _pendingServer, null);

			while (!_stopRequested)
			{
				// ---------- 确保有一个可用的管道实例 ----------
				if (server == null)
				{
					try
					{
						server = CreateServer ();
					}
					catch (Exception ex)
					{
						RaiseError ("CreateServer", ex);
						if (!SafeDelay (ReconnectDelayMs)) break;
						continue;
					}
				}

				bool recreateServer = false;
				_activeServer = server;

				try
				{
					// ---------- 内层：同一个管道实例上循环处理连接 ----------
					while (!_stopRequested)
					{
						if (!WaitForConnectionInterruptible (server))
							break;              // 停止或管道失效
						if (_stopRequested) break;

						try
						{
							ReadOneFrame (server);
						}
						catch (EndOfStreamException)
						{
							// 对端在发送中途关闭连接；这一帧不完整，丢弃。
							// 管道实例本身仍然可用，继续等待下一个连接。
						}
						catch (Exception ex)
						{
							// 连接层错误，可能管道已损坏，跳出内层重建
							RaiseError ("ReadFrame", ex);
							recreateServer = true;
							break;
						}

						// 断开当前连接，为下一个客户端做准备
						try { server.Disconnect (); } catch { }
					}
				}
				finally
				{
					_activeServer = null;
					try { server.Dispose (); } catch { }
					server = null;
				}

				if (recreateServer)
					continue;               // 立即重建，不额外延迟

				if (!SafeDelay (ReconnectDelayMs)) break;
			}
		}

		private NamedPipeServerStream CreateServer ()
		{
			return new NamedPipeServerStream (
				_pipeName,
				PipeDirection.In,
				1,
				PipeTransmissionMode.Byte,
				PipeOptions.Asynchronous);
		}

		/// <summary>
		/// 可中断的 WaitForConnection。每 100ms 检查一次停止标志。
		/// </summary>
		private bool WaitForConnectionInterruptible (NamedPipeServerStream server)
		{
			IAsyncResult ar;
			try
			{
				ar = server.BeginWaitForConnection (null, null);
			}
			catch (ObjectDisposedException) { return false; }
			catch (InvalidOperationException) { return false; }

			try
			{
				while (!ar.AsyncWaitHandle.WaitOne (100))
				{
					if (_stopRequested) return false;
				}
				if (_stopRequested) return false;

				server.EndWaitForConnection (ar);
				return true;
			}
			catch (ObjectDisposedException) { return false; }
			catch (InvalidOperationException) { return false; }
		}

		private void ReadOneFrame (NamedPipeServerStream server)
		{
			byte [] header = new byte [HeaderSize];
			int read = 0;
			while (read < HeaderSize)
			{
				int n = ReadInterruptible (server, header, read, HeaderSize - read);
				if (n <= 0)
					throw new EndOfStreamException ("Pipe closed while reading header");
				read += n;
			}

			int dwSize = BitConverter.ToInt32 (header, 8);
			if (dwSize < HeaderSize || dwSize > MaxFrameSize)
				throw new InvalidDataException ("Invalid dwSize: " + dwSize);

			byte [] frame = new byte [dwSize];
			Buffer.BlockCopy (header, 0, frame, 0, HeaderSize);

			int total = HeaderSize;
			while (total < dwSize)
			{
				int n = ReadInterruptible (server, frame, total, dwSize - total);
				if (n <= 0)
					throw new EndOfStreamException ("Pipe closed while reading body");
				total += n;
			}

			// 解析失败不会影响后续帧，只记录并跳过
			NotificationData nd;
			try
			{
				nd = ParseFrame (frame);
			}
			catch (Exception ex)
			{
				RaiseError ("ParseFrame", ex);
				return;
			}

			if (nd == null) return;

			AddToHistory (nd);
			DispatchToHandlers (nd);
		}

		private int ReadInterruptible (NamedPipeServerStream server, byte [] buffer, int offset, int count)
		{
			IAsyncResult ar;
			try
			{
				ar = server.BeginRead (buffer, offset, count, null, null);
			}
			catch (ObjectDisposedException) { return 0; }
			catch (InvalidOperationException) { return 0; }

			try
			{
				while (!ar.AsyncWaitHandle.WaitOne (100))
				{
					if (_stopRequested) return 0;
				}
				if (_stopRequested) return 0;

				return server.EndRead (ar);
			}
			catch (ObjectDisposedException) { return 0; }
			catch (InvalidOperationException) { return 0; }
		}

		private void InterruptActiveServer ()
		{
			var s = _activeServer;
			if (s != null)
			{
				try { s.Dispose (); } catch { }
			}
		}

		// ============================================================
		// 帧解析
		// ============================================================

		private static NotificationData ParseFrame (byte [] data)
		{
			if (data == null || data.Length < HeaderSize)
				throw new InvalidDataException ("Frame too small");

			long ts = BitConverter.ToInt64 (data, 0);
			int dwSize = BitConverter.ToInt32 (data, 8);
			byte bHWndSizeOf = data [12];
			byte bUIntSizeOf = data [13];
			byte bHIconSizeOf = data [14];
			uint dwMessage = BitConverter.ToUInt32 (data, 16);

			if (dwSize != data.Length)
				throw new InvalidDataException ("Size mismatch: header=" + dwSize + ", actual=" + data.Length);

			int offset = HeaderSize;

			var nd = new NotificationData {
				TimestampUtc = UnixTimeToUtc (ts),
				Message = (NotifyIconMessage)dwMessage,
				HWnd = ReadIntPtr (data, ref offset, bHWndSizeOf),
				ID = ReadUInt32 (data, ref offset),
				Flags = (NotifyIconFlags)ReadUInt32 (data, ref offset),
				CallbackMessage = ReadUInt32 (data, ref offset),
				HIcon = ReadIntPtr (data, ref offset, bHIconSizeOf),
				State = (NotifyIconState)ReadUInt32 (data, ref offset),
				StateMask = (NotifyIconState)ReadUInt32 (data, ref offset),
				Timeout = ReadUInt32 (data, ref offset),
				Version = (NotifyIconVersion)ReadUInt32 (data, ref offset),
				InfoFlags = ReadUInt32 (data, ref offset),
				Item = ReadGuid (data, ref offset),
			};

			nd.Tip = ReadStringW (data, ref offset);
			nd.Info = ReadStringW (data, ref offset);
			nd.InfoTitle = ReadStringW (data, ref offset);

			return nd;
		}

		private static DateTime UnixTimeToUtc (long seconds)
		{
			if (seconds < 0 || seconds > 253402300799L) return UnixEpoch;
			try { return UnixEpoch.AddSeconds (seconds); }
			catch { return UnixEpoch; }
		}

		private static IntPtr ReadIntPtr (byte [] data, ref int offset, int size)
		{
			if (size == 8)
			{
				EnsureRange (data, offset, 8);
				long v = BitConverter.ToInt64 (data, offset);
				offset += 8;
				return new IntPtr (v);
			}
			if (size == 4)
			{
				EnsureRange (data, offset, 4);
				uint v = BitConverter.ToUInt32 (data, offset);
				offset += 4;
				return new IntPtr ((long)v);
			}
			throw new NotSupportedException ("Unsupported pointer size: " + size);
		}

		private static uint ReadUInt32 (byte [] data, ref int offset)
		{
			EnsureRange (data, offset, 4);
			uint v = BitConverter.ToUInt32 (data, offset);
			offset += 4;
			return v;
		}

		private static Guid ReadGuid (byte [] data, ref int offset)
		{
			EnsureRange (data, offset, 16);
			byte [] buf = new byte [16];
			Buffer.BlockCopy (data, offset, buf, 0, 16);
			offset += 16;
			return new Guid (buf);
		}

		private static string ReadStringW (byte [] data, ref int offset)
		{
			if (offset < 0 || offset + 1 >= data.Length)
			{
				offset = data.Length;
				return string.Empty;
			}

			int start = offset;
			while (offset + 1 < data.Length)
			{
				if (data [offset] == 0 && data [offset + 1] == 0)
					break;
				offset += 2;
			}

			int charCount = (offset - start) / 2;
			string s = charCount > 0
				? Encoding.Unicode.GetString (data, start, charCount * 2)
				: string.Empty;

			if (offset + 1 < data.Length)
				offset += 2;

			return s;
		}

		private static void EnsureRange (byte [] data, int offset, int size)
		{
			if (offset < 0 || offset + size > data.Length)
				throw new InvalidDataException (
					"Read out of range: offset=" + offset + ", size=" + size + ", total=" + data.Length);
		}

		// ============================================================
		// 分派 / 历史
		// ============================================================

		private void AddToHistory (NotificationData nd)
		{
			lock (_historyLock)
			{
				if (_historyCapacity <= 0) return;
				while (_history.Count >= _historyCapacity)
					_history.Dequeue ();
				_history.Enqueue (nd);
			}
		}

		private void TrimHistory ()
		{
			while (_history.Count > _historyCapacity)
				_history.Dequeue ();
		}

		private void DispatchToHandlers (NotificationData nd)
		{
			NotificationReceivedHandler [] snapshot;
			lock (_handlerLock)
			{
				if (_handlers.Count == 0) return;
				snapshot = new NotificationReceivedHandler [_handlers.Count];
				_handlers.CopyTo (snapshot);
			}

			for (int i = 0; i < snapshot.Length; i++)
			{
				try { snapshot [i] (this, nd); }
				catch { }
			}
		}

		private void RaiseError (string context, Exception ex)
		{
			NotificationPipeErrorHandler [] snapshot;
			lock (_handlerLock)
			{
				if (_errorHandlers.Count == 0) return;
				snapshot = new NotificationPipeErrorHandler [_errorHandlers.Count];
				_errorHandlers.CopyTo (snapshot);
			}

			var args = new NotificationPipeErrorEventArgs (context, ex);
			for (int i = 0; i < snapshot.Length; i++)
			{
				try { snapshot [i] (this, args); }
				catch { }
			}
		}

		// ============================================================
		// 工具
		// ============================================================

		private bool SafeDelay (int ms)
		{
			const int step = 50;
			int waited = 0;
			while (waited < ms && !_stopRequested)
			{
				Thread.Sleep (step);
				waited += step;
			}
			return !_stopRequested;
		}
	}

	public class NotificationPipeErrorEventArgs: EventArgs
	{
		public NotificationPipeErrorEventArgs (string context, Exception exception)
		{
			Context = context;
			Exception = exception;
		}

		public string Context { get; private set; }
		public Exception Exception { get; private set; }

		public override string ToString ()
		{
			return "[" + Context + "] " + (Exception != null ? Exception.Message : "(no exception)");
		}
	}
}
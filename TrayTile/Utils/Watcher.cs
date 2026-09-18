using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static WindowsModern.TrayTile.Utils.TrayIconWatcher;

namespace WindowsModern.TrayTile.Utils
{
	// ITrayIcon interface is assumed to be defined elsewhere in your project.
	// This file contains only the implementation and the watcher.

	internal class TrayIconImpl: ITrayIcon
	{
		private ImageSource _icon;
		private string _tooltip;
		private string _className;
		private long _key;
		private byte _type;
		private ulong _tag;
		private object _data;

		internal IntPtr Hwnd;
		internal uint Uid;
		internal uint CallbackMessage;

		public event PropertyChangedEventHandler PropertyChanged;

		public long Key
		{
			get { return _key; }
		}

		public string ToolTip
		{
			get { return _tooltip; }
		}

		public string ClassName
		{
			get { return _className; }
		}

		public ImageSource Icon
		{
			get { return _icon; }
		}

		public ulong Tag
		{
			get { return _tag; }
			set
			{
				if (_tag != value)
				{
					_tag = value;
					OnPropertyChanged ("Tag");
				}
			}
		}

		public object Data
		{
			get { return _data; }
		}

		public byte Type
		{
			get { return _type; }
		}
		private bool IsTraditional => CallbackMessage != 0;
		public TrayIconImpl (long key, IntPtr hwnd, uint uid, uint callbackMsg,
							ImageSource icon, string tooltip, string className,
							object data, byte type)
		{
			_key = key;
			Hwnd = hwnd;
			Uid = uid;
			CallbackMessage = callbackMsg;
			_icon = icon;
			_tooltip = tooltip ?? "";
			_className = className ?? "";
			_data = data;
			_type = type;
		}

		internal void Update (ImageSource icon, string tooltip, string className)
		{
			bool changed = false;
			if (_icon != icon) { _icon = icon; changed = true; }
			if (_tooltip != tooltip) { _tooltip = tooltip ?? ""; changed = true; }
			if (_className != className) { _className = className ?? ""; changed = true; }
			if (changed)
			{
				OnPropertyChanged ("Icon");
				OnPropertyChanged ("ToolTip");
				OnPropertyChanged ("ClassName");
			}
		}

		private void SendMouseMessage (int msg)
		{
			if (Hwnd == IntPtr.Zero) return;

			if (IsTraditional)
			{
				// 传统：发给 owner 窗口，wParam = uID
				NativeMethods.PostMessage (Hwnd, CallbackMessage, (int)Uid, msg);
			}
			else
			{
				// 新式：直接发给按钮窗口自身
				NativeMethods.PostMessage (Hwnd, (uint)msg, IntPtr.Zero, IntPtr.Zero);
			}
		}
		private const int NIN_POPUPOPEN = 0x0406;  // WM_USER + 6
		private const int NIN_POPUPCLOSE = 0x0407;  // WM_USER + 7
		private const uint WM_MOUSEMOVE = 0x0200;
		private const uint WM_MOUSELEAVE = 0x02A3;

		public void OnHover ()
		{
			return;
			if (Hwnd == IntPtr.Zero) return;

			if (IsTraditional)
			{
				// 传统托盘图标：按 NOTIFYICON_VERSION_4 发 NIN_POPUPOPEN
				//   lParam = MAKELONG(uID, NIN_POPUPOPEN)
				//   wParam = MAKELONG(x, y)，owner 一般不读，填 0
				IntPtr lParam = (IntPtr)((NIN_POPUPOPEN << 16) | ((int)Uid & 0xFFFF));
				NativeMethods.SendMessage (Hwnd, CallbackMessage, IntPtr.Zero, lParam);
			}
			else
			{
				// 新式按钮：模拟鼠标进入客户区
				NativeMethods.SendMessage (Hwnd, WM_MOUSEMOVE, IntPtr.Zero, IntPtr.Zero);
			}
		}

		public void OnLeave ()
		{
			return;
			if (Hwnd == IntPtr.Zero) return;

			if (IsTraditional)
			{
				IntPtr lParam = (IntPtr)((NIN_POPUPCLOSE << 16) | ((int)Uid & 0xFFFF));
				NativeMethods.SendMessage (Hwnd, CallbackMessage, IntPtr.Zero, lParam);
			}
			else
			{
				NativeMethods.SendMessage (Hwnd, WM_MOUSELEAVE, IntPtr.Zero, IntPtr.Zero);
			}
		}

		public void OnClick ()
		{
			SendMouseMessage (0x0201); // WM_LBUTTONDOWN
			SendMouseMessage (0x0202); // WM_LBUTTONUP
		}

		public void OnRightClick ()
		{
			SendMouseMessage (0x0204); // WM_RBUTTONDOWN
			SendMouseMessage (0x0205); // WM_RBUTTONUP
		}

		public void OnDoubleClick ()
		{
			SendMouseMessage (0x0203); // WM_LBUTTONDBLCLK
		}

		protected void OnPropertyChanged (string name)
		{
			var handler = PropertyChanged;
			if (handler != null)
				handler (this, new PropertyChangedEventArgs (name));
		}
	}

	public class TrayIconWatcher: IDisposable
	{
		// Type constants
		private const byte TYPE_TRAY_MAIN = 0;
		private const byte TYPE_TRAY_OVERFLOW = 1;
		private const byte TYPE_PEN = 2;
		private const byte TYPE_TOUCH_KEYBOARD = 3;
		private const byte TYPE_ACTION_CENTER = 4;
		private const byte TYPE_INPUT_INDICATOR = 5;

		private readonly ObservableCollection<ITrayIcon> _icons = new ObservableCollection<ITrayIcon> ();
		private readonly Dispatcher _dispatcher;
		private Timer _timer;
		private readonly object _lock = new object ();
		private bool _isRunning;

		public ObservableCollection<ITrayIcon> Icons
		{
			get { return _icons; }
		}

		public TrayIconWatcher ()
		{
			_dispatcher = Dispatcher.CurrentDispatcher;
			Start ();
		}

		public void Start ()
		{
			lock (_lock)
			{
				if (_isRunning) return;
				_isRunning = true;
				_timer = new Timer (OnTimerElapsed, null, 0, 1500);
			}
		}

		public void Stop ()
		{
			lock (_lock)
			{
				_isRunning = false;
				if (_timer != null)
				{
					_timer.Dispose ();
					_timer = null;
				}
			}
		}

		public void Dispose ()
		{
			Stop ();
		}

		private void OnTimerElapsed (object state)
		{
			try
			{
				var currentIcons = GetAllTrayIcons ();
				UpdateCollection (currentIcons);
			}
			catch
			{
				// Silently ignore errors to keep timer running
			}
		}

		private void UpdateCollection (List<TrayIconImpl> currentIcons)
		{
			if (_dispatcher != null && !_dispatcher.CheckAccess ())
			{
				_dispatcher.BeginInvoke ((Action)(() => UpdateCollection (currentIcons)));
				return;
			}

			var existing = new Dictionary<long, TrayIconImpl> ();
			foreach (ITrayIcon item in _icons)
			{
				var impl = item as TrayIconImpl;
				if (impl != null)
					existing [impl.Key] = impl;
			}

			var currentKeys = new HashSet<long> ();
			foreach (var ci in currentIcons)
				currentKeys.Add (ci.Key);

			for (int i = _icons.Count - 1; i >= 0; i--)
			{
				if (!currentKeys.Contains (_icons [i].Key))
					_icons.RemoveAt (i);
			}

			foreach (var ci in currentIcons)
			{
				TrayIconImpl old;
				if (existing.TryGetValue (ci.Key, out old))
				{
					old.Update (ci.Icon, ci.ToolTip, ci.ClassName);
				}
				else
				{
					_icons.Add (ci);
				}
			}
		}

		private static List<TrayIconImpl> GetAllTrayIcons ()
		{
			var list = new List<TrayIconImpl> ();

			// 1) All main tray toolbars (XP - Win11)
			var mainToolbars = FindAllMainTrayToolbars ();
			foreach (var hTb in mainToolbars)
				EnumerateTrayIcons (hTb, TYPE_TRAY_MAIN, list);

			// 2) Overflow area (Vista+)
			IntPtr hOverflow = NativeMethods.FindWindow ("NotifyIconOverflowWindow", null);
			if (hOverflow != IntPtr.Zero)
			{
				IntPtr hToolbar = NativeMethods.FindWindowEx (hOverflow, IntPtr.Zero, "ToolbarWindow32", null);
				if (hToolbar != IntPtr.Zero && !mainToolbars.Contains (hToolbar))
					EnumerateTrayIcons (hToolbar, TYPE_TRAY_OVERFLOW, list);
			}

			// 3) Pen Workspace button (Win8+)
			IntPtr hPen = FindWindowInTrayNotify ("PenWorkspaceButton");
			if (hPen != IntPtr.Zero)
				TryAddButton (hPen, TYPE_PEN, list);

			// 4) Touch Keyboard button (Win8+)
			IntPtr hTouchKb = FindWindowInTrayNotify ("TIPBand");
			if (hTouchKb != IntPtr.Zero)
				TryAddButton (hTouchKb, TYPE_TOUCH_KEYBOARD, list);

			// 5) Action Center button (Win8+)
			IntPtr hAction = FindWindowInTrayNotify ("TrayButton");
			if (hAction != IntPtr.Zero)
				TryAddButton (hAction, TYPE_ACTION_CENTER, list);

			// 6) Input indicator buttons (IME mode, etc.) (Win8+)
			IntPtr hInputIndicator = FindWindowInTrayNotify ("TrayInputIndicatorWClass");
			if (hInputIndicator != IntPtr.Zero)
				EnumerateInputIndicatorButtons (hInputIndicator, list);

			return list;
		}

		private static IntPtr FindWindowInTrayNotify (string className)
		{
			IntPtr hTrayNotify = GetTrayNotifyWindow ();
			if (hTrayNotify == IntPtr.Zero) return IntPtr.Zero;
			return NativeMethods.FindWindowEx (hTrayNotify, IntPtr.Zero, className, null);
		}

		private static void TryAddButton (IntPtr hwnd, byte type, List<TrayIconImpl> list)
		{
			string className = GetWindowClassName (hwnd);
			string tooltip = GetWindowText (hwnd);
			ImageSource icon = GetWindowIcon (hwnd);
			//if (icon == null) return;

			// Generate unique key with high bit set to avoid collision with tray icons
			long key = (long)((ulong)hwnd | (ulong)0x8000000000000000);
			var impl = new TrayIconImpl (key, hwnd, 0, 0, icon, tooltip, className, null, type);
			list.Add (impl);
		}

		private static void EnumerateInputIndicatorButtons (IntPtr container, List<TrayIconImpl> list)
		{
			if (container == IntPtr.Zero) return;
			IntPtr hChild = IntPtr.Zero;
			Sidebar.HWND hContainer = container;
			if (!hContainer.Visibility) return;
			if (hContainer.ClientWidth <= 0) return;
			while (true)
			{
				hChild = NativeMethods.FindWindowEx (container, hChild, null, null);
				if (hChild == IntPtr.Zero) break;

				string cls = GetWindowClassName (hChild);
				// Known button classes inside the input indicator
				if (cls == "IMEModeButton" || cls == "InputIndicatorButton" ||
					cls == "TouchKeyboardButton" || cls == "SystemLanguageBarButton" ||
					cls == "Button")
				{
					Sidebar.HWND hWnd = hChild;
					if (hWnd.Visibility)
						TryAddButton (hChild, TYPE_INPUT_INDICATOR, list);
				}
			}
		}

		private static List<IntPtr> FindAllMainTrayToolbars ()
		{
			var list = new List<IntPtr> ();
			IntPtr hTrayNotify = GetTrayNotifyWindow ();
			if (hTrayNotify == IntPtr.Zero) return list;

			// XP path
			IntPtr hChild = IntPtr.Zero;
			while (true)
			{
				hChild = NativeMethods.FindWindowEx (hTrayNotify, hChild, "ToolbarWindow32", null);
				if (hChild == IntPtr.Zero) break;
				if (!list.Contains (hChild))
					list.Add (hChild);
			}

			// Vista+ path (SysPager)
			IntPtr hSysPager = NativeMethods.FindWindowEx (hTrayNotify, IntPtr.Zero, "SysPager", null);
			if (hSysPager != IntPtr.Zero)
			{
				hChild = IntPtr.Zero;
				while (true)
				{
					hChild = NativeMethods.FindWindowEx (hSysPager, hChild, "ToolbarWindow32", null);
					if (hChild == IntPtr.Zero) break;
					if (!list.Contains (hChild))
						list.Add (hChild);
				}
			}

			return list;
		}

		private static IntPtr GetTrayNotifyWindow ()
		{
			IntPtr hTray = NativeMethods.FindWindow ("Shell_TrayWnd", null);
			if (hTray == IntPtr.Zero) return IntPtr.Zero;
			return NativeMethods.FindWindowEx (hTray, IntPtr.Zero, "TrayNotifyWnd", null);
		}

		private static string GetWindowClassName (IntPtr hwnd)
		{
			var sb = new StringBuilder (256);
			NativeMethods.GetClassName (hwnd, sb, sb.Capacity);
			return sb.ToString ();
		}

		private static string GetWindowText (IntPtr hwnd)
		{
			var sb = new StringBuilder (256);
			NativeMethods.GetWindowText (hwnd, sb, sb.Capacity);
			return sb.ToString ();
		}

		private static ImageSource GetWindowIcon (IntPtr hwnd)
		{
			if (hwnd == IntPtr.Zero) return null;

			// 1. 尝试获取标准 HICON
			IntPtr hIcon = GetIconFromWindow (hwnd);
			if (hIcon != IntPtr.Zero)
			{
				IntPtr localIcon = NativeMethods.CopyIcon (hIcon);
				if (localIcon != IntPtr.Zero)
				{
					try
					{
						var src = Imaging.CreateBitmapSourceFromHIcon (
							localIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions ());
						if (src.CanFreeze) src.Freeze ();
						return src;
					}
					finally { NativeMethods.DestroyIcon (localIcon); }
				}
			}

			// 2. 尝试通过 BM_GETIMAGE 获取位图或图标（适用于 Button 类窗口）
			ImageSource bmpImage = GetButtonImage (hwnd);
			if (bmpImage != null)
				return EnsureTransparent (bmpImage);

			// 3. 窗口截图（处理自绘按钮）
			ImageSource printImage = CaptureWindow (hwnd);
			if (printImage != null)
			{
				var ret = EnsureTransparent (printImage);
				if (!IsSolidColorImage (ret)) return ret;
				else return null;
			}
			// 4. 进程图标后备（排除 explorer.exe）
			return GetProcessIconSafely (hwnd);
		}

		// 获取标准图标
		private static IntPtr GetIconFromWindow (IntPtr hwnd)
		{
			IntPtr hIcon = NativeMethods.SendMessage (hwnd, WM_GETICON, (IntPtr)ICON_SMALL, IntPtr.Zero);
			if (hIcon == IntPtr.Zero)
				hIcon = NativeMethods.SendMessage (hwnd, WM_GETICON, (IntPtr)ICON_BIG, IntPtr.Zero);
			if (hIcon == IntPtr.Zero)
				hIcon = NativeMethods.GetClassLongPtr (hwnd, GCL_HICONSM);
			return hIcon;
		}

		// 通过 BM_GETIMAGE 获取按钮图像
		private static ImageSource GetButtonImage (IntPtr hwnd)
		{
			// 发送 BM_GETIMAGE 请求图标
			IntPtr result = NativeMethods.SendMessage (hwnd, NativeMethods.BM_GETIMAGE, (IntPtr)NativeMethods.IMAGE_ICON, IntPtr.Zero);
			if (result != IntPtr.Zero)
			{
				IntPtr localIcon = NativeMethods.CopyIcon (result);
				if (localIcon != IntPtr.Zero)
				{
					try
					{
						var src = Imaging.CreateBitmapSourceFromHIcon (
							localIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions ());
						if (src.CanFreeze) src.Freeze ();
						return src;
					}
					finally { NativeMethods.DestroyIcon (localIcon); }
				}
			}

			// 再尝试请求位图
			result = NativeMethods.SendMessage (hwnd, NativeMethods.BM_GETIMAGE, (IntPtr)NativeMethods.IMAGE_BITMAP, IntPtr.Zero);
			if (result != IntPtr.Zero)
			{
				try
				{
					// result 是位图句柄，需要转换为 BitmapSource
					System.Drawing.Bitmap bmp = System.Drawing.Image.FromHbitmap (result);
					var src = Imaging.CreateBitmapSourceFromHBitmap (
						bmp.GetHbitmap (), IntPtr.Zero, Int32Rect.Empty,
						BitmapSizeOptions.FromEmptyOptions ());
					if (src.CanFreeze) src.Freeze ();
					return src;
				}
				catch { }
			}
			return null;
		}

		// 窗口截图
		private static ImageSource CaptureWindow (IntPtr hwnd)
		{
			// 获取窗口尺寸
			NativeMethods.RECT rect;
			if (!NativeMethods.GetWindowRect (hwnd, out rect))
				return null;
			int width = rect.right - rect.left;
			int height = rect.bottom - rect.top;
			if (width <= 0 || height <= 0)
				return null;

			// 创建内存 DC 和位图
			IntPtr hdcSrc = NativeMethods.GetWindowDC (hwnd);
			if (hdcSrc == IntPtr.Zero) return null;
			IntPtr hdcDest = NativeMethods.CreateCompatibleDC (hdcSrc);
			IntPtr hBitmap = NativeMethods.CreateCompatibleBitmap (hdcSrc, width, height);
			IntPtr oldBitmap = NativeMethods.SelectObject (hdcDest, hBitmap);

			// 尝试使用 PrintWindow（成功率高且能捕获部分离屏内容）
			bool success = NativeMethods.PrintWindow (hwnd, hdcDest, NativeMethods.PW_CLIENTONLY);
			if (!success)
			{
				// 回退到 BitBlt
				success = NativeMethods.BitBlt (hdcDest, 0, 0, width, height, hdcSrc, 0, 0, NativeMethods.SRCCOPY);
			}

			ImageSource img = null;
			if (success)
			{
				try
				{
					System.Drawing.Bitmap bmp = System.Drawing.Image.FromHbitmap (hBitmap);
					img = Imaging.CreateBitmapSourceFromHBitmap (
						bmp.GetHbitmap (), IntPtr.Zero, Int32Rect.Empty,
						BitmapSizeOptions.FromEmptyOptions ());
					if (img.CanFreeze) img.Freeze ();
				}
				catch { }
			}

			// 清理
			NativeMethods.SelectObject (hdcDest, oldBitmap);
			NativeMethods.DeleteObject (hBitmap);
			NativeMethods.DeleteDC (hdcDest);
			NativeMethods.ReleaseDC (hwnd, hdcSrc);

			return img;
		}
		/// <summary>
		/// 判断图片是否为纯色（忽略完全透明的像素）
		/// </summary>
		private static bool IsSolidColorImage (ImageSource image)
		{
			BitmapSource bitmap = image as BitmapSource;
			if (bitmap == null) return false;

			int width = bitmap.PixelWidth;
			int height = bitmap.PixelHeight;
			if (width <= 0 || height <= 0) return true; // 空图像视为纯色，应移除

			// 统一转为 32 位 BGRA 以方便逐像素分析
			FormatConvertedBitmap rgba32 = new FormatConvertedBitmap (bitmap, PixelFormats.Bgra32, null, 0);
			int stride = width * 4;
			byte [] pixels = new byte [height * stride];
			rgba32.CopyPixels (pixels, stride, 0);

			bool first = true;
			byte r0 = 0, g0 = 0, b0 = 0, a0 = 0;

			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					int idx = y * stride + x * 4;
					byte a = pixels [idx + 3];
					byte b = pixels [idx];
					byte g = pixels [idx + 1];
					byte r = pixels [idx + 2];

					if (first)
					{
						r0 = r; g0 = g; b0 = b; a0 = a;
						first = false;
					}
					else if (r != r0 || g != g0 || b != b0 || a != a0)
					{
						return false;               // 发现不同颜色，不是纯色
					}
				}
			}

			// 如果所有非透明像素颜色相同（或根本没有非透明像素）则视为纯色
			return true;
		}
		/// <summary>
		/// 判断像素格式是否包含 Alpha 通道（兼容 .NET Framework 4.x）
		/// </summary>
		private static bool PixelFormatHasAlpha (PixelFormat format)
		{
			// 带 Alpha 的格式通常提供 4 个通道掩码 (BGRA)
			return format.Masks.Count == 4;
		}
		/// <summary>
		/// 智能透明背景处理：
		/// - 如果图像已经存在大量透明/半透明像素，保留原样；
		/// - 如果背景为纯黑色不透明，则去除黑色并裁剪透明边缘。
		/// </summary>
		private static ImageSource EnsureTransparent (ImageSource source)
		{
			if (source == null) return null;

			BitmapSource bitmap = source as BitmapSource;
			if (bitmap == null) return source;

			// 统一转为 BGRA32 以便分析
			FormatConvertedBitmap rgba32 = new FormatConvertedBitmap (bitmap, PixelFormats.Bgra32, null, 0);
			try
			{
				var ret = SmartMakeTransparentAndCrop (rgba32);
				if (ret.CanFreeze) ret.Freeze ();
				return ret;
			}
			catch (Exception e)
			{
				if (source.CanFreeze) source.Freeze ();
				return source;
			}
		}

		private static BitmapSource SmartMakeTransparentAndCrop (BitmapSource source)
		{
			int width = source.PixelWidth;
			int height = source.PixelHeight;
			int stride = width * 4;
			byte [] pixels = new byte [height * stride];
			source.CopyPixels (pixels, stride, 0);

			// 统计：纯黑且不透明的像素数，以及半透明/全透明像素数
			int blackOpaqueCount = 0;
			int totalOpaqueCount = 0;
			int semiTransparentCount = 0;

			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					int idx = y * stride + x * 4;
					byte b = pixels [idx];
					byte g = pixels [idx + 1];
					byte r = pixels [idx + 2];
					byte a = pixels [idx + 3];

					if (a == 255)
					{
						totalOpaqueCount++;
						if (r < 16 && g < 16 && b < 16)
							blackOpaqueCount++;
					}
					else if (a > 0 && a < 255)
					{
						semiTransparentCount++;
					}
				}
			}

			int totalPixels = width * height;

			// 已经带透明通道
			if (semiTransparentCount > totalPixels * 0.05 ||
				(totalPixels - totalOpaqueCount) > totalPixels * 0.1)
			{
				return source;
			}

			// 黑色占比太低，不认为是黑底截图
			if (blackOpaqueCount < totalPixels * 0.1)
				return source;

			int minX = width;
			int minY = height;
			int maxX = 0;
			int maxY = 0;
			bool hasContent = false;

			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					int idx = y * stride + x * 4;
					byte b = pixels [idx];
					byte g = pixels [idx + 1];
					byte r = pixels [idx + 2];
					byte a = pixels [idx + 3];

					if (a == 255 &&
						r < 16 &&
						g < 16 &&
						b < 16)
					{
						pixels [idx + 3] = 0;
					}
					else if (pixels [idx + 3] > 0)
					{
						hasContent = true;

						if (x < minX) minX = x;
						if (y < minY) minY = y;
						if (x > maxX) maxX = x;
						if (y > maxY) maxY = y;
					}
				}
			}

			if (!hasContent)
				return source;

			int cropWidth = maxX - minX + 1;
			int cropHeight = maxY - minY + 1;

			if (cropWidth <= 0 || cropHeight <= 0)
				return source;

			// 先写入完整图像
			WriteableBitmap bitmap = new WriteableBitmap (
				width,
				height,
				source.DpiX,
				source.DpiY,
				PixelFormats.Bgra32,
				null);

			bitmap.WritePixels (
				new Int32Rect (0, 0, width, height),
				pixels,
				stride,
				0);

			// 再利用 CroppedBitmap 裁剪
			CroppedBitmap cropped = new CroppedBitmap (
				bitmap,
				new Int32Rect (minX, minY, cropWidth, cropHeight));

			if (cropped.CanFreeze)
				cropped.Freeze ();

			return cropped;
		}
		// 安全的进程图标后备
		private static ImageSource GetProcessIconSafely (IntPtr hwnd)
		{
			uint pid;
			NativeMethods.GetWindowThreadProcessId (hwnd, out pid);
			if (pid == 0) return null;

			try
			{
				using (var proc = Process.GetProcessById ((int)pid))
				{
					// 排除 explorer.exe，避免返回资源管理器图标
					if (proc.ProcessName.Equals ("explorer", StringComparison.OrdinalIgnoreCase))
						return null;

					using (var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon (proc.MainModule.FileName))
					{
						if (sysIcon != null)
						{
							var src = Imaging.CreateBitmapSourceFromHIcon (
								sysIcon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions ());
							if (src.CanFreeze) src.Freeze ();
							return src;
						}
					}
				}
			}
			catch { }
			return null;
		}

		private static ImageSource GetProcessIcon (IntPtr hwnd)
		{
			if (hwnd == IntPtr.Zero) return null;
			uint procId;
			NativeMethods.GetWindowThreadProcessId (hwnd, out procId);
			if (procId == 0) return null;

			try
			{
				using (var proc = Process.GetProcessById ((int)procId))
				{
					using (var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon (proc.MainModule.FileName))
					{
						if (sysIcon != null)
						{
							var src = Imaging.CreateBitmapSourceFromHIcon (
								sysIcon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions ());
							if (src.CanFreeze) src.Freeze ();
							return src;
						}
					}
				}
			}
			catch { }
			return null;
		}

		// Win32 message / class constants
		private const uint WM_GETICON = 0x007F;
		private const int ICON_SMALL = 0;
		private const int ICON_BIG = 1;
		private const int GCL_HICONSM = -34;

		private static unsafe void EnumerateTrayIcons (IntPtr hToolbar, byte type, List<TrayIconImpl> list)
		{
			uint pid;
			NativeMethods.GetWindowThreadProcessId (hToolbar, out pid);
			if (pid == 0) return;

			bool isTarget64 = IsProcess64Bit (pid);
			int tbbSize = isTarget64 ? 32 : 20;
			int trayDataSize = isTarget64 ? 32 : 24;

			IntPtr hProcess = NativeMethods.OpenProcess (
				NativeMethods.PROCESS_VM_OPERATION | NativeMethods.PROCESS_VM_READ |
				NativeMethods.PROCESS_VM_WRITE | NativeMethods.PROCESS_QUERY_INFORMATION,
				false, pid);
			if (hProcess == IntPtr.Zero) return;

			IntPtr remoteBuf = IntPtr.Zero;
			try
			{
				remoteBuf = NativeMethods.VirtualAllocEx (hProcess, IntPtr.Zero, (UIntPtr)4096,
														 NativeMethods.MEM_COMMIT, NativeMethods.PAGE_READWRITE);
				if (remoteBuf == IntPtr.Zero) return;

				int count = (int)NativeMethods.SendMessage (hToolbar, NativeMethods.TB_BUTTONCOUNT, IntPtr.Zero, IntPtr.Zero);
				if (count <= 0) return;

				var btnBuffer = stackalloc byte [tbbSize];
				var trayDataBuffer = stackalloc byte [trayDataSize];
				var tipBuffer = new byte [4096];

				for (int i = 0; i < count; i++)
				{
					NativeMethods.SendMessage (hToolbar, NativeMethods.TB_GETBUTTON, (IntPtr)i, remoteBuf);

					if (!NativeMethods.ReadProcessMemory (hProcess, remoteBuf, (IntPtr)btnBuffer,
														 (UIntPtr)tbbSize, IntPtr.Zero))
						continue;

					int cmdId = Marshal.ReadInt32 ((IntPtr)btnBuffer, 4);
					byte fsState = btnBuffer [8];
					if (fsState == 8) continue; // TBSTATE_HIDDEN

					IntPtr dwData;
					if (isTarget64)
						dwData = Marshal.ReadIntPtr ((IntPtr)btnBuffer, 16);
					else
						dwData = Marshal.ReadIntPtr ((IntPtr)btnBuffer, 12);

					if (dwData == IntPtr.Zero) continue;

					if (!NativeMethods.ReadProcessMemory (hProcess, dwData, (IntPtr)trayDataBuffer,
														 (UIntPtr)trayDataSize, IntPtr.Zero))
						continue;

					IntPtr hwnd, hIcon;
					uint uid, callbackMsg;
					if (isTarget64)
					{
						hwnd = Marshal.ReadIntPtr ((IntPtr)trayDataBuffer, 0);
						uid = (uint)Marshal.ReadInt32 ((IntPtr)trayDataBuffer, 8);
						callbackMsg = (uint)Marshal.ReadInt32 ((IntPtr)trayDataBuffer, 12);
						hIcon = Marshal.ReadIntPtr ((IntPtr)trayDataBuffer, 24);
					}
					else
					{
						hwnd = Marshal.ReadIntPtr ((IntPtr)trayDataBuffer, 0);
						uid = (uint)Marshal.ReadInt32 ((IntPtr)trayDataBuffer, 4);
						callbackMsg = (uint)Marshal.ReadInt32 ((IntPtr)trayDataBuffer, 8);
						hIcon = Marshal.ReadIntPtr ((IntPtr)trayDataBuffer, 20);
					}

					string tooltip = "";
					int textLen = (int)NativeMethods.SendMessage (hToolbar, NativeMethods.TB_GETBUTTONTEXT,
																 (IntPtr)cmdId, remoteBuf);
					if (textLen > 0 && textLen < 4096)
					{
						fixed (byte* pTip = tipBuffer)
						{
							if (NativeMethods.ReadProcessMemory (hProcess, remoteBuf, (IntPtr)pTip,
																(UIntPtr)(textLen * 2), IntPtr.Zero))
							{
								tooltip = Marshal.PtrToStringUni ((IntPtr)pTip, textLen);
							}
						}
					}

					string className = "";
					if (hwnd != IntPtr.Zero)
					{
						var sb = new StringBuilder (256);
						NativeMethods.GetClassName (hwnd, sb, sb.Capacity);
						className = sb.ToString ();
					}

					ImageSource iconSource = null;
					if (hIcon != IntPtr.Zero)
					{
						try
						{
							iconSource = Imaging.CreateBitmapSourceFromHIcon (hIcon, Int32Rect.Empty,
																			 BitmapSizeOptions.FromEmptyOptions ());
							if (iconSource.CanFreeze) iconSource.Freeze ();
						}
						catch
						{
							iconSource = GetProcessIcon (hwnd);
						}
					}
					if (iconSource == null) continue;

					long key = ((long)(uint)hwnd.ToInt32 () << 32) | uid;
					var impl = new TrayIconImpl (key, hwnd, uid, callbackMsg, iconSource,
												tooltip, className, null, type);
					list.Add (impl);
				}
			}
			finally
			{
				if (remoteBuf != IntPtr.Zero)
					NativeMethods.VirtualFreeEx (hProcess, remoteBuf, UIntPtr.Zero, NativeMethods.MEM_RELEASE);
				NativeMethods.CloseHandle (hProcess);
			}
		}

		private static bool IsProcess64Bit (uint pid)
		{
			IntPtr hProcess = NativeMethods.OpenProcess (NativeMethods.PROCESS_QUERY_INFORMATION, false, pid);
			if (hProcess == IntPtr.Zero)
				return (IntPtr.Size == 8);
			if (!Environment.Is64BitOperatingSystem) return false;
			try
			{
				bool isWow64;
				if (NativeMethods.IsWow64Process (hProcess, out isWow64))
					return !isWow64;
				return (IntPtr.Size == 8);
			}
			finally
			{
				NativeMethods.CloseHandle (hProcess);
			}
		}

		internal static class NativeMethods
		{
			public const uint PROCESS_VM_OPERATION = 0x0008;
			public const uint PROCESS_VM_READ = 0x0010;
			public const uint PROCESS_VM_WRITE = 0x0020;
			public const uint PROCESS_QUERY_INFORMATION = 0x0400;
			public const uint MEM_COMMIT = 0x1000;
			public const uint MEM_RELEASE = 0x8000;
			public const uint PAGE_READWRITE = 0x04;
			public const uint TB_BUTTONCOUNT = 0x418;
			public const uint TB_GETBUTTON = 0x417;
			public const uint TB_GETBUTTONTEXT = 0x44B;

			[DllImport ("user32.dll", SetLastError = true)]
			public static extern IntPtr FindWindow (string lpClassName, string lpWindowName);

			[DllImport ("user32.dll", SetLastError = true)]
			public static extern IntPtr FindWindowEx (IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

			[DllImport ("user32.dll", SetLastError = true)]
			public static extern uint GetWindowThreadProcessId (IntPtr hWnd, out uint lpdwProcessId);

			[DllImport ("kernel32.dll", SetLastError = true)]
			public static extern IntPtr OpenProcess (uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

			[DllImport ("kernel32.dll", SetLastError = true)]
			public static extern IntPtr VirtualAllocEx (IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint flAllocationType, uint flProtect);

			[DllImport ("kernel32.dll", SetLastError = true)]
			public static extern bool VirtualFreeEx (IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);

			[DllImport ("kernel32.dll", SetLastError = true)]
			public static extern bool ReadProcessMemory (IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, UIntPtr nSize, IntPtr lpNumberOfBytesRead);

			[DllImport ("user32.dll", CharSet = CharSet.Auto)]
			public static extern IntPtr SendMessage (IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

			[DllImport ("user32.dll", SetLastError = true)]
			public static extern bool PostMessage (IntPtr hWnd, uint Msg, int wParam, int lParam);

			[DllImport ("user32.dll", SetLastError = true)]
			public static extern bool PostMessage (IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

			[DllImport ("user32.dll")]
			public static extern IntPtr SetForegroundWindow (IntPtr hWnd);

			[DllImport ("user32.dll", CharSet = CharSet.Auto)]
			public static extern int GetClassName (IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

			[DllImport ("user32.dll", CharSet = CharSet.Auto)]
			public static extern int GetWindowText (IntPtr hWnd, StringBuilder lpString, int nMaxCount);

			[DllImport ("kernel32.dll", SetLastError = true)]
			public static extern bool IsWow64Process (IntPtr hProcess, out bool Wow64Process);

			[DllImport ("kernel32.dll", SetLastError = true)]
			public static extern bool CloseHandle (IntPtr hObject);

			[DllImport ("user32.dll", SetLastError = true)]
			public static extern IntPtr CopyIcon (IntPtr hIcon);

			[DllImport ("user32.dll", SetLastError = true)]
			public static extern bool DestroyIcon (IntPtr hIcon);

			// Safe GetClassLongPtr for both 32 and 64 bit
			public static IntPtr GetClassLongPtr (IntPtr hWnd, int nIndex)
			{
				if (IntPtr.Size == 8)
					return GetClassLongPtr64 (hWnd, nIndex);
				else
					return new IntPtr (GetClassLong32 (hWnd, nIndex));
			}

			[DllImport ("user32.dll", EntryPoint = "GetClassLongPtr", SetLastError = true)]
			private static extern IntPtr GetClassLongPtr64 (IntPtr hWnd, int nIndex);

			[DllImport ("user32.dll", EntryPoint = "GetClassLong", SetLastError = true)]
			private static extern int GetClassLong32 (IntPtr hWnd, int nIndex);
			// 在 NativeMethods 类中添加以下声明
			[DllImport ("user32.dll", SetLastError = true)]
			public static extern bool PrintWindow (IntPtr hwnd, IntPtr hDC, uint nFlags);

			[DllImport ("user32.dll", SetLastError = true)]
			public static extern IntPtr GetWindowDC (IntPtr hWnd);

			[DllImport ("user32.dll", SetLastError = true)]
			public static extern int ReleaseDC (IntPtr hWnd, IntPtr hDC);

			// BM_GETIMAGE constants
			public const uint BM_GETIMAGE = 0x00F6;
			public const int IMAGE_BITMAP = 0;
			public const int IMAGE_ICON = 1;

			// PrintWindow flags
			public const uint PW_CLIENTONLY = 0x00000001;
			// RECT 结构体
			[StructLayout (LayoutKind.Sequential)]
			public struct RECT
			{
				public int left, top, right, bottom;
			}

			[DllImport ("user32.dll", SetLastError = true)]
			public static extern bool GetWindowRect (IntPtr hWnd, out RECT lpRect);

			// GDI 操作
			[DllImport ("gdi32.dll", SetLastError = true)]
			public static extern IntPtr CreateCompatibleDC (IntPtr hdc);

			[DllImport ("gdi32.dll", SetLastError = true)]
			public static extern IntPtr CreateCompatibleBitmap (IntPtr hdc, int cx, int cy);

			[DllImport ("gdi32.dll", SetLastError = true)]
			public static extern IntPtr SelectObject (IntPtr hdc, IntPtr hObject);

			[DllImport ("gdi32.dll", SetLastError = true)]
			public static extern bool DeleteObject (IntPtr hObject);

			[DllImport ("gdi32.dll", SetLastError = true)]
			public static extern bool DeleteDC (IntPtr hdc);

			[DllImport ("gdi32.dll", SetLastError = true)]
			public static extern bool BitBlt (IntPtr hdcDest, int xDest, int yDest, int cx, int cy,
											 IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

			public const uint SRCCOPY = 0x00CC0020;

			// 已有 PrintWindow 声明（上面已添加）
		}
	}
}
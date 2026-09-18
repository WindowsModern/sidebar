using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsModern.NotificationHistoryTile
{
	public static class IconHelper
	{
		[DllImport ("user32.dll", SetLastError = true)]
		private static extern bool DestroyIcon (IntPtr hIcon);
		public static ImageSource FromHIcon (IntPtr hIcon)
		{
			if (hIcon == IntPtr.Zero) return null;

			try
			{
				// 关键一步：把 HICON 转成 BitmapSource
				BitmapSource bs = Imaging.CreateBitmapSourceFromHIcon (
					hIcon,
					Int32Rect.Empty,
					BitmapSizeOptions.FromEmptyOptions ());

				// 冻结，方便跨线程使用，也提升渲染性能
				bs.Freeze ();
				return bs;
			}
			catch { return null; }
			finally
			{
				// 如果这个 HICON 是你自己 new 出来的（比如 ExtractIcon 返回的），
				// 用完必须销毁，否则 GDI 句柄泄漏。
				// 如果 HICON 来自系统/别处、不属于你，就不要在这里销毁。
				//DestroyIcon (hIcon);
			}
		}
	}
	public static class Native
	{
		[DllImport ("user32.dll")]
		public static extern bool PostMessage (IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
	}
}

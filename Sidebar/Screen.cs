using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Forms;

namespace Sidebar
{
	internal struct ScreenInfo
	{
		public int ScreenWidth;
		public int ScreenHeight;
		public int DPI;
		public override bool Equals (object obj)
		{
			if (!(obj is ScreenInfo)) return false;
			var other = (ScreenInfo)obj;
			return ScreenWidth == other.ScreenWidth &&
				   ScreenHeight == other.ScreenHeight &&
				   DPI == other.DPI;
		}
		public override int GetHashCode ()
		{
			return ScreenWidth.GetHashCode () ^ ScreenHeight.GetHashCode () ^ DPI.GetHashCode ();
		}
		public override string ToString ()
		{
			return string.Format ("Screen({0}x{1}, DPI:{2})", ScreenWidth, ScreenHeight, DPI);
		}
	}
	internal struct ScreenChangeResult
	{
		public bool SizeChanged;
		public bool ScaleChanged;
	}
	public static class ScreenChangeNotifier
	{
		private const int WM_DISPLAYCHANGE = 0x007E;
		private const int WM_SETTINGCHANGE = 0x001A;
		private const int WM_DPICHANGED = 0x02E0;
		private static HwndSource _hwndSource;
		private static int _lastWorkingAreaHeight;
		private static int _lastWorkingAreaTop;
		private static System.Drawing.Rectangle _lastWorkingArea;
		public static event EventHandler SizeChanged;
		public static event EventHandler ScaleChanged;
		public static event EventHandler WorkingAreaHeightChanged;
		public static event EventHandler WorkingAreaChanged;
		private static Dictionary<int, ScreenInfo> _screenInfoCache = new Dictionary<int, ScreenInfo> ();
		public static void StartListening (Window window)
		{
			if (_hwndSource != null) return;
			InitializeScreenCache ();
			if (window.IsLoaded)
			{
				AttachHook (window);
			}
			else
			{
				window.Loaded += new RoutedEventHandler (delegate (object s, RoutedEventArgs e)
				{
					AttachHook (window);
				});
			}
		}
		public static void StopListening ()
		{
			if (_hwndSource != null)
			{
				_hwndSource.RemoveHook (WndProc);
				_hwndSource.Dispose ();
				_hwndSource = null;
			}
			_screenInfoCache.Clear ();
		}
		private static void AttachHook (Window window)
		{
			if (_hwndSource != null) return;
			var hwnd = new WindowInteropHelper (window).Handle;
			_hwndSource = HwndSource.FromHwnd (hwnd);
			_hwndSource.AddHook (WndProc);
			var currScreen = (ScreenHelper.GetScreenByHWND (hwnd) ?? Screen.PrimaryScreen);
			_lastWorkingArea = currScreen.WorkingArea;
			_lastWorkingAreaTop = currScreen.WorkingArea.Top;
			_lastWorkingAreaHeight = currScreen.WorkingArea.Height;
		}
		private static void CheckWorkingAreaChange ()
		{
			var currScreen = (ScreenHelper.GetScreenByHWND (_hwndSource.Handle) ?? Screen.PrimaryScreen);
			var currentRect = currScreen.WorkingArea;
			if (currentRect == _lastWorkingArea) return;
			_lastWorkingArea = currentRect;
			if (WorkingAreaChanged != null)
				WorkingAreaChanged (null, EventArgs.Empty);
			if (currentRect.Height != _lastWorkingAreaHeight || currentRect.Top != _lastWorkingAreaTop)
			{
				_lastWorkingAreaHeight = currentRect.Height;
				_lastWorkingAreaTop = currentRect.Top;
				if (WorkingAreaHeightChanged != null)
					WorkingAreaHeightChanged (null, EventArgs.Empty);
			}
		}
		private static void InitializeScreenCache ()
		{
			_screenInfoCache.Clear ();
			int screenIndex = 0;
			foreach (Screen screen in Screen.AllScreens)
			{
				_screenInfoCache [screenIndex] = GetScreenInfo (screen);
				screenIndex++;
			}
		}
		private static ScreenInfo GetScreenInfo (Screen screen)
		{
			int dpi = 96;
			try
			{
				System.Drawing.Graphics graphics = System.Drawing.Graphics.FromHwnd (IntPtr.Zero);
				dpi = (int)graphics.DpiX;
				graphics.Dispose ();
			}
			catch
			{
			}
			ScreenInfo info = new ScreenInfo ();
			info.ScreenWidth = screen.Bounds.Width;
			info.ScreenHeight = screen.Bounds.Height;
			info.DPI = dpi;
			return info;
		}
		private static ScreenChangeResult DetectScreenChanges ()
		{
			ScreenChangeResult result = new ScreenChangeResult ();
			result.SizeChanged = false;
			result.ScaleChanged = false;
			Dictionary<int, ScreenInfo> currentScreens = new Dictionary<int, ScreenInfo> ();
			int screenIndex = 0;
			foreach (Screen screen in Screen.AllScreens)
			{
				currentScreens [screenIndex] = GetScreenInfo (screen);
				screenIndex++;
			}
			if (currentScreens.Count != _screenInfoCache.Count)
			{
				result.SizeChanged = true;
				result.ScaleChanged = true;
			}
			else
			{
				for (int i = 0; i < currentScreens.Count; i++)
				{
					if (!_screenInfoCache.ContainsKey (i))
					{
						result.SizeChanged = true;
						break;
					}
					ScreenInfo oldInfo = _screenInfoCache [i];
					ScreenInfo newInfo = currentScreens [i];
					if (oldInfo.ScreenWidth != newInfo.ScreenWidth || oldInfo.ScreenHeight != newInfo.ScreenHeight)
					{
						result.SizeChanged = true;
					}
					if (oldInfo.DPI != newInfo.DPI)
					{
						result.ScaleChanged = true;
					}
				}
			}
			if (result.SizeChanged || result.ScaleChanged)
			{
				_screenInfoCache = currentScreens;
			}
			return result;
		}
		private static IntPtr WndProc (IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			switch (msg)
			{
				case WM_DISPLAYCHANGE:
					ScreenChangeResult result1 = DetectScreenChanges ();
					if (result1.SizeChanged)
					{
						if (SizeChanged != null)
						{
							SizeChanged (null, EventArgs.Empty);
						}
					}
					if (result1.ScaleChanged)
					{
						if (ScaleChanged != null)
						{
							ScaleChanged (null, EventArgs.Empty);
						}
					}
					break;
				case WM_DPICHANGED:
					ScreenChangeResult result2 = DetectScreenChanges ();
					if (result2.ScaleChanged)
					{
						if (ScaleChanged != null)
						{
							ScaleChanged (null, EventArgs.Empty);
						}
					}
					if (result2.SizeChanged)
					{
						if (SizeChanged != null)
						{
							SizeChanged (null, EventArgs.Empty);
						}
					}
					break;
				case WM_SETTINGCHANGE:
					ScreenChangeResult result3 = DetectScreenChanges ();
					if (result3.SizeChanged)
					{
						if (SizeChanged != null)
						{
							SizeChanged (null, EventArgs.Empty);
						}
					}
					if (result3.ScaleChanged)
					{
						if (ScaleChanged != null)
						{
							ScaleChanged (null, EventArgs.Empty);
						}
					}
					CheckWorkingAreaChange ();
					break;
			}
			return IntPtr.Zero;
		}
	}
}
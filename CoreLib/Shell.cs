// This file contains code derived from the LongBar project, licensed under the Microsoft Public License (Ms-PL).
// Original copyright belongs to the LongBar authors.
// As required by the Ms-PL, this derivative work retains the original copyright notice
// and is distributed under the terms of the Ms-PL license.
// Modification history:
// - 2026-05-09: Bruce adapted and modified the code for use with taskbar overlay functionality.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sidebar.Win32;
using static Sidebar.Win32WindowNative;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Threading;

namespace Sidebar
{
	public static class ExplorerShell
	{
		/// <summary>
		/// 获取任务栏的屏幕坐标矩形（位置和尺寸）。[Windows XP: 兼容]
		/// </summary>
		/// <returns>任务栏矩形（屏幕坐标），若获取失败则返回 <see cref="Win32.RECT"/> 的空实例（所有字段为 0）。</returns>
		public static Win32.RECT GetTaskbarRect ()
		{
			Win32.APPBARDATA data = Win32.APPBARDATA.Create ();
			data.hWnd = IntPtr.Zero; // ABM_GETTASKBARPOS 忽略 hWnd
			int result = Win32WindowNative.SHAppBarMessage ((int)Win32.AppBarMessage.GetTaskBarPos, ref data);
			if (result == 0)
			{
				return new Win32.RECT (0, 0, 0, 0);
			}
			return data.rc;
		}
		/// <summary>
		/// 获取任务栏的停靠边缘。[Windows XP: 兼容]
		/// </summary>
		/// <returns>任务栏停靠的边缘（Left, Top, Right, Bottom）。</returns>
		public static Win32.AppBarEdge GetTaskbarEdge ()
		{
			Win32.APPBARDATA data = Win32.APPBARDATA.Create ();
			data.hWnd = IntPtr.Zero;
			Win32WindowNative.SHAppBarMessage ((int)Win32.AppBarMessage.GetTaskBarPos, ref data);
			return (Win32.AppBarEdge)data.uEdge;
		}
		/// <summary>
		/// 获取任务栏的位置信息（矩形 + 边缘）。[Windows XP: 兼容]
		/// </summary>
		/// <param name="rect">任务栏矩形</param>
		/// <param name="edge">任务栏停靠边缘</param>
		/// <returns>是否成功获取（总是成功，除非系统异常）。</returns>
		public static bool GetTaskbarInfo (out Win32.RECT rect, out Win32.AppBarEdge edge)
		{
			Win32.APPBARDATA data = Win32.APPBARDATA.Create ();
			data.hWnd = IntPtr.Zero;
			int result = Win32WindowNative.SHAppBarMessage ((int)Win32.AppBarMessage.GetTaskBarPos, ref data);
			rect = data.rc;
			edge = (Win32.AppBarEdge)data.uEdge;
			return result != 0;
		}
		/// <summary>
		/// 任务栏位置改变时触发（位置、大小、停靠边缘变化）。
		/// </summary>
		public static event EventHandler TaskbarPositionChanged;
		private static readonly object _monitorLock = new object ();
		private static TaskbarMonitorWindow _monitorWindow;
		private static int _monitorRefCount;
		/// <summary>
		/// 当前是否正在监听任务栏位置改变。
		/// </summary>
		public static bool IsTaskbarMonitoring
		{
			get { lock (_monitorLock) return _monitorWindow != null; }
		}
		/// <summary>
		/// 当前注册计数。为 0 表示未监听，大于 0 表示已监听且记录着有效注册次数。
		/// </summary>
		public static int TaskbarMonitoringRefCount
		{
			get { lock (_monitorLock) return _monitorRefCount; }
		}
		/// <summary>
		/// 开始监听任务栏位置改变（计次）。
		/// 必须在 UI 线程上调用（例如窗体加载完成时）。
		/// 与 <see cref="StopTaskbarMonitoring"/> 一一对应；
		/// 只有全部 Stop 之后才会真正注销。
		/// </summary>
		public static void StartTaskbarMonitoring ()
		{
			lock (_monitorLock)
			{
				_monitorRefCount++;
				if (_monitorWindow != null) return;
				var wnd = new TaskbarMonitorWindow ();
				wnd.TaskbarPosChanged += OnTaskbarPosChanged;
				try
				{
					wnd.CreateHandle (); // 创建隐藏窗口并开始接收消息
				}
				catch
				{
					wnd.TaskbarPosChanged -= OnTaskbarPosChanged;
					try { wnd.DestroyHandle (); } catch { }
					_monitorRefCount--;
					throw;
				}
				_monitorWindow = wnd;
			}
		}
		/// <summary>
		/// 停止监听任务栏位置改变（计次）。
		/// 只有注册计数归零时才会真正注销。
		/// </summary>
		public static void StopTaskbarMonitoring ()
		{
			lock (_monitorLock)
			{
				if (_monitorRefCount > 0)
					_monitorRefCount--;
				if (_monitorRefCount > 0) return;
				DetachLocked ();
			}
		}
		/// <summary>
		/// 无条件完全注销：清空所有注册次数并真正停止监听。
		/// </summary>
		public static void StopAllTaskbarMonitoring ()
		{
			lock (_monitorLock)
			{
				_monitorRefCount = 0;
				DetachLocked ();
			}
		}
		private static void DetachLocked ()
		{
			if (_monitorWindow == null) return;
			_monitorWindow.TaskbarPosChanged -= OnTaskbarPosChanged;
			try { _monitorWindow.DestroyHandle (); }
			catch { }
			_monitorWindow = null;
		}
		private static void OnTaskbarPosChanged (object sender, EventArgs e)
		{
			TaskbarPositionChanged?.Invoke (null, EventArgs.Empty);
		}
	}
	/// <summary>
	/// 内部隐藏窗口，用于接收系统广播消息。
	/// </summary>
	internal class TaskbarMonitorWindow: NativeWindow
	{
		private const int WM_SETTINGCHANGE = 0x001A;
		public event EventHandler TaskbarPosChanged;
		public void CreateHandle ()
		{
			CreateParams cp = new CreateParams ();
			cp.ExStyle = 0;
			cp.Style = unchecked((int)Win32.WindowStyles.WS_POPUP);
			cp.X = 0;
			cp.Y = 0;
			cp.Width = 0;
			cp.Height = 0;
			cp.Caption = "SidebarTaskbarMonitor";
			cp.Parent = IntPtr.Zero;
			base.CreateHandle (cp);
		}
		protected override void WndProc (ref Message m)
		{
			if (m.Msg == WM_SETTINGCHANGE)
			{
				if (m.LParam != IntPtr.Zero)
				{
					string setting = Marshal.PtrToStringAuto (m.LParam);
					if (string.Equals (setting, "TaskbarPos", StringComparison.OrdinalIgnoreCase))
					{
						TaskbarPosChanged?.Invoke (this, EventArgs.Empty);
					}
				}
			}
			base.WndProc (ref m);
		}
	}
	/// <summary>
	/// 任务栏覆盖辅助类，用于裁剪任务栏区域，为侧边栏留出空间。
	/// 支持方向（左侧/右侧）和宽度的动态修改，自动响应任务栏位置变化。
	/// </summary>
	public class TaskbarOverlapHelper: IDisposable
	{
		HWND hw = IntPtr.Zero;
		HwndSource hs = null;
		Window wpfw = null;
		DispatcherTimer timer = new DispatcherTimer ();
		public Screen CurrentScreen { get; set; }
		public SidebarDirection Direction { get; set; }
		public bool Overlapped { get; private set; }
		public bool IsRegistered { get; private set; } = false;
		private int trayWndWidth;
		private int trayWndLeft;
		private IntPtr rgn = IntPtr.Zero;
		public TaskbarOverlapHelper (Window w)
		{
			wpfw = w;
			var interop = new WindowInteropHelper (w);
			hw = interop.Handle;
			hs = HwndSource.FromHwnd (hw);
			timer.Tick += Timer_Tick;
			timer.Interval = TimeSpan.FromMilliseconds (500);
		}
		public void Register ()
		{
			if (IsRegistered) return;
			if (CurrentScreen != Screen.PrimaryScreen) return;
			timer?.Start ();
			if (Overlapped) Restore ();
			Timer_Tick (null, null);
			IsRegistered = true;
		}
		public void Unregister ()
		{
			if (!IsRegistered) return;
			if (CurrentScreen != Screen.PrimaryScreen) return;
			timer?.Stop ();
			IsRegistered = false;
			if (Overlapped) Restore ();
		}
		private void Timer_Tick (object sender, EventArgs e)
		{
			if (CurrentScreen != Screen.PrimaryScreen)
			{
				if (Overlapped) Restore ();
			}
			HWND taskbarHwnd = FindWindowEx (IntPtr.Zero, IntPtr.Zero, "Shell_TrayWnd", null);
			HWND trayHwnd = FindWindowEx (taskbarHwnd, IntPtr.Zero, "TrayNotifyWnd", null);
			HWND rebarHwnd = FindWindowEx (taskbarHwnd, IntPtr.Zero, "RebarWindow32", null);
			WINDOWPLACEMENT lpwndpl = new WINDOWPLACEMENT ();
			lpwndpl.length = Marshal.SizeOf (typeof (WINDOWPLACEMENT));
			GetWindowPlacement (taskbarHwnd, ref lpwndpl);
			if (lpwndpl.rcNormalPosition.Top != 0 && lpwndpl.rcNormalPosition.Width == SystemInformation.PrimaryMonitorSize.Width)
			{
				//first, hide tray by setting it's width to 0
				GetWindowPlacement (trayHwnd, ref lpwndpl);
				if (lpwndpl.rcNormalPosition.Width > 0) trayWndWidth = lpwndpl.rcNormalPosition.Width; //save original width of tray
				trayWndLeft = lpwndpl.rcNormalPosition.X; //save original left pos of tray
														  //if (_hideTray)
				MoveWindow (trayHwnd, lpwndpl.rcNormalPosition.X, lpwndpl.rcNormalPosition.Y, 0, lpwndpl.rcNormalPosition.Height, true);
				//else
				//MoveWindow(trayHwnd, SystemInformation.PrimaryMonitorSize.Width - (int)window.Width - lpwndpl.rcNormalPosition.X, lpwndpl.rcNormalPosition.Y, lpwndpl.rcNormalPosition.Width, lpwndpl.rcNormalPosition.Height, true);

				GetWindowPlacement (rebarHwnd, ref lpwndpl);
				MoveWindow (rebarHwnd, lpwndpl.rcNormalPosition.X, lpwndpl.rcNormalPosition.Y, SystemInformation.PrimaryMonitorSize.Width - wpfw.GetPixelWidth () - lpwndpl.rcNormalPosition.X, lpwndpl.rcNormalPosition.Height, true);

				//second, cut taskbar window
				GetWindowPlacement (taskbarHwnd, ref lpwndpl);
				if (rgn != IntPtr.Zero) DeleteObject (rgn);
				rgn = IntPtr.Zero;
				rgn = CreateRectRgn (0, 0, SystemInformation.PrimaryMonitorSize.Width - wpfw.GetPixelWidth (), lpwndpl.rcNormalPosition.Height);
				SetWindowRgn (taskbarHwnd, rgn, true);
				if (Direction == SidebarDirection.Left)
					taskbarHwnd.Move (wpfw.GetPixelWidth ());
				else
					taskbarHwnd.Move (0);
				Overlapped = true;
			}
			else
			{
				if (Overlapped) Restore ();
			}
		}
		public void Restore ()
		{
			if (CurrentScreen != Screen.PrimaryScreen) return;
			HWND taskbarHwnd = FindWindowEx (IntPtr.Zero, IntPtr.Zero, "Shell_TrayWnd", null);
			HWND trayHwnd = FindWindowEx (taskbarHwnd, IntPtr.Zero, "TrayNotifyWnd", null);
			WINDOWPLACEMENT lpwndpl = new WINDOWPLACEMENT ();
			lpwndpl.length = Marshal.SizeOf (typeof (WINDOWPLACEMENT));
			GetWindowPlacement (taskbarHwnd, ref lpwndpl);
			//Check if taskbar at top or bottom and it is cropped
			if (lpwndpl.rcNormalPosition.Top != 0
				&& lpwndpl.rcNormalPosition.Width == SystemInformation.PrimaryMonitorSize.Width)
			{
				//first, return tray by setting it's width back
				GetWindowPlacement (trayHwnd, ref lpwndpl);
				lpwndpl.rcNormalPosition.Width = trayWndWidth; //restore original width of tray
				lpwndpl.rcNormalPosition.X = trayWndLeft; //restore original left pos of tray
														  //if (_hideTray)
				MoveWindow (trayHwnd, lpwndpl.rcNormalPosition.X, lpwndpl.rcNormalPosition.Y, trayWndWidth, lpwndpl.rcNormalPosition.Height, true);

				//second, extend taskbar window
				GetWindowPlacement (taskbarHwnd, ref lpwndpl);
				if (rgn != IntPtr.Zero) DeleteObject (rgn);
				rgn = IntPtr.Zero;
				rgn = CreateRectRgn (0, 0, SystemInformation.PrimaryMonitorSize.Width, lpwndpl.rcNormalPosition.Height);
				SetWindowRgn (taskbarHwnd, rgn, true);
				if (rgn != IntPtr.Zero) DeleteObject (rgn);
				rgn = IntPtr.Zero;
				taskbarHwnd.Move (0);
				Overlapped = false;
			}
			trayHwnd.Update ();
			taskbarHwnd.Update ();
			trayHwnd.Activate ();
			taskbarHwnd.Activate ();
		}
		public void Dispose ()
		{
			Unregister ();
			timer = null;
			hs?.Dispose ();
			wpfw = null;
			CurrentScreen = null;
		}
	}
}
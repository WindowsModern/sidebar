using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using Sidebar.Win32;
namespace Sidebar
{
	internal class SidebarAppBar: AppBar
	{
		public SidebarAppBar (Window wnd): base (wnd)
		{
			var cc = App.CurrentUserConfig;
		}
		public void Register (RECT ?desiredRect = null)
		{
			var cc = App.CurrentUserConfig;
			Register (cc.Direction, desiredRect);
			_hwndSource.AddHook (WndProc);
		}
		public override void Unregister ()
		{
			var cc = App.CurrentUserConfig;
			_hwndSource.RemoveHook (WndProc);
			base.Unregister ();
		}
		public void SizeAppBar ()
		{
			var cc = App.CurrentUserConfig;
			//if (Direction != cc.Direction) Direction = cc.Direction;
			var screen = cc.CurrentScreen;
			var rt = new RECT ();
			rt.Top = screen.Bounds.Top;
			rt.Bottom = screen.Bounds.Bottom;
			var wndWidth = this._window.GetPixelWidth ();
			if (wndWidth > screen.Bounds.Width * 0.4) wndWidth = (int)(150 * screen.GetDPI ());
			rt.Left = Direction == SidebarDirection.Left ? screen.Bounds.Left : screen.Bounds.Right - wndWidth;
			rt.Right = Direction == SidebarDirection.Left ? wndWidth : screen.Bounds.Right;
			var edge = Edge;
			if (edge != AppBarEdge.Left && edge != AppBarEdge.Right)
			{
				edge = AppBarEdge.Right;
			}
			var newrt = QueryPos (edge, rt);
			switch (Direction)
			{
				case SidebarDirection.Left:
					newrt.Right = wndWidth; break;
				case SidebarDirection.Right:
					newrt.Left = newrt.Right - wndWidth; break;
			}
			SetPos (newrt);
			if (cc.OverlapTaskbar)
			{
				newrt.Top = screen.Bounds.Top;
				newrt.Bottom = screen.Bounds.Bottom;
			}
			((HWND)_hwndSource.Handle).Move (newrt.Left, newrt.Top, newrt.Width, newrt.Height);
		}
		public void SetPos ()
		{
			var cc = App.CurrentUserConfig;
			if (Direction != cc.Direction) Direction = cc.Direction;
			var screen = cc.CurrentScreen ?? Screen.PrimaryScreen;
			var t = cc.OverlapTaskbar ? screen.Bounds.Top : screen.WorkingArea.Top;
			var h = cc.OverlapTaskbar ? screen.Bounds.Height : screen.WorkingArea.Height;
			int l = screen.WorkingArea.Left;
			switch (Direction)
			{
				case SidebarDirection.Left:
					l = screen.WorkingArea.Left; break;
				case SidebarDirection.Right:
					l = screen.WorkingArea.Right - _window.GetPixelWidth (); break;
			}
			//_window.MovePixel (l, t, null, h);
			((HWND)_hwndSource.Handle).Move (l, t, null, h);
		}
		private IntPtr WndProc (IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			var cc = App.CurrentUserConfig;
			if (msg == Message)
				switch (wParam.ToInt32 ())
				{
					case (int)AppBarNotification.PosChanged:
						SizeAppBar ();
						return new IntPtr (msg);
				}
			if (msg == 26 && wParam.ToInt32 () == 47 && !cc.OccupyWorkingArea)
			{
				SetPos ();
				return new IntPtr (msg);
			}
			else if (DWMAPI.IsElderWindows ())
			{

			}
			return IntPtr.Zero;
		}
	}
}

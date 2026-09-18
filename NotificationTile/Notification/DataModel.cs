using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WindowsModern.NotificationHistoryTile
{
	public enum NotifyIconMessage: uint
	{
		Add = 0,   // NIM_ADD
		Modify = 1,   // NIM_MODIFY
		Delete = 2,   // NIM_DELETE
		SetFocus = 3,   // NIM_SETFOCUS
		SetVersion = 4,   // NIM_SETVERSION
	}
	/// <summary>NOTIFYICONDATA.uFlags（NIF_*）。</summary>
	[Flags]
	public enum NotifyIconFlags: uint
	{
		None = 0x00000000,
		Message = 0x00000001, // NIF_MESSAGE
		Icon = 0x00000002, // NIF_ICON
		Tip = 0x00000004, // NIF_TIP
		State = 0x00000008, // NIF_STATE
		Info = 0x00000010, // NIF_INFO（气泡通知）
		Guid = 0x00000020, // NIF_GUID
		Realtime = 0x00000040, // NIF_REALTIME
		ShowTip = 0x00000080, // NIF_SHOWTIP
	}
	/// <summary>NOTIFYICONDATA.dwState / dwStateMask（NIS_*）。</summary>
	[Flags]
	public enum NotifyIconState: uint
	{
		Hidden = 0x00000001, // NIS_HIDDEN
		SharedIcon = 0x00000002, // NIS_SHAREDICON
	}
	/// <summary>
	/// NOTIFYICONDATA.dwInfoFlags 的低 4 位，表示气泡通知图标类型。
	/// 这几个值是互斥的，不是标志位。
	/// </summary>
	public enum NotifyIconInfoType: uint
	{
		None = 0x00, // NIIF_NONE
		Info = 0x01, // NIIF_INFO
		Warning = 0x02, // NIIF_WARNING
		Error = 0x03, // NIIF_ERROR
		User = 0x04, // NIIF_USER
	}
	/// <summary>
	/// NOTIFYICONDATA.dwInfoFlags 中除低 4 位之外的其他标志位。
	/// </summary>
	[Flags]
	public enum NotifyIconInfoOptions: uint
	{
		None = 0x00000000,
		NoSound = 0x00000010, // NIIF_NOSOUND
		LargeIcon = 0x00000020, // NIIF_LARGE_ICON
		RespectQuietTime = 0x00000080, // NIIF_RESPECT_QUIET_TIME
	}
	public enum NotifyIconVersion: uint
	{
		V0 = 0, // 默认
		V3 = 3, // Windows 2000 / XP
		V4 = 4, // Windows Vista 及以上
	}
	/// <summary>
	/// NOTIFYICONDATA.uCallbackMessage 消息的 lParam 值（NIN_*）。
	/// 这些值由 Explorer 在原生气泡通知的不同时机 Post 到 nid.hWnd 上，
	/// wParam 固定为 nid.uID。
	/// 
	/// 注意：本枚举中的值以 WM_USER (0x0400) 为基准，实际使用时
	/// lParam 的值就是这个枚举值本身，不需要再叠加 WM_USER。
	/// </summary>
	public enum NotifyIconNotification: uint
	{
		/// <summary>NIN_SELECT (WM_USER + 0)，鼠标左键单击托盘图标。</summary>
		Select = 0x0400,

		/// <summary>NIN_KEYSELECT (WM_USER + 1)，键盘 Enter/Space 激活托盘图标。</summary>
		KeySelect = 0x0401,

		/// <summary>NIN_BALLOONSHOW (WM_USER + 2)，气泡通知开始显示。</summary>
		BalloonShow = 0x0402,

		/// <summary>NIN_BALLOONHIDE (WM_USER + 3)，气泡通知被隐藏（非用户操作，例如被其他 UI 遮挡）。</summary>
		BalloonHide = 0x0403,

		/// <summary>NIN_BALLOONTIMEOUT (WM_USER + 4)，气泡通知超时自动消失。</summary>
		BalloonTimeout = 0x0404,

		/// <summary>NIN_BALLOONUSERCLICK (WM_USER + 5)，用户点击了气泡通知。</summary>
		BalloonUserClick = 0x0405,

		/// <summary>NIN_POPUPOPEN (WM_USER + 6)，鼠标悬停到托盘图标上。</summary>
		PopupOpen = 0x0406,

		/// <summary>NIN_POPUPCLOSE (WM_USER + 7)，鼠标离开托盘图标。</summary>
		PopupClose = 0x0407,

		/// <summary>NIN_BALLOONDISMISS (WM_USER + 8)，用户主动关闭气泡通知（Windows 7 及以上）。</summary>
		BalloonDismiss = 0x0408,
	}
	public class NotificationData
	{
		public DateTime TimestampUtc { get; set; }
		public NotifyIconMessage Message { get; set; }
		public IntPtr HWnd { get; set; }
		public uint ID { get; set; }
		public NotifyIconFlags Flags { get; set; }
		public uint CallbackMessage { get; set; }
		public IntPtr HIcon { get; set; }
		public string Tip { get; set; }
		public NotifyIconState State { get; set; }
		public NotifyIconState StateMask { get; set; }
		public string Info { get; set; } 
		public uint ?Timeout { get; set; }
		public NotifyIconVersion? Version { get; set; }
		public string InfoTitle { get; set; }
		public uint InfoFlags { get; set; }
		public Guid Item { get; set; }
		public bool IsBalloonNotification => (Flags & NotifyIconFlags.Info) != 0;
		public NotifyIconInfoType InfoType => (NotifyIconInfoType)(InfoFlags & 0x0F);
		public NotifyIconInfoOptions InfoOptions => (NotifyIconInfoOptions)(InfoFlags & ~0x0Fu);
		public bool IsRead { get; set; } = false;
	}
}

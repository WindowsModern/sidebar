using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Sidebar.Properties;

namespace Sidebar
{
	[ComVisible (true)]
	[Obsolete (DescRes.NeverUse, false)]
	public enum TileType
	{
		Content,
		/// <summary>
		/// 该磁贴仅能容纳其他磁贴。因此，不需要在以 Windows 8.x 模式时提供样式
		/// </summary>
		Container
	}
	[ComVisible (true)]
	[Obsolete (DescRes.NeverUse, false)]
	public enum TileSize
	{
		Small,
		Medium,
		Wide,
		Large
	}
	[ComVisible (true)]
	[Obsolete (DescRes.NeverUse, false)]
	public enum TileForegroundColor
	{
		Dark = 0x000000,
		Light = 0xFFFFFF
	}
	[ComVisible (true)]
	public enum TileOverflow
	{
		[LocalizedDescription ("TileOverflow_Auto", typeof (Resources))]
		Auto,
		[LocalizedDescription ("TileOverflow_Hidden", typeof (Resources))]
		Hidden,
		[LocalizedDescription ("TileOverflow_Scroll", typeof (Resources))]
		Scroll,
		[LocalizedDescription ("TileOverflow_Scale", typeof (Resources))]
		Scale,
		[LocalizedDescription ("TileOverflow_Resize", typeof (Resources))]
		[Obsolete (DescRes.NeverUse, false)]
		Resize 
	}
	[ComVisible (true)]
	public enum TileHostEvent
	{
		[LocalizedDescription ("TileHostEvent_Resize", typeof (Resources))]
		Resize,
		[LocalizedDescription ("TileHostEvent_FlyoutResize", typeof (Resources))]
		FlyoutResize,
		[LocalizedDescription ("TileHostEvent_FlyoutInit", typeof (Resources))]
		FlyoutInit,
		[LocalizedDescription ("TileHostEvent_FlyoutShow", typeof (Resources))]
		FlyoutShow,
		[LocalizedDescription ("TileHostEvent_FlyoutClosing", typeof (Resources))]
		FlyoutClosing,
		[LocalizedDescription ("TileHostEvent_FlyoutClosed", typeof (Resources))]
		FlyoutClosed,
		[LocalizedDescription ("TileHostEvent_SidebarDirectionChanged", typeof (Resources))]
		SidebarDirectionChanged,
		[LocalizedDescription ("TileHostEvent_PropertiesInit", typeof (Resources))]
		PropertiesInit,
		[LocalizedDescription ("TileHostEvent_ThemeChanged", typeof (Resources))]
		ThemeChanged,
		[LocalizedDescription ("TileHostEvent_PropertiesLoad", typeof (Resources))]
		PropertiesLoad,
		[LocalizedDescription ("TileHostEvent_PropertiesClosing", typeof (Resources))]
		PropertiesClosing,
		[LocalizedDescription ("TileHostEvent_PropertiesClosed", typeof (Resources))]
		PropertiesClosed,
		[LocalizedDescription ("TileHostEvent_PropertiesClickOkButton", typeof (Resources))]
		PropertiesClickOkButton,
		[LocalizedDescription ("TileHostEvent_PropertiesClickCancelButton", typeof (Resources))]
		PropertiesClickCancelButton
	}
	[ComVisible (true)]
	public enum SidebarDirection
	{
		[LocalizedDescription ("SidebarDirection_Right", typeof (Resources))]
		Right,
		[LocalizedDescription ("SidebarDirection_Left", typeof (Resources))]
		Left
	}
}

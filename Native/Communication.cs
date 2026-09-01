using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Sidebar
{
	[ComVisible (true)]
	public class NotifyIconNotification
	{
		public int Timeout { get; set; } = 5000;
		public string Title { get; set; }
		public string Content { get; set; }
		public ToolTipIcon Icon { get; set; } = ToolTipIcon.None;
	}
	[ComVisible (true)]
	public class NotifyIconNotification2: NotifyIconNotification
	{
		/// <summary>
		/// 如果可以的话，默认使用用于磁贴显示图标的图标。
		/// </summary>
		public System.Windows.Media.ImageSource IconImage { get; set; } = null;
	}
}

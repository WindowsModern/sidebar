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
		public NotifyIconNotification2 () { }
		public NotifyIconNotification2 (NotifyIconNotification nin, System.Windows.Media.ImageSource iconimg = null)
		{
			Timeout = nin.Timeout;
			Title = nin.Title;
			Content = nin.Content;
			Icon = nin.Icon;
			IconImage = iconimg;
		}
		/// <summary>
		/// 如果可以的话，默认使用用于磁贴显示图标的图标。
		/// </summary>
		public System.Windows.Media.ImageSource IconImage { get; set; } = null;
		private Guid _id;
		/// <summary>
		/// 该属性不要动，不用管。这是只由主程序处理的属性。
		/// </summary>
		private Guid Id
		{
			get { return _id; }
			set { _id = value; }
		}
	}
}

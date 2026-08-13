using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace WindowsModern.PowerTile
{
	public partial class PowerHostForm: Form
	{
		private const int WM_POWERBROADCAST = 0x0218;
		private const int PBT_APMPOWERSTATUSCHANGE = 0x000A; // 电源状态发生变化（电量变动、插拔电源都会触发）
		private const int PBT_APMBATTERYLOW = 0x0009;        // 电池电量低
		private const int PBT_APMOEMEVENT = 0x000B;          // OEM 自定义事件（一般很少用）
		private const int PBT_APMRESUMEAUTOMATIC = 0x0012;
		public PowerHostForm ()
		{
			InitializeComponent ();
		}
		protected override void WndProc (ref Message m)
		{
			if (m.Msg == WM_POWERBROADCAST)
			{
				switch (m.WParam.ToInt32 ())
				{
					case PBT_APMPOWERSTATUSCHANGE:
					case PBT_APMBATTERYLOW:
					case PBT_APMRESUMEAUTOMATIC:
						foreach (var eh in powerChanged)
							eh?.Invoke (this, null);
						break;
				}
			}
			base.WndProc (ref m);
		}
		private HashSet<EventHandler> powerChanged = new HashSet<EventHandler> ();
		public event EventHandler PowerChanged
		{
			add { powerChanged.Add (value); }
			remove { powerChanged.Remove (value); }
		}
	}
}

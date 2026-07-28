using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace Applications.Sidebar
{
	public class FlyoutEventArgs
	{
		private FrameworkElement _FlyoutContent;
		public FrameworkElement FlyoutContent
		{
			get
			{
				return this._FlyoutContent;
			}
			set
			{
				this._FlyoutContent = value;
			}
		}
	}
	public class ConfigWindowEventArgs
	{
		private Window _configWDW;
		public Window ConfigurationWindow
		{
			get
			{
				return this._configWDW;
			}
			set
			{
				this._configWDW = value;
			}
		}
	}
}

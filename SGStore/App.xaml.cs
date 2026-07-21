using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Net;
using System.Windows;

namespace Sidebar
{
	/// <summary>
	/// App.xaml 的交互逻辑
	/// </summary>
	public partial class App: Application
	{
		private void Application_Startup (object sender, StartupEventArgs e)
		{
			ServicePointManager.SecurityProtocol |= (SecurityProtocolType)192 |   // TLS 1.0
				(SecurityProtocolType)768 |   // TLS 1.1
				(SecurityProtocolType)3072;   // TLS 1.2
		}
	}
}

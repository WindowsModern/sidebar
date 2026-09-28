using System;
using System.Windows;
using System.Windows.Threading;

namespace SGPkgMgr
{
	public partial class App: Application
	{
		protected override void OnStartup (StartupEventArgs e)
		{
			base.OnStartup (e);
			DispatcherUnhandledException += OnUnhandledException;
		}
		private void OnUnhandledException (object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			MessageBox.Show (
				e.Exception?.Message ?? "Unknown error",
				"Unhandled Exception",
				MessageBoxButton.OK, MessageBoxImage.Error);
			e.Handled = true;
		}
	}
}
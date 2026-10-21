using System;
using System.Windows;

namespace SGTheme
{
	public interface INeedWindowControl
	{
		bool CanShowOnWindow { get; }
		WindowStyle WindowStyle { get; }
		bool AllowTransparency { get; }
		void Window_SourceInitialized (object sender, EventArgs e);
		void Window_Loaded (object sender, RoutedEventArgs e);
		void Window_Unloaded (object sender, RoutedEventArgs e);
		void Window_Closed (object sender, EventArgs e);
		void Window_OnThemeChanged ();
	}
}

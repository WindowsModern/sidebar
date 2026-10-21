using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;

namespace SGTheme
{
	/// <summary>
	/// Presentation types migrated from Sidebar.Presentation so that
	/// the Sidebar/Styles dictionaries can be compiled inside SGTheme.
	/// Native (snt:) and CoreLib are referenced as assemblies.
	/// </summary>
	public sealed class SidebarUIConfig: INotifyPropertyChanged
	{
		public static SidebarUIConfig Instance { get; } = new SidebarUIConfig ();
		public bool IsReversed => global::SGTheme.SidebarConfig.Global.IsReversed;
		public event PropertyChangedEventHandler PropertyChanged;
	}

	public sealed class SystemExplorerShell: INotifyPropertyChanged
	{
		public static SystemExplorerShell Instance { get; } = new SystemExplorerShell ();
		public bool IsTaskbarTop => global::SGTheme.SidebarConfig.Global.IsTaskbarTop;
		public bool IsAppDarkMode => false;
		public bool IsShellDarkMode => false;
		public event PropertyChangedEventHandler PropertyChanged;
	}

	public sealed class StringResourceExtension: MarkupExtension
	{
		public string Key { get; set; }
		public string Fallback { get; set; }

		public StringResourceExtension ()
		{
		}

		public StringResourceExtension (string key)
		{
			Key = key;
		}

		public override object ProvideValue (IServiceProvider serviceProvider)
		{
			if (!string.IsNullOrEmpty (Fallback))
				return Fallback;
			switch (Key)
			{
				case "SIDEBAR_OPTIONS": return "Options";
				case "SIDEBAR_MESSAGE_ERROR_COPYTEXT": return "Copy Info";
				case "SIDEBAR_MESSAGE_ERROR_OK": return "OK";
				case "SIDEBAR_MESSAGE_ERROR_DETAIL": return "Details";
				case "SIDEBAR_NOTIFY_OPTIONS": return "Options";
				case "SIDEBAR_NOTIFY_RULES": return "Rules and Alerts";
				default: return Key ?? string.Empty;
			}
		}
	}

	public sealed class StringResourceConverter: IValueConverter
	{
		public static StringResourceConverter Instance { get; } = new StringResourceConverter ();

		public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value;
		}

		public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value;
		}
	}

	public sealed class ObjectToVisibilityConverter: IValueConverter
	{
		public static ObjectToVisibilityConverter Instance { get; } = new ObjectToVisibilityConverter ();

		public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value == null ? Visibility.Collapsed : Visibility.Visible;
		}

		public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value is Visibility && (Visibility)value == Visibility.Visible;
		}
	}

	public sealed class CalendarModeToBooleanConverter: IValueConverter
	{
		public static CalendarModeToBooleanConverter Instance { get; } = new CalendarModeToBooleanConverter ();

		public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
		{
			return true;
		}

		public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value;
		}
	}

	/// <summary>
	/// Attached property used by Sidebar styles; SGTheme only needs the
	/// property to exist, not the auto-hide behavior.
	/// </summary>
	public static class AutoHideScrollBar
	{
		public static readonly DependencyProperty IsEnabledProperty =
			DependencyProperty.RegisterAttached ("IsEnabled", typeof (bool), typeof (AutoHideScrollBar), new PropertyMetadata (false));

		public static bool GetIsEnabled (DependencyObject element)
		{
			return (bool)element.GetValue (IsEnabledProperty);
		}

		public static void SetIsEnabled (DependencyObject element, bool value)
		{
			element.SetValue (IsEnabledProperty, value);
		}
	}

	/// <summary>
	/// Style target used by Sidebar/Styles/Tile.xaml (AncestorType).
	/// </summary>
	public class TileControl: ContentControl
	{
		public bool IsPinned { get; set; }
	}
}

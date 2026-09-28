using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using Sidebar;

namespace SGInstaller
{
	public static class LocalizationManager
	{
		public static ILocaleResources Resources
		{
			get { return App.StringResources; }
		}
		public static string Get (string key, string fallback, string localeName = null)
		{
			if (string.IsNullOrEmpty (key)) return fallback ?? string.Empty;
			var res = Resources;
			if (res == null) return fallback ?? key;
			if (string.IsNullOrEmpty (localeName)) localeName = null;
			try
			{
				var r = res.SuitableResource (key, fallback, localeName);
				if (!string.IsNullOrEmpty (r)) return r;
			}
			catch { }
			return fallback ?? key;
		}
		public static string Get (string key, string fallback)
		{
			return Get (key, fallback, null);
		}
		public static string Get (string key)
		{
			return Get (key, key, null);
		}
		public static string Format (string key, params object [] args)
		{
			var format = Get (key, key);
			if (args == null || args.Length == 0) return format;
			try { return string.Format (format, args); }
			catch { return format; }
		}
	}

	[MarkupExtensionReturnType (typeof (string))]
	public sealed class StringResourceExtension: MarkupExtension
	{
		[ConstructorArgument ("key")]
		public string Key { get; set; }
		public string Fallback { get; set; }
		public string Locale { get; set; }
		public StringResourceExtension ()
		{
		}
		public StringResourceExtension (string key)
		{
			Key = key;
		}
		public override object ProvideValue (IServiceProvider serviceProvider)
		{
			if (string.IsNullOrEmpty (Key)) return string.Empty;
			return LocalizationManager.Get (Key, Fallback, Locale);
		}
	}

	public sealed class StringResourceConverter: IValueConverter
	{
		public static StringResourceConverter Instance { get; private set; } = new StringResourceConverter ();
		public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
		{
			var key = value as string;
			if (string.IsNullOrEmpty (key)) return string.Empty;
			var fallback = parameter as string ?? key;
			return LocalizationManager.Get (key, fallback);
		}
		public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
		{
			return Binding.DoNothing;
		}
	}
}
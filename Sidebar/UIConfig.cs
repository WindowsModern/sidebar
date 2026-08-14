using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Sidebar
{
	public class DirectionToVisibilityConverter: IValueConverter
	{
		public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is SidebarDirection)
			{
				var direction = (SidebarDirection)value;
				string param = parameter as string;
				if (param == "Left")
					return direction == SidebarDirection.Left ? Visibility.Visible : Visibility.Collapsed;
				if (param == "Right")
					return direction == SidebarDirection.Right ? Visibility.Visible : Visibility.Collapsed;
			}
			return Visibility.Collapsed;
		}
		public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException ();
		}
	}
	public class DirectionToHorizontalAlignmentConverter: IValueConverter
	{
		public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is SidebarDirection)
			{
				var direction = (SidebarDirection)value;
				return direction == SidebarDirection.Left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
			}
			return HorizontalAlignment.Right;
		}

		public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException ();
		}
	}
	public class DirectionToFlowDirectionConverter: IValueConverter
	{
		public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is SidebarDirection)
			{
				var direction = (SidebarDirection)value;
				return direction == SidebarDirection.Left ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
			}
			return FlowDirection.LeftToRight;
		}

		public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException ();
		}
	}
	public class DirectionToScaleXConverter: IValueConverter
	{
		public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is SidebarDirection)
			{
				var direction = (SidebarDirection)value;
				return direction == SidebarDirection.Left ? -1.0 : 1.0;
			}
			return 1.0;
		}
		public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException ();
		}
	}
	[ComVisible (true)]
	public class DwmThemeProvider: INotifyPropertyChanged
	{
		private static DwmThemeProvider _instance;
		public static DwmThemeProvider Instance
		{
			get { return _instance ?? (_instance = new DwmThemeProvider ()); }
		}
		private Color _themeColor;
		public Color ThemeColor
		{
			get { return _themeColor; }
			private set
			{
				if (_themeColor != value)
				{
					Color oldColor = _themeColor;
					_themeColor = value;
					OnPropertyChanged (nameof (ThemeColor));
					OnPropertyChanged (nameof (ThemeBrush));
					OnPropertyChanged (nameof (ForegroundBrush));
					// 新增：通知不透明颜色相关属性
					OnPropertyChanged (nameof (ThemeColorOpaque));
					OnPropertyChanged (nameof (OpaqueThemeBrush));
					OnPropertyChanged (nameof (ForegroundColor));
					ColorChanged?.Invoke (this, new ColorChangedEventArgs (oldColor, value));
				}
			}
		}
		public Color ThemeColorOpaque
		{
			get { return Color.FromArgb (255, ThemeColor.R, ThemeColor.G, ThemeColor.B); }
		}
		private SolidColorBrush _themeBrush;
		public SolidColorBrush ThemeBrush
		{
			get
			{
				if (_themeBrush == null)
					_themeBrush = new SolidColorBrush (ThemeColor);
				else
					_themeBrush.Color = ThemeColor;
				return _themeBrush;
			}
		}
		private SolidColorBrush _opaqueThemeBrush;
		public SolidColorBrush OpaqueThemeBrush
		{
			get
			{
				Color opaque = ThemeColorOpaque;
				if (_opaqueThemeBrush == null)
					_opaqueThemeBrush = new SolidColorBrush (opaque);
				else
					_opaqueThemeBrush.Color = opaque;
				return _opaqueThemeBrush;
			}
		}
		private SolidColorBrush _foregroundBrush;
		public SolidColorBrush ForegroundBrush
		{
			get
			{
				if (_foregroundBrush == null)
					_foregroundBrush = new SolidColorBrush (GetForegroundColor (ThemeColor));
				else
					_foregroundBrush.Color = GetForegroundColor (ThemeColor);
				return _foregroundBrush;
			}
		}
		public Color ForegroundColor => GetForegroundColor (ThemeColor);
		private Color GetForegroundColor (Color bg)
		{
			double luminance = 0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B;
			if (luminance > 140) return Colors.Black;
			else return Colors.White;
		}
		public event EventHandler<ColorChangedEventArgs> ColorChanged;
		private DwmThemeProvider ()
		{
			UpdateThemeColor ();
		}
		private void UpdateThemeColor ()
		{
			try
			{
				uint color;
				bool opaque;
				int hr = DwmGetColorizationColor (out color, out opaque);
				if (hr == 0)
				{
					byte a = (byte)((color >> 24) & 0xFF);
					byte r = (byte)((color >> 16) & 0xFF);
					byte g = (byte)((color >> 8) & 0xFF);
					byte b = (byte)(color & 0xFF);
					ThemeColor = Color.FromArgb (a, r, g, b);
					return;
				}
			}
			catch { }
			ThemeColor = Color.FromRgb (0x38, 0x41, 0x46);
		}
		[DllImport ("dwmapi.dll")]
		private static extern int DwmGetColorizationColor (out uint pcrColorization, out bool pfOpaqueBlend);
		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged (string name)
		{
			PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (name));
		}
		private HwndSource _hwndSource;
		public void StartListening (HwndSource window)
		{
			if (_hwndSource != null) return;
			_hwndSource = window;
			if (_hwndSource != null)
			{
				_hwndSource.AddHook (WndProc);
			}
		}
		private const int WM_DWMCOMPOSITIONCHANGED = 0x0000031E;
		private const int WM_DWMCOLORIZATIONCOLORCHANGED = 0x0320;
		private IntPtr WndProc (IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			if (msg == WM_DWMCOLORIZATIONCOLORCHANGED) 
			{
				UpdateThemeColor ();
			}
			return IntPtr.Zero;
		}
		public void StopListening ()
		{
			if (_hwndSource != null)
			{
				_hwndSource.RemoveHook (WndProc);
				_hwndSource = null;
			}
		}
	}
	public class ColorChangedEventArgs: EventArgs
	{
		public Color OldColor { get; private set; }
		public Color NewColor { get; private set; }
		public ColorChangedEventArgs (Color oldColor, Color newColor)
		{
			OldColor = oldColor;
			NewColor = newColor;
		}
	}
	public class FilePathToImageConverter: IValueConverter
	{
		public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null) return null;

			string path = value.ToString ();

			if (!Path.IsPathRooted (path))
				path = Path.Combine (AppDomain.CurrentDomain.BaseDirectory, path);

			if (!File.Exists (path))
				return null;

			BitmapImage img = new BitmapImage ();
			img.BeginInit ();
			img.UriSource = new Uri (path, UriKind.Absolute);
			img.CacheOption = BitmapCacheOption.OnLoad;
			img.EndInit ();

			return img;
		}

		public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
			=> Binding.DoNothing;
	}
	public class TextEmptyToVisibilityConverter: IValueConverter
	{
		public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
		{
			var text = value as string;
			return string.IsNullOrEmpty (text) ? Visibility.Visible : Visibility.Collapsed;
		}
		public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}
	}
	public class ObjectToVisibilityConverter: IValueConverter
	{
		public object Convert (object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			bool visible = value != null;
			string param = parameter as string;
			if (param == "CollapsedWhenNull")
				return visible ? Visibility.Visible : Visibility.Collapsed;
			return visible ? Visibility.Visible : Visibility.Collapsed;
		}
		public object ConvertBack (object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return Binding.DoNothing;
		}
	}
	public class StringResourceConverter: IValueConverter
	{
		public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null) return null;
			string key = value.ToString ();
			try
			{
				if (string.IsNullOrEmpty (key)) return null;
				return App.ProgramFolder.StringResources?.SuitableResource (key, key) ?? key;
			}
			catch
			{
				return key;
			}
		}
		public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
		{
			return Binding.DoNothing;
		}
	}
}

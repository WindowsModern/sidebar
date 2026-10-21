using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SGTheme.Tiles.Clock
{
	/// <summary>
	/// TileShortTime.xaml 的交互逻辑
	/// </summary>
	public partial class TileShortTime: UserControl
	{
		private bool isLoaded = false;
		public TileShortTime ()
		{
			InitializeComponent ();
		}
		public static readonly DependencyProperty CurrentTimeProperty =
		   DependencyProperty.Register ("CurrentTime", typeof (DateTime), typeof (TileShortTime),
			   new FrameworkPropertyMetadata (DateTime.Now, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCurrentTimeChanged));
		public DateTime CurrentTime
		{
			get { return (DateTime)GetValue (CurrentTimeProperty); }
			set { SetValue (CurrentTimeProperty, value); }
		}
		private static void OnCurrentTimeChanged (DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var control = d as TileShortTime;
			control?.UpdateDisplay ();
		}
		public static readonly DependencyProperty TimeZoneIdProperty =
		 DependencyProperty.Register ("TimeZoneId", typeof (string), typeof (TileShortTime),
			 new FrameworkPropertyMetadata (null, OnTimeZoneIdChanged));
		public string TimeZoneId
		{
			get { return (string)GetValue (TimeZoneIdProperty); }
			set { SetValue (TimeZoneIdProperty, value); }
		}
		private static void OnTimeZoneIdChanged (DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var control = d as TileShortTime;
			control?.UpdateDisplay ();
		}
		private void UpdateDisplay ()
		{
			if (!isLoaded) return;

			DateTime displayTime = CurrentTime;
			// 如果指定了时区，则将 CurrentTime 视为 UTC，转换到目标时区
			if (!string.IsNullOrWhiteSpace (TimeZoneId))
			{
				try
				{
					TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById (TimeZoneId);
					displayTime = TimeZoneInfo.ConvertTimeFromUtc (CurrentTime, tz);
				}
				catch
				{
					// 时区无效时，保持原始值（视为本地时间）
				}
			}
			else
			{
				displayTime = TimeZoneInfo.ConvertTime (CurrentTime, TimeZoneInfo.Local);
			}
			string shortTimePattern = CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern;
			bool hasAmPm = shortTimePattern.Contains ("tt");
			string formatted;
			MeridiemText.Visibility = BlankSpace.Visibility = hasAmPm ? Visibility.Visible : Visibility.Collapsed;
			if (!hasAmPm)
			{
				TimeText.Text = displayTime.ToString (shortTimePattern, CultureInfo.CurrentCulture);
				return;
				//formatted = displayTime.ToString ("h:mm tt", CultureInfo.InvariantCulture);
			}
			else
			{
				formatted = displayTime.ToString (shortTimePattern, CultureInfo.CurrentCulture);
			}

			string timePart = formatted;
			string meridiemPart = "";
			int spaceIndex = formatted.IndexOf (' ');
			if (spaceIndex > 0)
			{
				timePart = formatted.Substring (0, spaceIndex);
				meridiemPart = formatted.Substring (spaceIndex + 1);
			}

			TimeText.Text = timePart;
			MeridiemText.Text = meridiemPart;
		}
		public static readonly DependencyProperty IsLargeProperty =
		DependencyProperty.Register ("IsLarge", typeof (bool), typeof (TileShortTime),
			new FrameworkPropertyMetadata (false, OnIsLargeChanged));
		public bool IsLarge
		{
			get { return (bool)GetValue (IsLargeProperty); }
			set { SetValue (IsLargeProperty, value); }
		}
		private static void OnIsLargeChanged (DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var control = d as TileShortTime;
			control?.UpdateStyles ();
		}
		private void UpdateStyles ()
		{
			//if (!isLoaded) return;
			string timeStyleKey = IsLarge ? "TileClockShortTimeTimePartLarge" : "TileClockShortTimeTimePart";
			string meridiemStyleKey = IsLarge ? "TileClockShortTimeMeridiemPartLarge" : "TileClockShortTimeMeridiemPart";
			string blankStyleKey = IsLarge ? "TileClockShortTimeBlankPartLarge" : "TileClockShortTimeBlankPart";
			TimeText.Style = (Style)FindResource (timeStyleKey);
			MeridiemText.Style = (Style)FindResource (meridiemStyleKey);
			BlankSpace.Style = (Style)FindResource (blankStyleKey);
		}
		public static readonly new DependencyProperty HorizontalContentAlignmentProperty =
	DependencyProperty.Register ("HorizontalContentAlignment", typeof (HorizontalAlignment), typeof (TileShortTime),
		new FrameworkPropertyMetadata (HorizontalAlignment.Left, OnHorizontalContentAlignmentChanged));
		public new HorizontalAlignment HorizontalContentAlignment
		{
			get { return (HorizontalAlignment)GetValue (HorizontalContentAlignmentProperty); }
			set { SetValue (HorizontalContentAlignmentProperty, value); }
		}
		private static void OnHorizontalContentAlignmentChanged (DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var control = d as TileShortTime;
			if (control != null && control.isLoaded)
			{
				control.InvalidateMeasure ();
			}
		}
		private void UserControl_Loaded (object sender, RoutedEventArgs e)
		{
			isLoaded = true;
			UpdateDisplay ();
			UpdateStyles ();
		}
		private void UserControl_Unloaded (object sender, RoutedEventArgs e)
		{
			isLoaded = false;
		}
	}
}

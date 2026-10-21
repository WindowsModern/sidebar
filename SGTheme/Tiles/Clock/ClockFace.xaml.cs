using System;
using System.Collections.Generic;
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
	/// ClockFace.xaml 的交互逻辑
	/// </summary>
	public partial class ClockFace: UserControl
	{
		public ClockFace ()
		{
			InitializeComponent ();
			InitImages ();
			//UpdateAngles ();
		}
		public void InitImages ()
		{
			return;
		}
		private bool isLoaded = false;
		public static readonly DependencyProperty HourProperty =
			DependencyProperty.Register ("Hour", typeof (int), typeof (ClockFace),
				new FrameworkPropertyMetadata (0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTimePropertyChanged));
		public int Hour
		{
			get { return (int)GetValue (HourProperty); }
			set { SetValue (HourProperty, value); }
		}
		public static readonly DependencyProperty MinuteProperty =
			DependencyProperty.Register ("Minute", typeof (int), typeof (ClockFace),
				new FrameworkPropertyMetadata (0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTimePropertyChanged));
		public int Minute
		{
			get { return (int)GetValue (MinuteProperty); }
			set { SetValue (MinuteProperty, value); }
		}
		public static readonly DependencyProperty SecondProperty =
			DependencyProperty.Register ("Second", typeof (int), typeof (ClockFace),
				new FrameworkPropertyMetadata (0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTimePropertyChanged));
		public int Second
		{
			get { return (int)GetValue (SecondProperty); }
			set { SetValue (SecondProperty, value); }
		}
		private static void OnTimePropertyChanged (DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var control = d as ClockFace;
			if (control != null)
				control.UpdateAngles ();
		}
		private void UpdateAngles ()
		{
			//if (!isLoaded) return;

			double hourAngle = (Hour % 12) * 30 + Minute * 0.5;
			double minuteAngle = Minute * 6 + Second * 0.1;
			double secondAngle = Second * 6;
			RotateTransform hourRt = HourHand.RenderTransform as RotateTransform;
			if (hourRt != null) hourRt.Angle = hourAngle;
			RotateTransform minuteRt = MinuteHand.RenderTransform as RotateTransform;
			if (minuteRt != null) minuteRt.Angle = minuteAngle;
			RotateTransform secondRt = SecondHand.RenderTransform as RotateTransform;
			if (secondRt != null) secondRt.Angle = secondAngle;
		}
		private void UserControl_Loaded (object sender, RoutedEventArgs e)
		{
			isLoaded = true;
			//UpdateAngles ();
		}
		private void UserControl_Unloaded (object sender, RoutedEventArgs e)
		{
			isLoaded = false;
		}
		private bool isUpdatingFromCurrentTime = false;
		public static readonly DependencyProperty CurrentTimeProperty =
		   DependencyProperty.Register ("CurrentTime", typeof (DateTime), typeof (ClockFace),
			   new FrameworkPropertyMetadata (DateTime.Now, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCurrentTimeChanged));
		public DateTime CurrentTime
		{
			get { return (DateTime)GetValue (CurrentTimeProperty); }
			set { SetValue (CurrentTimeProperty, value); }
		}
		public static readonly DependencyProperty TimeZoneIdProperty =
			DependencyProperty.Register ("TimeZoneId", typeof (string), typeof (ClockFace),
				new FrameworkPropertyMetadata (null, OnTimeZoneIdChanged));
		public string TimeZoneId
		{
			get { return (string)GetValue (TimeZoneIdProperty); }
			set { SetValue (TimeZoneIdProperty, value); }
		}
		private static void OnTimeZoneIdChanged (DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var control = d as ClockFace;
			if (control != null)
				control.UpdateFromCurrentTime ();
		}
		private static void OnCurrentTimeChanged (DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var control = d as ClockFace;
			if (control != null)
				control.UpdateFromCurrentTime ();
		}
		private void UpdateFromCurrentTime ()
		{
			//if (!isLoaded) return;
			DateTime targetTime = CurrentTime;
			if (!string.IsNullOrWhiteSpace (TimeZoneId))  // 改为 IsNullOrWhiteSpace
			{
				try
				{
					TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById (TimeZoneId);
					targetTime = TimeZoneInfo.ConvertTime (CurrentTime, TimeZoneInfo.Local, tz);
				}
				catch
				{
					targetTime = CurrentTime;
				}
			}
			else
			{
				targetTime = TimeZoneInfo.ConvertTime (CurrentTime, TimeZoneInfo.Local);
			}
			isUpdatingFromCurrentTime = true;
			Hour = targetTime.Hour;
			Minute = targetTime.Minute;
			Second = targetTime.Second;
			isUpdatingFromCurrentTime = false;
			UpdateAngles ();
		}
		private void UpdateCurrentTimeFromHMS ()
		{
			// 注意：此时无法获知原始时区，我们假定 CurrentTime 的 Date 部分保持不变，只替换时间部分
			DateTime oldTime = CurrentTime;
			DateTime newTime = new DateTime (oldTime.Year, oldTime.Month, oldTime.Day, Hour, Minute, Second);
			isUpdatingFromCurrentTime = true;
			CurrentTime = newTime;
			isUpdatingFromCurrentTime = false;
		}
	}
}

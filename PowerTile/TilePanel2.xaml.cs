using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using BatteryChargeStatus = System.Windows.Forms.BatteryChargeStatus;
namespace WindowsModern.PowerTile
{
	public partial class TilePanel2: UserControl, IDisposable
	{
		private readonly PowerTextDecorator _ptd;
		private readonly DispatcherTimer _timer;
		private readonly Random _random = new Random ();
		private AnimationClock _acLogoClock;
		private int _lastPercent = -1;
		private int _lastSeconds = -1;
		private bool _lastOnBattery = false;
		private BatteryChargeStatus _lastLevel = BatteryChargeStatus.Unknown;
		public TilePanel2 ()
		{
			InitializeComponent ();
			_ptd = new PowerTextDecorator ();
			TilePowerTile_TextPanel.Children.Clear ();
			_ptd.Child = TilePowerTile_Numbers;
			TilePowerTile_TextPanel.Children.Add (_ptd);
			_timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds (_random.Next (100, 500) * 0.01) };
			_timer.Tick += Timer_Tick;
		}
		private void UserControl_Loaded (object sender, RoutedEventArgs e)
		{
			InitAcLogoAnimation ();
			SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
			SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
			_timer.Start ();
			PollForce ();
		}
		private void UserControl_Unloaded (object sender, RoutedEventArgs e)
		{
			SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
			_timer.Stop ();
		}
		private void InitAcLogoAnimation ()
		{
			if (_acLogoClock != null) return;
			var anim = new DoubleAnimation (0, 1, TimeSpan.FromSeconds (2)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
			_acLogoClock = anim.CreateClock ();
			TilePowerTile_ACLogo.ApplyAnimationClock (UIElement.OpacityProperty, _acLogoClock);
		}
		private void SystemEvents_PowerModeChanged (object sender, PowerModeChangedEventArgs e)
		{
			PollForce ();
		}
		private void Timer_Tick (object sender, EventArgs e)
		{
			_timer.Interval = TimeSpan.FromSeconds (_random.Next (100, 500) * 0.01);
			Poll ();
		}
		private void PollForce ()
		{
			_lastPercent = -1;
			_lastSeconds = -1;
			_lastLevel = BatteryChargeStatus.Unknown;
			Poll ();
		}
		private void Poll ()
		{
			var ps = System.Windows.Forms.SystemInformation.PowerStatus;
			bool onBattery = ps.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Offline;
			int percent = ps.BatteryLifePercent < 0 ? 0 : (int)Math.Round (ps.BatteryLifePercent * 100);
			int seconds = ps.BatteryLifeRemaining;
			var level = ps.BatteryChargeStatus;
			bool force = _lastPercent < 0;
			bool changed = force || onBattery != _lastOnBattery || percent != _lastPercent || seconds != _lastSeconds || level != _lastLevel;
			if (!changed) return;
			if (force || onBattery != _lastOnBattery)
			{
				_lastOnBattery = onBattery;
				StatusUpdate (onBattery);
			}
			if (force || percent != _lastPercent || seconds != _lastSeconds)
			{
				_lastPercent = percent;
				_lastSeconds = seconds;
				TextUpdate (percent, seconds, onBattery);
			}
			if (force || level != _lastLevel || percent != _lastPercent)
			{
				_lastLevel = level;
				GasUpdate (percent, onBattery, level, 20);
			}
			ToolTip = GetToolTipText (percent, seconds, onBattery);
		}
		private void TextUpdate (int percent, int seconds, bool onBattery)
		{
			string percentText = string.Format ("{0}%", percent);
			string meterLong = GetMeterText (percent, seconds, onBattery, true);
			string withPercentLong = string.Format ("{0} ({1})", meterLong, percentText);
			string meterShort = GetMeterText (percent, seconds, onBattery, false);
			string withPercentShort = string.Format ("{0} ({1})", meterShort, percentText);
			if (seconds == -1) _ptd.SetTexts (new [] { meterLong, meterShort });
			else _ptd.SetTexts (new [] { withPercentLong, withPercentShort, meterShort, percentText });
		}
		private void GasUpdate (int percent, bool onBattery, BatteryChargeStatus level, int warnPercent)
		{
			percent = Math.Min (percent, 100);
			string resourceName = "TilePowerTileShinyGreen";
			bool lowOrCritical = (level & BatteryChargeStatus.Low) != 0 || (level & BatteryChargeStatus.Critical) != 0;
			if (onBattery && lowOrCritical) resourceName = "TilePowerTileShinyRed";
			else if (percent <= warnPercent) resourceName = "TilePowerTileShinyYellow";
			var brush = (Brush)FindResource (resourceName);
			TilePowerTile_gaugebkg.Fill = brush;
			TilePowerTile_gauge.Fill = brush;
			TilePowerTile_GaugeColumn.Width = new GridLength (percent, GridUnitType.Star);
			TilePowerTile_GaugeRemainColumn.Width = new GridLength (100 - percent, GridUnitType.Star);
		}
		private void StatusUpdate (bool onBattery)
		{
			if (onBattery)
			{
				if (_acLogoClock != null)
				{
					_acLogoClock.Controller.Pause ();
					_acLogoClock.Controller.Seek (TimeSpan.Zero, TimeSeekOrigin.BeginTime);
				}
				TilePowerTile_ACLogo.Visibility = Visibility.Collapsed;
			}
			else
			{
				if (_acLogoClock != null) _acLogoClock.Controller.Resume ();
				TilePowerTile_ACLogo.Visibility = Visibility.Visible;
			}
		}
		private string GetMeterText (int percent, int seconds, bool onBattery, bool longFormat)
		{
			if (seconds != -1) return longFormat ? TimeSpanPrinter.LongTimeSpanString (seconds) : TimeSpanPrinter.ShortTimeSpanString (seconds);
			if (percent >= 100) return R ("TilePowerTileMeterFull");
			if (longFormat) return onBattery ? string.Format (R ("TilePowerTileMeterCalculatingFormat"), percent) : string.Format (R ("TilePowerTileMeterChargingFormat"), percent);
			return string.Format ("{0}%", percent);
		}
		private string GetToolTipText (int percent, int seconds, bool onBattery)
		{
			if (seconds != -1) return string.Format (R ("TilePowerTileToolTipBatteryRemainingFormat"), TimeSpanPrinter.LongTimeSpanString (seconds), percent);
			if (percent >= 100) return R ("TilePowerTileToolTipFullyCharged");
			return onBattery ? string.Format (R ("TilePowerTileMeterCalculatingFormat"), percent) : string.Format (R ("TilePowerTileMeterChargingFormat"), percent);
		}
		public void Dispose ()
		{
			SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
			_timer.Tick -= Timer_Tick;
			_timer.Stop ();
		}
		private string R (string id)
		{
			return Tile.TileFolder.StringResources.SuitableResource (id);
		}
	}
}
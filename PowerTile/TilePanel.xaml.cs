using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.Management;
using System.Windows.Threading;

namespace WindowsModern.PowerTile
{
	/// <summary>
	/// TilePanel.xaml 的交互逻辑
	/// </summary>
	public partial class TilePanel: System.Windows.Controls.UserControl, IDisposable
	{
		private DispatcherTimer powerUpdateTimer = null;
		private Random random = new Random ();
		public TilePanel ()
		{
			InitializeComponent ();
			ACDisplay.Text = Tile.TileFolder.StringResources.SuitableResource ("STATUS_ACPOWER");
			powerUpdateTimer = new DispatcherTimer ();
			powerUpdateTimer.Interval = TimeSpan.FromSeconds (random.Next (100, 500) * 0.01);
		}
		private void InitPanel ()
		{
			UpdateStatusIcon ();
			UpdateCurrentVolume ();
			UpdateStatusText ();
		}
		private void SystemEvents_PowerModeChanged (object sender, PowerModeChangedEventArgs e)
		{
			UpdateStatusIcon ();
			UpdateCurrentVolume ();
			UpdateStatusText ();
		}
		private void UpdateStatusIcon ()
		{
			var basedir = Tile.TileFolder.FolderPath;
			var ps = SystemInformation.PowerStatus;
			if (ps.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Offline)
			{
				PowerIcon.Source = ImageSourceManager.GetImage (System.IO.Path.Combine (basedir, "Images\\OnBattery.ico"));
				return;
			}
			bool isCharging = (ps.BatteryChargeStatus & BatteryChargeStatus.Charging) != 0;
			if (isCharging)
				PowerIcon.Source = ImageSourceManager.GetImage (System.IO.Path.Combine (basedir, "Images\\Charging.ico"));
			else
				PowerIcon.Source = ImageSourceManager.GetImage (System.IO.Path.Combine (basedir, "Images\\OnAC.ico"));
		}
		public static bool UseBattery ()
		{
			PowerStatus powerStatus = SystemInformation.PowerStatus;
			return powerStatus.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery;
		}
		private void UpdateCurrentVolume ()
		{
			if (UseBattery ())
			{
				PowerStatus powerStatus = SystemInformation.PowerStatus;
				float batteryPercent = powerStatus.BatteryLifePercent;
				VolumeDisplay.Visibility = Visibility.Visible;
				ACDisplay.Visibility = Visibility.Collapsed;
				StatusText.Visibility = Visibility.Visible;
				BatteryVolume.Text = ((int)(batteryPercent * 100)).ToString ();
			}
			else
			{
				VolumeDisplay.Visibility = Visibility.Collapsed;
				ACDisplay.Visibility = Visibility.Visible;
				StatusText.Visibility = Visibility.Collapsed;
			}
		}
		private void UpdateStatusText ()
		{
			var ps = SystemInformation.PowerStatus;
			var sr = Tile.TileFolder.StringResources;
			string resourceId = null;
			bool hasBattery = (ps.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) == 0;
			if (!hasBattery) resourceId = "STATUS_ON_AC_NOT_CHARGING";
			else if (ps.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Offline)
				resourceId = "STATUS_ON_BATTERY";
			else
			{
				bool isCharging = (ps.BatteryChargeStatus & BatteryChargeStatus.Charging) != 0;
				bool isFull = ps.BatteryLifePercent >= 0.99;

				if (isFull && !isCharging) resourceId = "STATUS_FULLY_CHARGED";
				else if (isCharging) resourceId = "STATUS_ON_AC_CHARGING";
				else resourceId = "STATUS_ON_AC_NOT_CHARGING";
			}
			if (resourceId != null)
			{
				if (StatusText.Text != sr.SuitableResource (resourceId))
				StatusText.Text = sr.SuitableResource (resourceId);
			}
		}
		private void UserControl_Loaded (object sender, RoutedEventArgs e)
		{
			InitPanel ();
			SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
			SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
			//Tile.PowerHost.PowerChanged -= PowerHost_PowerChanged;
			//Tile.PowerHost.PowerChanged += PowerHost_PowerChanged;
			powerUpdateTimer.Tick -= PowerUpdateTimer_Tick;
			powerUpdateTimer.Tick += PowerUpdateTimer_Tick;
			powerUpdateTimer?.Start ();
		}
		private float lastPowerPercent = -1;
		private void PowerUpdateTimer_Tick (object sender, EventArgs e)
		{
			powerUpdateTimer.Interval = TimeSpan.FromSeconds (random.Next (100, 500) * 0.01);
			var nowPercent = SystemInformation.PowerStatus.BatteryLifePercent;
			if (lastPowerPercent != nowPercent)
			{
				lastPowerPercent = nowPercent;
				UpdateCurrentVolume ();
			}
		}
		private void Watcher_EventArrived (object sender, EventArrivedEventArgs e)
		{
			Dispatcher.BeginInvoke (new Action (() =>
			{
				UpdateCurrentVolume ();
			}));
		}
		private void PowerHost_PowerChanged (object sender, EventArgs e)
		{
			UpdateStatusIcon ();
			UpdateCurrentVolume ();
			UpdateStatusText ();
		}
		private void UserControl_Unloaded (object sender, RoutedEventArgs e)
		{
			SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
			//Tile.PowerHost.PowerChanged -= PowerHost_PowerChanged;
			powerUpdateTimer.Tick -= PowerUpdateTimer_Tick;
			powerUpdateTimer?.Stop ();
		}
		private void UserControl_SizeChanged (object sender, SizeChangedEventArgs e)
		{
			if (TextPart == null) return;
			double fixedWidth = 40 + 10 + 7 * 2;
			double availableWidth = this.ActualWidth - fixedWidth;
			if (availableWidth > 0) TextPart.MaxWidth = availableWidth;
			else TextPart.MaxWidth = 0;
		}
		public void Dispose ()
		{
			powerUpdateTimer?.Stop ();
		}
	}
}

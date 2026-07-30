using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Sidebar;

namespace ClockTile
{
	public class Tile: TileBase
	{
		public static ITileManifest TileManifest { get; private set; }
		public static IProgramFolder TileFolder { get; private set; }
		public static IProgramFolder TileUserFolder { get; private set; }
		public static TileOptions Options { get; private set; }
		public static ISidebarFeatures SidebarFeatures { get; private set; }
		public static ITileBase TileInstance { get; private set; }
		private TilePanel tilePanel = null;
		private FlyoutPanel flyoutPanel = null;
		private OptionsPanel optionsPanel = null;
		public override void OnInitialize ()
		{
			Region?.StringResources?.CleanRedundantValues ();
			UserRegion?.StringResources?.CleanRedundantValues ();
			TileInstance = this;
			TileFolder = this.Region;
			TileManifest = this.Manifest;
			TileUserFolder = this.UserRegion;
			Options = new TileOptions (this.Config);
			SidebarFeatures = this.Features;
			var container = TileUI as Panel;
			if (container == null) return;
			(tilePanel?.Parent as Panel)?.Children?.Clear ();
			container.Children.Add (tilePanel = new TilePanel ());
			FlyoutInit += OnFlyoutInit;
			PropertiesInit += OnOptionsFormInit;
			PropertiesClosed += OnPropertiesFormClosed;
			Options.PropertyChanged += Options_PropertyChanged;
			StartAlarmTimer ();
			oneTimeAlarmTriggered = false;
			lastDailyAlarmDate = DateTime.UtcNow.Date.AddDays (-1);                                         // 如果一次性闹钟时间已过且未触发，但 EnableAlarm 仍为 true，可立即触发（可选）
			Utilities.ReleaseLargeResourcesAsync ();
		}
		private void Options_PropertyChanged (object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case "EnableAlarm":
				case "AlarmTime":
				case "AlarmEveryDay":
					ResetAlarmState ();
					break;
			}
		}
		private void OnPropertiesFormClosed (object sender, EventArgs e)
		{
			try
			{
				(optionsPanel?.Parent as Panel)?.Children?.Clear ();
				PropertiesClickOK -= optionsPanel.OkButton_Click;
				optionsPanel = null;
			}
			catch { }
		}
		private void OnFlyoutInit (object sender, FlyoutAboutEventArgs e)
		{
			var flyoutContent = e.FlyoutUI as Panel;
			if (flyoutContent == null) return;
			flyoutContent.Children.Clear ();
			(flyoutPanel?.Parent as Panel)?.Children?.Clear ();
			flyoutContent.Children.Add (flyoutPanel = flyoutPanel ?? new FlyoutPanel ());
		}
		private void OnOptionsFormInit (object sender, PropertiesAboutEventArgs e)
		{
			var propWnd = e.PropertiesWindow as Window;
			if (propWnd != null)
			{
				const double clientWidth = 350;
				const double clientHeight = 371.0;
				bool isResizable = (propWnd.ResizeMode == ResizeMode.CanResize || propWnd.ResizeMode == ResizeMode.CanResizeWithGrip);
				double borderWidth = isResizable
					? SystemParameters.ResizeFrameVerticalBorderWidth   // 可调整边框宽度
					: SystemParameters.BorderWidth;                    // 固定边框宽度
				double topBorderHeight = isResizable
					? SystemParameters.ResizeFrameHorizontalBorderHeight // 顶部可调整边框高度
					: SystemParameters.BorderWidth;                     // 顶部固定边框高度
				double captionHeight = SystemParameters.WindowCaptionHeight;
				double totalWidth = clientWidth + 2 * borderWidth;
				double totalHeight = clientHeight + topBorderHeight + captionHeight;
				propWnd.Width = totalWidth;
				propWnd.Height = totalHeight;
			}
			var propContent = e.PropertiesContent as Panel;
			if (propContent == null) return;
			propContent?.Children?.Clear ();
			(optionsPanel?.Parent as Panel)?.Children?.Clear ();
			propContent.Children.Add (optionsPanel = optionsPanel ?? new OptionsPanel ());
			PropertiesClickOK -= optionsPanel.OkButton_Click;
			PropertiesClickOK += optionsPanel.OkButton_Click;
		}
		public override void OnDestroy ()
		{
			StopAlarmTimer ();
			StopAlarmPlayback ();
			FlyoutInit -= OnFlyoutInit;
			PropertiesInit -= OnOptionsFormInit;
			PropertiesClosed -= OnPropertiesFormClosed;
			Options.PropertyChanged -= Options_PropertyChanged;
			(tilePanel?.Parent as Panel)?.Children?.Clear ();
			tilePanel?.Dispose ();
			(flyoutPanel?.Parent as Panel)?.Children?.Clear ();
			flyoutPanel = null;
			optionsPanel = null;
			ImageSourceManager.ClearCache ();
			TileInstance = null;
			TileManifest = null;
			TileFolder = null;
			TileUserFolder = null;
			Options = null;
			SidebarFeatures = null;
		}
		#region AlarmPart
		private DispatcherTimer alarmTimer;
		private MediaPlayer alarmPlayer;
		private DateTime lastDailyAlarmDate;
		private bool oneTimeAlarmTriggered; 
		private bool isAlarmPlaying;
		private int playCount;
		private int targetPlayCount;
		private double audioDurationSeconds;
		private bool _hasNotifiedForCurrentAlarm = false;
		private void StartAlarmTimer ()
		{
			if (alarmTimer == null)
			{
				alarmTimer = new DispatcherTimer ();
				alarmTimer.Interval = TimeSpan.FromSeconds (1);
				alarmTimer.Tick += AlarmTimer_Tick;
			}
			alarmTimer.Start ();
		}
		private void StopAlarmTimer ()
		{
			alarmTimer?.Stop ();
		}
		private void AlarmTimer_Tick (object sender, EventArgs e)
		{
			if (Options == null) return;
			if (!Options.EnableAlarm) return;
			if (isAlarmPlaying) return;
			DateTime nowUtc = DateTime.UtcNow;
			if (Options.AlarmEveryDay)
			{
				if (lastDailyAlarmDate.Date < nowUtc.Date && nowUtc.TimeOfDay >= Options.AlarmTime.TimeOfDay)
				{
					TriggerAlarm ();
					lastDailyAlarmDate = nowUtc.Date; 
				}
			}
			else
			{
				if (!oneTimeAlarmTriggered && nowUtc >= Options.AlarmTime)
				{
					TriggerAlarm ();
					oneTimeAlarmTriggered = true;
					Options.EnableAlarm = false; 
				}
			}
		}
		private void TriggerAlarm ()
		{
			if (isAlarmPlaying) return;
			string ringRelativePath = Options.AlarmRing;
			if (string.IsNullOrEmpty (ringRelativePath))
				return;
			string soundFolder = Path.Combine (TileFolder.FolderPath, "Sounds");
			string fullPath = Path.Combine (soundFolder, ringRelativePath);
			if (!File.Exists (fullPath))
				return;

			if (!_hasNotifiedForCurrentAlarm)
			{
				_hasNotifiedForCurrentAlarm = true;
				string displayName = TileFolder.StringResources.SuitableResource (Manifest.Properties.DisplayName, Manifest.Properties.DisplayName) ?? "闹钟";
				string message = Options.AlarmMessage;
				PushNotification (60000, message, null, System.Windows.Forms.ToolTipIcon.Info);
			}
			alarmPlayer = new MediaPlayer ();
			alarmPlayer.Volume = Math.Max (0, Math.Min (1, Options.AlarmVolume));
			alarmPlayer.MediaOpened += AlarmPlayer_MediaOpened;
			alarmPlayer.MediaEnded += AlarmPlayer_MediaEnded;
			alarmPlayer.MediaFailed += AlarmPlayer_MediaFailed;
			alarmPlayer.Open (new Uri (fullPath, UriKind.Absolute));
		}
		private void AlarmPlayer_MediaOpened (object sender, EventArgs e)
		{
			if (alarmPlayer == null) return;
			audioDurationSeconds = alarmPlayer.NaturalDuration.HasTimeSpan
				? alarmPlayer.NaturalDuration.TimeSpan.TotalSeconds
				: 0;
			if (audioDurationSeconds > 0)
			{
				if (audioDurationSeconds >= 30)
					targetPlayCount = 1;
				else
				{
					int times = (int)Math.Ceiling (30.0 / audioDurationSeconds);
					int maxTimes = (int)Math.Floor (60.0 / audioDurationSeconds);
					targetPlayCount = Math.Min (times, maxTimes);
					if (targetPlayCount < 1) targetPlayCount = 1;
				}
			}
			else
			{
				targetPlayCount = 1;
			}
			playCount = 0;
			isAlarmPlaying = true;
			alarmPlayer.Play ();
		}
		private void AlarmPlayer_MediaEnded (object sender, EventArgs e)
		{
			playCount++;
			if (playCount < targetPlayCount)
			{
				alarmPlayer.Position = TimeSpan.Zero;
				alarmPlayer.Play ();
			}
			else
			{
				StopAlarmPlayback ();
			}
		}
		private void AlarmPlayer_MediaFailed (object sender, ExceptionEventArgs e)
		{
			StopAlarmPlayback ();
		}
		private void StopAlarmPlayback ()
		{
			if (alarmPlayer != null)
			{
				alarmPlayer.Stop ();
				alarmPlayer.Close ();
				alarmPlayer.MediaOpened -= AlarmPlayer_MediaOpened;
				alarmPlayer.MediaEnded -= AlarmPlayer_MediaEnded;
				alarmPlayer.MediaFailed -= AlarmPlayer_MediaFailed;
				alarmPlayer = null;
			}
			isAlarmPlaying = false;
			_hasNotifiedForCurrentAlarm = false;
		}
		private void ResetAlarmState ()
		{
			oneTimeAlarmTriggered = false;
			lastDailyAlarmDate = DateTime.MinValue;
			_hasNotifiedForCurrentAlarm = false;
		}
		#endregion
		private bool PushNotification (int timeout, string text, string title = null, System.Windows.Forms.ToolTipIcon icon = System.Windows.Forms.ToolTipIcon.None)
		{
			var req = new SidebarRequest (this);
			req.RequestName = "Notification";
			req.RequestDatas = new NotifyIconNotification {
				Timeout = timeout,
				Title = title,
				Content = text,
				Icon = icon
			};
			return Features.Request (req);
		}
		public override bool OnResponse (ITileResponse resp)
		{
			if (resp.ResponseSource.NEquals ("Sidebar"))
			{
				if (resp.ResponseName.NEquals ("NotificationClick"))
				{
					if (isAlarmPlaying)
					{
						StopAlarmPlayback ();
					}
					return true;
				}
			}
			return false;
		}
	}
}
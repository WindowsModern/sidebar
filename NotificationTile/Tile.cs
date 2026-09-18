using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sidebar;

namespace WindowsModern.NotificationHistoryTile
{
	public class Tile: TileBase
	{
		internal NotificationPipeClient Client { get; private set; }
		internal NotificationHistory History { get; private set; }
		private TilePanel tilePanel = null;
		private FlyoutPanel flyoutPanel = null;
		private HashSet<Guid> displayedId = new HashSet<Guid> ();
		public override void OnInitialize ()
		{
			History = new NotificationHistory (UserRegion.FolderPath);
			Client = new NotificationPipeClient ();
			Client.NotificationReceived += Client_NotificationReceived;
			Client.Start ();
			SetNotificationServer ("-rreg");
			Region?.StringResources?.CleanRedundantValues ();
			tilePanel = new TilePanel ();
			tilePanel.History = History;
			tilePanel.NotificationClicked += TilePanel_NotificationClicked;
			var panel = TileUI as Panel;
			panel.Children.Add (tilePanel);
			FlyoutInit += Tile_FlyoutInit;
			FlyoutClosed += Tile_FlyoutClosed;
		}
		private void Tile_FlyoutClosed (object sender, EventArgs e)
		{
			flyoutPanel.NotificationClicked -= TilePanel_NotificationClicked;
			(flyoutPanel?.Parent as Panel)?.Children?.Clear ();
		}
		private void Tile_FlyoutInit (object sender, FlyoutAboutEventArgs e)
		{
			(flyoutPanel?.Parent as Panel)?.Children?.Clear ();
			if (flyoutPanel == null)
			{
				flyoutPanel = new FlyoutPanel ();
				flyoutPanel.GotoRulesAlertButton.Content = Region.StringResources.SuitableResource ("TILE_FLYOUT_RULES");
				flyoutPanel.DataContext = History;
			}
			e.ClientArea.Children.Add (flyoutPanel);
			flyoutPanel.NotificationClicked -= TilePanel_NotificationClicked;
			flyoutPanel.NotificationClicked += TilePanel_NotificationClicked;
		}
		private void TilePanel_NotificationClicked (object sender, NotificationStoreData data)
		{
			if (displayedId.Contains (data.Id)) return;
			displayedId.Add (data.Id);
			data.IsRead = true;
			ImageSource imgsrc = null;
			try
			{
				var bm = new BitmapImage (new Uri (Path.Combine (History.BaseImageDir, data.IconFileName)));
				if (bm.CanFreeze) bm.Freeze ();
				imgsrc = bm;
			}
			catch { }
			var nin2 = new NotifyIconNotification2 {
				Content = data.Info,
				Title = data.InfoTitle,
				Timeout = data.Timeout,
				IconImage = imgsrc
			};
			Features.Request (new SidebarRequest (this) {
				RequestName = "SidebarNotification",
				RequestDatas = nin2,
				TransferDatas = data
			});
		}
		private void Client_NotificationReceived (object sender, NotificationData notification)
		{
			if (notification == null) return;
			if (!(notification?.IsBalloonNotification ?? false)) return;
			if (notification.Message != NotifyIconMessage.Add && notification.Message != NotifyIconMessage.Modify) return;
			RunOnUIThread (() => {
				var stored = History?.Add (notification);
				
				NotificationDataPair? pair = null;
				var nin2 = new NotifyIconNotification2 {
					Content = notification.Info,
					Title = notification.InfoTitle,
					Timeout = (int)(notification.Timeout ?? 30),
					IconImage = IconHelper.FromHIcon (notification.HIcon)
				};

				if (stored != null)
				{
					pair = new NotificationDataPair {
						DataFromObject = notification,
						DataFromStore = stored
					};
				}

				if (pair != null)
				{
					Features.Request (new SidebarRequest (this) {
						RequestName = "SidebarNotification",
						RequestDatas = nin2,
						TransferDatas = pair.Value
					});
				}
				else
				{
					Features.Request (new SidebarRequest (this) {
						RequestName = "SidebarNotification",
						RequestDatas = nin2,
						TransferDatas = notification
					});
				}
			});
		}
		private static void RunOnUIThread (Action action)
		{
			if (action == null) return;
			var dispatcher = System.Windows.Application.Current?.Dispatcher;
			if (dispatcher == null || dispatcher.CheckAccess ())
			{
				action ();
			}
			else
			{
				dispatcher.BeginInvoke (action, System.Windows.Threading.DispatcherPriority.Normal);
			}
		}
		public override void OnDestroy ()
		{
			tilePanel.History = null;
			if (flyoutPanel != null) flyoutPanel.DataContext = null;
			tilePanel.NotificationClicked -= TilePanel_NotificationClicked;
			(tilePanel?.Parent as Panel)?.Children?.Clear ();
			(flyoutPanel?.Parent as Panel)?.Children?.Clear ();
			SetNotificationServer ("-ruregex");
			Client?.Stop ();
			Client?.Dispose ();
			Client = null;
			History?.Dispose ();
			History = null;
		}
		private readonly PropertyInfo IdPropertyInfo = typeof (NotifyIconNotification2).GetProperty ("Id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
		public override bool OnRequest (ITileRequest req)
		{
			if (req.RequestSource.NEquals ("Sidebar"))
			{
				if (req.RequestName.NEquals ("InternalNotificationAddHistoryInternal"))
				{
					if (req.RequestDatas is NotifyIconNotification2)
					{
						var nin2 = req.RequestDatas as NotifyIconNotification2;
						var stored = History.Add (nin2); 
						IdPropertyInfo.SetValue (nin2, stored.Id, null);
						return true;
					}
				}
				if (req.RequestName.NEquals ("InternalNotificationSetRead"))
				{
					if (req.RequestDatas is NotifyIconNotification2)
					{
						var nin2 = req.RequestDatas as NotifyIconNotification2;
						var id = (Guid)IdPropertyInfo.GetValue (nin2, null);
						foreach (var n in History)
						{
							if (n.Id == id)
							{
								n.IsRead = true;
								return true;
							}
						}
					}
				}
			}
			return false;
		}
		public override bool OnResponse (ITileResponse resp)
		{
			if (resp.ResponseSource.NEquals ("Sidebar"))
			{
				NotificationDataPair ?pair = null;
				if (resp.TransferDatas is NotificationDataPair)
					pair = (NotificationDataPair)resp.TransferDatas;
				NotificationStoreData storedata = null;
				if (resp.TransferDatas is NotificationStoreData)
					storedata = resp.TransferDatas as NotificationStoreData;
				NotificationData ndata = null;
				if (resp.TransferDatas is NotificationData)
					ndata = resp.TransferDatas as NotificationData;
				switch (resp.ResponseName)
				{
					case "SidebarNotificationClick":
						{
							if (pair != null)
							{
								pair.Value.DataFromStore.IsRead = true;
								var n = pair.Value.DataFromObject;
								var result = Native.PostMessage (n.HWnd, n.CallbackMessage, (IntPtr)n.ID, (IntPtr)NotifyIconNotification.BalloonUserClick);
							}
							if (storedata != null) storedata.IsRead = true;
							if (ndata != null)
							{
								var n = ndata;
								n.IsRead = true;
								var result = Native.PostMessage (n.HWnd, n.CallbackMessage, (IntPtr)n.ID, (IntPtr)NotifyIconNotification.BalloonUserClick);
							}
						}
						return true;
						break;
					case "SidebarNotificationCloseClick":
						{
							if (pair != null)
							{
								pair.Value.DataFromStore.IsRead = true;
								var n = pair.Value.DataFromObject;
								var result = Native.PostMessage (n.HWnd, n.CallbackMessage, (IntPtr)n.ID, (IntPtr)NotifyIconNotification.BalloonDismiss);
							}
							if (storedata != null) storedata.IsRead = true;
							if (ndata != null)
							{
								var n = ndata;
								n.IsRead = true;
								var result = Native.PostMessage (n.HWnd, n.CallbackMessage, (IntPtr)n.ID, (IntPtr)NotifyIconNotification.BalloonDismiss);
							}
						}
						return true;
						break;
					case "SidebarNotificationClose":
						{
							if (pair != null)
							{
								if (!pair.Value.DataFromStore.IsRead)
								{
									var n = pair.Value.DataFromObject;
									var result = Native.PostMessage (n.HWnd, n.CallbackMessage, (IntPtr)n.ID, (IntPtr)NotifyIconNotification.BalloonTimeout);
								}
							}
							if (ndata != null)
							{
								var n = ndata;
								if (!n.IsRead)
								{
									var result = Native.PostMessage (n.HWnd, n.CallbackMessage, (IntPtr)n.ID, (IntPtr)NotifyIconNotification.BalloonTimeout);
								}
							}
							if (storedata != null)
							{
								displayedId.Remove (storedata.Id);
							}
						}
						return true;
						break;
				}
			}
			return false;
		}
		public bool SetNotificationServer (string args)
		{
			try
			{
				var folder = "Unknown";
				var arch = ProcessorDetector.GetCurrentArchitecture ();
				switch (arch)
				{
					case Sidebar.ProcessorArchitecture.ARM:
						folder = "ARM"; break;
					case Sidebar.ProcessorArchitecture.ARM64:
					case Sidebar.ProcessorArchitecture.IA64:
					case Sidebar.ProcessorArchitecture.Neutral:
					case Sidebar.ProcessorArchitecture.Unknown:
						break;
					case Sidebar.ProcessorArchitecture.X64:
						folder = "x64"; break;
					case Sidebar.ProcessorArchitecture.X86:
						folder = "x86"; break;
				}
				var psi = new ProcessStartInfo (Path.Combine (Region.FolderPath, "NotificationServer", folder, "NotifyServer.exe"), args);
				psi.WindowStyle = ProcessWindowStyle.Hidden;
				psi.CreateNoWindow = true;
				return Process.Start (psi) != null;
			}
			catch
			{
				return false;
			}
		}
	}
}

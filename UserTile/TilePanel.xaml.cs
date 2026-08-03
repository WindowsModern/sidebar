using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Cauldron;

namespace WindowsModern.UserTile
{
	/// <summary>
	/// TilePanel.xaml 的交互逻辑
	/// </summary>
	public partial class TilePanel: UserControl
	{
		private BitmapImage userImage = null;
		private readonly HashSet<UIElement> _monitoredElements = new HashSet<UIElement> ();
		public TilePanel ()
		{
			InitializeComponent ();
			InitStrings ();
			_updateTimer = new DispatcherTimer ();
		}
		private void InitStrings ()
		{
			var sr = Tile.Instance.Region.StringResources;
			WelcomeText.Text = sr.SuitableResource ("TILE_WELCOME", "Welcome");
		}
		public void UpdateUserInfo ()
		{
			try
			{
				var imgPath = UserUtils.GetAccountPicturePath ();
				BitmapImage img = new BitmapImage ();
				img.BeginInit ();
				if (string.IsNullOrWhiteSpace (imgPath))
				{
					byte [] pictureBytes = UserInformation.AccountPicture;
					if (pictureBytes != null && pictureBytes.Length > 0)
					{
						using (MemoryStream stream = new MemoryStream (pictureBytes))
						{
							img.StreamSource = stream;
							img.CacheOption = BitmapCacheOption.OnLoad;
						}
					}
				}
				else
				{
					img.UriSource = new Uri (imgPath, UriKind.RelativeOrAbsolute);
				}
				img.EndInit ();
				if (img.CanFreeze) img.Freeze ();
				userImage = img;
			}
			catch (Exception e)
			{
				try
				{
					var imgPath = UserUtils.GetAccountPicturePath ();
					BitmapImage img = new BitmapImage ();
					img.BeginInit ();
					var currUser = System.Security.Principal.WindowsIdentity.GetCurrent ();
					var isguest = currUser.IsGuest;
					var userfilename = isguest ? "guest" : "user";
					var dfltImg = System.IO.Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.CommonApplicationData), @"Microsoft\User Account Pictures\" + (isguest ? "guest" : UserUtils.GetUserName ()) + ".bmp");
					if (!File.Exists (dfltImg))
					{
						dfltImg = System.IO.Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.CommonApplicationData), @"Microsoft\User Account Pictures\" + (isguest ? "guest" : UserUtils.GetUserName ()) + ".png");
					}
					if (!File.Exists (dfltImg))
					{
						dfltImg = System.IO.Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.CommonApplicationData), @"Microsoft\User Account Pictures\" + (isguest ? "guest" : UserUtils.GetUserName ()) + ".jpg");
					}
					if (!File.Exists (dfltImg))
					{
						dfltImg = System.IO.Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.CommonApplicationData), @"Microsoft\User Account Pictures\" + userfilename + ".png");
					}
					if (!File.Exists (dfltImg))
					{
						dfltImg = System.IO.Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.CommonApplicationData), @"Microsoft\User Account Pictures\" + userfilename + ".bmp");
					}
					if (!File.Exists (dfltImg))
					{
						dfltImg = System.IO.Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.CommonApplicationData), @"Microsoft\User Account Pictures\" + userfilename + ".bmp");
					}
					if (!File.Exists (dfltImg))
					{
						dfltImg = System.IO.Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.CommonApplicationData), @"Microsoft\User Account Pictures\" + userfilename + ".jpg");
					}
					img.UriSource = new Uri (dfltImg, UriKind.RelativeOrAbsolute);
					img.EndInit ();
					if (img.CanFreeze) img.Freeze ();
					userImage = img;
				}
				catch { }
			}
		}
		public void UpdateUserDisplay ()
		{
			UserImage.Source = userImage;
			UserName.Text = UserUtils.GetUserName ();
		}
		public void UpdateUserPanel ()
		{
			UpdateUserInfo ();
			UpdateUserDisplay ();
		}
		private DispatcherTimer _updateTimer;
		private string _cachedUserName;          // 上次显示的用户名
		private string _cachedImageIdentifier;   // 上次显示的图片标识
		private string _imageIdentifier;         // 本次加载的图片标识（若成功加载则赋值）
		private void UserControl_Loaded (object sender, RoutedEventArgs e)
		{
			AttachOpacityMonitor (WelcomePart);
			AttachOpacityMonitor (CommonPanel);
			UpdateUserPanelDiffer ();
			_updateTimer.Interval = TimeSpan.FromMinutes (1);
			_updateTimer.Tick -= UpdateTimer_Tick;
			_updateTimer.Tick += UpdateTimer_Tick;
			_updateTimer.Start ();
			StartIntroAnimation ();
		}
		private void UpdateTimer_Tick (object sender, EventArgs e)
		{
			UpdateUserPanelDiffer ();
		}
		/// <summary>
		/// 加载用户头像信息，并更新 _imageIdentifier（仅当成功加载时）
		/// </summary>
		public void UpdateUserInfoDiffer ()
		{
			bool loaded = false;
			string newIdentifier = null;
			try
			{
				var imgPath = UserUtils.GetAccountPicturePath ();
				BitmapImage img = new BitmapImage ();
				img.BeginInit ();
				if (string.IsNullOrWhiteSpace (imgPath))
				{
					byte [] pictureBytes = UserInformation.AccountPicture;
					if (pictureBytes != null && pictureBytes.Length > 0)
					{
						using (MemoryStream stream = new MemoryStream (pictureBytes))
						{
							img.StreamSource = stream;
							img.CacheOption = BitmapCacheOption.OnLoad;
						}
						newIdentifier = ComputeHash (pictureBytes);
						loaded = true;
					}
				}
				else
				{
					img.UriSource = new Uri (imgPath, UriKind.RelativeOrAbsolute);
					newIdentifier = imgPath + "_" + File.GetLastWriteTime (imgPath).Ticks.ToString ();
					loaded = true;
				}

				img.EndInit ();
				if (img.CanFreeze) img.Freeze ();
				userImage = img;
			}
			catch (Exception)
			{
				try
				{
					var currUser = System.Security.Principal.WindowsIdentity.GetCurrent ();
					bool isguest = currUser.IsGuest;
					string userfilename = isguest ? "guest" : "user";
					string dfltImg = null;
					string [] extensions = { ".bmp", ".png", ".jpg" };
					string basePath = System.IO.Path.Combine (
						Environment.GetFolderPath (Environment.SpecialFolder.CommonApplicationData),
						@"Microsoft\User Account Pictures\");
					string [] names = isguest ? new [] { "guest", "user" } : new [] { UserUtils.GetUserName (), "user" };
					foreach (var name in names)
					{
						foreach (var ext in extensions)
						{
							string candidate = System.IO.Path.Combine (basePath, name + ext);
							if (File.Exists (candidate))
							{
								dfltImg = candidate;
								break;
							}
						}
						if (dfltImg != null) break;
					}
					if (dfltImg != null)
					{
						BitmapImage img = new BitmapImage ();
						img.BeginInit ();
						img.UriSource = new Uri (dfltImg, UriKind.RelativeOrAbsolute);
						img.EndInit ();
						if (img.CanFreeze) img.Freeze ();
						userImage = img;
						newIdentifier = dfltImg + "_" + File.GetLastWriteTime (dfltImg).Ticks.ToString ();
						loaded = true;
					}
				}
				catch { }
			}
			if (loaded && userImage != null)
			{
				_imageIdentifier = newIdentifier;
			}
		}
		public void UpdateUserDisplayDiffer ()
		{
			string currentUserName = UserUtils.GetUserName ();
			if (currentUserName != _cachedUserName)
			{
				UserName.Text = currentUserName;
				WelcomeUser.Text = currentUserName;
				if (flyoutPanel?.UserName != null) flyoutPanel.UserName.Text = currentUserName;
				_cachedUserName = currentUserName;
			}
			if (_imageIdentifier != null && _imageIdentifier != _cachedImageIdentifier)
			{
				if (userImage != null)
				{
					UserImage.Source = userImage;
					WelcomeUserImage.Source = userImage;
					if (flyoutPanel?.UserImage != null) flyoutPanel.UserImage.Source = userImage;
				}
				_cachedImageIdentifier = _imageIdentifier; 
			}
		}
		public void UpdateUserPanelDiffer ()
		{
			UpdateUserInfoDiffer ();
			UpdateUserDisplayDiffer ();
		}
		private string ComputeHash (byte [] data)
		{
			using (var sha1 = SHA1.Create ())
			{
				byte [] hash = sha1.ComputeHash (data);
				return Convert.ToBase64String (hash);
			}
		}
		private void UserControl_Unloaded (object sender, RoutedEventArgs e)
		{
			StopIntroAnimation ();
			if (_updateTimer != null)
			{
				_updateTimer.Tick -= UpdateTimer_Tick;
				_updateTimer.Stop ();
			}
			DetachAllOpacityMonitors ();
			flyoutPanel = null;
		}
		/// <summary>
		/// 为指定的 UIElement 附加 Opacity 监视。
		/// 当 Opacity <= 0 时 Visibility = Collapsed，否则 Visible。
		/// </summary>
		public void AttachOpacityMonitor (UIElement element)
		{
			if (element == null) return;
			if (_monitoredElements.Contains (element)) return; // 避免重复订阅
			var descriptor = DependencyPropertyDescriptor.FromProperty (
				UIElement.OpacityProperty, typeof (UIElement));
			descriptor.AddValueChanged (element, OnOpacityChanged);
			_monitoredElements.Add (element);
			OnOpacityChanged (element, EventArgs.Empty);
		}
		/// <summary>
		/// 解除指定元素的 Opacity 监视。
		/// </summary>
		public void DetachOpacityMonitor (UIElement element)
		{
			if (element == null) return;
			if (!_monitoredElements.Contains (element)) return;
			var descriptor = DependencyPropertyDescriptor.FromProperty (
				UIElement.OpacityProperty, typeof (UIElement));
			descriptor.RemoveValueChanged (element, OnOpacityChanged);
			_monitoredElements.Remove (element);
		}
		/// <summary>
		/// 解除所有已附加的 Opacity 监视。
		/// </summary>
		public void DetachAllOpacityMonitors ()
		{
			// 复制一份，避免在遍历时修改集合
			var elements = new List<UIElement> (_monitoredElements);
			foreach (var element in elements)
			{
				DetachOpacityMonitor (element);
			}
		}
		/// <summary>
		/// Opacity 变化时的处理函数（命名方法，非 lambda）
		/// </summary>
		private void OnOpacityChanged (object sender, EventArgs e)
		{
			var element = sender as UIElement;
			if (element == null) return;
			element.Visibility = (element.Opacity <= 0) ? Visibility.Collapsed : Visibility.Visible;
		}
		private Storyboard _introStoryboard;
		private DispatcherTimer _delayTimer;
		/// <summary>
		/// 开始入场动画（可打断，重新调用会重置）
		/// </summary>
		private void StartIntroAnimation ()
		{
			StopIntroAnimation ();   // 停止当前所有动画

			// 重置两个面板为隐藏状态（Opacity=0，监视器会自动将其 Collapsed）
			WelcomePart.Opacity = 0;
			WelcomePart.Visibility = Visibility.Collapsed;
			CommonPanel.Opacity = 0;
			CommonPanel.Visibility = Visibility.Collapsed;

			AnimateWelcomeIn ();
		}
		/// <summary>
		/// 停止所有正在进行的动画和延迟定时器
		/// </summary>
		private void StopIntroAnimation ()
		{
			if (_introStoryboard != null)
			{
				_introStoryboard.Stop ();
				_introStoryboard = null;
			}
			if (_delayTimer != null)
			{
				_delayTimer.Tick -= OnDelayTick;
				_delayTimer.Stop ();
				_delayTimer = null;
			}
		}
		/// <summary>
		/// 动画显示 WelcomePart（0→1，0.5秒）
		/// </summary>
		private void AnimateWelcomeIn ()
		{
			_introStoryboard = new Storyboard ();
			var anim = new DoubleAnimation (0, 1, TimeSpan.FromSeconds (0.5));
			Storyboard.SetTarget (anim, WelcomePart);
			Storyboard.SetTargetProperty (anim, new PropertyPath (UIElement.OpacityProperty));
			_introStoryboard.Children.Add (anim);
			_introStoryboard.Completed += OnWelcomeInCompleted;
			_introStoryboard.Begin ();
			CommonPanel.Opacity = 0;
		}
		private void OnWelcomeInCompleted (object sender, EventArgs e)
		{
			_introStoryboard = null;   // 已完成，释放引用

			// 启动 5 秒延迟定时器
			_delayTimer = new DispatcherTimer ();
			_delayTimer.Interval = TimeSpan.FromSeconds (5);
			_delayTimer.Tick += OnDelayTick;
			_delayTimer.Start ();
		}
		private void OnDelayTick (object sender, EventArgs e)
		{
			_delayTimer.Stop ();
			_delayTimer.Tick -= OnDelayTick;
			_delayTimer = null;
			AnimateWelcomeOut ();
		}
		/// <summary>
		/// 动画隐藏 WelcomePart（1→0，0.5秒）
		/// </summary>
		private void AnimateWelcomeOut ()
		{
			_introStoryboard = new Storyboard ();
			var anim = new DoubleAnimation (1, 0, TimeSpan.FromSeconds (0.5));
			Storyboard.SetTarget (anim, WelcomePart);
			Storyboard.SetTargetProperty (anim, new PropertyPath (UIElement.OpacityProperty));
			_introStoryboard.Children.Add (anim);
			_introStoryboard.Completed += OnWelcomeOutCompleted;
			_introStoryboard.Begin ();
		}
		private void OnWelcomeOutCompleted (object sender, EventArgs e)
		{
			_introStoryboard = null;
			AnimateCommonIn ();
		}
		/// <summary>
		/// 动画显示 CommonPanel（0→1，0.5秒）
		/// </summary>
		private void AnimateCommonIn ()
		{
			_introStoryboard = new Storyboard ();
			var anim = new DoubleAnimation (0, 1, TimeSpan.FromSeconds (0.5));
			Storyboard.SetTarget (anim, CommonPanel);
			Storyboard.SetTargetProperty (anim, new PropertyPath (UIElement.OpacityProperty));
			_introStoryboard.Children.Add (anim);
			_introStoryboard.Completed += (s, e) =>
			{
				_introStoryboard = null;   // 序列结束
			};
			_introStoryboard.Begin ();
		}
		private FlyoutPanel flyoutPanel;
		public void OnFlyoutInit (Sidebar.FlyoutAboutEventArgs e)
		{
			if (flyoutPanel == null) flyoutPanel = new FlyoutPanel ();
			(flyoutPanel?.Parent as Panel)?.Children?.Clear ();
			e.ClientArea.Children.Add (flyoutPanel);
			flyoutPanel.UserImage.Source = userImage;
			flyoutPanel.UserName.Text = UserName.Text;
		}
		public void OnFlyoutClosed ()
		{
			(flyoutPanel?.Parent as Panel)?.Children?.Clear ();
		}
	}
}

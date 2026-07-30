using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Sidebar
{
	/// <summary>
	/// SettingsWnd.xaml 的交互逻辑
	/// </summary>
	public partial class SettingsWnd: Window
	{
		public SettingsWnd ()
		{
			InitializeComponent ();
			InitStrings ();
		}
		private void InitStrings ()
		{
			var sr = App.AppRoot.StringResources;
			TabGeneral.Header = sr.SuitableResource ("STORE_GENERAL");
			LabelCurrCache.Text = sr.SuitableResource ("STORE_CURRCACHE");
			ButtonClear.Content = sr.SuitableResource ("STORE_CLEAR");
			TabAbout.Header = sr.SuitableResource ("STORE_ABOUT");
			LabelAppTitle.Text = sr.SuitableResource ("STORE_INFO_TITLE");
			var version = Assembly.GetExecutingAssembly ().GetName ().Version;
			LabelVersion.Text = String.Format (sr.SuitableResource ("STORE_INFO_VERSION"), version.ToString ());
			LabelIntroduction.Text = String.Format (sr.SuitableResource ("STORE_INFO_INTRO"), sr.SuitableResource ("STORE_INFO_TITLE"));
			LabelDisclaimer.Text = sr.SuitableResource ("STORE_INFO_DISCLAIMER");
			LabelCopyright.Text = sr.SuitableResource ("STORE_INFO_COPYRIGHT");
			Title = sr.SuitableResource ("STORE_SETTINGS");
		}
		private void ButtonClear_Click (object sender, RoutedEventArgs e)
		{
			var sr = App.AppRoot.StringResources;
			try
			{
				var tempdir = System.IO.Path.Combine (App.AppData.FolderPath, "Temp");
				Directory.Delete (tempdir, true);
			}
			catch (Exception ex)
			{
				MessageBox.Show (ex.Message, sr.SuitableResource ("STORE_ERRORTITLE"), MessageBoxButton.OK, MessageBoxImage.Error);
			}
			finally
			{
				UpdateCacheDisplay ();
			}
		}
		private void TabControl_SelectionChanged (object sender, SelectionChangedEventArgs e)
		{
			return;
			if (AboutContent == null)
				return;

			var tab = ((TabControl)sender).SelectedItem as TabItem;

			if (tab == null)
				return;

			if (tab.Header.ToString () == "About")
			{
				AboutContent.Opacity = 0;

				var transform = new TranslateTransform (0, 15);
				AboutContent.RenderTransform = transform;

				var storyboard = new Storyboard ();

				var opacity = new DoubleAnimation {
					From = 0,
					To = 1,
					Duration = TimeSpan.FromMilliseconds (250)
				};

				var move = new DoubleAnimation {
					From = 15,
					To = 0,
					Duration = TimeSpan.FromMilliseconds (250),
					EasingFunction = new CubicEase {
						EasingMode = EasingMode.EaseOut
					}
				};

				Storyboard.SetTarget (opacity, AboutContent);
				Storyboard.SetTargetProperty (opacity,
					new PropertyPath ("Opacity"));
				Storyboard.SetTarget (move, AboutContent);
				Storyboard.SetTargetProperty (move,
					new PropertyPath ("(UIElement.RenderTransform).(TranslateTransform.Y)"));
				storyboard.Children.Add (opacity);
				storyboard.Children.Add (move);
				storyboard.Begin ();
			}
		}
		private void UpdateCacheDisplay ()
		{
			var tempDir = System.IO.Path.Combine (App.AppData.FolderPath, "Temp");
			long size = GetDirectorySize (tempDir);
			string sizeText = FormatSize (size);
			CurrCacheDisplay.Text = sizeText;
		}
		private long GetDirectorySize (string folderPath)
		{
			if (!Directory.Exists (folderPath))
				return 0;

			try
			{
				return Directory.GetFiles (folderPath, "*", SearchOption.AllDirectories)
								.Sum (f => new FileInfo (f).Length);
			}
			catch
			{
				// 如果无法访问某些文件，返回已成功计算的部分？
				// 简单起见，返回0或部分结果。这里为简化返回0。
				return 0;
			}
		}
		private string FormatSize (long bytes)
		{
			string [] sizes = { "B", "KB", "MB", "GB", "TB" };
			double len = bytes;
			int order = 0;
			while (len >= 1024 && order < sizes.Length - 1)
			{
				order++;
				len = len / 1024;
			}
			return $"{len:0.##} {sizes [order]}";
		}
		private void Window_Loaded (object sender, RoutedEventArgs e)
		{
			UpdateCacheDisplay ();
		}
		private void Window_Unloaded (object sender, RoutedEventArgs e)
		{
			App.ReleaseLargeResourcesAsync ();
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Sidebar
{
	/// <summary>
	/// ConfigWindow.xaml 的交互逻辑
	/// </summary>
	public partial class ConfigWindow: Window
	{
		public ConfigWindow ()
		{
			InitializeComponent ();
			InitStrings ();
			InitConfigValues ();
		}
		public void InitStrings ()
		{
			var sres = App.ProgramFolder.StringResources;
			LabelAutoRun.Text = sres.SuitableResource ("CONFIG_AUTORUN");
			LabelLock.Text = sres.SuitableResource ("CONFIG_LOCK");
			LabelAppBar.Text = sres.SuitableResource ("CONFIG_APPBAR");
			LabelTopmost.Text = sres.SuitableResource ("CONFIG_TOPMOST");
			LabelOverlap.Text = sres.SuitableResource ("CONFIG_OVERLAP");
			LabelLocation.Text = sres.SuitableResource ("CONFIG_SIDEBARLOC");
			LabelLocationLeft.Text = sres.SuitableResource ("CONFIG_LOCLEFT");
			LabelLocationRight.Text = sres.SuitableResource ("CONFIG_LOCRIGHT");
			LabelTheme.Text = sres.SuitableResource ("CONFIG_THEME");
			HeaderSettings.Header = sres.SuitableResource ("CONFIG_TITLE");
			HeaderAbout.Header = sres.SuitableResource ("ABOUT_TITLE");
			Title = sres.SuitableResource ("SIDEBAR_CONTEXTMENU_PROP");
			ButtonRefreshTheme.Content = sres.SuitableResource ("CONFIG_REFRESH");
			LabelScreen.Text = sres.SuitableResource ("CONFIG_SCREEN");
			GroupGeneral.Header = sres.SuitableResource ("CONFIG_GENERAL");
			GroupDisplay.Header = sres.SuitableResource ("CONFIG_APPEARANCE");
			LabelWidth.Text = sres.SuitableResource ("CONFIG_WIDTH");
			LabelAboutAppTitle.Text = sres.SuitableResource ("SIDEBAR_ABOUT_TITLE");
			var version = Assembly.GetExecutingAssembly ().GetName ().Version;
			LabelAboutVersion.Text = String.Format (sres.SuitableResource ("SIDEBAR_ABOUT_VERSION"), version.ToString ());
			LabelIntroduction.Text = String.Format (sres.SuitableResource ("SIDEBAR_ABOUT_INTRODUCTION"), sres.SuitableResource ("SIDEBAR_ABOUT_TITLE"));
			LabelCopyright.Text = sres.SuitableResource ("SIDEBAR_ABOUT_COPYRIGHT");
			LabelProject.Text = sres.SuitableResource ("SIDEBAR_ABOUT_PROJECT");
			LabelLicense.Text = sres.SuitableResource ("SIDEBAR_ABOUT_LICENSE");
			LabelCanScroll.Text = sres.SuitableResource ("CONFIG_CANSCROLL");
		}
		public void InitConfigValues ()
		{
			var cc = App.CurrentUserConfig;
			InputAutoRun.IsChecked = cc.AutoRun;
			InputLock.IsChecked = cc.Locked;
			InputAppBar.IsChecked = cc.OccupyWorkingArea;
			InputTopmost.IsChecked = cc.Topmost;
			InputOverlap.IsChecked = cc.OverlapTaskbar;
			InputLeft.IsChecked = cc.Direction == SidebarDirection.Left;
			InputRight.IsChecked = cc.Direction == SidebarDirection.Right;
			InputCanScroll.IsChecked = cc.CanScroll;
			RefreshThemeSelect ();
			RefreshScreenSelect ();
			{
				var loc = App.ProgramFolder.StringResources;
				string smallText = loc.SuitableResource ("CONFIG_SMALL", "Small");
				string mediumText = loc.SuitableResource ("CONFIG_MEDIUM", "Medium");
				string largeText = loc.SuitableResource ("CONFIG_LARGE", "Large");
				string customText = loc.SuitableResource ("CONFIG_CUSTOM", "Custom");
				var items = new List<KeyValuePair<string, string>>
				{
					new KeyValuePair<string, string>("Small", smallText),
					new KeyValuePair<string, string>("Medium", mediumText),
					new KeyValuePair<string, string>("Large", largeText),
					new KeyValuePair<string, string>("Custom", customText)
				};
				SelectWidth.ItemsSource = items;
				SelectWidth.DisplayMemberPath = "Value";
				SelectWidth.SelectedValuePath = "Key";
				var widthText = "";
				switch (cc.Width.ToString ())
				{
					case "100": widthText = "Small"; break;
					case "150": widthText = "Medium"; break;
					case "200": widthText = "Large"; break;
					default: widthText = "Custom"; break;
				}
				InputWidth.Value = (decimal)cc.Width;
				SelectWidth.SelectedValue = widthText;
				InputWidth.Enabled = (widthText == "Custom");
			}
		}
		private void RefreshThemeSelect ()
		{
			SelectTheme.SelectionChanged -= SelectTheme_SelectionChanged;
			SelectTheme.ItemsSource = App.ThemeMgr.ValidThemes;
			SelectTheme.DisplayMemberPath = "ThemeName";
			SelectTheme.SelectedValuePath = "ThemeName";
			string currentThemeName = App.ThemeMgr.CurrentUserTheme?.ThemeName;
			if (!string.IsNullOrWhiteSpace (currentThemeName))
			{
				SelectTheme.SelectedValue = currentThemeName;
			}
			SelectTheme.SelectionChanged -= SelectTheme_SelectionChanged;
			SelectTheme.SelectionChanged += SelectTheme_SelectionChanged;
		}
		private void RefreshScreenSelect ()
		{
			SelectScreen.SelectionChanged -= SelectScreen_SelectionChanged;
			var items = new List<KeyValuePair<string, string>>
			{
				new KeyValuePair<string, string>("Primary",
					App.ProgramFolder.StringResources.SuitableResource("CONFIG_SCREEN_PRIMARY", "Primary Screen"))
			};
			items.AddRange (
				System.Windows.Forms.Screen.AllScreens.Select (screen => {
					string friendlyName = ScreenHelper.GetScreenFriendlyName (screen);
					if (string.IsNullOrEmpty (friendlyName))
						friendlyName = screen.DeviceName;
					return new KeyValuePair<string, string> (
						screen.DeviceName,  
						$"{friendlyName} ({screen.DeviceName})"
					);
				})
			);
			SelectScreen.DisplayMemberPath = "Value";
			SelectScreen.SelectedValuePath = "Key";
			SelectScreen.ItemsSource = items;
			string current = App.CurrentUserConfig.Screen ?? "Primary";
			SelectScreen.SelectedValue = current;
			if (SelectScreen.SelectedIndex < 0) SelectScreen.SelectedIndex = 0;
			SelectScreen.SelectionChanged -= SelectScreen_SelectionChanged;
			SelectScreen.SelectionChanged += SelectScreen_SelectionChanged;
		}
		public void RegisterEvents ()
		{
			UnregisterEvents ();
			InputAutoRun.Checked += InputAutoRun_CheckChanged;
			InputAutoRun.Unchecked += InputAutoRun_CheckChanged;
			InputTopmost.Checked += InputTopmost_CheckChanged;
			InputTopmost.Unchecked += InputTopmost_CheckChanged;
			InputAppBar.Checked += InputAppBar_CheckChanged;
			InputAppBar.Unchecked += InputAppBar_CheckChanged;
			InputOverlap.Checked += InputOverlap_CheckChanged;
			InputOverlap.Unchecked += InputOverlap_CheckChanged;
			SelectScreen.SelectionChanged += SelectScreen_SelectionChanged;
			InputLock.Checked += InputLock_CheckChanged;
			InputLock.Unchecked += InputLock_CheckChanged;
			InputCanScroll.Checked += InputCanScroll_CheckChanged;
			InputCanScroll.Unchecked += InputCanScroll_CheckChanged;
			SelectWidth.SelectionChanged += SelectWidth_SelectionChanged;
			InputWidth.ValueChanged += InputWidth_ValueChanged;
			InputLeft.Checked += InputLeft_Checked;
			InputRight.Checked += InputRight_Checked;
			SelectTheme.SelectionChanged += SelectTheme_SelectionChanged;
		}
		private void SelectTheme_SelectionChanged (object sender, SelectionChangedEventArgs e)
		{
			if (SelectTheme.SelectedItem is Theme)
			{ 
				var selectedTheme = SelectTheme.SelectedItem as Theme;
				App.ThemeMgr.SetCurrentUserTheme (selectedTheme);
				ThemeManager.Apply (selectedTheme);
			}
		}
		private void InputRight_Checked (object sender, RoutedEventArgs e)
		{
			var cc = App.CurrentUserConfig;
			cc.Direction = SidebarDirection.Right;
		}
		private void InputLeft_Checked (object sender, RoutedEventArgs e)
		{
			var cc = App.CurrentUserConfig;
			cc.Direction = SidebarDirection.Left;
		}
		private void InputWidth_ValueChanged (object sender, EventArgs e)
		{
			var cc = App.CurrentUserConfig;
			if (SelectWidth.SelectedValue as string == "Custom")
			{
				cc.Width = (double)InputWidth.Value;
			}
		}
		private void SelectWidth_SelectionChanged (object sender, SelectionChangedEventArgs e)
		{
			var cc = App.CurrentUserConfig;
			string selectedKey = SelectWidth.SelectedValue as string;
			bool isCustom = (selectedKey == "Custom");
			InputWidth.Enabled = isCustom;
			switch (selectedKey)
			{
				case "Large":
					cc.Width = 200;
					break;
				default:
				case "Medium":
					cc.Width = 150;
					break;
				case "Small":
					cc.Width = 100;
					break;
				case "Custom":
					cc.Width = (double)InputWidth.Value;
					break;
			}
		}
		private void InputCanScroll_CheckChanged (object sender, RoutedEventArgs e)
		{
			App.CurrentUserConfig.CanScroll = InputCanScroll.IsChecked ?? false;
		}
		private void InputLock_CheckChanged (object sender, RoutedEventArgs e)
		{
			App.CurrentUserConfig.Locked = InputLock.IsChecked ?? false;
		}
		private void SelectScreen_SelectionChanged (object sender, SelectionChangedEventArgs e)
		{
			if (SelectScreen.SelectedItem == null) return;
			var selected = (KeyValuePair<string, string>)SelectScreen.SelectedItem;
			string screenId = selected.Key;
			if (App.CurrentUserConfig.Screen != screenId)
			{
				App.CurrentUserConfig.Screen = screenId;
			}
		}
		private void InputOverlap_CheckChanged (object sender, RoutedEventArgs e)
		{
			App.CurrentUserConfig.OverlapTaskbar = InputOverlap.IsChecked ?? false;
		}
		private void InputAppBar_CheckChanged (object sender, RoutedEventArgs e)
		{
			App.CurrentUserConfig.OccupyWorkingArea = InputAppBar.IsChecked ?? false;
		}
		private void InputTopmost_CheckChanged (object sender, RoutedEventArgs e)
		{
			App.CurrentUserConfig.Topmost = InputTopmost.IsChecked ?? false;
		}
		private void InputAutoRun_CheckChanged (object sender, RoutedEventArgs e)
		{
			App.CurrentUserConfig.AutoRun = InputAutoRun.IsChecked ?? false;
		}
		public void UnregisterEvents ()
		{
			InputAutoRun.Checked -= InputAutoRun_CheckChanged;
			InputAutoRun.Unchecked -= InputAutoRun_CheckChanged;
			InputTopmost.Checked -= InputTopmost_CheckChanged;
			InputTopmost.Unchecked -= InputTopmost_CheckChanged;
			InputAppBar.Checked -= InputAppBar_CheckChanged;
			InputAppBar.Unchecked -= InputAppBar_CheckChanged;
			InputOverlap.Checked -= InputOverlap_CheckChanged;
			InputOverlap.Unchecked -= InputOverlap_CheckChanged;
			SelectScreen.SelectionChanged -= SelectScreen_SelectionChanged;
			InputLock.Checked -= InputLock_CheckChanged;
			InputLock.Unchecked -= InputLock_CheckChanged;
			InputCanScroll.Checked -= InputCanScroll_CheckChanged;
			InputCanScroll.Unchecked -= InputCanScroll_CheckChanged;
			SelectWidth.SelectionChanged -= SelectWidth_SelectionChanged;
			InputWidth.ValueChanged -= InputWidth_ValueChanged;
			InputLeft.Checked -= InputLeft_Checked;
			InputRight.Checked -= InputRight_Checked;
			SelectTheme.SelectionChanged -= SelectTheme_SelectionChanged;
		}
		private void ButtonRefreshTheme_Click (object sender, RoutedEventArgs e)
		{
			RefreshThemeSelect ();
		}
		private void Window_Loaded (object sender, RoutedEventArgs e)
		{
			RegisterEvents ();
			Utilities.ReleaseLargeResourcesAsync ();
		}
		private void Window_Closed (object sender, EventArgs e)
		{
			UnregisterEvents ();
			Utilities.ReleaseLargeResourcesAsync ();
		}
		private void TabControl_SelectionChanged (object sender, SelectionChangedEventArgs e)
		{
			Utilities.ReleaseLargeResourcesAsync ();
		}
	}
}

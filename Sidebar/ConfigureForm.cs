using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace Sidebar
{
	public partial class ConfigureForm: Form
	{
		internal class ScreenItem
		{
			public Screen Screen { get; }
			public string FriendlyName => Screen.GetFriendlyName ();
			public string DeviceName => Screen.DeviceName;
			public ScreenItem (Screen sc) { this.Screen = sc; }
		}
		public ConfigureForm ()
		{
			InitializeComponent ();
			InitLocalizationStrings ();
			this.Font = FontHelper.GetFirstAvailableFont (FontHelper.preferredFonts);
			labelAppTitle.Font = FontHelper.GetFirstAvailableFont (FontHelper.preferredFonts, 18);
			InitConfigValues ();
			InitConfigControlEvents ();
		}
		public void InitLocalizationStrings ()
		{
			var sres = App.ProgramFolder.StringResources;
			inputAutorun.Text = sres.SuitableResource ("CONFIG_AUTORUN");
			inputLock.Text = sres.SuitableResource ("CONFIG_LOCK");
			inputOccupyWorkArea.Text = sres.SuitableResource ("CONFIG_APPBAR");
			inputTopmost.Text = sres.SuitableResource ("CONFIG_TOPMOST");
			inputOverlap.Text = sres.SuitableResource ("CONFIG_OVERLAP");
			label1.Text = sres.SuitableResource ("CONFIG_SIDEBARLOC");
			inputLeftLoc.Text = sres.SuitableResource ("CONFIG_LOCLEFT");
			inputRightLoc.Text = sres.SuitableResource ("CONFIG_LOCRIGHT");
			label2.Text = sres.SuitableResource ("CONFIG_THEME");
			tabPage1.Text = sres.SuitableResource ("CONFIG_TITLE");
			tabPage2.Text = sres.SuitableResource ("ABOUT_TITLE");
			Text = sres.SuitableResource ("SIDEBAR_CONTEXTMENU_PROP");
			button1.Text = sres.SuitableResource ("CONFIG_REFRESH");
			label3.Text = sres.SuitableResource ("CONFIG_SCREEN");
			groupBox1.Text = sres.SuitableResource ("CONFIG_GENERAL");
			groupBox2.Text = sres.SuitableResource ("CONFIG_APPEARANCE");
			label4.Text = sres.SuitableResource ("CONFIG_WIDTH");
			labelAppTitle.Text = sres.SuitableResource ("SIDEBAR_ABOUT_TITLE");
			var version = Assembly.GetExecutingAssembly ().GetName ().Version;
			labelVersion.Text = String.Format (sres.SuitableResource ("SIDEBAR_ABOUT_VERSION"), version.ToString ());
			labelIntroduction.Text = String.Format (sres.SuitableResource ("SIDEBAR_ABOUT_INTRODUCTION"), sres.SuitableResource ("SIDEBAR_ABOUT_TITLE"));
			labelCopyright.Text = sres.SuitableResource ("SIDEBAR_ABOUT_COPYRIGHT");
			buttonProjectPage.Text = sres.SuitableResource ("SIDEBAR_ABOUT_PROJECT");
			buttonLicense.Text = sres.SuitableResource ("SIDEBAR_ABOUT_LICENSE");
		}
		public void InitConfigValues ()
		{
			var cc = App.CurrentUserConfig;
			inputAutorun.Checked = cc.AutoRun;
			inputLock.Checked = cc.Locked;
			inputOccupyWorkArea.Checked = cc.OccupyWorkingArea;
			inputTopmost.Checked = cc.Topmost;
			inputOverlap.Checked = cc.OverlapTaskbar;
			inputLeftLoc.Checked = cc.Direction == SidebarDirection.Left;
			inputRightLoc.Checked = cc.Direction == SidebarDirection.Right;
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
				selectWidth.DisplayMember = "Value";
				selectWidth.ValueMember = "Key";
				selectWidth.DataSource = items;
				var widthText = "";
				switch (cc.Width.ToString ())
				{
					case "100": widthText = "Small"; break;
					case "150": widthText = "Medium"; break;
					case "200": widthText = "Large"; break;
					default: widthText = "Custom"; break;
				}
				inputCustomWidth.Value = (decimal)cc.Width;
				selectWidth.SelectedValue = widthText;
				inputCustomWidth.Enabled = (widthText == "Custom");
			}
		}
		private void InitConfigControlEvents ()
		{
			this.inputAutorun.CheckedChanged += new System.EventHandler (this.inputAutorun_CheckedChanged);
			this.inputTopmost.CheckedChanged += new System.EventHandler (this.inputTopmost_CheckedChanged);
			this.inputOccupyWorkArea.CheckedChanged += new System.EventHandler (this.inputOccupyWorkArea_CheckedChanged);
			this.inputOverlap.CheckedChanged += new System.EventHandler (this.inputOverlap_CheckedChanged);
			this.selectScreen.SelectedIndexChanged += new System.EventHandler (this.selectScreen_SelectedIndexChanged);
			this.selectTheme.SelectedIndexChanged += new System.EventHandler (this.selectTheme_SelectedIndexChanged);
			this.button1.Click += new System.EventHandler (this.button1_Click);
			this.inputLeftLoc.CheckedChanged += new System.EventHandler (this.inputLeftLoc_CheckedChanged);
			this.inputRightLoc.CheckedChanged += new System.EventHandler (this.inputRightLoc_CheckedChanged);
			this.inputLock.CheckedChanged += new System.EventHandler (this.inputLock_CheckedChanged);
			this.selectWidth.SelectedIndexChanged += new System.EventHandler (this.selectWidth_SelectedIndexChanged);
			this.inputCustomWidth.ValueChanged += new System.EventHandler (this.inputCustomWidth_ValueChanged);
		}
		private void RefreshThemeSelect ()
		{
			selectTheme.SelectedIndexChanged -= selectTheme_SelectedIndexChanged;
			selectTheme.DataSource = App.ThemeMgr.ValidThemes;
			selectTheme.DisplayMember = "ThemeName";
			selectTheme.ValueMember = "ThemeName";
			string currentThemeName = App.ThemeMgr.CurrentUserTheme?.ThemeName;
			if (!string.IsNullOrWhiteSpace (currentThemeName))
			{
				int idx = selectTheme.FindStringExact (currentThemeName);
				if (idx >= 0) selectTheme.SelectedIndex = idx;
			}
			selectTheme.SelectedIndexChanged += selectTheme_SelectedIndexChanged;
		}
		private void RefreshScreenSelect ()
		{
			selectScreen.SelectedIndexChanged -= selectScreen_SelectedIndexChanged;
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
						screen.DeviceName,                       // Key  -> 实际设备名
						$"{friendlyName} ({screen.DeviceName})" // Value -> 显示文本
					);
				})
			);
			selectScreen.DisplayMember = "Value";
			selectScreen.ValueMember = "Key";
			selectScreen.DataSource = items;
			string current = App.CurrentUserConfig.Screen ?? "Primary";
			selectScreen.SelectedValue = current;
			if (selectScreen.SelectedIndex < 0) selectScreen.SelectedIndex = 0;
			selectScreen.SelectedIndexChanged += selectScreen_SelectedIndexChanged;
		}
		private void ConfigureForm_Load (object sender, EventArgs e)
		{

		}
		private void inputAutorun_CheckedChanged (object sender, EventArgs e)
		{
			App.CurrentUserConfig.AutoRun = inputAutorun.Checked;
		}
		private void inputLock_CheckedChanged (object sender, EventArgs e)
		{
			App.CurrentUserConfig.Locked = inputLock.Checked;
		}
		private void inputTopmost_CheckedChanged (object sender, EventArgs e)
		{
			App.CurrentUserConfig.Topmost = inputTopmost.Checked;
		}
		private void inputOccupyWorkArea_CheckedChanged (object sender, EventArgs e)
		{
			App.CurrentUserConfig.OccupyWorkingArea = inputOccupyWorkArea.Checked;
		}
		private void inputOverlap_CheckedChanged (object sender, EventArgs e)
		{
			App.CurrentUserConfig.OverlapTaskbar = inputOverlap.Checked;
		}
		private void inputLeftLoc_CheckedChanged (object sender, EventArgs e)
		{
			if (inputLeftLoc.Checked)
				App.CurrentUserConfig.Direction = SidebarDirection.Left;
		}
		private void selectTheme_SelectedIndexChanged (object sender, EventArgs e)
		{
			if (selectTheme.SelectedItem is Theme)
			{
				var selectedTheme = selectTheme.SelectedItem as Theme;
				App.ThemeMgr.SetCurrentUserTheme (selectedTheme);
				ThemeManager.Apply (selectedTheme);
			}
		}
		private void button1_Click (object sender, EventArgs e)
		{
			RefreshThemeSelect ();
		}
		private void selectScreen_SelectedIndexChanged (object sender, EventArgs e)
		{
			if (selectScreen.SelectedItem == null) return;
			var selected = (KeyValuePair<string, string>)selectScreen.SelectedItem;
			string screenId = selected.Key; 
			if (App.CurrentUserConfig.Screen != screenId)
			{
				App.CurrentUserConfig.Screen = screenId;
			}
		}
		private void inputRightLoc_CheckedChanged (object sender, EventArgs e)
		{
			if (inputRightLoc.Checked)
				App.CurrentUserConfig.Direction = SidebarDirection.Right;
		}
		private void selectWidth_SelectedIndexChanged (object sender, EventArgs e)
		{
			var cc = App.CurrentUserConfig;
			inputCustomWidth.Enabled = selectWidth.SelectedValue as string == "Custom";
			switch (selectWidth.SelectedValue as string)
			{
				case "Large": cc.Width = 200; break;
				case "Medium": cc.Width = 150; break;
				case "Small": cc.Width = 100; break;
				case "Custom": cc.Width = (double)inputCustomWidth.Value; break;
			}
		}
		private void inputCustomWidth_ValueChanged (object sender, EventArgs e)
		{
			var cc = App.CurrentUserConfig;
			if (selectWidth.SelectedValue as string == "Custom")
			{
				cc.Width = (double)inputCustomWidth.Value;
			}
		}
		private void buttonProjectPage_Click (object sender, EventArgs e)
		{
			Process.Start ("https://github.com/modernw/Sidebar");
		}
		private void buttonLicense_Click (object sender, EventArgs e)
		{

		}
	}
}

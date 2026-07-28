using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using WMS = Sidebar;
namespace Applications.Sidebar
{
	public class UsefulMethods
	{
		public static string getTileDllPath ()
		{
			return Path.GetDirectoryName (Assembly.GetCallingAssembly ().Location);
		}
	}
	[Serializable]
	public abstract class Tile: TileLib.BaseTile
	{
		public bool hasConfigWindow;
		public bool hasFlyout;
		public event ConfigurationWindowDelegate ConfigurationWindowClosing;
		public event ConfigurationWindowDelegate ConfigurationWindowOpened;
		public new event FlyoutDelegate FlyoutClosing;
		public event FlyoutDelegate FlyoutOpened;
		protected Tile ()
		{
		}
		public override void OnInitialize ()
		{
			var panel = TileUI as Panel;
			panel.Children.Add (SidebarContent);
			FlyoutInit += Tile_FlyoutInit;
			FlyoutClosed += Tile_FlyoutClosed;
			PropertiesInit += Tile_PropertiesInit;
			PropertiesClosed += Tile_PropertiesClosed;
			FlyoutOpened += Tile_FlyoutOpened;
			FlyoutClosing += Tile_FlyoutClosing;
			ConfigurationWindowOpened += Tile_ConfigurationWindowOpened;
			ConfigurationWindowClosing += Tile_ConfigurationWindowClosing;
		}
		private void Tile_ConfigurationWindowClosing (ConfigWindowEventArgs e)
		{
			
		}
		private void Tile_ConfigurationWindowOpened (ConfigWindowEventArgs e)
		{
			e.ConfigurationWindow.Activate ();
		}
		private WMS.FlyoutAboutEventArgs fea = null;
		private WMS.PropertiesAboutEventArgs pea = null;
		private void Tile_FlyoutClosing (FlyoutEventArgs e)
		{
			
		}
		private void Tile_FlyoutOpened (FlyoutEventArgs e)
		{
			fea.ClientArea.Children.Add (e.FlyoutContent);
		}
		private void Tile_PropertiesClosed (object sender, EventArgs e)
		{
			OnConfigurationWindowClosing (pea.Window);
			if (pea?.Window != null) pea.Window.Content = null;
			pea = null;
		}
		private void Tile_PropertiesInit (object sender, global::Sidebar.PropertiesAboutEventArgs e)
		{
			pea = e;
			OnConfigurationWindowOpened (e.Window);
		}
		private void Tile_FlyoutClosed (object sender, EventArgs e)
		{
			OnFlyoutClosing ();
			fea?.ClientArea?.Children?.Clear ();
			fea = null;
		}
		private void Tile_FlyoutInit (object sender, global::Sidebar.FlyoutAboutEventArgs e)
		{
			fea = e;
			OnFlyoutOpened ();
		}
		public override void OnDestroy ()
		{
			FlyoutInit -= Tile_FlyoutInit;
			FlyoutClosed -= Tile_FlyoutClosed;
			PropertiesInit -= Tile_PropertiesInit;
			PropertiesClosed -= Tile_PropertiesClosed;
			FlyoutOpened -= Tile_FlyoutOpened;
			FlyoutClosing -= Tile_FlyoutClosing;
			ConfigurationWindowOpened -= Tile_ConfigurationWindowOpened;
			ConfigurationWindowClosing -= Tile_ConfigurationWindowClosing;
			var panel = TileUI as Panel;
			panel?.Children?.Clear ();
		}
		public virtual void OnConfigurationWindowClosing (Window wdw)
		{
			ConfigWindowEventArgs args2 = new ConfigWindowEventArgs ();
			args2.ConfigurationWindow = wdw;
			ConfigWindowEventArgs e = args2;
			this.ConfigurationWindowClosing (e);
		}
		public virtual void OnConfigurationWindowOpened (Window wdw)
		{
			ConfigWindowEventArgs args2 = new ConfigWindowEventArgs ();
			args2.ConfigurationWindow = wdw;
			ConfigWindowEventArgs e = args2;
			this.ConfigurationWindowOpened (e);
		}
		public virtual void OnFlyoutClosing ()
		{
			FlyoutEventArgs args2 = new FlyoutEventArgs ();
			args2.FlyoutContent = this.FlyoutContent;
			FlyoutEventArgs e = args2;
			this.FlyoutClosing (e);
		}
		public virtual void OnFlyoutOpened ()
		{
			FlyoutEventArgs args2 = new FlyoutEventArgs ();
			args2.FlyoutContent = this.FlyoutContent;
			FlyoutEventArgs e = args2;
			this.FlyoutOpened (e);
		}
		public abstract Window ConfigurationWindow { get; }
		public abstract new FrameworkElement FlyoutContent { get; set; }
		public abstract FrameworkElement SidebarContent { get; }
		public delegate void ConfigurationWindowDelegate (ConfigWindowEventArgs e);
		public delegate void FlyoutDelegate (FlyoutEventArgs e);
	}
    [Serializable]
    public class BaseTile : Tile
    {
        public BaseTile()
        {
            base.hasConfigWindow = false;
            base.hasFlyout = false;
            base.ConfigurationWindowClosing += new Tile.ConfigurationWindowDelegate(this.BaseTile_ConfigurationWindowClosing);
            base.ConfigurationWindowOpened += new Tile.ConfigurationWindowDelegate(this.BaseTile_ConfigurationWindowOpened);
            base.FlyoutOpened += new Tile.FlyoutDelegate(this.BaseTile_FlyoutOpened);
            base.FlyoutClosing += new Tile.FlyoutDelegate(this.BaseTile_FlyoutClosing);
        }
        private void BaseTile_ConfigurationWindowClosing(ConfigWindowEventArgs e)
        {
        }
        private void BaseTile_ConfigurationWindowOpened(ConfigWindowEventArgs e)
        {
        }
        private void BaseTile_FlyoutClosing(FlyoutEventArgs e)
        {
        }
        private void BaseTile_FlyoutOpened(FlyoutEventArgs e)
        {
        }
		public override UserControl Load () { return null; }
		public override Window ConfigurationWindow
        {
            get
            {
                return null;
            }
        }
        public override FrameworkElement FlyoutContent
        {
            get
            {
                return null;
            }
			set {  }
        }
        public override FrameworkElement SidebarContent
        {
            get
            {
                return new Button();
            }
        }
	}
}

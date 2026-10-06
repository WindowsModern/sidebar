using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32.Init;

namespace Sidebar
{
	public class TileInstance: ITileRuntime
	{
		public TileConfig Config { get; private set; }
		public TileManifest Manifest { get; private set; }
		public ProgramFolder Region { get; private set; }
		public UIElement TileUI { get; set; }
		public TileStorage Storage { get; private set; }
		public TileBase Instance { get; private set; }
		public ProgramFolder UserRegion { get; private set; }
		public ISidebarFeatures Features { get; private set; }
		public ITileApplication Application { get; private set; }
		public string UserModelId { get; private set; }
		ITileManifest ITileRuntime.Manifest => Manifest;
		IProgramFolder ITileRuntime.Region => Region;
		ITileConfig ITileRuntime.Config => Config;
		IProgramFolder ITileRuntime.UserRegion => UserRegion;
		UIElement ITileRuntime.TileUI => TileUI;
		ISidebarFeatures ITileRuntime.Features => Features;
		ITileApplication ITileRuntime.Application => Application;
		private void LoadTileBase ()
		{
			Assembly assembly = Assembly.LoadFrom (Path.Combine (Storage.FolderPath, Application.Executable));
			Instance = (TileBase)SdkCompatible.GetTileInstance (assembly, Application.EntryPoint);
			Instance.InitTileInstance (this);
		}
		private static void PreloadAllDlls (string root)
		{
			if (string.IsNullOrEmpty (root) || !Directory.Exists (root)) return;
			var pending = new Stack<string> ();
			pending.Push (root);
			while (pending.Count > 0)
			{
				string dir = pending.Pop ();
				string [] files = null;
				try { files = Directory.GetFiles (dir, "*.dll"); } catch { }
				if (files != null)
				{
					foreach (var f in files)
					{
						try { Assembly.LoadFrom (f); } catch { }
					}
				}
				string [] subs = null;
				try { subs = Directory.GetDirectories (dir); } catch { }
				if (subs != null)
				{
					foreach (var s in subs) pending.Push (s);
				}
			}
		}
		public TileInstance (TileApplicationStorage application, UIElement container, ISidebarFeatures sidebar = null)
		{
			UserModelId = application.UserModelId;
			Storage = application.Storage;
			Region = application.Storage.TileFolder;
			UserRegion = application.AppCurrentUserFolder;
			Config = new TileConfig (UserRegion, application.UserModelId);
			Manifest = Storage.Manifest;
			TileUI = container;
			Features = sidebar;
			Application = application.Infomation;
		}
		public void Initialize ()
		{
			LoadTileBase ();
			Instance.OnInitialize ();
		}
	}
	public class TileContext: ITileContext, IDisposable
	{
		public FrameworkElement FlyoutWindow { get; set; }
		UIElement ITileContext.FlyoutWindow
		{
			get { return (this as TileContext).FlyoutWindow; }
		}
		public UIElement FlyoutUI { get; set; }
		public bool IsFlyoutShow { get; set; } = false;
		private System.Drawing.Size ConvertToPhysicalPixels (FrameworkElement element)
		{
			if (element == null) return System.Drawing.Size.Empty;
			var source = PresentationSource.FromVisual (element);
			double dpiScaleX = 1.0, dpiScaleY = 1.0;
			if (source?.CompositionTarget != null)
			{
				dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
				dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
			}
			int width = (int)Math.Round (element.ActualWidth * dpiScaleX);
			int height = (int)Math.Round (element.ActualHeight * dpiScaleY);
			return new System.Drawing.Size (width, height);
		}
		public System.Drawing.Size TileSize => ConvertToPhysicalPixels (container);
		public System.Drawing.Size TileClientSize => ConvertToPhysicalPixels (content);
		public System.Drawing.Size TileFlyoutClientSize
		{
			get
			{
				FrameworkElement fe = FlyoutUI as FrameworkElement;
				if (fe != null)
					return ConvertToPhysicalPixels (fe);
				return System.Drawing.Size.Empty;
			}
		}
		public System.Drawing.Size TileFlyoutSize
		{
			get
			{
				if (FlyoutWindow != null)
					return ConvertToPhysicalPixels (FlyoutWindow);
				return System.Drawing.Size.Empty;
			}
		}
		public UIElement PropertiesWindow { get; set; }
		public UIElement PropertiesContent { get; set; }
		public System.Windows.Size WpfTileSize =>
			container == null
				? System.Windows.Size.Empty
				: new System.Windows.Size (
					container.ActualWidth,
					container.ActualHeight);
		public System.Windows.Size WpfTileClientSize =>
			content == null
				? System.Windows.Size.Empty
				: new System.Windows.Size (
					content.ActualWidth,
					content.ActualHeight);
		public System.Windows.Size WpfTileFlyoutSize => 
			FlyoutWindow == null ?
			System.Windows.Size.Empty :
			new System.Windows.Size (FlyoutWindow.ActualWidth, FlyoutWindow.ActualHeight);
		public System.Windows.Size WpfTileFlyoutClientSize =>
			(FlyoutUI as FrameworkElement) == null ?
			System.Windows.Size.Empty :
			new System.Windows.Size ((FlyoutUI as FrameworkElement).ActualWidth, (FlyoutUI as FrameworkElement).ActualHeight);
		FrameworkElement container = null;
		FrameworkElement content = null;
		public TileContext (FrameworkElement tileContainer, FrameworkElement tileContent)
		{
			container = tileContainer;
			content = tileContent;
		}
		public void Dispose ()
		{
			container = null;
			content = null;
			FlyoutUI = null;
			FlyoutWindow = null;
			IsFlyoutShow = false;
		}
	}
	public class SidebarContext: ISidebarContext
	{
		public SidebarDirection Direction { set; get; } = App.CurrentUserConfig.Direction;
		public ITheme Theme { set; get; } = App.ThemeMgr.CurrentUserTheme;
		public string ThemeName { set; get; } = App.ThemeMgr.CurrentUserTheme.ThemeName;
	}
}

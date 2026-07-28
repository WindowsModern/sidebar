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
	public class TileInstance: ITileRuntime, IDisposable
	{
		public ITileConfig Config { get; private set; }
		public ITileManifest Manifest { get; private set; }
		public IProgramFolder Region { get; private set; }
		public UIElement TileUI { get; set; }
		public TileStorage Storage { get; private set; }
		public TileBase Instance { get; private set; }
		public IProgramFolder UserRegion => Storage?.TileCurrentUserFolder;
		public ISidebarFeatures Features { get; private set; }
		private void LoadTileBase ()
		{
			Assembly assembly = Assembly.LoadFrom (Storage.TileFilePath);
			Type tileType = assembly.GetTypes ().FirstOrDefault (t => t.IsSubclassOf (typeof (TileBase)) && !t.IsAbstract);
			if (tileType == null)
				throw new InvalidOperationException ($"Cannot find tile \"{Manifest.Identity.FullName}\" class.");
			Instance = (TileBase)Activator.CreateInstance (tileType);
			Instance.InitTileInstance (this);
		}
		public void Dispose ()
		{
			Config = null;
			Manifest = null;
			Region = null;
			TileUI = null;
			Storage = null;
			Instance = null;
			//UserRegion = null;
			Features = null;
		}
		public TileInstance (TileStorage storage, UIElement container, ISidebarFeatures sidebar)
		{
			Storage = storage;
			Region = storage.TileFolder;
			Config = new TileConfig (storage.TileCurrentUserFolder);
			//UserRegion = storage.TileCurrentUserFolder;
			Manifest = storage.Manifest;
			TileUI = container;
			Features = sidebar;
		}
		public void Initialize ()
		{
			LoadTileBase ();
			Instance.OnInitialize ();
		}
	}
	public class TileMgrItem: IEquatable <TileMgrItem>
	{
		public TileMgrItem (TileStorage storage) { Storage = storage; }
		public TileStorage Storage { get; private set; }
		public string Title
		{
			get
			{
				try
				{
					var name = Storage.Manifest.Properties.DisplayName;
					return Storage.TileFolder.StringResources.SuitableResource (name, name); 
				}
				catch
				{
					return "";
				}
			}
		}
		public string Publisher
		{
			get
			{
				try
				{
					var name = Storage.Manifest.Properties.PublisherDisplayName;
					return Storage.TileFolder.StringResources.SuitableResource (name, name);
				}
				catch
				{
					return "";
				}
			}
		}
		public Version Version
		{
			get
			{
				try
				{
					return Storage.Manifest.Identity.Version;
				}
				catch
				{
					return new Version ();
				}
			}
		}
		public string Description
		{
			get
			{
				try
				{
					var name = Storage.Manifest.Properties.Description;
					return Storage.TileFolder.StringResources.SuitableResource (name, name);
				}
				catch
				{
					return "";
				}
			}
		}
		public string Logo
		{
			get
			{
				try
				{
					var logo = Storage.Manifest.Properties.Logo;
					var logores = Storage.TileFolder.FileResources.SuitableResource (logo, logo) ?? logo;
					var fullpath = Path.Combine (Storage.FolderPath, logores);
					if (File.Exists (fullpath)) return fullpath;
					fullpath = Path.Combine (Storage.FolderPath, logo);
					if (File.Exists (fullpath)) return fullpath;
					else return "";
				}
				catch
				{
					return "";
				}
			}
		}
		public bool Equals (TileMgrItem other)
		{
			return Storage?.Manifest?.Identity?.FamilyName?.NNormalize () == other?.Storage?.Manifest?.Identity?.FamilyName?.NNormalize ();
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
	public abstract class TileVisualInfo
	{
		public virtual ImageSource TileLogo { get; }
		public virtual string TileTitle { get; }
		public virtual UIElement TileElement { get; }
		public override bool Equals (object obj)
		{
			if (obj is TileVisualInfo) return TileElement == (obj as TileVisualInfo)?.TileElement;
			else if (obj is UIElement) return TileElement == obj;
			else return base.Equals (obj);
		}
	}
	public class SidebarContext: ISidebarContext
	{
		public SidebarDirection Direction { set; get; } = App.CurrentUserConfig.Direction;
		public ITheme Theme { set; get; } = App.ThemeMgr.CurrentUserTheme;
		public string ThemeName { set; get; } = App.ThemeMgr.CurrentUserTheme.ThemeName;
	}
}

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Sidebar
{
	[ComVisible (true)]
	public interface ITileRequest
	{
		string RequestId { get; }
		string RequestName { get; }
		string RequestSource { get; }
		string RequestTarget { get; }
		object RequestDatas { get; }
		object TransferDatas { get; }
	}
	public class TileRequest: ITileRequest
	{
		private DateTime genTime = DateTime.UtcNow;
		public object RequestDatas { get; set; }
		public string RequestId
		{
			get
			{
				string raw = $"{(RequestName ?? "").NNormalize ()}|{(RequestSource ?? "").NNormalize ()}|{(RequestTarget ?? "").NNormalize ()}|{genTime.Ticks}";
				using (var sha256 = SHA256.Create ())
				{
					byte [] hash = sha256.ComputeHash (Encoding.UTF8.GetBytes (raw));
					StringBuilder sb = new StringBuilder (32);
					for (int i = 0; i < 16; i++) sb.Append (hash [i].ToString ("x2"));
					return sb.ToString ();
				}
			}
		}
		public string RequestName { get; set; }
		public string RequestSource { get; }
		public string RequestTarget { get; set; }
		public object TransferDatas { get; set; }
		public TileRequest (ITileBase itb)
		{
			RequestSource = itb.UserModelId;
		}
	}
	public interface ISidebarRequest: ITileRequest { }
	public class SidebarRequest: ISidebarRequest
	{
		private DateTime genTime = DateTime.UtcNow;
		public object RequestDatas { get; set; }
		public string RequestId
		{
			get
			{
				string raw = $"{(RequestName ?? "").NNormalize ()}|{(RequestTarget ?? "").NNormalize ()}|{genTime.Ticks}";
				using (var sha256 = SHA256.Create ())
				{
					byte [] hash = sha256.ComputeHash (Encoding.UTF8.GetBytes (raw));
					StringBuilder sb = new StringBuilder (32);
					for (int i = 0; i < 16; i++) sb.Append (hash [i].ToString ("x2"));
					return sb.ToString ();
				}
			}
		}
		public string RequestName { get; set; } 
		public string RequestSource { get; }
		public string RequestTarget { get; } = "Sidebar";
		public object TransferDatas { get; set; }
		public SidebarRequest (ITileBase itb)
		{
			RequestSource = itb.UserModelId;
		}
	}
	[ComVisible (true)]
	public interface ITileResponse
	{
		string RequestId { get; }
		string ResponseName { get; }
		string ResponseSource { get; }
		string ResponseTarget { get; }
		bool Success { get; }
		string ErrorMessage { get; }
		object ResponseData { get; }
		object TransferDatas { get; }
	}
	[ComVisible (true)]
	public class TileResponse: ITileResponse
	{
		public string ErrorMessage { get; set; }
		public string RequestId { get; }
		public object ResponseData { get; set; }
		public string ResponseSource { get; }
		public bool Success { get; set; }
		public string ResponseTarget { get; }
		public string ResponseName { get; set; }
		public object TransferDatas { get; }
		public TileResponse (ITileRequest itr)
		{
			RequestId = itr.RequestId;
			ResponseSource = itr.RequestTarget;
			ResponseTarget = itr.RequestSource;
			TransferDatas = itr.TransferDatas;
		}
	}
	[ComVisible (true)]
	public interface ISidebarConfig
	{
		double Width { get; }
		string ThemeName { get; }
		SidebarDirection Direction { get; }
		System.Windows.Forms.Screen CurrentScreen { get; }
	}
	[ComVisible (true)]
	public interface ISidebarFeatures
	{
		bool Request (ISidebarRequest request);
		bool Communicate (ITileRequest request);
		bool Response (ITileResponse resp);
		ISidebarConfig Config { get; }
	}
	[ComVisible (true)]
	public interface ITileBase
	{
		ITileManifest Manifest { get; }
		IProgramFolder Region { get; }
		ITileConfig Config { get; }
		ISidebarFeatures Features { get; }
		IProgramFolder UserRegion { get; }
		UIElement TileUI { get; }
		ITileApplication Application { get; }
		string UserModelId { get; }
		void OnLoad ();
		void OnUnload ();
		void OnHostChanged (TileHostEvent eventName, object context, object sender = null);
		bool OnRequest (ITileRequest req);
		bool OnResponse (ITileResponse resp);
	}
	[ComVisible (true)]
	public interface ITileContext
	{
		System.Drawing.Size TileSize { get; }
		System.Drawing.Size TileClientSize { get; }
		System.Drawing.Size TileFlyoutSize { get; }
		System.Drawing.Size TileFlyoutClientSize { get; }
		System.Windows.Size WpfTileSize { get; }
		System.Windows.Size WpfTileClientSize { get; }
		System.Windows.Size WpfTileFlyoutSize { get; }
		System.Windows.Size WpfTileFlyoutClientSize { get; }
		UIElement FlyoutWindow { get; }
		UIElement FlyoutUI { get; }
		UIElement PropertiesWindow { get; }
		UIElement PropertiesContent { get; }
	}
	[ComVisible (true)]
	public interface ISidebarContext
	{
		SidebarDirection Direction { get; }
		string ThemeName { get; }
		ITheme Theme { get; }
	}
	[ComVisible (true)]
	public interface ITileRuntime
	{
		ITileManifest Manifest { get; }
		IProgramFolder Region { get; }
		ITileConfig Config { get; }
		IProgramFolder UserRegion { get; }
		UIElement TileUI { get; }
		ISidebarFeatures Features { get; }
		ITileApplication Application { get; }
	}
	[ComVisible (true)]
	public class SizeChangedEventArgs: EventArgs
	{
		public System.Drawing.Size NowSize { get; }
		public System.Drawing.Size NowClientSize { get; }
		public System.Windows.Size WpfNowSize { get; }
		public System.Windows.Size WpfNowClientSize { get; }
		public SizeChangedEventArgs (System.Drawing.Size size, System.Drawing.Size clientSize, System.Windows.Size wpfNowSize, System.Windows.Size wpfNowClientSize)
		{
			NowSize = size;
			NowClientSize = clientSize;
			WpfNowSize = wpfNowSize;
			WpfNowClientSize = wpfNowClientSize;
		}
	}
	[ComVisible (true)]
	public interface IUnionWindowProvider
	{
		Window Window { get; }
		Panel ClientArea { get; }
	}
	[ComVisible (true)]
	public class FlyoutAboutEventArgs: EventArgs, IUnionWindowProvider
	{
		public UIElement FlyoutWindow { get; }
		public UIElement FlyoutUI { get; }
		public Window Window => FlyoutWindow as Window;
		public Panel ClientArea => FlyoutUI as Panel;
		public IFlyoutToolMembers FlyoutWindowFeatures => FlyoutWindow as IFlyoutToolMembers;
		public FlyoutAboutEventArgs (UIElement flyoutUI, UIElement flyoutWindow)
		{
			FlyoutUI = flyoutUI;
			FlyoutWindow = flyoutWindow;
		}
	}
	[ComVisible (true)]
	public class SidebarDirectionChangedEventArgs: EventArgs
	{
		public SidebarDirection Direction { get; }
		public SidebarDirectionChangedEventArgs (SidebarDirection direct)
		{
			Direction = direct;
		}
	}
	[ComVisible (true)]
	public class ThemeChangedEventArgs: EventArgs
	{
		public string ThemeName { get; }
		public ITheme Theme { get; }
		public ThemeChangedEventArgs (string themeName, ITheme theme)
		{
			ThemeName = themeName;
			Theme = theme;
		}
	}
	[ComVisible (true)]
	public class PropertiesAboutEventArgs: EventArgs
	{
		public UIElement PropertiesWindow { get; }
		public UIElement PropertiesContent { get; }
		public Window Window => PropertiesWindow as Window;
		public Panel ClientArea => PropertiesContent as Panel;
		public PropertiesAboutEventArgs (UIElement propertiesWindow, UIElement propertiesContent)
		{
			PropertiesWindow = propertiesWindow;
			PropertiesContent = propertiesContent;
		}
	}
	[ComVisible (true)]
	public abstract class TileBase: ITileBase
	{
		public TileBase () { }
		internal void InitTileInstance (ITileRuntime runtime)
		{
			Manifest = runtime.Manifest;
			Region = runtime.Region;
			TileUI = runtime.TileUI;
			Config = runtime.Config;
			Features = runtime.Features;
			UserRegion = runtime.UserRegion;
			Application = runtime.Application;
		}
		public ITileManifest Manifest { get; private set; } = null;
		public IProgramFolder Region { get; private set; } = null;
		public IProgramFolder UserRegion { get; private set; } = null;
		public UIElement TileUI { get; private set; } = null;
		public ITileApplication Application { get; private set; } = null;
		public string UserModelId => $"{Manifest?.Identity?.FamilyName}!{Application?.Id}";
		public virtual void OnInitialize () { }
		public virtual void OnDestroy () { }
		public void OnHostChanged (TileHostEvent eventName, object context, object sender = null)
		{
			var tileContext = context as ITileContext;
			var sidebarContext = context as ISidebarContext;
			switch (eventName)
			{
				case TileHostEvent.FlyoutClosed: FlyoutClosed?.Invoke (sender, null); break;
				case TileHostEvent.FlyoutClosing: FlyoutClosing?.Invoke (sender, new FlyoutAboutEventArgs (tileContext.FlyoutUI, tileContext.FlyoutWindow)); break;
				case TileHostEvent.FlyoutInit: FlyoutInit?.Invoke (this, new FlyoutAboutEventArgs (tileContext.FlyoutUI, tileContext.FlyoutWindow)); break;
				case TileHostEvent.FlyoutResize: FlyoutResize?.Invoke (sender, new SizeChangedEventArgs (tileContext.TileFlyoutSize, tileContext.TileFlyoutClientSize, tileContext.WpfTileFlyoutSize, tileContext.WpfTileFlyoutClientSize)); break;
				case TileHostEvent.FlyoutShow: FlyoutShow?.Invoke (sender, new FlyoutAboutEventArgs (tileContext.FlyoutUI, tileContext.FlyoutWindow)); break;
				case TileHostEvent.Resize: Resize?.Invoke (sender, new SizeChangedEventArgs (tileContext.TileSize, tileContext.TileClientSize, tileContext.WpfTileSize, tileContext.WpfTileClientSize)); break;
				case TileHostEvent.SidebarDirectionChanged: SidebarDirectionChanged?.Invoke (this, new SidebarDirectionChangedEventArgs (sidebarContext.Direction)); break;
				case TileHostEvent.ThemeChanged: ThemeChanged?.Invoke (this, new ThemeChangedEventArgs (sidebarContext.ThemeName, sidebarContext.Theme)); break;
				case TileHostEvent.PropertiesInit: PropertiesInit?.Invoke (this, new PropertiesAboutEventArgs (tileContext.PropertiesWindow, tileContext.PropertiesContent)); break;
				case TileHostEvent.PropertiesLoad: PropertiesShow?.Invoke (sender, new PropertiesAboutEventArgs (tileContext.PropertiesWindow, tileContext.PropertiesContent)); break;
				case TileHostEvent.PropertiesClosing: PropertiesClosing?.Invoke (sender, new PropertiesAboutEventArgs (tileContext.PropertiesWindow, tileContext.PropertiesContent)); break;
				case TileHostEvent.PropertiesClickOkButton: PropertiesClickOK?.Invoke (sender, new PropertiesAboutEventArgs (tileContext.PropertiesWindow, tileContext.PropertiesContent)); break;
				case TileHostEvent.PropertiesClickCancelButton: PropertiesClickCancel?.Invoke (sender, new PropertiesAboutEventArgs (tileContext.PropertiesWindow, tileContext.PropertiesContent)); break;
				case TileHostEvent.PropertiesClosed: PropertiesClosed?.Invoke (sender, null); break;
			}
		}
		public void OnLoad () { Load?.Invoke (this, null); }
		public void OnUnload () { Unload?.Invoke (this, null); }
		public virtual bool OnResponse (ITileResponse resp) { return false; }
		public virtual bool OnRequest (ITileRequest req) { return false; }
		public event EventHandler Load;
		public event EventHandler Unload;
		public event EventHandler<SizeChangedEventArgs> Resize;
		public event EventHandler<SizeChangedEventArgs> FlyoutResize;
		public event EventHandler<FlyoutAboutEventArgs> FlyoutInit;
		public event EventHandler<FlyoutAboutEventArgs> FlyoutShow;
		public event EventHandler<FlyoutAboutEventArgs> FlyoutClosing;
		public event EventHandler FlyoutClosed;
		public event EventHandler<SidebarDirectionChangedEventArgs> SidebarDirectionChanged;
		public event EventHandler<ThemeChangedEventArgs> ThemeChanged;
		public event EventHandler<PropertiesAboutEventArgs> PropertiesInit;
		public event EventHandler<PropertiesAboutEventArgs> PropertiesShow;
		public event EventHandler<PropertiesAboutEventArgs> PropertiesClosing;
		public event EventHandler<PropertiesAboutEventArgs> PropertiesClickOK;
		public event EventHandler<PropertiesAboutEventArgs> PropertiesClickCancel;
		public event EventHandler PropertiesClosed;
		public ITileConfig Config { get; private set; } = null;
		public ISidebarFeatures Features { get; private set; }
		public virtual ContextMenu ContextMenu { get; set; } = null;
	}
	[ComVisible (true)]
	public class TileBaseEventRouter: IDisposable
	{
		private TileBase tbase = null;
		protected TileBase Instance => tbase;
		public TileBaseEventRouter (TileBase tileBase)
		{
			if (tileBase == null)
				throw new ArgumentNullException ("Error: cannot init TileEventRouter because the arg TileBase is null.");
			tbase = tileBase;
			FreeEventHandlers ();
			InitEventHandlers ();
		}
		private void InitEventHandlers ()
		{
			if (tbase == null) return;
			tbase.FlyoutClosed += FlyoutForm_Closed;
			tbase.FlyoutClosing += FlyoutForm_Closing;
			tbase.FlyoutInit += FlyoutForm_Init;
			tbase.FlyoutResize += FlyoutForm_Resize;
			tbase.FlyoutShow += FlyoutForm_Loaded;
			tbase.PropertiesClickCancel += PropertiesForm_ClickCancel;
			tbase.PropertiesClickOK += PropertiesForm_ClickOK;
			tbase.PropertiesClosed += PropertiesForm_Closed;
			tbase.PropertiesClosing += PropertiesForm_Closing;
			tbase.PropertiesInit += PropertiesForm_Init;
			tbase.PropertiesShow += PropertiesForm_Loaded;
			tbase.Resize += Tile_Resize;
			tbase.SidebarDirectionChanged += Sidebar_DirectionChanged;
			tbase.ThemeChanged += Sidebar_ThemeChanged;
		}
		private void FreeEventHandlers ()
		{
			if (tbase == null) return;
			tbase.FlyoutClosed -= FlyoutForm_Closed;
			tbase.FlyoutClosing -= FlyoutForm_Closing;
			tbase.FlyoutInit -= FlyoutForm_Init;
			tbase.FlyoutResize -= FlyoutForm_Resize;
			tbase.FlyoutShow -= FlyoutForm_Loaded;
			tbase.PropertiesClickCancel -= PropertiesForm_ClickCancel;
			tbase.PropertiesClickOK -= PropertiesForm_ClickOK;
			tbase.PropertiesClosed -= PropertiesForm_Closed;
			tbase.PropertiesClosing -= PropertiesForm_Closing;
			tbase.PropertiesInit -= PropertiesForm_Init;
			tbase.PropertiesShow -= PropertiesForm_Loaded;
			tbase.Resize -= Tile_Resize;
			tbase.SidebarDirectionChanged -= Sidebar_DirectionChanged;
			tbase.ThemeChanged -= Sidebar_ThemeChanged;
		}
		public virtual void Sidebar_ThemeChanged (object sender, ThemeChangedEventArgs e) { }
		public virtual void Sidebar_DirectionChanged (object sender, SidebarDirectionChangedEventArgs e) { }
		public virtual void Tile_Resize (object sender, SizeChangedEventArgs e) { }
		public virtual void PropertiesForm_Loaded (object sender, PropertiesAboutEventArgs e) { }
		public virtual void PropertiesForm_Init (object sender, PropertiesAboutEventArgs e) { }
		public virtual void PropertiesForm_Closing (object sender, PropertiesAboutEventArgs e) { }
		public virtual void PropertiesForm_Closed (object sender, EventArgs e) { }
		public virtual void PropertiesForm_ClickOK (object sender, PropertiesAboutEventArgs e) { }
		public virtual void PropertiesForm_ClickCancel (object sender, PropertiesAboutEventArgs e) { }
		public virtual void FlyoutForm_Loaded (object sender, FlyoutAboutEventArgs e) { }
		public virtual void FlyoutForm_Resize (object sender, SizeChangedEventArgs e) { }
		public virtual void FlyoutForm_Init (object sender, FlyoutAboutEventArgs e) { }
		public virtual void FlyoutForm_Closing (object sender, FlyoutAboutEventArgs e) { }
		public virtual void FlyoutForm_Closed (object sender, EventArgs e) { }
		private bool isWillDestroyRun = false;
		private bool isAlreadyDestroyRun = false;
		public virtual void Router_WillDestroy () { }
		public virtual void Router_AlreadyDestroy () { }
		public void Dispose ()
		{
			if (!isWillDestroyRun)
			{
				isWillDestroyRun = true;
				try { Router_WillDestroy (); } catch { }
			}
			FreeEventHandlers ();
			if (!isAlreadyDestroyRun)
			{
				isAlreadyDestroyRun = true;
				try { Router_AlreadyDestroy (); } catch { }
			}
			tbase = null;
		}
		~TileBaseEventRouter ()
		{
			try { Dispose (); } catch { }
		}
	}
}

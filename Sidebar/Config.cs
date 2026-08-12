using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using Microsoft.Win32.Init;

namespace Sidebar
{
	public class SidebarConfig: INotifyPropertyChanged, ISidebarConfig
	{
		InitConfig initConfig = null;
		XmlConfig xmlConfig = null;
		public event PropertyChangedEventHandler PropertyChanged;
		public InitConfig Ini => initConfig;
		public XmlConfig Xml => xmlConfig;
		protected virtual void OnPropertyChanged (string propertyName = null)
		{
			PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (propertyName));
		}
		private SidebarDirection _direction;
		private double _width;
		private bool _overlapTaskbar;
		private string _screen;
		private Screen _screenInst;
		private bool _locked;
		private string _themeName;
		private bool _topmost;
		private bool _occupyWorkingArea;
		private bool _autorun;
		private bool _canScroll;
		/// <summary>
		///	边栏位置
		/// </summary>
		public SidebarDirection Direction
		{
			get { return _direction; }
			set
			{
				var str = "";
				_direction = value;
				switch (value)
				{
					case SidebarDirection.Left: str = "Left"; break;
					case SidebarDirection.Right: str = "Right"; break;
				}
				initConfig ["Settings"] ["Direction"] = str;
				OnPropertyChanged ("Direction");
			}
		}
		/// <summary>
		/// 边栏宽度
		/// </summary>
		public double Width
		{
			get { return _width; }
			set
			{
				_width = value;
				initConfig ["Settings"] ["Width"] = value;
				OnPropertyChanged ("Width");
			}
		}
		/// <summary>
		/// 穿透任务栏
		/// </summary>
		public bool OverlapTaskbar
		{
			get { return _overlapTaskbar; }
			set
			{
				_overlapTaskbar = value;
				initConfig ["Settings"] ["OverlapTaskbar"] = value;
				OnPropertyChanged ("OverlapTaskbar");
			}
		}
		/// <summary>
		/// 当前屏幕标识（设备名称或 "Primary" 表示主屏幕）
		/// </summary>
		public string Screen
		{
			get { return _screen; }
			set
			{
				_screen = value;
				_screenInst = null;
				if (string.IsNullOrWhiteSpace (value))
					value = "Primary";
				initConfig ["Settings"] ["CurrentScreen"] = value;
				OnPropertyChanged ("Screen");
				_screenInst = ScreenExtraMethods.GetScreen (value);
			}
		}
		public Screen CurrentScreen
		{
			get { return _screenInst; }
			set { Screen = value.DeviceName; OnPropertyChanged ("CurrentScreen"); }
		}
		public bool Locked
		{
			get { return _locked; }
			set
			{
				_locked = value;
				initConfig ["Settings"] ["Locked"] = _locked;
				OnPropertyChanged ("Locked");
			}
		}
		public string ThemeName
		{
			get { return _themeName; }
			set
			{
				_themeName = value;
				initConfig ["Theme"] ["ThemeName"] = value;
				OnPropertyChanged ("ThemeName");
			}
		}
		private ObservableCollection<string> _pinnedTiles;
		public ObservableCollection<string> PinnedTiles
		{
			get
			{
				if (_pinnedTiles == null)
				{
					LoadPinnedTiles ();
					_pinnedTiles.CollectionChanged += OnPinnedTilesChanged;
				}
				return _pinnedTiles;
			}
		}
		/// <summary>
		/// 总在最前
		/// </summary>
		public bool Topmost
		{
			get { return _topmost; }
			set
			{
				_topmost = value;
				initConfig ["Settings"] ["Topmost"] = value;
				OnPropertyChanged ("Topmost");
			}
		}
		/// <summary>
		/// 占用屏幕工作区域
		/// </summary>
		public bool OccupyWorkingArea
		{
			get { return _occupyWorkingArea; }
			set
			{
				_occupyWorkingArea = value;
				initConfig ["Settings"] ["OccupyWorkingArea"] = value;
				OnPropertyChanged ("OccupyWorkingArea");
			}
		}
		public bool AutoRun
		{
			get { return _autorun; }
			set
			{
				_autorun = value;
				initConfig ["Settings"] ["AutoRun"] = value;
				OnPropertyChanged ("AutoRun");
			}
		}
		public bool CanScroll
		{
			get { return _canScroll; }
			set
			{
				_canScroll = value;
				initConfig ["Settings"] ["CanScroll"] = value;
				OnPropertyChanged (nameof (CanScroll));
			}
		}
		private void LoadPinnedTiles ()
		{
			if (xmlConfig?.Global != null)
			{
				var list = xmlConfig.Global.Get<List<string>> ("PinnedTiles", new List<string> ());
				_pinnedTiles = new ObservableCollection<string> (list ?? new List<string> ());
			}
			else
			{
				_pinnedTiles = new ObservableCollection<string> ();
			}
		}
		private void OnPinnedTilesChanged (object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			if (xmlConfig?.Global != null)
			{
				var list = _pinnedTiles.ToList ();
				xmlConfig.Global ["PinnedTiles"] = list;
			}
			OnPropertyChanged (nameof (PinnedTiles));
		}
		private void InitConfigValues ()
		{
			{
				var gotted = initConfig? ["Settings"]?.GetKey ("Direction")?.ReadString ("right")?.NNormalize ();
				switch (gotted)
				{
					case "left": _direction = SidebarDirection.Left; break;
					default:
					case "right": _direction = SidebarDirection.Right; break;
				}
			}
			_width = initConfig? ["Settings"]?.GetKey ("Width")?.ReadDouble (150) ?? 150;
			_overlapTaskbar = initConfig? ["Settings"]?.GetKey ("OverlapTaskbar")?.ReadBool (false) ?? false;
			_screen = initConfig? ["Settings"]?.GetKey ("CurrentScreen")?.ReadString ("Primary") ?? "Primary";
			_screenInst = ScreenExtraMethods.GetScreen (Screen);
			_locked = initConfig? ["Settings"]?.GetKey ("Locked")?.ReadBool (false) ?? false;
			_themeName = initConfig? ["Theme"]?.GetKey ("ThemeName").ReadString () ?? "";
			_topmost = initConfig? ["Settings"]?.GetKey ("Topmost").ReadBool (false) ?? false;
			_occupyWorkingArea = initConfig? ["Settings"]?.GetKey ("OccupyWorkingArea").ReadBool (false) ?? false;
			_autorun = initConfig? ["Settings"]?.GetKey ("AutoRun").ReadBool (false) ?? false;
			_canScroll = initConfig? ["Settings"]?.GetKey ("CanScroll").ReadBool (false) ?? false;
		}
		public SidebarConfig (InitConfig ini, XmlConfig xml)
		{
			initConfig = ini;
			xmlConfig = xml;
			InitConfigValues ();
		}
		public SidebarConfig (IProgramFolder pf): this (pf.InitConfig, pf.XmlConfig) { }
	}
}

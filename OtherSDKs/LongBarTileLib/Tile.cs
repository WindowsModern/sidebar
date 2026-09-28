using System;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Sidebar.NativeCompatible;
using Microsoft.Win32.Init.NativeCompatible;
namespace TileLib
{
	public class TileInfo: Attribute
	{
		public string Name;
		public readonly bool hasflyout;
		public readonly bool hasOptions;
		public TileInfo (string name, bool hasflyout, bool hasOptions)
		{
			this.Name = name;
			this.hasflyout = hasflyout;
			this.hasOptions = hasOptions;
		}
	}

	public abstract class BaseTile
	{
		#region Events
		public delegate void CaptionChangedEventHandler (string value);
		public delegate void IconChangedEventHandler (BitmapImage image);
		public delegate void ShowFlyoutEventHandler ();
		public delegate void ShowOptionsEventHandler ();
		public delegate void HeightChangedEventHandler (double height);
		public delegate void ShowNotificationEventHandler (string header, string message);

		public event CaptionChangedEventHandler CaptionChanged;
		public event IconChangedEventHandler IconChanged;
		public event ShowFlyoutEventHandler ShowFlyoutEvent;
		public event ShowOptionsEventHandler ShowOptionsEvent;
		public event HeightChangedEventHandler HeightChangedEvent;
		public event ShowNotificationEventHandler ShowNotificationEvent;
		#endregion
		private string _caption;
		private BitmapImage _icon;
		private bool _isMinimized;
		private bool _isPinned;
		private UserControl _flyoutContent;
		private UserControl _optionsContent;
		private double _height;
		public string _path;
		public string Caption
		{
			get { return _caption; }
			set
			{
				_caption = value;
				CaptionChanged (value);
			}
		}
		public bool IsMinimized
		{
			get { return _isMinimized; }
			set { _isMinimized = value; }
		}
		public bool IsPinned
		{
			get { return _isPinned; }
			set { _isPinned = value; }
		}
		public BitmapImage Icon
		{
			get { return _icon; }
			set
			{
				_icon = value;
				IconChanged (value);
			}
		}
		public double Height
		{
			get { return _height; }
			set
			{
				_height = value;
				if (HeightChangedEvent != null)
					HeightChangedEvent (value);
			}
		}
		public UserControl FlyoutContent
		{
			get { return _flyoutContent; }
			set { _flyoutContent = value; }
		}
		public UserControl OptionsContent
		{
			get { return _optionsContent; }
			set { _optionsContent = value; }
		}
		public abstract UserControl Load ();
		public virtual void Unload () { }
		public virtual void ChangeSide (int side) { }
		public virtual void ChangeLocale (string locale) { }
		public virtual void ChangeTheme (string theme) { }
		public virtual void Minimized () { }
		public virtual void Unminimized () { }
		public virtual void ShowFlyout ()
		{
			ShowFlyoutEvent ();
		}
		public virtual void ShowOptions ()
		{
			ShowOptionsEvent ();
		}
		public virtual void ShowNotification (string header, string message)
		{
			ShowNotificationEvent (header, message);
		}
		private InitConfig ini = null;
		private XmlConfig xml = null;
		public void SetSettingsStorage (InitConfig _ini, XmlConfig _xml)
		{
			ini = _ini;
			xml = _xml;
		}
		public void WriteSetting (string tileName, string setting, string value)
		{
			xml?.GetSection (tileName)?.Set (setting, value);
			return;
		}
		public void WriteSetting (string tileName, string setting, int value)
		{
			xml?.GetSection (tileName)?.Set (setting, value);
			return;
		}
		public void WriteSetting (string tileName, string setting, string [] value)
		{
			xml?.GetSection (tileName)?.Set (setting, value);
		}
		public object ReadSetting (string tileName, string setting)
		{
			return xml?.GetSection (tileName)?.Get<object> (setting);
		}
	}
}

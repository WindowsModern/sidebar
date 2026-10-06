using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using Microsoft.Win32.Init;

namespace Sidebar
{
	public class TileConfig: ITileConfig
	{
		private string _userModelid = null;
		private double _height = 0;
		private bool _autosize = true;
		private DispatcherTimer _heightUpdateTimer = null;
		public double Height
		{
			get { return _height; }
			set
			{
				_height = value;
				_heightUpdateTimer.Tick -= HeightUpdateTimer_Tick;
				_heightUpdateTimer.Tick += HeightUpdateTimer_Tick;
				_heightUpdateTimer?.Stop ();
				_heightUpdateTimer?.Start ();
				OnPropertyChanged ("Height");
			}
		}
		private void HeightUpdateTimer_Tick (object sender, EventArgs e)
		{
			_heightUpdateTimer?.Stop ();
			_heightUpdateTimer.Tick -= HeightUpdateTimer_Tick;
			Ini ["Settings"] ["Height"] = _height;
		}
		public InitConfig Ini { get; }
		public bool Pinned
		{
			get { return App.CurrentUserConfig.IsPinnedBottom (_userModelid); }
			set
			{
				if (value) App.CurrentUserConfig.PinBottom (_userModelid);
				else App.CurrentUserConfig.PinTop (_userModelid);
				OnPropertyChanged ("Pinned");
			}
		}
		public void NotifyPinned ()
		{
			OnPropertyChanged ("Pinned");
		}
		private void InitConfigValues ()
		{
			_height = Ini? ["Settings"]?.GetKey ("Height").ReadDouble (0) ?? 0;
			_autosize = Ini? ["Settings"]?.GetKey ("AutoSize").ReadBool (true) ?? true;
		}
		public XmlConfig Xml { get; }
		public bool AutoSize
		{
			get { return _autosize; }
			set
			{
				_autosize = value;
				Ini ["Settings"] ["AutoSize"] = value;
				OnPropertyChanged ("AutoSize");
			}
		}
		public event PropertyChangedEventHandler PropertyChanged;
		protected virtual void OnPropertyChanged (string propertyName = null)
		{
			PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (propertyName));
		}
		public TileConfig (InitConfig tileIni, XmlConfig tileXml, string userModelId)
		{
			Ini = tileIni;
			Xml = tileXml;
			_userModelid = userModelId;
			if (_heightUpdateTimer == null) _heightUpdateTimer = new DispatcherTimer ();
			_heightUpdateTimer.Interval = TimeSpan.FromSeconds (0.05);
			InitConfigValues ();
		}
		public TileConfig (IProgramFolder tileFolder, string userModelId) :
			this (tileFolder.InitConfig, tileFolder.XmlConfig, userModelId)
		{ }
	}
}

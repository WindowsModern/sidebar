using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Microsoft.Win32.Init;

namespace Sidebar
{
	public class TileConfig: ITileConfig
	{
		private double _height = 0;
		private bool _pinned = false;
		private bool _autosize = true;
		public double Height
		{
			get { return _height; }
			set
			{
				_height = value;
				Ini ["Settings"] ["Height"] = value;
				OnPropertyChanged ("Height");
			}
		}
		public InitConfig Ini { get; }
		public bool Pinned
		{
			get { return _pinned; }
			set
			{
				_pinned = value;
				Ini ["Settings"] ["Pinned"] = value;
				OnPropertyChanged ("Pinned");
			}
		}
		private void InitConfigValues ()
		{
			_height = Ini? ["Settings"]?.GetKey ("Height").ReadDouble (0) ?? 0;
			_pinned = Ini? ["Settings"]?.GetKey ("Pinned").ReadBool (false) ?? false;
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
		public TileConfig (InitConfig tileIni, XmlConfig tileXml)
		{
			Ini = tileIni;
			Xml = tileXml;
			InitConfigValues ();
		}
		public TileConfig (IProgramFolder tileFolder) :
			this (tileFolder.InitConfig, tileFolder.XmlConfig)
		{ }
	}
}

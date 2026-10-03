using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Sidebar
{
	/// <summary>
	/// TileControl.xaml 的交互逻辑
	/// </summary>
	public partial class TileControl: UserControl, ITileElement
	{
		private TileContext tileContext = null;
		public TileInstance Instance { get; private set; }
		private void SetInstance (TileInstance inst) => Instance = inst;
		public static TileControl CreateTileControl (TileApplicationStorage tas, ISidebarFeatures isf = null)
		{
			var ctrl = new TileControl ();
			var inst = new TileInstance (tas, ctrl.ContentContainer, isf);
			ctrl.SetInstance (inst);
			inst.Initialize ();
			return ctrl;
		}
		public TileControl ()
		{
			InitializeComponent ();
			tileContext = new TileContext (this, TileContentControl);
		}
		public Panel ContentContainer => TileContentControl;
		public string DisplayName
		{
			get { return TileDisplayNameControl?.Text; }
			set { TileDisplayNameControl.Text = value; }
		}
		public ImageSource Icon
		{
			get { return TileDisplayImageControl?.Source; }
			set { TileDisplayImageControl.Source = value; }
		}

	}
}

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
		public TileControl ()
		{
			InitializeComponent ();
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

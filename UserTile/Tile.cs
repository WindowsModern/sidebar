using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using Sidebar;
namespace WindowsModern.UserTile
{
	public class Tile: TileBase
	{
		TilePanel tilePanel = null;
		public override void OnInitialize ()
		{
			tilePanel = tilePanel ?? new TilePanel ();
			(tilePanel?.Parent as Panel)?.Children?.Clear ();
			var panel = TileUI as Panel;
			panel.Children.Add (tilePanel);
		}
		public override void OnDestroy ()
		{
			(tilePanel?.Parent as Panel)?.Children?.Clear ();
			tilePanel = null;
		}
	}
}

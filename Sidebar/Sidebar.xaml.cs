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
using System.Windows.Shapes;
using System.Windows.Threading;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using FormContextMenu = System.Windows.Forms.ContextMenu;
using Sidebar;
using System.Collections.Specialized;

namespace Sidebar
{
	/// <summary>
	/// Sidebar.xaml 的交互逻辑
	/// </summary>
	public partial class SidebarWindow: Window
	{
		// Key: UserModelId, Value: TileControl
		Dictionary<string, TileControl> tileCache = new Dictionary<string, TileControl> (NormalizedStringComparer.Instance);
		public SidebarWindow ()
		{
			InitializeComponent ();
			var cc = App.CurrentUserConfig;
			cc.PinnedTiles.CollectionChanged += PinnedTiles_CollectionChanged;
		}
		private TileControl GetOrCreateTile (string userModelId)
		{
			TileControl tc = null;
			if (tileCache.TryGetValue (userModelId, out tc)) return tc;
			var tas = App.TileMgr.GetApplicationByUserModelId (userModelId);
			tc = TileControl.CreateTileControl (tas);
			tileCache [userModelId] = tc;
			return tc;
		}
		private TileControl GetOrCreateTile (TileApplicationStorage tas)
			=> GetOrCreateTile (tas.UserModelId);
		private void InitTiles ()
		{
			var cc = App.CurrentUserConfig;
			var taslist = cc.PinnedTiles.Select (pt => App.TileMgr.GetApplicationByUserModelId (pt)).SkipWhile (item => item == null).ToList ();
			foreach (var tas in taslist)
			{
				
			}
		}
		private void PinnedTiles_CollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:

					break;
				case NotifyCollectionChangedAction.Move:

					break;
				case NotifyCollectionChangedAction.Remove:

					break;
				case NotifyCollectionChangedAction.Replace:

					break;
				case NotifyCollectionChangedAction.Reset:

					break;
			}
		}
		private void Window_Loaded (object sender, RoutedEventArgs e)
		{
			App.CurrentUserConfig.PropertyChanged -= CurrentUserConfig_PropertyChanged;
			App.CurrentUserConfig.PropertyChanged += CurrentUserConfig_PropertyChanged;
		}
		private void CurrentUserConfig_PropertyChanged (object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			var cuc = App.CurrentUserConfig;
			switch (e.PropertyName)
			{
				case nameof (cuc.Direction):
					break;
			}
		}
		private void Window_Closed (object sender, EventArgs e)
		{
			App.CurrentUserConfig.PropertyChanged -= CurrentUserConfig_PropertyChanged;
		}
	}
}

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

namespace SGTheme.Tiles.Feeds
{
	/// <summary>
	/// FlyoutFeedItem.xaml 的交互逻辑
	/// </summary>
	public partial class FlyoutFeedItem: UserControl
	{
		public FlyoutFeedItem ()
		{
			InitializeComponent ();
		}
		private void FeedItemContainer_MouseEnter (object sender, MouseEventArgs e)
		{
			Title.SetResourceReference (StyleProperty, "TileFeedItemTitleHover");
			Desc.SetResourceReference (StyleProperty, "TileFeedItemDescriptionHover");
		}
		private void FeedItemContainer_MouseLeave (object sender, MouseEventArgs e)
		{
			Title.SetResourceReference (StyleProperty, "TileFeedItemTitle");
			Desc.SetResourceReference (StyleProperty, "TileFeedItemDescription");
		}
		bool isdown = false;
		private void FeedItemContainer_MouseDown (object sender, MouseButtonEventArgs e)
		{
			isdown = true;
		}
		private void FeedItemContainer_MouseUp (object sender, MouseButtonEventArgs e)
		{
			if (isdown)
			{

				isdown = false;
			}
		}
		private void FeedItemContainer_TouchDown (object sender, TouchEventArgs e)
		{
			isdown = true;
		}
		private void FeedItemContainer_TouchUp (object sender, TouchEventArgs e)
		{
			if (isdown)
			{

				isdown = false;
			}
		}
		private bool disabledClick = false;
		public bool DisableClick (bool? value = null)
		{
			if (value != null) disabledClick = value ?? false;
			return disabledClick;
		}
	}
}

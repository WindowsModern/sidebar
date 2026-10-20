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
using Sidebar;

namespace WindowsModern.FeedTile
{
	/// <summary>
	/// TileFeedItem.xaml 的交互逻辑
	/// </summary>
	public partial class TileFeedItem: UserControl
	{
		public TileFeedItem ()
		{
			InitializeComponent ();
		}
		private void StackPanel_MouseEnter (object sender, MouseEventArgs e)
		{
			FeedTitle.SetResourceReference (StyleProperty, "TileFeedTitleHover");
		}
		private void StackPanel_MouseLeave (object sender, MouseEventArgs e)
		{
			FeedTitle.SetResourceReference (StyleProperty, "TileFeedTitleNormal");
		}
		private FeedItemData currData = null;
		public void SetItemValue (FeedItemData curr)
		{
			currData = curr;
			FeedTitle.Text = currData?.Title ?? "";
			this.ToolTip = currData?.Title ?? "";
			if (!string.IsNullOrWhiteSpace (currData?.Parent?.Title))
			{
				this.ToolTip += "\n" + currData.Parent.Title;
			}
			if (currData != null)
			{
				this.ToolTip += "\n" + currData.PublishDate.ToString ();
			}
		}
		private void UserControl_SizeChanged (object sender, System.Windows.SizeChangedEventArgs e)
		{
			if (double.IsNaN (this.ActualWidth) || this.ActualWidth <= 0)
				return;
			var grid = ItemPoint.Parent as FrameworkElement; // ItemPoint 的父级是 Grid
			double gridWidth = grid?.ActualWidth ?? 0;
			Thickness gridMargin = (grid as FrameworkElement)?.Margin ?? new Thickness (0);
			double totalGridWidth = gridWidth + gridMargin.Left + gridMargin.Right;
			Thickness titleMargin = FeedTitle.Margin;
			double titleMarginHorizontal = titleMargin.Left + titleMargin.Right;
			double availableWidth = this.ActualWidth - totalGridWidth - titleMarginHorizontal;
			if (availableWidth > 0)
				FeedTitle.MaxWidth = availableWidth;
		}
		public string GetLocalId ()
		{
			return currData?.LocalId;
		}
		public static FeedArticlePanel DetailPanel { get; set; }
		private void RequireShowDetailPage ()
		{
			if (currData != null && !currData.IsRead)
			{
				currData.IsRead = true;
				if (currData.Parent != null && currData.Parent.UnreadCount > 0)
				{
					currData.Parent.UnreadCount--;
					Tile.MarkItemAsRead (currData.Parent?.Path, currData.LocalId);
				}
			}
			if (DetailPanel == null) DetailPanel = new FeedArticlePanel ();
			var page = DetailPanel;
			(page?.Parent as Panel)?.Children?.Clear ();
			page.SetFeedItem (currData);
			Tile.SidebarFeatures.Request (new SidebarRequest (Tile.TileInstance) {
				RequestName = "RequireExtraFlyout",
				TransferDatas = page
			});
		}
		private void ClickContainer_Click (object sender, RoutedEventArgs e)
		{
			RequireShowDetailPage ();
		}
	}
}

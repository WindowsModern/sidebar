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
		bool isdown = false;
		private void StackPanel_MouseLeftButtonDown (object sender, MouseButtonEventArgs e)
		{
			isdown = true;
		}

		private void StackPanel_MouseLeftButtonUp (object sender, MouseButtonEventArgs e)
		{
			if (isdown)
			{
				isdown = false;
			}
		}

		private void StackPanel_TouchDown (object sender, TouchEventArgs e)
		{
			isdown = true;
		}

		private void StackPanel_TouchUp (object sender, TouchEventArgs e)
		{
			if (isdown)
			{
				isdown = false;
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace SGTheme.Tiles.Search
{
	/// <summary>
	/// TilePanel.xaml 的交互逻辑
	/// </summary>
	public partial class TilePanel: UserControl
	{
		private DependencyPropertyDescriptor _tagDescriptor;
		private EventHandler _tagChangedHandler;

		public TilePanel ()
		{
			InitializeComponent ();
			SearchBox.Tag = "Search for...";
			// 保存委托实例，以便后续移除
			_tagChangedHandler = (s, e) => UpdateClip ();
			_tagDescriptor = DependencyPropertyDescriptor.FromProperty (
				FrameworkElement.TagProperty,
				typeof (FrameworkElement));
			_tagDescriptor.AddValueChanged (ClipGrid, _tagChangedHandler);
		}

		private void FlyoutPanel_SearchClick (object sender, RoutedEventArgs e)
		{
			InvokeSearch ();
		}

		private void FlyoutPanel_TextChanged (object sender, TextChangedEventArgs e)
		{
			
		}

		private void UserControl_SizeChanged (object sender, System.Windows.SizeChangedEventArgs e)
		{
			UpdateClip ();
		}

		private void UpdateClip ()
		{
			if (ClipGrid == null || ClipGrid.ActualWidth <= 0 || ClipGrid.ActualHeight <= 0)
				return;

			double radius = 4;
			if (ClipGrid.Tag != null)
			{
				double parsed;
				if (double.TryParse (ClipGrid.Tag.ToString (), out parsed))
					radius = parsed;
			}

			ClipGrid.Clip = new RectangleGeometry {
				RadiusX = radius,
				RadiusY = radius,
				Rect = new Rect (0, 0, ClipGrid.ActualWidth, ClipGrid.ActualHeight)
			};
		}

		public void OnFlyoutInit (UIElement flyoutContent)
		{
			
		}

		public void OnFlyoutClose ()
		{
			
		}

		public void InvokeSearch ()
		{
			
		}

		public void Dispose ()
		{
			// 移除 Tag 属性监听
			if (_tagDescriptor != null && _tagChangedHandler != null)
			{
				_tagDescriptor.RemoveValueChanged (ClipGrid, _tagChangedHandler);
				_tagDescriptor = null;
				_tagChangedHandler = null;
			}

		}

		private void SearchBox_KeyDown (object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				InvokeSearch ();
				e.Handled = true;
			}
		}
	}
}

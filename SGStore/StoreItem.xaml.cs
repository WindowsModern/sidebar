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
	/// StoreItem.xaml 的交互逻辑
	/// </summary>
	public partial class StoreItem: UserControl
	{
		public StoreItem ()
		{
			InitializeComponent ();
			Source = new Uri ("Images\\Gadget.ico", UriKind.RelativeOrAbsolute);
		}
		public static readonly DependencyProperty SourceProperty =
		   DependencyProperty.Register ("Source", typeof (Uri), typeof (StoreItem),
			   new PropertyMetadata (null));
		public static readonly DependencyProperty TitleProperty =
			DependencyProperty.Register ("Title", typeof (string), typeof (StoreItem),
				new PropertyMetadata ("Title"));
		public static readonly DependencyProperty VersionProperty =
			DependencyProperty.Register ("Version", typeof (string), typeof (StoreItem),
				new PropertyMetadata ("Version"));
		public static readonly DependencyProperty PublisherProperty =
			DependencyProperty.Register ("Publisher", typeof (string), typeof (StoreItem),
				new PropertyMetadata ("Publisher"));
		public Uri Source
		{
			get { return (Uri)GetValue (SourceProperty); }
			set { SetValue (SourceProperty, value); }
		}
		public string Title
		{
			get { return (string)GetValue (TitleProperty); }
			set { SetValue (TitleProperty, value); }
		}
		public string Version
		{
			get { return (string)GetValue (VersionProperty); }
			set { SetValue (VersionProperty, value); }
		}
		public string Publisher
		{
			get { return (string)GetValue (PublisherProperty); }
			set { SetValue (PublisherProperty, value); }
		}
	}
}

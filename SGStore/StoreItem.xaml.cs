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
			Source = new Uri (System.IO.Path.Combine (App.AppRoot.FolderPath, "Images\\Gadget.ico"));
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
		public static readonly RoutedEvent ClickEvent =
			EventManager.RegisterRoutedEvent ("Click",
				RoutingStrategy.Bubble,
				typeof (RoutedEventHandler),
				typeof (StoreItem));
		public event RoutedEventHandler Click
		{
			add { AddHandler (ClickEvent, value); }
			remove { RemoveHandler (ClickEvent, value); }
		}
		public static readonly DependencyProperty CommandProperty =
			DependencyProperty.Register ("Command", typeof (ICommand), typeof (StoreItem),
				new PropertyMetadata (null));
		public static readonly DependencyProperty CommandParameterProperty =
			DependencyProperty.Register ("CommandParameter", typeof (object), typeof (StoreItem),
				new PropertyMetadata (null));
		public ICommand Command
		{
			get { return (ICommand)GetValue (CommandProperty); }
			set { SetValue (CommandProperty, value); }
		}
		public object CommandParameter
		{
			get { return GetValue (CommandParameterProperty); }
			set { SetValue (CommandParameterProperty, value); }
		}
		private void InternalButton_Click (object sender, RoutedEventArgs e)
		{
			RaiseEvent (new RoutedEventArgs (ClickEvent, this));
		}
		private TilePackageItem item = null;
		public TilePackageItem ItemData
		{
			get { return item; }
			set
			{
				item = value;
				Source = item?.SupportedVersionLogo;
				Title = item?.DisplayName;
				Version = item?.SupportedNewestVersion;
				Publisher = item?.Publisher;
			}
		}
		public ImageSource Image => StoreLogo?.Source;
	}
}

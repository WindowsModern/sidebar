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
using System.Windows.Media.Animation;
using System.Windows.Controls.Primitives;
using System.Threading.Tasks;

namespace Sidebar
{
	/// <summary>
	/// TileControl.xaml 的交互逻辑
	/// </summary>
	public partial class TileControl: UserControl, ITileElement
	{
		private TileContext tileContext = null;
		public TileInstance Instance { get; private set; }
		public TileApplicationStorage AppStorage { get; private set; }
		public string UserModelId => AppStorage?.UserModelId;
		#region Instance
		private void SetInstance (TileInstance inst)
		{
			if (Instance != null)
			{
				Instance.Config.PropertyChanged -= Config_PropertyChanged;
				TileContentContainer.Children.Clear ();
			}
			Instance = inst;
			if (Instance != null)
			{
				Instance.Config.PropertyChanged -= Config_PropertyChanged;
				Instance.Config.PropertyChanged += Config_PropertyChanged;
			}
		}
		private void Config_PropertyChanged (object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (!Dispatcher.CheckAccess ())
			{
				Dispatcher.BeginInvoke (System.Windows.Threading.DispatcherPriority.DataBind, new Action (() => Config_PropertyChanged (sender, e)));
				return;
			}
			switch (e.PropertyName)
			{
				case nameof (TileConfig.Pinned):
					IsPinned = Instance?.Config?.Pinned ?? false;
					break;
				case nameof (TileConfig.AutoSize):
				case nameof (TileConfig.Height):
					//if (_heightInitialized && !_isAnimating && !_isDragging)
						//PlayRuntimeHeightAnimation ();
					break;
			}
		}
		private void SetAppStorage (TileApplicationStorage tas)
		{
			AppStorage = tas;
			tileContext = new TileContext (this, ContentContainer = InitTileContentContainer ());
		}
		public static TileControl CreateTileControl (TileApplicationStorage tas, ISidebarFeatures isf = null)
		{
			var ctrl = new TileControl ();
			ctrl.SetAppStorage (tas);
			var inst = new TileInstance (tas, ctrl.ContentContainer, isf);
			ctrl.SetInstance (inst);
			inst.Initialize ();
			ctrl.DisplayName = tas.DisplayName;
			ctrl.Icon = tas.LogoImage;
			return ctrl;
		}
		#endregion
		public TileControl ()
		{
			InitializeComponent ();
		}
		public Task DestroyTileControl ()
		{
			return Task.Factory.StartNew (new Action (() => { System.Threading.Thread.Sleep (500); }));
		}
		#region Public Properties
		public Panel ContentContainer { get; private set; }
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
		public Panel InitTileContentContainer ()
		{
			switch (AppStorage.Infomation.VisualElements.Tile.Overflow)
			{
				default:
				case TileOverflow.Auto:
				case TileOverflow.Hidden:
					return TileContentContainer;
				case TileOverflow.Scale:
					var vb = new Viewbox ();
					TileContentContainer.Children.Add (vb);
					var grid = new Grid ();
					vb.Child = grid;
					return grid;
				case TileOverflow.Scroll:
					var sv = new ScrollViewer ();
					sv.SetResourceReference (ScrollViewer.StyleProperty, "AutoHideScrollViewer");
					TileContentContainer.Children.Add (sv);
					var sg = new Grid ();
					sv.Content = sg;
					return sg;
			}
			return TileContentContainer;
		}
		#endregion
		#region Pinned
		public static readonly DependencyProperty IsPinnedProperty = DependencyProperty.Register (nameof (IsPinned), typeof (bool), typeof (TileControl), new FrameworkPropertyMetadata (false, FrameworkPropertyMetadataOptions.AffectsRender));
		public bool IsPinned { get { return (bool)GetValue (IsPinnedProperty); } set { SetValue (IsPinnedProperty, value); } }
		#endregion
		#region Height

		#endregion
		private void RootTileElement_Loaded (object sender, RoutedEventArgs e)
		{
			Instance?.Instance?.OnLoad ();
			//InitializeHeight ();
			//PlayInitialOpacityAnimation ();
		}
		private void RootTileElement_Unloaded (object sender, RoutedEventArgs e)
		{
			Instance?.Instance?.OnUnload ();
		}

		private void TileContentContainerControl_SizeChanged (object sender, System.Windows.SizeChangedEventArgs e)
		{

		}


	}
}
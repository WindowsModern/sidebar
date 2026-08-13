using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Sidebar;

namespace WindowsModern.PowerTile
{
	/// <summary>
	/// FlyoutPanel.xaml 的交互逻辑
	/// </summary>
	public partial class FlyoutPanel: UserControl
	{
		public FlyoutPanel ()
		{
			InitializeComponent ();
			InitLoaleStrings ();
			FoldButton.IsChecked = false;
			var sr = Tile.TileFolder.StringResources;
			PowerPlansPart.Visibility = (FoldButton.IsChecked ?? false) ? Visibility.Visible : Visibility.Collapsed;
			LabelFold.Text = (FoldButton.IsChecked ?? false) ? sr.SuitableResource ("FLYOUT_PP_FOLD") : sr.SuitableResource ("FLYOUT_PP_UNFOLD");
		}
		private void InitLoaleStrings ()
		{
			var sr = Tile.TileFolder.StringResources;
			TitlePowerStatus.Text = sr.SuitableResource ("FLYOUT_PS_TITLE");
			TitlePrds.Text = sr.SuitableResource ("FLYOUT_PRDS_TITLE");
			TitleKeepScreen.Text = sr.SuitableResource ("FLYOUT_PRDS_KEEP_TITLE");
			DescKeepScreen.Text = sr.SuitableResource ("FLYOUT_PRDS_KEEP_TEXT");
			InputKeepScreen.Content = sr.SuitableResource ("FLYOUT_PRDS_KEEP_CHKBTN");
			TitleAsb.Text = sr.SuitableResource ("FLYOUT_ASB_TITLE");
			DescAsb.Text = sr.SuitableResource ("FLYOUT_ASB_TEXT");
			TipDim.Text = sr.SuitableResource ("FLYOUT_ASB_DIM");
			TipBright.Text = sr.SuitableResource ("FLYOUT_ASB_BRIGHT");
			LabelFold.Text = sr.SuitableResource ("FLYOUT_PP_FOLD");
			TitlePowerPlans.Text = sr.SuitableResource ("FLYOUT_PP_TITLE");
			TitleLongest.Text = sr.SuitableResource ("FLYOUT_PP_LONGEST_TITLE");
			DescLongest.Text = sr.SuitableResource ("FLYOUT_PP_LONGEST_TEXT");
			TitleBalanced.Text = sr.SuitableResource ("FLYOUT_PP_BALANCED_TITLE");
			DescBalanced.Text = sr.SuitableResource ("FLYOUT_PP_BALANCED_TEXT");
			TitleHighest.Text = sr.SuitableResource ("FLYOUT_PP_HIGHEST_TITLE");
			DescHighest.Text = sr.SuitableResource ("FLYOUT_PP_HIGHEST_TEXT");
		}
		private void FoldButton_Checked (object sender, RoutedEventArgs e)
		{
			var sr = Tile.TileFolder.StringResources;
			PowerPlansPart.Visibility = (FoldButton.IsChecked ?? false) ? Visibility.Visible : Visibility.Collapsed;
			LabelFold.Text = (FoldButton.IsChecked ?? false) ? sr.SuitableResource ("FLYOUT_PP_FOLD") : sr.SuitableResource ("FLYOUT_PP_UNFOLD");
			//TransToNewHeight (PowerPlansPart, 0);
			TransToNewOpacity (PowerPlansPart, 0, 1).ContinueWith (task => {
				PowerPlansPart.Opacity = 1;
				PowerPlansPart.Visibility = (FoldButton.IsChecked ?? false) ? Visibility.Visible : Visibility.Collapsed;
				Tile.SidebarFeatures.Request (new SidebarRequest (Tile.TileInstance) {
					RequestName = "FlyoutUpdatePosition"
				});
			}, TaskScheduler.FromCurrentSynchronizationContext ());
		}
		private void FoldButton_Unchecked (object sender, RoutedEventArgs e)
		{
			var sr = Tile.TileFolder.StringResources;
			try
			{
				TaskExtra.WhenAll (
					TransToNewOpacity (PowerPlansPart, 1, 0)
					).ContinueWith (task => {
						PowerPlansPart.Visibility = (FoldButton.IsChecked ?? false) ? Visibility.Visible : Visibility.Collapsed;
						Tile.SidebarFeatures.Request (new SidebarRequest (Tile.TileInstance) {
							RequestName = "FlyoutUpdatePosition"
						});
					}, TaskScheduler.FromCurrentSynchronizationContext ());
			}
			catch
			{
				PowerPlansPart.Visibility = (FoldButton.IsChecked ?? false) ? Visibility.Visible : Visibility.Collapsed;
			}
			LabelFold.Text = (FoldButton.IsChecked ?? false) ? sr.SuitableResource ("FLYOUT_PP_FOLD") : sr.SuitableResource ("FLYOUT_PP_UNFOLD");
		}
		private DoubleAnimation _currentHeightAnimation = null;
		private bool _isHeightAnimating = false;
		private int _heightAnimationVersion = 0;
		private Task TransToNewHeight (FrameworkElement component, double elderHeight, double? newHeight = null, TimeSpan? timeout = null)
		{
			var tcs = new TaskCompletionSource<bool> ();
			if (component == null ||
				!component.IsLoaded ||
				component.Visibility == Visibility.Collapsed)
			{
				tcs.SetResult (true);
				return tcs.Task;
			}
			component.UpdateLayout ();
			double targetHeight = newHeight ?? component.ActualHeight;
			if (
				double.IsNaN (targetHeight) ||
				double.IsInfinity (targetHeight) ||
				targetHeight < 0 ||

				double.IsNaN (elderHeight) ||
				double.IsInfinity (elderHeight) ||
				elderHeight < 0)
			{
				tcs.SetResult (true);
				return tcs.Task;
			}
			if (Math.Abs (targetHeight - elderHeight) < 0.01)
			{
				tcs.SetResult (true);
				return tcs.Task;
			}
			int version = ++_heightAnimationVersion;
			double originalHeight = component.Height;
			component.BeginAnimation (
				FrameworkElement.HeightProperty,
				null);
			_isHeightAnimating = true;
			component.Height = elderHeight;
			var animation = new DoubleAnimation {
				From = elderHeight,
				To = targetHeight,
				Duration = timeout ?? TimeSpan.FromSeconds (0.4),
				FillBehavior = FillBehavior.Stop
			};
			EventHandler completedHandler = null;
			completedHandler = (s, e) => {
				animation.Completed -= completedHandler;
				if (version != _heightAnimationVersion)
				{
					tcs.TrySetCanceled ();
					return;
				}
				component.BeginAnimation (
					FrameworkElement.HeightProperty,
					null);
				component.Height = originalHeight;
				_isHeightAnimating = false;
				tcs.TrySetResult (true);
			};
			animation.Completed += completedHandler;
			component.BeginAnimation (
				FrameworkElement.HeightProperty,
				animation,
				HandoffBehavior.SnapshotAndReplace);
			return tcs.Task;
		}
		private void OnHeightAnimationCompleted (object sender, EventArgs e)
		{
		}
		private DoubleAnimation _currentOpacityAnimation = null;
		private int _opacityAnimationVersion = 0;
		private Task TransToNewOpacity (FrameworkElement component, double elderOpacity, double? newOpacity = null, TimeSpan? timeout = null)
		{
			var tcs = new TaskCompletionSource<bool> ();
			if (component == null)
			{
				tcs.SetResult (true);
				return tcs.Task;
			}
			if (!component.IsLoaded)
			{
				tcs.SetResult (true);
				return tcs.Task;
			}
			double targetOpacity = newOpacity ?? component.Opacity;
			if (
				double.IsNaN (targetOpacity) ||
				double.IsInfinity (targetOpacity) ||

				double.IsNaN (elderOpacity) ||
				double.IsInfinity (elderOpacity))
			{
				tcs.SetResult (true);
				return tcs.Task;
			}
			targetOpacity = Math.Max (0, Math.Min (1, targetOpacity));
			elderOpacity = Math.Max (0, Math.Min (1, elderOpacity));
			if (Math.Abs (targetOpacity - elderOpacity) < 0.001)
			{
				tcs.SetResult (true);
				return tcs.Task;
			}
			int version = ++_opacityAnimationVersion;
			double originalOpacity = component.Opacity;
			component.BeginAnimation (
				UIElement.OpacityProperty,
				null);
			component.Opacity = elderOpacity;
			var animation = new DoubleAnimation {
				From = elderOpacity,
				To = targetOpacity,
				Duration = timeout ?? TimeSpan.FromSeconds (0.4),
				FillBehavior = FillBehavior.Stop
			};
			EventHandler completedHandler = null;
			completedHandler = (s, e) => {
				animation.Completed -= completedHandler;
				if (version != _opacityAnimationVersion)
				{
					tcs.TrySetCanceled ();
					return;
				}
				component.BeginAnimation (
					UIElement.OpacityProperty,
					null);
				component.Opacity = originalOpacity;
				tcs.TrySetResult (true);
			};
			animation.Completed += completedHandler;
			_currentOpacityAnimation = animation;
			component.BeginAnimation (
				UIElement.OpacityProperty,
				animation,
				HandoffBehavior.SnapshotAndReplace);
			return tcs.Task;
		}
		private void OnOpacityAnimationCompleted (object sender, EventArgs e) { }
		public void CompleteAnimationImmediately (FrameworkElement target, DependencyProperty property, DoubleAnimation animation)
		{
			if (animation == null) return;
			double endValue = animation.To ?? (double)target.GetValue (property) + (animation.By ?? 0);
			target.BeginAnimation (property, null);
			target.SetValue (property, endValue);
		}
		private void AddEventHandlers ()
		{
			RemoveEventHandlers ();
			InputKeepScreen.Checked += InputKeepScreen_ValueChanged;
			InputKeepScreen.Unchecked += InputKeepScreen_ValueChanged;
			InputBrightness.ValueChanged += InputBrightness_ValueChanged;
			InputPlanLongest.Checked += InputPlanLongest_Checked;
			InputPlanBalanced.Checked += InputPlanBalanced_Checked;
			InputPlanHighest.Checked += InputPlanHighest_Checked;
		}
		private	void RemoveEventHandlers ()
		{
			InputKeepScreen.Checked -= InputKeepScreen_ValueChanged;
			InputKeepScreen.Unchecked -= InputKeepScreen_ValueChanged;
			InputBrightness.ValueChanged -= InputBrightness_ValueChanged;
			InputPlanLongest.Checked -= InputPlanLongest_Checked;
			InputPlanBalanced.Checked -= InputPlanBalanced_Checked;
			InputPlanHighest.Checked -= InputPlanHighest_Checked;
		}
		private void BeginChangeValue () => RemoveEventHandlers ();
		private void EndChangeValue () => AddEventHandlers ();
		private void InputPlanHighest_Checked (object sender, RoutedEventArgs e)
		{
			SystemPowerHelper.SetPerformanceMode (SystemPowerHelper.PowerMode.HighPerformance);
			UpdatePowerModeImage ();
		}
		private void InputPlanBalanced_Checked (object sender, RoutedEventArgs e)
		{
			SystemPowerHelper.SetPerformanceMode (SystemPowerHelper.PowerMode.Balanced);
			UpdatePowerModeImage ();
		}
		private void InputPlanLongest_Checked (object sender, RoutedEventArgs e)
		{
			SystemPowerHelper.SetPerformanceMode (SystemPowerHelper.PowerMode.PowerSaver);
			UpdatePowerModeImage ();
		}
		private void InputBrightness_ValueChanged (object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			SystemPowerHelper.SetBrightness ((byte)InputBrightness.Value);
		}
		private void InputKeepScreen_ValueChanged (object sender, RoutedEventArgs e)
		{
			Tile.TileOptions.KeepScreen = InputKeepScreen.IsChecked ?? false;
		}
		private void UserControl_Loaded (object sender, RoutedEventArgs e)
		{
			InputKeepScreen.IsEnabled = SystemPowerHelper.IsScreenKeepAwakeSupported ();
			InputBrightness.IsEnabled = SystemPowerHelper.IsBrightnessControlSupported ();
			InputPlanBalanced.IsEnabled =
				InputPlanHighest.IsEnabled =
				InputPlanLongest.IsEnabled =
				SystemPowerHelper.IsPerformanceModeSupported ();
			InputKeepScreen.IsChecked = Tile.TileOptions.KeepScreen;
			InputBrightness.Value = (double)SystemPowerHelper.GetCurrentBrightness ();
			var ppm = SystemPowerHelper.GetCurrentPerformanceMode ();
			InputPlanBalanced.IsChecked = ppm == SystemPowerHelper.PowerMode.Balanced;
			InputPlanHighest.IsChecked = ppm == SystemPowerHelper.PowerMode.HighPerformance;
			InputPlanLongest.IsChecked = ppm == SystemPowerHelper.PowerMode.PowerSaver;
			UpdatePowerModeImage ();
			TitlePowerStatus.Text = String.Format (
				Tile.TileFolder.StringResources.SuitableResource ("FLYOUT_PS_TITLE"),
				SystemPowerHelper.GetCurrentPowerModeFriendlyName ()
			);
			SystemPowerHelper.BrightnessChanged += SystemPowerHelper_BrightnessChanged;
			SystemPowerHelper.PerformanceModeChanged += SystemPowerHelper_PerformanceModeChanged;
			SystemPowerHelper.StartBrightnessWatcher ();
			SystemPowerHelper.StartPerformanceModeWatcher ();
			AddEventHandlers ();
			if (TilePanelContainer.Children.Count == 0)
				TilePanelContainer.Children.Add (new TilePanel ());
		}
		private void SystemPowerHelper_PerformanceModeChanged (SystemPowerHelper.PowerMode obj)
		{
			try
			{
				BeginChangeValue ();
				InputPlanLongest.IsChecked = obj == SystemPowerHelper.PowerMode.PowerSaver;
				InputPlanBalanced.IsChecked = obj == SystemPowerHelper.PowerMode.Balanced;
				InputPlanHighest.IsChecked = obj == SystemPowerHelper.PowerMode.HighPerformance;
				TitlePowerStatus.Text = String.Format (
					Tile.TileFolder.StringResources.SuitableResource ("FLYOUT_PS_TITLE"),
					SystemPowerHelper.GetCurrentPowerModeFriendlyName ()
				);
				UpdatePowerModeImage ();
			}
			finally
			{
				EndChangeValue ();
			}
		}
		private void SystemPowerHelper_BrightnessChanged (byte obj)
		{
			try
			{
				BeginChangeValue ();
				InputBrightness.Value = (double)obj;
			}
			finally
			{
				EndChangeValue ();
			}
		}
		private void UserControl_Unloaded (object sender, RoutedEventArgs e)
		{
			if (TilePanelContainer.Children.Count != 0)
			{
				var tilePanel = TilePanelContainer.Children [0] as TilePanel;
				(tilePanel?.Parent as Panel)?.Children?.Clear ();
				tilePanel?.Dispose ();
				TilePanelContainer.Children.Clear ();
			}
			SystemPowerHelper.StopBrightnessWatcher ();
			SystemPowerHelper.StopPerformanceModeWatcher ();
			SystemPowerHelper.BrightnessChanged -= SystemPowerHelper_BrightnessChanged;
			SystemPowerHelper.PerformanceModeChanged -= SystemPowerHelper_PerformanceModeChanged;
			RemoveEventHandlers ();
		}
		private void UpdatePowerModeImage ()
		{
			var basedir = Tile.TileFolder.FolderPath;
			if (InputPlanBalanced.IsChecked ?? false)
				ImagePowerPlan.Source = ImageSourceManager.GetImage (System.IO.Path.Combine (basedir, "Images\\Balanced.png"));
			else if (InputPlanHighest.IsChecked ?? false)
				ImagePowerPlan.Source = ImageSourceManager.GetImage (System.IO.Path.Combine (basedir, "Images\\Highest.png"));
			else if (InputPlanLongest.IsChecked ?? false)
				ImagePowerPlan.Source = ImageSourceManager.GetImage (System.IO.Path.Combine (basedir, "Images\\Longest.png"));
			else ImagePowerPlan.Source = null;
		}
	}
}

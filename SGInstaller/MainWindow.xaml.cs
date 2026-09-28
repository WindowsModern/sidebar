using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SGInstaller
{
	public partial class MainWindow: Window
	{
		private readonly TranslateTransform pageTransition;
		public MainWindow ()
		{
			InitializeComponent ();
			pageTransition = new TranslateTransform ();
			PageHost.RenderTransform = pageTransition;
			DataContextChanged += OnDataContextChanged;
			Loaded += (s, e) => PlayPageTransition ();
		}
		private void OnDataContextChanged (object sender, DependencyPropertyChangedEventArgs e)
		{
			var oldVm = e.OldValue as MainViewModel;
			var newVm = e.NewValue as MainViewModel;
			if (oldVm != null) oldVm.PropertyChanged -= OnMainVmPropertyChanged;
			if (newVm != null) newVm.PropertyChanged += OnMainVmPropertyChanged;
		}
		private void OnMainVmPropertyChanged (object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "CurrentPage")
			{
				PlayPageTransition ();
			}
		}
		private void PlayPageTransition ()
		{
			if (PageHost == null) return;
			var duration = new Duration (TimeSpan.FromMilliseconds (350));
			var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
			pageTransition.BeginAnimation (TranslateTransform.XProperty, null);
			pageTransition.X = 32;
			var opacityAnim = new DoubleAnimation (0, 1, duration) { EasingFunction = ease };
			var translateAnim = new DoubleAnimation (32, 0, duration) { EasingFunction = ease };
			PageHost.BeginAnimation (UIElement.OpacityProperty, opacityAnim);
			pageTransition.BeginAnimation (TranslateTransform.XProperty, translateAnim);
		}
	}
}
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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Sidebar
{
	/// <summary>
	/// FlyoutMask.xaml 的交互逻辑
	/// </summary>
	public partial class FlyoutMask: UserControl
	{
		public FlyoutMask ()
		{
			InitializeComponent ();
		}
		public static readonly DependencyProperty ChildProperty =
			 DependencyProperty.Register (
				 nameof (Child),
				 typeof (object),
				 typeof (FlyoutMask),
				 new PropertyMetadata (null, OnChildChanged));

		public object Child
		{
			get { return GetValue (ChildProperty); }
			set { SetValue (ChildProperty, value); }
		}
		private static void OnChildChanged (DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var control = (FlyoutMask)d;
			control.StopCurrentAnimation ();
			control.LoadingFlyout.Opacity = 0.0;
			control.LoadingFlyout.Visibility = Visibility.Collapsed;
			control._currentOpacityStoryboard = null;
			if (control.IsShowing)
			{
				control.ShowInternal (); 
			}
		}
		public static readonly DependencyProperty IsShowingProperty =
		   DependencyProperty.Register (
			   "IsShowing",
			   typeof (bool),
			   typeof (FlyoutMask),
			   new PropertyMetadata (false, OnIsShowingChanged));
		public bool IsShowing
		{
			get { return (bool)GetValue (IsShowingProperty); }
			set { SetValue (IsShowingProperty, value); }
		}
		private static void OnIsShowingChanged (DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var control = (FlyoutMask)d;
			if ((bool)e.NewValue) control.ShowInternal (); 
			else control.HideInternal ();   
		}
		private Storyboard _currentOpacityStoryboard;
		/// <summary>
		/// 显示遮罩（淡入动画），同时将 IsShowing 置为 true
		/// </summary>
		public void Show ()
		{
			if (IsShowing) return;
			IsShowing = true;
		}
		/// <summary>
		/// 隐藏遮罩（淡出动画），同时将 IsShowing 置为 false
		/// </summary>
		public void Hide ()
		{
			if (!IsShowing) return;
			IsShowing = false;
		}
		private void ShowInternal (double durationSeconds = 0.4)
		{
			if (LoadingFlyout.Visibility == Visibility.Visible && LoadingFlyout.Opacity >= 1.0)
				return;
			StopCurrentAnimation ();
			LoadingFlyout.Visibility = Visibility.Visible;
			var animation = new DoubleAnimation {
				From = LoadingFlyout.Opacity,
				To = 1.0,
				Duration = TimeSpan.FromSeconds (durationSeconds),
				FillBehavior = FillBehavior.Stop
			};
			animation.Completed += (s, e) => {
				LoadingFlyout.Opacity = 1.0;
				_currentOpacityStoryboard = null;
			};
			_currentOpacityStoryboard = new Storyboard ();
			Storyboard.SetTarget (animation, LoadingFlyout);
			Storyboard.SetTargetProperty (animation, new PropertyPath (UIElement.OpacityProperty));
			_currentOpacityStoryboard.Children.Add (animation);
			_currentOpacityStoryboard.Begin ();
		}
		private void HideInternal (double durationSeconds = 0.4)
		{
			if (LoadingFlyout.Visibility == Visibility.Collapsed && LoadingFlyout.Opacity <= 0.0)
				return;
			StopCurrentAnimation ();
			var animation = new DoubleAnimation {
				From = LoadingFlyout.Opacity,
				To = 0.0,
				Duration = TimeSpan.FromSeconds (durationSeconds),
				FillBehavior = FillBehavior.Stop
			};
			animation.Completed += (s, e) => {
				LoadingFlyout.Opacity = 0.0;
				LoadingFlyout.Visibility = Visibility.Collapsed;
				_currentOpacityStoryboard = null;
			};
			_currentOpacityStoryboard = new Storyboard ();
			Storyboard.SetTarget (animation, LoadingFlyout);
			Storyboard.SetTargetProperty (animation, new PropertyPath (UIElement.OpacityProperty));
			_currentOpacityStoryboard.Children.Add (animation);
			_currentOpacityStoryboard.Begin ();
		}
		private void StopCurrentAnimation ()
		{
			if (_currentOpacityStoryboard != null)
			{
				_currentOpacityStoryboard.Stop ();
				_currentOpacityStoryboard = null;
			}
		}
		public DataTemplate FlyoutTemplate
		{
			get { return (DataTemplate)GetValue (FlyoutTemplateProperty); }
			set { SetValue (FlyoutTemplateProperty, value); }
		}
		public static readonly DependencyProperty FlyoutTemplateProperty =
			DependencyProperty.Register ("FlyoutTemplate", typeof (DataTemplate), typeof (FlyoutMask));
	}
}

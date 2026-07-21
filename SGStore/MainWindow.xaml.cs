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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Sidebar
{
	/// <summary>
	/// MainWindow.xaml 的交互逻辑
	/// </summary>
	public partial class MainWindow: Window
	{
		public MainWindow ()
		{
			InitializeComponent ();
		}
		private DependencyPropertyDescriptor _opacityDescriptor;
		private void Window_Loaded (object sender, RoutedEventArgs e)
		{
			_opacityDescriptor = DependencyPropertyDescriptor.FromProperty (OpacityProperty, typeof (UIElement));
			_opacityDescriptor.AddValueChanged (LoadingFlyout, OnLoadingFlyoutOpacityChanged);
			UpdateVisibilityFromOpacity ();
			LoadList ();
		}
		private void Window_Unloaded (object sender, RoutedEventArgs e)
		{
			if (_opacityDescriptor != null)
			{
				_opacityDescriptor.RemoveValueChanged (LoadingFlyout, OnLoadingFlyoutOpacityChanged);
				_opacityDescriptor = null;
			}
		}
		private void OnLoadingFlyoutOpacityChanged (object sender, EventArgs e)
		{
			UpdateVisibilityFromOpacity ();
		}
		private void UpdateVisibilityFromOpacity ()
		{
			LoadingFlyout.Visibility = LoadingFlyout.Opacity > 0 ? Visibility.Visible : Visibility.Collapsed;
		}
		private Storyboard _currentOpacityStoryboard; // 跟踪当前播放的动画
		/// <summary>
		/// 淡入显示元素（从当前 Opacity 动画到 1）
		/// </summary>
		/// <param name="element">要显示的元素</param>
		/// <param name="durationSeconds">动画持续时间（秒），默认 0.4</param>
		private void ShowElement (UIElement element, double durationSeconds = 0.4)
		{
			if (element == null) return;
			if (element.Visibility == Visibility.Visible && element.Opacity >= 1.0)
				return;
			StopCurrentAnimation ();
			element.Visibility = Visibility.Visible;
			var animation = new DoubleAnimation {
				From = element.Opacity,
				To = 1.0,
				Duration = TimeSpan.FromSeconds (durationSeconds),
				FillBehavior = FillBehavior.Stop 
			};
			animation.Completed += (s, e) =>
			{
				element.Opacity = 1.0;
				_currentOpacityStoryboard = null;
			};
			_currentOpacityStoryboard = new Storyboard ();
			Storyboard.SetTarget (animation, element);
			Storyboard.SetTargetProperty (animation, new PropertyPath (UIElement.OpacityProperty));
			_currentOpacityStoryboard.Children.Add (animation);
			_currentOpacityStoryboard.Begin ();
		}
		/// <summary>
		/// 淡出隐藏元素（从当前 Opacity 动画到 0）
		/// </summary>
		/// <param name="element">要隐藏的元素</param>
		/// <param name="durationSeconds">动画持续时间（秒），默认 0.4</param>
		private void HideElement (UIElement element, double durationSeconds = 0.4)
		{
			if (element == null) return;
			if (element.Visibility == Visibility.Collapsed && element.Opacity <= 0.0)
				return;
			StopCurrentAnimation ();
			var animation = new DoubleAnimation {
				From = element.Opacity,
				To = 0.0,
				Duration = TimeSpan.FromSeconds (durationSeconds),
				FillBehavior = FillBehavior.Stop
			};
			animation.Completed += (s, e) =>
			{
				element.Opacity = 0.0;
				element.Visibility = Visibility.Collapsed;
				_currentOpacityStoryboard = null;
			};
			_currentOpacityStoryboard = new Storyboard ();
			Storyboard.SetTarget (animation, element);
			Storyboard.SetTargetProperty (animation, new PropertyPath (UIElement.OpacityProperty));
			_currentOpacityStoryboard.Children.Add (animation);
			_currentOpacityStoryboard.Begin ();
		}
		/// <summary>
		/// 停止当前正在播放的动画，释放资源
		/// </summary>
		private void StopCurrentAnimation ()
		{
			if (_currentOpacityStoryboard != null)
			{
				_currentOpacityStoryboard.Stop ();
				_currentOpacityStoryboard = null;
			}
		}
		/// <summary>
		/// 异步加载数据列表，并更新界面
		/// </summary>
		private void LoadList ()
		{
			// 显示加载浮出（淡入）
			ShowElement (LoadingFlyout);

			// 开始异步获取数据
			WebAPI.GetListAsync ()
				.ContinueWith (task => {
			// 所有 UI 更新必须通过 Dispatcher 调度到 UI 线程
			Dispatcher.Invoke (new Action (() => {
						try
						{
							if (task.IsFaulted)
							{
						// 处理异常（显示错误信息）
						MessageBox.Show ($"加载数据失败：{task.Exception?.InnerException?.Message ?? "未知错误"}",
												"错误", MessageBoxButton.OK, MessageBoxImage.Error);
								return;
							}

							var result = task.Result; // TileList
					if (result?.List == null || result.List.Count == 0)
							{
								MessageBox.Show ("未获取到任何数据。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
								return;
							}

					// 清空现有面板
					DownTilesPanel.Children.Clear ();

					// 遍历数据，创建 StoreItem 并添加到面板
					foreach (var item in result.List)
							{
								var storeItem = new StoreItem {
							// 取第一个可用版本的 Logo（或使用 RandomLogo 属性）
							Source = item.RandomLogo,
									Title = item.DisplayName ?? "未命名",
									Version = item.SupportedNewestVersion ?? item.NewestVersion ?? "未知版本",
									Publisher = item.Publisher ?? "未知发布者"
								};
								DownTilesPanel.Children.Add (storeItem);
							}

					// 更新状态文本
					ItemsCount.Text = $"Elements: {result.List.Count}";
							DownTilesCaption.Text = $"Downloadable Tiles ({result.List.Count})";
						}
						catch (Exception ex)
						{
							MessageBox.Show ($"处理数据时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
						}
						finally
						{
					// 无论成败，隐藏加载浮出（淡出）
					HideElement (LoadingFlyout);
						}
					}));
				});
		}
	}
}

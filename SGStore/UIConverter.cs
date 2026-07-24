using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Sidebar
{
	public class TextEmptyToVisibilityConverter: IValueConverter
	{
		public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
		{
			var text = value as string;
			return string.IsNullOrEmpty (text) ? Visibility.Visible : Visibility.Collapsed;
		}
		public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}
	}
	public static class VisibilityAutoController
	{
		// 存储已注册元素及其对应的事件处理器委托
		private static readonly Dictionary<UIElement, EventHandler> _registeredElements
			= new Dictionary<UIElement, EventHandler> ();

		/// <summary>
		/// 为指定元素注册监视：当 Opacity 变化时，自动调整 Visibility。
		/// 若 Opacity > 0 则可见，否则 Collapsed。
		/// 重复注册同一个元素会被忽略。
		/// </summary>
		public static void Register (UIElement element)
		{
			if (element == null)
				throw new ArgumentNullException ("element");

			// 避免重复注册
			if (_registeredElements.ContainsKey (element))
				return;

			// 创建事件处理器（不使用 lambda 捕获，以便注销时能匹配）
			EventHandler handler = new EventHandler ((sender, e) =>
			{
				UIElement ui = sender as UIElement;
				if (ui != null)
				{
					// 根据当前透明度设置可见性
					ui.Visibility = ui.Opacity > 0 ? Visibility.Visible : Visibility.Collapsed;
				}
			});

			// 立即应用当前透明度状态（防止初始状态与规则不符）
			handler (element, EventArgs.Empty);

			// 获取 Opacity 属性的依赖属性描述符，订阅值变化事件
			DependencyPropertyDescriptor descriptor = DependencyPropertyDescriptor.FromProperty (
				UIElement.OpacityProperty, typeof (UIElement));
			descriptor.AddValueChanged (element, handler);

			// 保存记录，便于注销
			_registeredElements [element] = handler;
		}

		/// <summary>
		/// 注销指定元素的自动可见性绑定，停止监视。
		/// </summary>
		public static void Unregister (UIElement element)
		{
			if (element == null)
				return;

			EventHandler handler = null;
			if (_registeredElements.TryGetValue (element, out handler))
			{
				// 取消订阅事件
				DependencyPropertyDescriptor descriptor = DependencyPropertyDescriptor.FromProperty (
					UIElement.OpacityProperty, typeof (UIElement));
				descriptor.RemoveValueChanged (element, handler);

				// 从字典中移除
				_registeredElements.Remove (element);
			}
		}
	}
}

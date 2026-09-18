using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WindowsModern.NotificationHistoryTile
{
	/// <summary>
	/// 通知历史面板：横向时间轴布局，把时间相近的通知合并成小栈。
	/// 与 <see cref="NotificationHistory"/> 通过 <see cref="History"/> 属性对接。
	/// 使用方式：
	/// <code>
	/// var panel = new NotificationPanel();
	/// panel.History = history;
	/// panel.NotificationClicked += (s, data) => { ... };
	/// </code>
	/// </summary>
	public class NotificationPanel: Panel, IDisposable
	{
		// -------- 常量 --------
		private const int MaxTotalItems = 8;      // 面板最多显示的图标数
		private const int MaxStackSize = 4;      // 每个栈最多元素数
		private const double StackGapThreshold = 20.0;   // 两栈之间小于此距离会被合并
		private const double MinTimeWindowMinutes = 30.0;   // 时间轴最小窗口（分钟）
		private const double ElementBaseSize = 15.0;   // 图标基准尺寸
		private const double ElementSpacing = 4.0;    // 栈内元素间距
		private const double DefaultPanelHeight = 30.0;   // 面板默认高度

		// -------- 内部状态 --------
		private readonly List<StackInfo> stacks = new List<StackInfo> ();
		private readonly Dictionary<NotificationElement, double> elementOffsets
			= new Dictionary<NotificationElement, double> ();
		private readonly DispatcherTimer refreshTimer;

		private bool isLoaded;
		private bool disposed;
		private double panelWidth;
		private double minutesSinceOldest;

		/// <summary>一个栈：若干 NotificationElement + 一个水平偏移。</summary>
		private sealed class StackInfo
		{
			public readonly List<NotificationElement> Elements = new List<NotificationElement> ();
			public double Offset;
		}

		#region History 依赖属性

		public static readonly DependencyProperty HistoryProperty =
			DependencyProperty.Register (
				"History",
				typeof (NotificationHistory),
				typeof (NotificationPanel),
				new FrameworkPropertyMetadata (
					null,
					FrameworkPropertyMetadataOptions.AffectsMeasure
					| FrameworkPropertyMetadataOptions.AffectsArrange,
					OnHistoryChanged));

		/// <summary>数据源，外部设置为 NotificationHistory 实例即可。</summary>
		public NotificationHistory History
		{
			get { return (NotificationHistory)GetValue (HistoryProperty); }
			set { SetValue (HistoryProperty, value); }
		}

		private static void OnHistoryChanged (DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			((NotificationPanel)d).OnHistoryChanged (
				(NotificationHistory)e.OldValue,
				(NotificationHistory)e.NewValue);
		}

		private void OnHistoryChanged (NotificationHistory oldValue, NotificationHistory newValue)
		{
			// 先断开旧源
			if (oldValue != null)
				oldValue.CollectionChanged -= History_CollectionChanged;

			// 清空现有 UI
			ClearAllInternal ();

			// 如果已加载，立即接入新源并填充
			if (newValue != null && isLoaded)
			{
				newValue.CollectionChanged += History_CollectionChanged;
				LoadFromHistory (newValue);
			}

			InvalidateMeasure ();
			InvalidateArrange ();
		}

		#endregion

		#region 对外事件

		/// <summary>通知图标被点击的事件委托。</summary>
		public delegate void NotificationClickedEventHandler (object sender, NotificationStoreData data);

		public event NotificationClickedEventHandler NotificationClicked;

		protected virtual void OnNotificationClicked (NotificationStoreData data)
		{
			var handler = NotificationClicked;
			if (handler != null) handler (this, data);
		}

		#endregion

		#region 构造

		public NotificationPanel ()
		{
			Height = DefaultPanelHeight;
			SnapsToDevicePixels = true;
			ClipToBounds = true;

			refreshTimer = new DispatcherTimer (DispatcherPriority.Background) {
				Interval = TimeSpan.FromMinutes (1)
			};
			refreshTimer.Tick += RefreshTimer_Tick;

			// 使用 Loaded/Unloaded 管理订阅和计时器
			Loaded += NotificationPanel_Loaded;
			Unloaded += NotificationPanel_Unloaded;
		}

		#endregion

		#region 生命周期

		private void NotificationPanel_Loaded (object sender, RoutedEventArgs e)
		{
			if (disposed || isLoaded) return;
			isLoaded = true;

			var h = History;
			if (h != null)
			{
				h.CollectionChanged += History_CollectionChanged;
				LoadFromHistory (h);
			}

			if (!refreshTimer.IsEnabled)
				refreshTimer.Start ();

			InvalidateMeasure ();
			InvalidateArrange ();
		}

		private void NotificationPanel_Unloaded (object sender, RoutedEventArgs e)
		{
			if (!isLoaded) return;
			isLoaded = false;

			var h = History;
			if (h != null)
				h.CollectionChanged -= History_CollectionChanged;

			refreshTimer.Stop ();
			ClearAllInternal ();

			InvalidateMeasure ();
			InvalidateArrange ();
		}

		/// <summary>清理事件订阅、定时器与可视化子元素。</summary>
		public void Dispose ()
		{
			if (disposed) return;
			disposed = true;

			Loaded -= NotificationPanel_Loaded;
			Unloaded -= NotificationPanel_Unloaded;

			var h = History;
			if (h != null)
				h.CollectionChanged -= History_CollectionChanged;

			refreshTimer.Tick -= RefreshTimer_Tick;
			refreshTimer.Stop ();

			ClearAllInternal ();
		}

		#endregion

		#region 集合同步（响应式）

		private void History_CollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
		{
			if (disposed) return;

			// 如果集合变更来自非 UI 线程，切回 UI 线程
			if (!Dispatcher.CheckAccess ())
			{
				Dispatcher.BeginInvoke (
					new Action (() => History_CollectionChanged (sender, e)),
					DispatcherPriority.Background);
				return;
			}

			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					if (e.NewItems != null)
					{
						foreach (var obj in e.NewItems)
						{
							var data = obj as NotificationStoreData;
							if (data != null) AddElement (data);
						}
					}
					break;

				case NotifyCollectionChangedAction.Remove:
					if (e.OldItems != null)
					{
						foreach (var obj in e.OldItems)
						{
							var data = obj as NotificationStoreData;
							if (data != null) RemoveElementByData (data);
						}
					}
					break;

				case NotifyCollectionChangedAction.Replace:
				case NotifyCollectionChangedAction.Move:
				case NotifyCollectionChangedAction.Reset:
				default:
					ClearAllInternal ();
					if (History != null) LoadFromHistory (History);
					break;
			}

			InvalidateMeasure ();
			InvalidateArrange ();
		}

		private void LoadFromHistory (NotificationHistory history)
		{
			if (history == null) return;

			// NotificationHistory 枚举顺序为“旧 -> 新”，只取最近 MaxTotalItems 条
			int total = history.Count;
			int skip = Math.Max (0, total - MaxTotalItems);

			int index = 0;
			foreach (var data in history)
			{
				if (index++ < skip) continue;
				if (data == null) continue;
				AddElement (data);
			}
		}

		private void AddElement (NotificationStoreData data)
		{
			if (data == null) return;

			var element = new NotificationElement {
				DataContext = data,
				Width = ElementBaseSize,
				Height = ElementBaseSize
			};
			element.Click += Element_Click;

			Children.Add (element);
			stacks.Add (new StackInfo { Elements = { element } });

			// 超出上限时移除最旧的（左端的栈）
			while (Children.Count > MaxTotalItems && stacks.Count > 0)
				RemoveStackAt (0);
		}

		private void RemoveElementByData (NotificationStoreData data)
		{
			for (int i = 0; i < Children.Count; i++)
			{
				var el = Children [i] as NotificationElement;
				if (el != null && ReferenceEquals (el.DataContext, data))
				{
					RemoveElement (el);
					return;
				}
			}
		}

		private void RemoveElement (NotificationElement element)
		{
			if (element == null) return;

			element.Click -= Element_Click;
			elementOffsets.Remove (element);

			for (int i = 0; i < stacks.Count; i++)
			{
				var stack = stacks [i];
				if (stack.Elements.Remove (element))
				{
					if (stack.Elements.Count == 0) stacks.RemoveAt (i);
					break;
				}
			}

			Children.Remove (element);
		}

		private void RemoveStackAt (int index)
		{
			if (index < 0 || index >= stacks.Count) return;
			var stack = stacks [index];
			for (int i = 0; i < stack.Elements.Count; i++)
			{
				var el = stack.Elements [i];
				el.Click -= Element_Click;
				elementOffsets.Remove (el);
				Children.Remove (el);
			}
			stack.Elements.Clear ();
			stacks.RemoveAt (index);
		}

		private void ClearAllInternal ()
		{
			foreach (UIElement child in Children)
			{
				var button = child as NotificationElement;
				if (button != null) button.Click -= Element_Click;
			}
			Children.Clear ();
			stacks.Clear ();
			elementOffsets.Clear ();
			minutesSinceOldest = 0;
			panelWidth = 0;
		}

		private void Element_Click (object sender, RoutedEventArgs e)
		{
			var element = sender as NotificationElement;
			if (element == null) return;

			var data = element.DataContext as NotificationStoreData;
			if (data != null) OnNotificationClicked (data);
		}

		#endregion

		#region 布局

		private void RefreshTimer_Tick (object sender, EventArgs e)
		{
			// 每分钟重算时间轴
			InvalidateMeasure ();
			InvalidateArrange ();
		}

		protected override Size MeasureOverride (Size availableSize)
		{
			if (double.IsPositiveInfinity (availableSize.Width))
				availableSize.Width = 0;

			if (availableSize.Width != panelWidth)
			{
				panelWidth = availableSize.Width;
				SplitAllStacks ();
			}

			RecomputePositions ();
			FixStacks ();

			var childConstraint = new Size (ElementBaseSize, ElementBaseSize);
			foreach (UIElement child in Children)
				child.Measure (childConstraint);

			return new Size (availableSize.Width, DefaultPanelHeight);
		}

		protected override Size ArrangeOverride (Size finalSize)
		{
			double width = finalSize.Width;

			// 1) 左 -> 右：防止栈重叠
			double cursor = 0;
			for (int i = 0; i < stacks.Count; i++)
			{
				var stack = stacks [i];
				double stackWidth = ElementBaseSize + ElementSpacing * (stack.Elements.Count - 1);
				stack.Offset = Math.Max (cursor, stack.Offset);
				cursor = stack.Offset + stackWidth;
			}

			// 2) 右 -> 左：防止越界
			double right = width;
			for (int i = stacks.Count - 1; i >= 0; i--)
			{
				var stack = stacks [i];
				double stackWidth = ElementBaseSize + ElementSpacing * (stack.Elements.Count - 1);
				stack.Offset = Math.Min (stack.Offset, right - stackWidth);
				right = stack.Offset;
			}

			// 3) 排列每个元素，形成错位堆叠
			for (int i = 0; i < stacks.Count; i++)
			{
				var stack = stacks [i];
				int count = stack.Elements.Count;
				for (int k = 0; k < count; k++)
				{
					var el = stack.Elements [k];
					double x = stack.Offset + ElementSpacing * (k - 1);
					double y = ElementBaseSize - ElementSpacing * (count - 1)
							 + ElementSpacing * k;

					el.Arrange (new Rect (x, y, ElementBaseSize, ElementBaseSize));
				}
			}

			return finalSize;
		}

		#endregion

		#region 时间轴与分栈

		private void RecomputePositions ()
		{
			if (Children.Count == 0) return;

			var oldest = Children [0] as NotificationElement;
			var oldestData = oldest != null ? oldest.DataContext as NotificationStoreData : null;
			if (oldestData == null) return;

			DateTime now = DateTime.UtcNow;
			double oldestAge = (now - oldestData.TimeStampUtc).TotalMinutes;
			minutesSinceOldest = Math.Max (MinTimeWindowMinutes, oldestAge);

			elementOffsets.Clear ();

			for (int i = 0; i < Children.Count; i++)
			{
				var el = Children [i] as NotificationElement;
				if (el == null) continue;

				var data = el.DataContext as NotificationStoreData;
				if (data == null) continue;

				double input = (now - data.TimeStampUtc).TotalMinutes / minutesSinceOldest;
				double transformed = Transform (input);

				// 旧在左（0），新在右（panelWidth）
				elementOffsets [el] = (1.0 - transformed) * panelWidth;
			}

			// 用栈的第一个元素的位置作为栈位置
			for (int i = 0; i < stacks.Count; i++)
			{
				var stack = stacks [i];
				if (stack.Elements.Count == 0) continue;

				double off;
				if (elementOffsets.TryGetValue (stack.Elements [0], out off))
					stack.Offset = off;
			}
		}

		private double Transform (double input)
		{
			// 超过最小时间窗口时用平方根压缩旧通知，让分布更均匀
			if (minutesSinceOldest > MinTimeWindowMinutes)
				return Math.Sqrt (input);
			return input;
		}

		private void SplitAllStacks ()
		{
			stacks.Clear ();
			foreach (UIElement child in Children)
			{
				var el = child as NotificationElement;
				if (el != null)
					stacks.Add (new StackInfo { Elements = { el } });
			}
		}

		private void FixStacks ()
		{
			for (int i = 0; i < stacks.Count; i++)
			{
				var stack = stacks [i];

				while (stack.Elements.Count < MaxStackSize && i + 1 < stacks.Count)
				{
					var next = stacks [i + 1];

					// 跳过空栈
					if (next.Elements.Count == 0)
					{
						stacks.RemoveAt (i + 1);
						continue;
					}

					if (next.Offset - stack.Offset > StackGapThreshold)
						break;

					CombineStacks (stack, next);

					if (next.Elements.Count == 0)
						stacks.RemoveAt (i + 1);
				}
			}
		}

		private static void CombineStacks (StackInfo a, StackInfo b)
		{
			while (a.Elements.Count < MaxStackSize && b.Elements.Count > 0)
			{
				var el = b.Elements [0];
				b.Elements.RemoveAt (0);
				a.Elements.Add (el);
			}
		}

		#endregion
	}
}
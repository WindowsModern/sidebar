using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Data;

namespace WindowsModern.NotificationHistoryTile
{
	/// <summary>
	/// 单个日期的通知分组。
	/// 组内成员按 TimeStampUtc 从最近到最远排列。
	/// </summary>
	public sealed class NotificationGroup
		: ObservableCollection<NotificationStoreData>, IDisposable
	{
		public DateTime Date { get; }
		public NotificationGroup (DateTime date)
		{
			Date = date;
		}
		/// <summary>
		/// 按 TimeStampUtc 倒序插入到正确位置。
		/// 使用二分查找 + ObservableCollection.Insert，让 UI 只收到一次 Insert 通知。
		/// </summary>
		public void InsertSorted (NotificationStoreData item)
		{
			if (item == null) return;

			int lo = 0;
			int hi = Count;

			while (lo < hi)
			{
				int mid = (lo + hi) / 2;

				// 倒序：this[mid] 比 item 旧 -> 往左找
				if (this [mid].TimeStampUtc < item.TimeStampUtc)
					hi = mid;
				else
					lo = mid + 1;
			}

			Insert (lo, item);
		}

		public void Dispose ()
		{
			// 仅清理自身元素，不负责解除事件订阅；
			// 订阅由 GroupedNotificationHistory 统一管理。
			Clear ();
		}
	}
	/// <summary>
	/// 按日期分组的通知历史视图。
	/// - 组按日期从最近到最远排序。
	/// - 组内成员按 TimeStampUtc 从最近到最远排序。
	/// - 响应 NotificationHistory 的集合变化，最小化更新。
	/// - 需在 UI 线程使用（内部使用 ObservableCollection）。
	/// </summary>
	public sealed class GroupedNotificationHistory
		: ObservableCollection<NotificationGroup>, IDisposable
	{
		private readonly NotificationHistory _source;
		private readonly Dictionary<NotificationStoreData, NotificationGroup> _groupMap
			= new Dictionary<NotificationStoreData, NotificationGroup> ();
		private bool _disposed;

		public GroupedNotificationHistory (NotificationHistory source)
		{
			if (source == null) throw new ArgumentNullException (nameof (source));

			_source = source;
			_source.CollectionChanged += OnSourceCollectionChanged;

			// 初始构建
			RebuildAll ();
		}

		// ============================================================
		// 源集合变化
		// ============================================================
		private void OnSourceCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
		{
			if (_disposed) return;

			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					if (e.NewItems != null)
						foreach (NotificationStoreData item in e.NewItems)
							AddItem (item);
					break;

				case NotifyCollectionChangedAction.Remove:
					if (e.OldItems != null)
						foreach (NotificationStoreData item in e.OldItems)
							RemoveItem (item);
					break;

				case NotifyCollectionChangedAction.Replace:
					if (e.OldItems != null)
						foreach (NotificationStoreData item in e.OldItems)
							RemoveItem (item);
					if (e.NewItems != null)
						foreach (NotificationStoreData item in e.NewItems)
							AddItem (item);
					break;

				case NotifyCollectionChangedAction.Reset:
					RebuildAll ();
					break;

				case NotifyCollectionChangedAction.Move:
					// 本实现不主动产生 Move；如源触发 Move，按 Replace 处理
					if (e.OldItems != null)
						foreach (NotificationStoreData item in e.OldItems)
							RemoveItem (item);
					if (e.NewItems != null)
						foreach (NotificationStoreData item in e.NewItems)
							AddItem (item);
					break;
			}
		}

		// ============================================================
		// 增 / 删 / 重建
		// ============================================================
		private void AddItem (NotificationStoreData item)
		{
			if (item == null) return;

			SubscribeItem (item);

			DateTime date = GetDate (item);
			NotificationGroup group = FindGroup (date);

			if (group == null)
			{
				group = new NotificationGroup (date);
				int groupIndex = FindGroupInsertIndex (date);
				Insert (groupIndex, group);
			}

			group.InsertSorted (item);
			_groupMap [item] = group;
		}

		private void RemoveItem (NotificationStoreData item)
		{
			if (item == null) return;

			UnsubscribeItem (item);

			NotificationGroup group;
			if (!_groupMap.TryGetValue (item, out group))
				return;

			group.Remove (item);
			_groupMap.Remove (item);

			if (group.Count == 0)
			{
				Remove (group);
				group.Dispose ();
			}
		}

		private void RebuildAll ()
		{
			// 1. 先解除所有旧订阅
			foreach (var kv in _groupMap)
				UnsubscribeItem (kv.Key);

			// 2. 清空旧状态
			foreach (var g in this) g.Dispose ();
			Clear ();
			_groupMap.Clear ();

			// 3. 按时间从新到旧排序，逐项放入对应组
			var snapshot = _source
				.Where (x => x != null)
				.OrderByDescending (x => x.TimeStampUtc)
				.ToList ();

			NotificationGroup currentGroup = null;
			DateTime currentDate = DateTime.MinValue;

			foreach (var item in snapshot)
			{
				SubscribeItem (item);

				DateTime date = GetDate (item);

				if (currentGroup == null || date != currentDate)
				{
					currentGroup = new NotificationGroup (date);
					Add (currentGroup);   // 顺序天然降序
					currentDate = date;
				}

				currentGroup.Add (item);  // 已按顺序，直接 Add
				_groupMap [item] = currentGroup;
			}
		}

		// ============================================================
		// 项属性变化：只需处理 TimeStampUtc 变化
		// ============================================================
		private void OnItemPropertyChanged (object sender, PropertyChangedEventArgs e)
		{
			if (_disposed) return;

			var item = sender as NotificationStoreData;
			if (item == null) return;

			// 其它属性变化不影响分组/排序
			if (!string.IsNullOrEmpty (e.PropertyName) &&
				e.PropertyName != nameof (NotificationStoreData.TimeStampUtc))
				return;

			RemoveItem (item);
			AddItem (item);
		}

		private void SubscribeItem (NotificationStoreData item)
		{
			if (item == null) return;
			item.PropertyChanged += OnItemPropertyChanged;
		}

		private void UnsubscribeItem (NotificationStoreData item)
		{
			if (item == null) return;
			item.PropertyChanged -= OnItemPropertyChanged;
		}

		// ============================================================
		// 查找
		// ============================================================
		private static DateTime GetDate (NotificationStoreData item)
		{
			// 以本地时间的一天为组
			return item.TimeStampUtc.ToLocalTime ().Date;
		}

		/// <summary>
		/// 组按日期降序，二分查找已存在的组；不存在返回 null。
		/// </summary>
		private NotificationGroup FindGroup (DateTime date)
		{
			int lo = 0;
			int hi = Count - 1;

			while (lo <= hi)
			{
				int mid = (lo + hi) / 2;
				var g = this [mid];

				if (g.Date == date) return g;

				if (g.Date > date)
					lo = mid + 1;   // 目标更旧，往右
				else
					hi = mid - 1;   // 目标更新，往左
			}

			return null;
		}

		/// <summary>
		/// 组按日期降序，找到第一个 Date &lt; date 的位置。
		/// 不存在的日期插入此处即可保持整体降序。
		/// </summary>
		private int FindGroupInsertIndex (DateTime date)
		{
			int lo = 0;
			int hi = Count;

			while (lo < hi)
			{
				int mid = (lo + hi) / 2;

				if (this [mid].Date > date)
					lo = mid + 1;
				else
					hi = mid;
			}

			return lo;
		}

		// ============================================================
		// Dispose
		// ============================================================
		public void Dispose ()
		{
			if (_disposed) return;
			_disposed = true;

			_source.CollectionChanged -= OnSourceCollectionChanged;

			// 解除所有项订阅
			foreach (var kv in _groupMap)
				UnsubscribeItem (kv.Key);

			foreach (var g in this) g.Dispose ();

			_groupMap.Clear ();
			Clear ();
		}
	}
	public sealed class LocalTimeConverter: IValueConverter
	{
		/// <summary>
		/// 全局共享实例，可在 XAML 中通过 {x:Static} 引用。
		/// </summary>
		public static readonly LocalTimeConverter Instance = new LocalTimeConverter ();

		public object Convert (object value, Type targetType,
							  object parameter, CultureInfo culture)
		{
			if (value is DateTime)
			{
				DateTime utc = (DateTime)value;
				return utc.ToLocalTime ().ToString ("t", CultureInfo.CurrentCulture);
			}

			return string.Empty;
		}

		public object ConvertBack (object value, Type targetType,
								  object parameter, CultureInfo culture)
		{
			throw new NotSupportedException ();
		}
	}
	public sealed class LocalDateConverter: IValueConverter
	{
		/// <summary>
		/// 全局共享实例，可在 XAML 中通过 {x:Static} 引用。
		/// </summary>
		public static readonly LocalDateConverter Instance = new LocalDateConverter ();

		public object Convert (object value, Type targetType,
							  object parameter, CultureInfo culture)
		{
			if (value is DateTime)
			{
				DateTime utc = (DateTime)value;
				return utc.ToLocalTime ().ToString ("D", CultureInfo.CurrentCulture);
			}

			return string.Empty;
		}

		public object ConvertBack (object value, Type targetType,
								  object parameter, CultureInfo culture)
		{
			throw new NotSupportedException ();
		}
	}
}

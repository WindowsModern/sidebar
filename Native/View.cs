using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
namespace Sidebar
{
	/// <summary>
	/// 对 IList&lt;T&gt; 施加过滤视图。若源实现 INotifyCollectionChanged，则自动订阅并同步。
	/// 对外触发 CollectionChanged，便于 WPF 绑定刷新。
	/// 内部状态受 _sync 保护；事件在锁外触发。
	/// 约定：本类应在 UI 线程使用；若源可能从后台线程变化，请自行调度到 UI 线程再触发。
	/// </summary>
	public class View<T>: IList<T>, INotifyCollectionChanged, IDisposable, IList, ICollection, IBindingList, IListSource
	{
		private readonly object _sync = new object ();
		private readonly object _handlersLock = new object ();
		private readonly HashSet<NotifyCollectionChangedEventHandler> _handlers = new HashSet<NotifyCollectionChangedEventHandler> ();
		private IList<T> _source;
		private List<KeyValuePair<T, int>> _cast;
		private Func<T, int, bool> _selector;
		private volatile int _disposed;
		public event NotifyCollectionChangedEventHandler CollectionChanged
		{
			add { if (value != null) lock (_handlersLock) _handlers.Add (value); }
			remove { if (value != null) lock (_handlersLock) _handlers.Remove (value); }
		}
		public View (IList<T> src) : this (src, null) { }
		public View (IList<T> src, Func<T, int, bool> selector)
		{
			if (src == null) throw new ArgumentNullException (nameof (src));
			_source = src;
			_selector = selector;
			var incc = src as INotifyCollectionChanged;
			if (incc != null)
				incc.CollectionChanged += OnSourceCollectionChanged;
			lock (_sync) BuildCastLocked ();
		}
		public IList<T> Source
		{
			get { lock (_sync) return _source; }
		}
		public bool IsSourceObservable
		{
			get { lock (_sync) return _source is INotifyCollectionChanged; }
		}
		public Func<T, int, bool> Selector
		{
			get { lock (_sync) return _selector; }
			set
			{
				lock (_sync) { _selector = value; BuildCastLocked (); }
				RaiseReset ();
			}
		}
		public int Count
		{
			get
			{
				lock (_sync)
				{
					if (_source == null) return 0;
					return _cast == null ? _source.Count : _cast.Count;
				}
			}
		}
		public bool IsReadOnly
		{
			get { lock (_sync) return _source == null || _source.IsReadOnly; }
		}
		public bool IsFixedSize => false;
		public object SyncRoot => _sync;
		public bool IsSynchronized => false;
		object System.Collections.IList.this [int index]
		{
			get { return this [index]; }
			set
			{
				if (value == null && default (T) != null)
					throw new ArgumentNullException (nameof (value));
				if (value != null && !(value is T))
					throw new ArgumentException (
						$"Value must be assignable to {typeof (T).FullName}.", nameof (value));
				this [index] = value == null ? default (T) : (T)value;
			}
		}
		public T this [int index]
		{
			get
			{
				lock (_sync)
				{
					if (_cast == null)
					{
						if (_source == null) throw new ArgumentOutOfRangeException (nameof (index));
						return _source [index];
					}
					return _cast [index].Key;
				}
			}
			set
			{
				bool manualReset = false;
				lock (_sync)
				{
					if (_source == null) throw new InvalidOperationException ("Source is null.");
					if (_source.IsReadOnly) throw new NotSupportedException ("Source is read-only.");
					if (_cast == null)
					{
						_source [index] = value;
						if (!(_source is INotifyCollectionChanged)) manualReset = true;
					}
					else
					{
						var srcIdx = _cast [index].Value;
						_source [srcIdx] = value;
						_cast [index] = new KeyValuePair<T, int> (value, srcIdx);
						if (!(_source is INotifyCollectionChanged)) manualReset = true;
					}
				}
				if (manualReset) RaiseReset ();
			}
		}
		public void Add (T item)
		{
			bool manualReset = false;
			lock (_sync)
			{
				if (_source == null) throw new InvalidOperationException ("Source is null.");
				if (_source.IsReadOnly) throw new NotSupportedException ("Source is read-only.");
				_source.Add (item);
				if (!(_source is INotifyCollectionChanged))
				{
					BuildCastLocked ();
					manualReset = true;
				}
			}
			if (manualReset) RaiseReset ();
		}
		public void Clear ()
		{
			bool manualReset = false;
			lock (_sync)
			{
				if (_source == null) return;
				if (_source.IsReadOnly) throw new NotSupportedException ("Source is read-only.");
				var sourceObservable = _source is INotifyCollectionChanged;
				var before = _cast == null ? _source.Count : _cast.Count;
				if (before == 0) return;
				if (_cast == null)
				{
					_source.Clear ();
					if (!sourceObservable) manualReset = true;
				}
				else
				{
					var items = _cast.Select (kv => kv.Key).ToList ();
					foreach (var item in items)
					{
						var idx = IndexOfLocked (item);
						if (idx >= 0 && RemoveAtLocked (idx)) manualReset = true;
					}
				}
			}
			if (manualReset) RaiseReset ();
		}
		public bool Contains (T item)
		{
			lock (_sync)
			{
				if (_source == null) return false;
				if (_cast == null) return _source.Contains (item);
				for (int i = 0; i < _cast.Count; i++)
					if (EqualityComparer<T>.Default.Equals (_cast [i].Key, item)) return true;
				return false;
			}
		}
		public void CopyTo (T [] array, int arrayIndex)
		{
			if (array == null) throw new ArgumentNullException (nameof (array));
			if (arrayIndex < 0) throw new ArgumentOutOfRangeException (nameof (arrayIndex));
			lock (_sync)
			{
				var n = _cast == null ? (_source == null ? 0 : _source.Count) : _cast.Count;
				if (array.Length - arrayIndex < n)
					throw new ArgumentException ("Target array is too small.", nameof (array));
				if (_cast == null)
				{
					if (_source == null) return;
					for (int i = 0; i < n; i++) array [arrayIndex + i] = _source [i];
				}
				else
				{
					for (int i = 0; i < n; i++) array [arrayIndex + i] = _cast [i].Key;
				}
			}
		}
		public IEnumerator<T> GetEnumerator ()
		{
			T [] snapshot;
			lock (_sync)
			{
				if (_source == null) yield break;
				if (_cast == null)
				{
					snapshot = new T [_source.Count];
					for (int i = 0; i < snapshot.Length; i++) snapshot [i] = _source [i];
				}
				else
				{
					snapshot = new T [_cast.Count];
					for (int i = 0; i < snapshot.Length; i++) snapshot [i] = _cast [i].Key;
				}
			}
			for (int i = 0; i < snapshot.Length; i++) yield return snapshot [i];
		}
		IEnumerator IEnumerable.GetEnumerator () { return GetEnumerator (); }
		public int IndexOf (T item)
		{
			lock (_sync) return IndexOfLocked (item);
		}
		public void Insert (int index, T item)
		{
			bool manualReset = false;
			lock (_sync)
			{
				if (_source == null) throw new InvalidOperationException ("Source is null.");
				if (_source.IsReadOnly) throw new NotSupportedException ("Source is read-only.");
				var n = _cast == null ? _source.Count : _cast.Count;
				if (index < 0 || index > n) throw new ArgumentOutOfRangeException (nameof (index));
				int srcIndex;
				if (_cast == null) srcIndex = index;
				else if (_cast.Count == 0) srcIndex = _source.Count;
				else if (index >= _cast.Count) srcIndex = _cast [_cast.Count - 1].Value + 1;
				else srcIndex = _cast [index].Value;
				_source.Insert (srcIndex, item);
				if (!(_source is INotifyCollectionChanged))
				{
					BuildCastLocked ();
					manualReset = true;
				}
			}
			if (manualReset) RaiseReset ();
		}
		public bool Remove (T item)
		{
			bool removed;
			bool manualReset = false;
			lock (_sync)
			{
				var i = IndexOfLocked (item);
				if (i < 0) removed = false;
				else { removed = true; manualReset = RemoveAtLocked (i); }
			}
			if (manualReset) RaiseReset ();
			return removed;
		}
		public void RemoveAt (int index)
		{
			bool manualReset;
			lock (_sync) manualReset = RemoveAtLocked (index);
			if (manualReset) RaiseReset ();
		}
		/// <summary>
		/// 将视图中 oldIndex 处的元素移动到 newIndex 处。
		/// 若源为 ObservableCollection&lt;T&gt;，走源的原生 Move；否则退化为移除+插入。
		/// </summary>
		public void Move (int oldIndex, int newIndex)
		{
			bool manualReset = false;
			lock (_sync)
			{
				if (_source == null) throw new InvalidOperationException ("Source is null.");
				if (_source.IsReadOnly) throw new NotSupportedException ("Source is read-only.");
				var n = _cast == null ? _source.Count : _cast.Count;
				if (oldIndex < 0 || oldIndex >= n) throw new ArgumentOutOfRangeException (nameof (oldIndex));
				if (newIndex < 0 || newIndex >= n) throw new ArgumentOutOfRangeException (nameof (newIndex));
				if (oldIndex == newIndex) return;
				var srcOld = _cast == null ? oldIndex : _cast [oldIndex].Value;
				var srcNew = _cast == null ? newIndex : _cast [newIndex].Value;
				var oc = _source as ObservableCollection<T>;
				if (oc != null)
				{
					oc.Move (srcOld, srcNew);
				}
				else
				{
					var it = _source [srcOld];
					_source.RemoveAt (srcOld);
					_source.Insert (srcNew, it);
					if (!(_source is INotifyCollectionChanged))
					{
						BuildCastLocked ();
						manualReset = true;
					}
				}
			}
			if (manualReset) RaiseReset ();
		}
		public int IndexFromSourceIndex (int indexInSource)
		{
			lock (_sync)
			{
				if (_cast == null || _source == null) return -1;
				if (indexInSource < 0 || indexInSource >= _source.Count) return -1;
				for (int i = 0; i < _cast.Count; i++)
					if (_cast [i].Value == indexInSource) return i;
				return -1;
			}
		}
		public int IndexToSourceIndex (int index)
		{
			lock (_sync)
			{
				if (_cast == null || _source == null) return -1;
				if (index < 0 || index >= _cast.Count) return -1;
				return _cast [index].Value;
			}
		}
		public virtual void Refresh ()
		{
			lock (_sync) BuildCastLocked ();
			RaiseReset ();
		}
		/// <summary>
		/// 类似 LINQ Select：将每个元素投影为另一种类型，得到一个随本视图自动更新的 ProjectedView。
		/// </summary>
		public ProjectedView<T, U> Select<U> (Func<T, U> selector)
		{
			return new ProjectedView<T, U> (this, selector);
		}
		/// <summary>
		/// 类似 LINQ Select：带逆转换器的可写投影。
		/// </summary>
		public ProjectedView<T, U> Select<U> (Func<T, U> selector, Func<U, T> reverse)
		{
			return new ProjectedView<T, U> (this, selector, reverse);
		}
		public virtual void Dispose ()
		{
			if (Interlocked.Exchange (ref _disposed, 1) != 0) return;
			var incc = _source as INotifyCollectionChanged;
			if (incc != null)
				incc.CollectionChanged -= OnSourceCollectionChanged;
			lock (_handlersLock) _handlers.Clear ();
			ClearFormsWhenDispose ();
		}
		private void OnSourceCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
		{
			if (_disposed != 0) return;
			bool hasFilter;
			lock (_sync)
			{
				if (_disposed != 0) return;
				hasFilter = _cast != null;
				if (hasFilter) BuildCastLocked ();
			}
			if (hasFilter) RaiseReset ();
			else RaiseRaw (e);
		}
		private void BuildCastLocked ()
		{
			_cast = null;
			if (_source == null || _selector == null) return;
			_cast = new List<KeyValuePair<T, int>> ();
			for (int i = 0; i < _source.Count; i++)
			{
				var item = _source [i];
				if (_selector (item, i))
					_cast.Add (new KeyValuePair<T, int> (item, i));
			}
		}
		private int IndexOfLocked (T item)
		{
			if (_source == null) return -1;
			if (_cast == null) return _source.IndexOf (item);
			for (int i = 0; i < _cast.Count; i++)
				if (EqualityComparer<T>.Default.Equals (_cast [i].Key, item)) return i;
			return -1;
		}
		/// <summary>删除视图索引处的元素。返回 true 表示调用方需手动触发 Reset（源不可观察时）。</summary>
		private bool RemoveAtLocked (int index)
		{
			if (_source == null) throw new InvalidOperationException ("Source is null.");
			if (_source.IsReadOnly) throw new NotSupportedException ("Source is read-only.");
			var n = _cast == null ? _source.Count : _cast.Count;
			if (index < 0 || index >= n) throw new ArgumentOutOfRangeException (nameof (index));
			var srcIndex = _cast == null ? index : _cast [index].Value;
			_source.RemoveAt (srcIndex);
			if (!(_source is INotifyCollectionChanged))
			{
				BuildCastLocked ();
				return true;
			}
			return false;
		}
		protected void RaiseReset ()
		{
			RaiseRaw (new NotifyCollectionChangedEventArgs (NotifyCollectionChangedAction.Reset));
		}
		protected void RaiseRaw (NotifyCollectionChangedEventArgs e)
		{
			NotifyCollectionChangedEventHandler [] snapshot = null;
			lock (_handlersLock)
			{
				if (_handlers.Count > 0)
					snapshot = _handlers.ToArray ();
			}
			if (snapshot != null)
			{
				for (int i = 0; i < snapshot.Length; i++)
				{
					snapshot [i] (this, e);
				}
			}
			ScheduleListChanged ();
		}
		public int Add (object value)
		{
			if (value == null && default (T) != null)
				throw new ArgumentNullException (nameof (value));
			if (value != null && !(value is T))
				throw new ArgumentException (
					$"Value must be assignable to {typeof (T).FullName}.", nameof (value));
			int idx = Count;
			Add (value == null ? default (T) : (T)value);
			return idx;
		}
		public bool Contains (object value)
		{
			if (value == null)
				return default (T) == null && Contains (default (T));
			return value is T && Contains ((T)value);
		}
		public int IndexOf (object value)
		{
			if (value == null)
				return default (T) == null ? IndexOf (default (T)) : -1;
			return value is T ? IndexOf ((T)value) : -1;
		}
		public void Insert (int index, object value)
		{
			if (value == null && default (T) != null)
				throw new ArgumentNullException (nameof (value));
			if (value != null && !(value is T))
				throw new ArgumentException (
					$"Value must be assignable to {typeof (T).FullName}.", nameof (value));
			Insert (index, value == null ? default (T) : (T)value);
		}
		public void Remove (object value)
		{
			if (value == null)
			{
				if (default (T) == null) Remove (default (T));
				return;
			}
			if (value is T) Remove ((T)value);
		}
		public void CopyTo (Array array, int index)
		{
			if (array == null) throw new ArgumentNullException (nameof (array));
			if (index < 0) throw new ArgumentOutOfRangeException (nameof (index));
			var n = Count;
			if (array.Length - index < n) throw new ArgumentException ("Target array is too small.", nameof (array));
			for (int i = 0; i < n; i++) array.SetValue (this [i], index + i);
		}
		#region IBindingList
		private bool _allowNew = true;
		private bool _allowEdit = true;
		private bool _allowRemove = true;
		private static readonly bool _hasDefaultCtor =
			typeof (T).IsValueType || typeof (T).GetConstructor (Type.EmptyTypes) != null;
		public bool AllowNew
		{
			get
			{
				lock (_sync)
				{
					if (!_allowNew) return false;
					if (_source == null || _source.IsReadOnly) return false;
					if (_cast != null) return false;
					if (!_hasDefaultCtor) return false;
					return true;
				}
			}
			set { lock (_sync) _allowNew = value; }
		}
		public bool AllowEdit
		{
			get
			{
				lock (_sync)
					return _allowEdit && _source != null && !_source.IsReadOnly;
			}
			set { lock (_sync) _allowEdit = value; }
		}
		public bool AllowRemove
		{
			get
			{
				lock (_sync)
					return _allowRemove && _source != null && !_source.IsReadOnly;
			}
			set { lock (_sync) _allowRemove = value; }
		}
		public bool SupportsChangeNotification => true;
		public bool SupportsSearching => false;
		public bool SupportsSorting => false;
		public bool IsSorted => false;
		public ListSortDirection SortDirection => ListSortDirection.Ascending;
		public PropertyDescriptor SortProperty => null;
		public event ListChangedEventHandler ListChanged;
		public object AddNew ()
		{
			T item;
			lock (_sync)
			{
				if (!AllowNew)
					throw new NotSupportedException (
						"AddNew is not supported by this view (read-only, filtered, or T has no parameterless constructor).");
				item = Activator.CreateInstance<T> ();
			}
			Add (item);
			return item;
		}
		public void ApplySort (PropertyDescriptor property, ListSortDirection direction)
		{
			throw new NotSupportedException ("Sorting is not supported.");
		}
		public void RemoveSort ()
		{
			throw new NotSupportedException ("Sorting is not supported.");
		}
		public int Find (PropertyDescriptor property, object key)
		{
			throw new NotSupportedException ("Searching is not supported.");
		}
		void IBindingList.AddIndex (PropertyDescriptor property) { }
		void IBindingList.RemoveIndex (PropertyDescriptor property) { }
		#endregion
		bool IListSource.ContainsListCollection => false;
		System.Collections.IList IListSource.GetList () => this;
		#region WinForms ListChanged 调度字段
		private SynchronizationContext _winFormsContext;
		private System.Windows.Forms.Timer _listChangedTimer;
		private readonly object _listChangedTimerLock = new object ();
		private int _listChangedScheduled;  // 0=idle, 1=scheduled
											/// <summary>
											/// 由 WinForms 宿主在 UI 线程调用一次，注册 UI 同步上下文。
											/// 注册后 ListChanged 会 Post 到该上下文，并在 50ms 内防抖。
											/// 未注册时 ListChanged 退化为同步触发（兼容 WPF 场景）。
											/// </summary>
		public void AttachWinFormsContext (SynchronizationContext context)
		{
			if (context == null) throw new ArgumentNullException (nameof (context));
			_winFormsContext = context;
		}
		private void ScheduleListChanged ()
		{
			if (ListChanged == null) return;
			var ctx = _winFormsContext;
			if (ctx == null)
			{
				RaiseListChangedNow ();
				return;
			}
			if (Interlocked.CompareExchange (ref _listChangedScheduled, 1, 0) != 0)
				return;
			try
			{
				ctx.Post (_ => OnWinFormsListChangedTick (), null);
			}
			catch (Exception ex)
			{
				Interlocked.Exchange (ref _listChangedScheduled, 0);
				System.Diagnostics.Debug.WriteLine ("[ScheduleListChanged] " + ex);
			}
		}
		private void OnWinFormsListChangedTick ()
		{
			lock (_listChangedTimerLock)
			{
				if (_disposed != 0)
				{
					Interlocked.Exchange (ref _listChangedScheduled, 0);
					return;
				}
				if (_listChangedTimer == null)
				{
					_listChangedTimer = new System.Windows.Forms.Timer { Interval = 50 };
					_listChangedTimer.Tick += (s, e) => {
						_listChangedTimer.Stop ();
						Interlocked.Exchange (ref _listChangedScheduled, 0);
						RaiseListChangedNow ();
					};
				}
				_listChangedTimer.Stop ();
				_listChangedTimer.Start ();
			}
		}
		private void RaiseListChangedNow ()
		{
			var lc = ListChanged;
			if (lc == null) return;
			var args = new ListChangedEventArgs (ListChangedType.Reset, -1);
			try { lc (this, args); } catch { }
		}
		private void ClearFormsWhenDispose ()
		{
			lock (_listChangedTimerLock)
			{
				if (_listChangedTimer != null)
				{
					try { _listChangedTimer.Stop (); } catch { }
					try { _listChangedTimer.Dispose (); } catch { }
					_listChangedTimer = null;
				}
			}
			Interlocked.Exchange (ref _listChangedScheduled, 0);
			ListChanged = null;
		}
		#endregion
		#region 链式变换
		/// <summary>
		/// 返回一个新的 View&lt;T&gt;，只保留满足 predicate 的元素（带源索引）。
		/// 新视图订阅本视图的 CollectionChanged，源变化自动同步。
		/// 新视图不拥有源，调用方负责按需 Dispose。
		/// </summary>
		public View<T> Where (Func<T, int, bool> predicate)
		{
			if (predicate == null) throw new ArgumentNullException (nameof (predicate));
			return new View<T> (this, predicate);
		}
		/// <summary>
		/// 返回一个新的 View&lt;T&gt;，只保留满足 predicate 的元素。
		/// </summary>
		public View<T> Where (Func<T, bool> predicate)
		{
			if (predicate == null) throw new ArgumentNullException (nameof (predicate));
			return new View<T> (this, (item, _) => predicate (item));
		}
		#endregion
	}
	/// <summary>
	/// 只读/可写投影视图。继承自 View&lt;U&gt;，因此具备完整的 IList&lt;U&gt; 语义。
	/// 通过 Func&lt;T, U&gt; 把源集合（View&lt;T&gt; 或普通 IList&lt;T&gt;）的每个元素投影为 U。
	/// 如果提供了 Func&lt;U, T&gt; 逆转换器，则视图变为可写。
	/// 如果源实现了 INotifyCollectionChanged，会自动订阅并随源刷新（由 View&lt;T&gt; 负责）。
	/// </summary>
	public sealed class ProjectedView<T, U>: View<U>
	{
		private readonly View<T> _sourceView;
		private readonly Func<T, U> _forward;
		private readonly Func<U, T> _reverse;
		private readonly ProjectionAdapter _adapter;
		private readonly bool _ownsSource;
		private int _disposed;
		// ---------- View<T> 源 ----------
		public ProjectedView (View<T> source, Func<T, U> forward)
			: this (source, forward, null, false) { }
		public ProjectedView (View<T> source, Func<T, U> forward, Func<U, T> reverse)
			: this (source, forward, reverse, false) { }
		// ---------- IList<T> 源 ----------
		/// <summary>只读投影，源为任意 IList&lt;T&gt;。</summary>
		public ProjectedView (IList<T> source, Func<T, U> forward)
			: this (source, forward, null, true) { }
		/// <summary>可写投影，源为任意 IList&lt;T&gt;。</summary>
		public ProjectedView (IList<T> source, Func<T, U> forward, Func<U, T> reverse)
			: this (source, forward, reverse, true) { }
		// ---------- 实际构造函数 ----------
		private ProjectedView (IList<T> source, Func<T, U> forward, Func<U, T> reverse, bool wrapIList)
			: base (CreateAdapter (source, forward, reverse))
		{
			_adapter = (ProjectionAdapter)Source;
			_sourceView = _adapter.SourceView;
			_forward = forward;
			_reverse = reverse;
			// 只有当我们真的创建了一个新的 View<T> 时才需要释放它。
			// wrapIList == false  → 用户传入 View<T>，我们绝不拥有。
			// wrapIList == true   → 用户传入 IList<T>；如果它本身就是 View<T>，也不拥有。
			_ownsSource = wrapIList && !(source is View<T>);
		}
		private static IList<U> CreateAdapter (IList<T> source, Func<T, U> forward, Func<U, T> reverse)
		{
			if (source == null) throw new ArgumentNullException ("source");
			if (forward == null) throw new ArgumentNullException ("forward");
			var v = source as View<T>;
			if (v == null) v = new View<T> (source);   // 关键：由 View<T> 处理 INotifyCollectionChanged 检测与订阅
			return new ProjectionAdapter (v, forward, reverse);
		}
		/// <summary>投影的源视图（始终是 View&lt;T&gt;，若外部传入普通 IList&lt;T&gt; 则内部包装而成）。</summary>
		public View<T> SourceView
		{
			get { return _sourceView; }
		}
		/// <summary>正向投影函数 T → U。</summary>
		public Func<T, U> Projection
		{
			get { return _forward; }
		}
		/// <summary>逆向转换函数 U → T；为 null 时视图只读。</summary>
		public Func<U, T> ReverseProjection
		{
			get { return _reverse; }
		}
		public override void Refresh ()
		{
			_adapter.Rebuild ();
		}
		public override void Dispose ()
		{
			if (Interlocked.Exchange (ref _disposed, 1) != 0) return;
			base.Dispose ();                         // 将本实例与适配器解耦
			_adapter.Dispose ();                     // 将适配器与源视图解耦
			if (_ownsSource) _sourceView.Dispose (); // 释放内部包装的 View<T>，防止源上的事件悬空引用
		}
		/// <summary>
		/// 内部适配器：把 View&lt;T&gt; 投影成 IList&lt;U&gt;，并对外暴露 INotifyCollectionChanged。
		/// </summary>
		private sealed class ProjectionAdapter: IList<U>, INotifyCollectionChanged, IDisposable
		{
			private readonly View<T> _sourceView;
			private readonly Func<T, U> _forward;
			private readonly Func<U, T> _reverse;
			private readonly object _sync = new object ();
			private List<U> _cache;
			private int _disposed;
			public ProjectionAdapter (View<T> sourceView, Func<T, U> forward, Func<U, T> reverse)
			{
				_sourceView = sourceView;
				_forward = forward;
				_reverse = reverse;
				var initial = new List<U> ();
				foreach (var item in sourceView)
					initial.Add (forward (item));
				_cache = initial;
				sourceView.CollectionChanged += OnSourceChanged;
			}
			public event NotifyCollectionChangedEventHandler CollectionChanged;
			/// <summary>暴露给外层 ProjectedView 使用。</summary>
			public View<T> SourceView
			{
				get { return _sourceView; }
			}
			public void Rebuild ()
			{
				if (_disposed != 0) return;
				var newCache = new List<U> ();
				foreach (var item in _sourceView)
					newCache.Add (_forward (item));
				lock (_sync) _cache = newCache;
				RaiseReset ();
			}
			public void Dispose ()
			{
				if (Interlocked.Exchange (ref _disposed, 1) != 0) return;
				_sourceView.CollectionChanged -= OnSourceChanged;
			}
			private void OnSourceChanged (object sender, NotifyCollectionChangedEventArgs e)
			{
				if (_disposed != 0) return;
				Rebuild ();
			}
			private void RaiseReset ()
			{
				var h = CollectionChanged;
				if (h != null)
					h (this, new NotifyCollectionChangedEventArgs (NotifyCollectionChangedAction.Reset));
			}
			private void EnsureWritable ()
			{
				if (_reverse == null)
					throw new NotSupportedException (
						"ProjectedView is read-only. Provide a reverse selector (Func<U, T>) to enable writes.");
			}
			public int Count
			{
				get { lock (_sync) return _cache.Count; }
			}
			public bool IsReadOnly
			{
				get { return _reverse == null; }
			}
			public U this [int index]
			{
				get { lock (_sync) return _cache [index]; }
				set
				{
					EnsureWritable ();
					// 视图索引与源 View<T> 的可见索引一一对应
					_sourceView [index] = _reverse (value);
				}
			}
			public int IndexOf (U item)
			{
				lock (_sync) return _cache.IndexOf (item);
			}
			public void Insert (int index, U item)
			{
				EnsureWritable ();
				_sourceView.Insert (index, _reverse (item));
			}
			public void RemoveAt (int index)
			{
				EnsureWritable ();
				_sourceView.RemoveAt (index);
			}
			public void Add (U item)
			{
				EnsureWritable ();
				_sourceView.Add (_reverse (item));
			}
			public void Clear ()
			{
				EnsureWritable ();
				_sourceView.Clear ();
			}
			public bool Contains (U item)
			{
				lock (_sync) return _cache.Contains (item);
			}
			public void CopyTo (U [] array, int arrayIndex)
			{
				lock (_sync) _cache.CopyTo (array, arrayIndex);
			}
			public bool Remove (U item)
			{
				EnsureWritable ();
				int idx;
				lock (_sync) idx = _cache.IndexOf (item);
				if (idx < 0) return false;
				_sourceView.RemoveAt (idx);
				return true;
			}
			public IEnumerator<U> GetEnumerator ()
			{
				List<U> snapshot;
				lock (_sync) snapshot = new List<U> (_cache);
				return snapshot.GetEnumerator ();
			}
			IEnumerator IEnumerable.GetEnumerator ()
			{
				return GetEnumerator ();
			}
		}
	}
}
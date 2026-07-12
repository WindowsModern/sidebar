using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Collections.Specialized;
namespace Sidebar
{
	public class View <T>: IEnumerable <T>, IList <T>, ICollection <T>, IDisposable
	{
		IList<T> _source = null;
		List<KeyValuePair<T, int>> _cast = null;
		private Func<T, int, bool> _selector = null;
		public View (IList <T> src)
		{
			_source = src;
			if (_source is ObservableCollection <T>)
			{
				var oc = _source as ObservableCollection<T>;
				oc.CollectionChanged += ObservableCollection_CollectionChanged;
			}
			BuildCast ();
		}
		public IList<T> Source => _source;
		public bool IsSourceObservable => _source is ObservableCollection<T>;
		private void ObservableCollection_CollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
		{
			if (_cast == null || _selector == null)
			{
				BuildCast ();
				return;
			}
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					{
						int baseIndex = e.NewStartingIndex;
						int count = e.NewItems.Count;
						for (int i = 0; i < _cast.Count; i++)
						{
							var kv = _cast [i];
							if (kv.Value >= baseIndex)
								_cast [i] = new KeyValuePair<T, int> (kv.Key, kv.Value + count);
						}
						for (int i = 0; i < count; i++)
						{
							var item = (T)e.NewItems [i];
							int sourceIndex = baseIndex + i;
							if (_selector (item, sourceIndex))
							{
								int insertPos = _cast.Count;
								for (int j = 0; j < _cast.Count; j++)
								{
									if (_cast [j].Value >= sourceIndex)
									{
										insertPos = j;
										break;
									}
								}
								_cast.Insert (insertPos, new KeyValuePair<T, int> (item, sourceIndex));
							}
						}
						break;
					}
				case NotifyCollectionChangedAction.Remove:
					{
						int baseIndex = e.OldStartingIndex;
						int count = e.OldItems.Count;
						List<int> removeIndices = new List<int> ();
						for (int i = 0; i < _cast.Count; i++)
						{
							int srcIdx = _cast [i].Value;
							if (srcIdx >= baseIndex && srcIdx < baseIndex + count)
								removeIndices.Add (i);
						}
						for (int i = removeIndices.Count - 1; i >= 0; i--)
						{
							_cast.RemoveAt (removeIndices [i]);
						}
						for (int i = 0; i < _cast.Count; i++)
						{
							var kv = _cast [i];
							if (kv.Value >= baseIndex)
								_cast [i] = new KeyValuePair<T, int> (kv.Key, kv.Value - count);
						}
						break;
					}

				case NotifyCollectionChangedAction.Replace:
					{
						int baseIndex = e.OldStartingIndex;
						int count = e.OldItems.Count;
						List<int> removeIndices = new List<int> ();
						for (int i = 0; i < _cast.Count; i++)
						{
							int srcIdx = _cast [i].Value;
							if (srcIdx >= baseIndex && srcIdx < baseIndex + count)
								removeIndices.Add (i);
						}
						for (int i = removeIndices.Count - 1; i >= 0; i--)
						{
							_cast.RemoveAt (removeIndices [i]);
						}
						for (int i = 0; i < e.NewItems.Count; i++)
						{
							var item = (T)e.NewItems [i];
							int sourceIndex = baseIndex + i;
							if (_selector (item, sourceIndex))
							{
								int insertPos = _cast.Count;
								for (int j = 0; j < _cast.Count; j++)
								{
									if (_cast [j].Value >= sourceIndex)
									{
										insertPos = j;
										break;
									}
								}
								_cast.Insert (insertPos, new KeyValuePair<T, int> (item, sourceIndex));
							}
						}
						break;
					}
				case NotifyCollectionChangedAction.Move:
				case NotifyCollectionChangedAction.Reset:
				default:
					BuildCast ();
					break;
			}
		}
		public View (IList <T> src, Func <T, int, bool> selector)
		{
			_source = src;
			_selector = selector;
			BuildCast ();
		}
		private void BuildCast ()
		{
			_cast?.Clear ();
			_cast = null;
			if (_source == null || _source.Count < 0) return;
			if (_selector == null) return;
			_cast = new List<KeyValuePair<T, int>> ();
			for (int i = 0; i < _source.Count; i ++)
			{
				var item = _source [i];
				if (_selector (item, i))
				{
					_cast.Add (new KeyValuePair<T, int> (item, i));
				}
			}
		}
		public Func <T, int, bool> Selector
		{
			get { return _selector; }
			set { _selector = value; BuildCast (); }
		}
		public T this [int index]
		{
			get
			{
				if (_cast == null) return _source [index];
				else return _cast [index].Key;
			}
			set
			{
				if (_cast == null) _source [index] = value;
				else
				{
					_source [_cast [index].Value] = value;
					if (!IsSourceObservable) BuildCast ();
				}
			}
		}
		public int Count
		{
			get
			{
				if (_source == null) return 0;
				if (_cast == null) return _source.Count;
				return _cast.Count;
			}
		}
		public bool IsReadOnly => _source?.IsReadOnly ?? false;
		public void Add (T item)
		{
			_source.Add (item);
			if (!IsSourceObservable) BuildCast ();
		}
		public void Clear ()
		{
			for (var i = _cast.Count - 1; i >= 0; i --)
			{
				_source.RemoveAt (_cast [i].Value);
			}
			if (!IsSourceObservable) BuildCast ();
		}
		public bool Contains (T item)
		{
			foreach (var kv in _cast)
			{
				if (EqualityComparer<T>.Default.Equals (kv.Key, item)) return true;
			}
			return false;
		}
		public void CopyTo (T [] array, int arrayIndex)
		{
			if (array == null) throw new ArgumentNullException (nameof (array));
			if (arrayIndex < 0) throw new ArgumentOutOfRangeException (nameof (arrayIndex));
			int count = this.Count;
			if (array.Length - arrayIndex < count) throw new ArgumentException ("Not enough capability of target array.");
			if (_cast == null)
			{
				for (int i = 0; i < count; i++)
				{
					array [arrayIndex + i] = _source [i];
				}
			}
			else
			{
				for (int i = 0; i < count; i++)
				{
					array [arrayIndex + i] = _cast [i].Key;
				}
			}
		}
		public IEnumerator<T> GetEnumerator ()
		{
			if (_source == null) yield break;
			if (_cast == null)
			{
				foreach (var item in _source) yield return item;
			}
			else
			{
				foreach (var kv in _cast) yield return kv.Key;
			}
		}
		public int IndexOf (T item)
		{
			if (_source == null) return -1;
			if (_cast == null) return _source.IndexOf (item);
			else
			{
				for (int i = 0; i < _cast.Count; i++)
				{
					if (EqualityComparer<T>.Default.Equals (_cast [i].Key, item)) return i;
				}
				return -1;
			}
		}
		public void Insert (int index, T item)
		{
			if (_source == null) throw new InvalidOperationException ("Source collection is null.");
			if (index < 0 || index > this.Count) throw new ArgumentOutOfRangeException (nameof (index), "Index is out of range.");
			int sourceIndex;
			if (_cast == null || index == this.Count)
			{
				sourceIndex = _source.Count;
			}
			else
			{
				sourceIndex = _cast [index].Value;
			}
			_source.Insert (sourceIndex, item);
			if (!IsSourceObservable) BuildCast ();
		}
		public bool Remove (T item)
		{
			if (_source == null) return false;
			int index = IndexOf (item);
			if (index == -1) return false;
			RemoveAt (index);
			return true;
		}
		public void RemoveAt (int index)
		{
			if (_source == null) throw new InvalidOperationException ("Source collection is null.");
			if (index < 0 || index >= this.Count) throw new ArgumentOutOfRangeException (nameof (index), "Index is out of range.");
			int sourceIndex;
			if (_cast == null) sourceIndex = index;
			else sourceIndex = _cast [index].Value;
			_source.RemoveAt (sourceIndex);
			if (!IsSourceObservable) BuildCast ();
		}
		IEnumerator IEnumerable.GetEnumerator ()
		{
			return GetEnumerator ();
		}
		public void Refresh () => BuildCast ();
		public int IndexFromSourceIndex (int indexInSource)
		{
			if (_cast == null || _source == null) return -1;
			if (indexInSource < 0 || indexInSource >= _source.Count) return -1;
			for (int i = 0; i < _cast.Count; i++)
			{
				if (_cast [i].Value == indexInSource)
					return i;
			}
			return -1;
		}
		public int IndexToSourceIndex (int index)
		{
			if (_cast == null || _source == null) return -1;
			if (index < 0 || index > _cast.Count) return -1;
			return _cast [index].Value;
		}
		/// <summary>
		/// 将视图中指定索引的元素移动到新位置。
		/// </summary>
		/// <param name="oldIndex">要移动的元素在视图中的当前索引。</param>
		/// <param name="newIndex">元素在视图中的目标索引。</param>
		/// <exception cref="InvalidOperationException">源列表为 null。</exception>
		/// <exception cref="ArgumentOutOfRangeException">索引超出范围。</exception>
		/// <exception cref="NotSupportedException">源列表为只读。</exception>
		public void Move (int oldIndex, int newIndex)
		{
			if (_source == null)
				throw new InvalidOperationException ("Source collection is null.");
			if (IsReadOnly)
				throw new NotSupportedException ("Source list is read-only.");
			if (oldIndex < 0 || oldIndex >= this.Count)
				throw new ArgumentOutOfRangeException (nameof (oldIndex));
			if (newIndex < 0 || newIndex >= this.Count)
				throw new ArgumentOutOfRangeException (nameof (newIndex));
			if (oldIndex == newIndex)
				return;
			if (IsSourceObservable)
			{
				var oc = _source as ObservableCollection<T>;
				oc.Move (IndexToSourceIndex (oldIndex), IndexToSourceIndex (newIndex));
				return;
			}
			T item = this [oldIndex];
			RemoveAt (oldIndex);
			Insert (newIndex, item);
		}
		public void Dispose ()
		{
			if (_source is ObservableCollection<T>)
			{
				var oc = _source as ObservableCollection<T>;
				oc.CollectionChanged -= ObservableCollection_CollectionChanged;
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Sidebar
{
	public class UniqueObservableCollection<T>: ObservableCollection<T>
	{
		private readonly Func<T, object> _keySelector;
		public UniqueObservableCollection (Func<T, object> keySelector)
		{
			_keySelector = keySelector;
		}
		public UniqueObservableCollection ()
		{
			_keySelector = item => item;
		}
		/// <summary>
		/// 插入元素时，若已存在相同键的元素，则忽略本次插入（不触发任何事件）。
		/// </summary>
		protected override void InsertItem (int index, T item)
		{
			var key = _keySelector (item);
			for (int i = 0; i < Count; i++)
			{
				if (Equals (_keySelector (this [i]), key))
				{
					return;
				}
			}
			base.InsertItem (index, item);
		}
		/// <summary>
		/// 替换指定索引的元素时，若新元素与集合中其他元素键冲突，则忽略本次替换。
		/// </summary>
		protected override void SetItem (int index, T item)
		{
			var key = _keySelector (item);
			for (int i = 0; i < Count; i++)
			{
				if (i != index && Equals (_keySelector (this [i]), key))
				{
					return;
				}
			}
			base.SetItem (index, item);
		}
	}
}

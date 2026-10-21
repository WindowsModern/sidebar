using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SGTheme
{
	/// <summary>
	/// 表示命名空间节点（用于 TreeView）
	/// </summary>
	public class NamespaceNode: INotifyPropertyChanged
	{
		private bool _isExpanded = true;
		private bool _isSelected;

		public string Namespace { get; set; }

		public bool IsExpanded
		{
			get { return _isExpanded; }
			set
			{
				if (_isExpanded == value) return;
				_isExpanded = value;
				OnPropertyChanged (nameof (IsExpanded));
			}
		}

		public bool IsSelected
		{
			get { return _isSelected; }
			set
			{
				if (_isSelected == value) return;
				_isSelected = value;
				OnPropertyChanged (nameof (IsSelected));
			}
		}

		public ObservableCollection<ControlTypeNode> Controls { get; set; } = new ObservableCollection<ControlTypeNode> ();

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged (string propertyName)
		{
			PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (propertyName));
		}
	}

	/// <summary>
	/// 表示具体的控件类型节点
	/// </summary>
	public class ControlTypeNode: INotifyPropertyChanged
	{
		private bool _isExpanded;
		private bool _isSelected;

		public string Name { get; set; }
		public Type ControlType { get; set; }

		public bool IsExpanded
		{
			get { return _isExpanded; }
			set
			{
				if (_isExpanded == value) return;
				_isExpanded = value;
				OnPropertyChanged (nameof (IsExpanded));
			}
		}

		public bool IsSelected
		{
			get { return _isSelected; }
			set
			{
				if (_isSelected == value) return;
				_isSelected = value;
				OnPropertyChanged (nameof (IsSelected));
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged (string propertyName)
		{
			PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (propertyName));
		}
	}
}

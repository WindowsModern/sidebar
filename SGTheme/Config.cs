using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Sidebar;
namespace SGTheme
{
	public class SidebarConfig: INotifyPropertyChanged
	{
		private Dictionary<string, object> dict = new Dictionary<string, object> (StringComparer.OrdinalIgnoreCase);
		public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged (string propName)
		{
			PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (propName));
		}
		private T GetValue <T> (string propName, T dflt = default (T))
		{
			object value;
			dict.TryGetValue (propName, out value);
			return (T)(value ?? dflt);
		}
		private void SetValue <T> (string propName, T value)
		{
			dict [propName] = value;
			OnPropertyChanged (propName);
		}
		public Screen CurrentScreen
		{
			get { return GetValue<Screen> (nameof (CurrentScreen)); }
			set { SetValue (nameof (CurrentScreen), value); }
		}
		public SidebarDirection Direction
		{
			get { return GetValue (nameof (Direction), SidebarDirection.Right); }
			set
			{
				SetValue (nameof (Direction), value);
				OnPropertyChanged (nameof (IsReversed));
			}
		}
		public bool IsReversed
		{
			get { return Direction == SidebarDirection.Left; }
		}
		public bool IsTaskbarTop
		{
			get { return false; }
		}
		public string ThemeName
		{
			get { return GetValue<string> (nameof (ThemeName)); }
			set { SetValue (nameof (ThemeName), value); }
		}
		public double Width
		{
			get { return GetValue<double> (nameof (Width), 150); }
			set { SetValue (nameof (Width), 150D); }
		}
		public static SidebarConfig Global { get; } = new SidebarConfig ();
	}
}

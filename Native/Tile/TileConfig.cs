using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.Init;
namespace Sidebar
{
	[ComVisible (true)]
	public interface ITileFixedConfig: INotifyPropertyChanged
	{
		double Height { get; set; }
		bool Pinned { get; }
		bool AutoSize { get; }
	}
	[ComVisible (true)]
	public interface ITileStoreConfigInstance
	{
		InitConfig Ini { get; }
		XmlConfig Xml { get; }
	}
	[ComVisible (true)]
	public interface ITileConfig: ITileFixedConfig, ITileStoreConfigInstance { }
}

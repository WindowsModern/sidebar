using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Sidebar
{
	/// <summary>
	/// TileProperties.xaml 的交互逻辑
	/// </summary>
	public partial class TilePropertiesForm: Window, IPropertiesToolMembers
	{
		public TilePropertiesForm ()
		{
			InitializeComponent ();
			this.Title = App.ProgramFolder.StringResources.SuitableResource ("TILEPROPERTIES_TITLE");
			TilePropertiesOkButton.Content = App.ProgramFolder.StringResources.SuitableResource ("TILEPROPERTIES_OK");
			TilePropertiesCancelButton.Content = App.ProgramFolder.StringResources.SuitableResource ("TILEPROPERTIES_CANCEL");
		}
		public Button CancelButton => TilePropertiesCancelButton;
		public Button OKButton => TilePropertiesOkButton;
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sidebar
{
	public interface ITileElement
	{
		ImageSource Icon { get; }
		string DisplayName { get; }
		Panel ContentContainer { get; }
		string UserModelId { get; }
	}
}

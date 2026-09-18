using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace WindowsModern.NotificationHistoryTile
{
	public class NotificationElement: Button
	{
		public NotificationElement ()
		{
			SetResourceReference (StyleProperty, "TileNotificationButton");
		}
	}
}

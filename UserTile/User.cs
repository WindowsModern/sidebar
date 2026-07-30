using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;

namespace WindowsModern.UserTile
{
	public class UserUtils
	{
		public static string GetUserName ()
		{
			WindowsIdentity identity = WindowsIdentity.GetCurrent ();
			if (identity != null) return identity.Name;
			return Environment.UserName;
		}
		public static string GetUserSid ()
		{
			using (WindowsIdentity identity = WindowsIdentity.GetCurrent ())
			{
				return identity.User.Value;
			}
		}
	}
}

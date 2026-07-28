using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Sidebar
{
	public enum TileObjectType
	{
		Unknown,
		Sidebar,
		LongBar,
		KarlSidebar
	}
	public static class TileObjectHelper
	{
		public static TileObjectType GetTileModelType (Assembly assembly)
		{
			return TileObjectType.Unknown;
		}
	}
		
}

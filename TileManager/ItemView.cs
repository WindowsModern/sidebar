using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using Sidebar;
namespace TileManager
{
	public enum PinnedLocation
	{
		Top,
		Bottom
	}
	public struct PinnedItem
	{
		public ImageSource LogoImage { get; set; }
		public string DisplayName { get; set; }
		public PinnedLocation PinnedLocation { get; set; }
		public bool Exists { get; set; }
		public string UserModelId { get; set; }
		public override string ToString () => UserModelId;
		public PinnedItem (TileApplicationStorage tas, string pinnedLocation)
		{
			LogoImage = tas.LogoImage;
			DisplayName = tas.DisplayName;
			switch (pinnedLocation.Trim ().ToLowerInvariant ())
			{
				default:
				case "top":
					PinnedLocation = PinnedLocation.Top;
					break;
				case "bottom":
					PinnedLocation = PinnedLocation.Bottom;
					break;
			}
			Exists = true;
			UserModelId = tas.UserModelId;
		}
		public PinnedItem (string pinnedLocation, string userModelId)
		{
			LogoImage = null;
			DisplayName = userModelId;
			switch (pinnedLocation.Trim ().ToLowerInvariant ())
			{
				default:
				case "top":
					PinnedLocation = PinnedLocation.Top;
					break;
				case "bottom":
					PinnedLocation = PinnedLocation.Bottom;
					break;
			}
			Exists = false;
			UserModelId = userModelId;
		}
		public PinnedItem (TileApplicationStorage tas, PinnedLocation pinnedLocation)
		{
			LogoImage = tas.LogoImage;
			DisplayName = tas.DisplayName;
			PinnedLocation = pinnedLocation;
			Exists = true;
			UserModelId = tas.UserModelId;
		}
		public PinnedItem (PinnedLocation pinnedLocation, string userModelId)
		{
			LogoImage = null;
			DisplayName = userModelId;
			PinnedLocation = pinnedLocation;
			Exists = false;
			UserModelId = userModelId;
		}
		public string PinnedLocationDisplay
		{
			get
			{
				var key = PinnedLocation == PinnedLocation.Top
						? "TILEMGR_LOCATION_TOP"
						: "TILEMGR_LOCATION_BOTTOM";
				return App.S (key, PinnedLocation.ToString ());
			}
		}
	}
}

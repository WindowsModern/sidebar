using System.Collections.Generic;
using Sidebar;

namespace SGPkgMgr.Models
{
	public sealed class StartupOS
	{
		public string DisplayName { get; set; }
		public Sidebar.Version Version { get; set; }
		public bool IsCustom { get; set; }
		public override string ToString () => DisplayName ?? "";
	}

	public static class OSVersionCatalog
	{
		public static readonly List<StartupOS> Items = new List<StartupOS>
		{
			new StartupOS { DisplayName = "Windows XP Service Pack 3", Version = new Sidebar.Version (5, 1, 2600, 0) },
			new StartupOS { DisplayName = "Windows Vista Service Pack 2", Version = new Sidebar.Version (6, 0, 6002, 0) },
			new StartupOS { DisplayName = "Windows 7 Service Pack 1", Version = new Sidebar.Version (6, 1, 7601, 0) },
			new StartupOS { DisplayName = "Windows 8", Version = new Sidebar.Version (6, 2, 9200, 0) },
			new StartupOS { DisplayName = "Windows 8.1", Version = new Sidebar.Version (6, 3, 9600, 0) },
			new StartupOS { DisplayName = "Windows 10 (1507)", Version = new Sidebar.Version (10, 0, 10240, 0) },
			new StartupOS { DisplayName = "Windows 10 (1511)", Version = new Sidebar.Version (10, 0, 10586, 0) },
			new StartupOS { DisplayName = "Windows 10 (1607)", Version = new Sidebar.Version (10, 0, 14393, 0) },
			new StartupOS { DisplayName = "Windows 10 (1703)", Version = new Sidebar.Version (10, 0, 15063, 0) },
			new StartupOS { DisplayName = "Windows 10 (1709)", Version = new Sidebar.Version (10, 0, 16299, 0) },
			new StartupOS { DisplayName = "Windows 10 (1803)", Version = new Sidebar.Version (10, 0, 17134, 0) },
			new StartupOS { DisplayName = "Windows 10 (1809)", Version = new Sidebar.Version (10, 0, 17763, 0) },
			new StartupOS { DisplayName = "Windows 10 (1903)", Version = new Sidebar.Version (10, 0, 18362, 0) },
			new StartupOS { DisplayName = "Windows 10 (1909)", Version = new Sidebar.Version (10, 0, 18363, 0) },
			new StartupOS { DisplayName = "Windows 10 (2004)", Version = new Sidebar.Version (10, 0, 19041, 0) },
			new StartupOS { DisplayName = "Windows 10 (20H2)", Version = new Sidebar.Version (10, 0, 19042, 0) },
			new StartupOS { DisplayName = "Windows 10 (21H1)", Version = new Sidebar.Version (10, 0, 19043, 0) },
			new StartupOS { DisplayName = "Windows 10 (21H2)", Version = new Sidebar.Version (10, 0, 19044, 0) },
			new StartupOS { DisplayName = "Windows 11 (21H2)", Version = new Sidebar.Version (10, 0, 22000, 0) },
			new StartupOS { DisplayName = "Windows 11 (22H2)", Version = new Sidebar.Version (10, 0, 22621, 0) },
			new StartupOS { DisplayName = "Windows 11 (23H2)", Version = new Sidebar.Version (10, 0, 22631, 0) },
			new StartupOS { DisplayName = "Windows 11 (24H2)", Version = new Sidebar.Version (10, 0, 26100, 0) },
			new StartupOS { DisplayName = "Custom...", IsCustom = true }
		};

		/// <summary>目录里最后一个 "Custom..." 项。</summary>
		public static StartupOS Custom
		{
			get { return Items [Items.Count - 1]; }
		}

		/// <summary>按版本号精确匹配目录项；找不到返回 Custom。</summary>
		public static StartupOS Find (Sidebar.Version v)
		{
			foreach (var item in Items)
			{
				if (item.IsCustom) continue;
				if (SameVersion (item.Version, v)) return item;
			}
			return Custom;
		}

		private static bool SameVersion (Sidebar.Version a, Sidebar.Version b)
		{
			return a.Major == b.Major
				&& a.Minor == b.Minor
				&& a.Build == b.Build
				&& a.Revision == b.Revision;
		}
	}
}
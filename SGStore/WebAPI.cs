using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace Sidebar
{
	public static class WebAPI
	{
		public static TileList GetList ()
		{
			const string url = "https://raw.githubusercontent.com/modernw/SidebarGadgetsStore/main/list.json";
			using (var client = new WebClient ())
			{
				var data = client.DownloadData (url);
				var jsonstr = Encoding.UTF8.GetString (data);
				return JsonConvert.DeserializeObject<TileList> (jsonstr);
			}
		}
		public static Task<TileList> GetListAsync ()
		{
			return Task.Factory.StartNew (() => GetList ());
		}
	}
}

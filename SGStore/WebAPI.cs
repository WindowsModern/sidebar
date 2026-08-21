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
		const string baseUri = "http://127.0.0.1:5500";
		public static string BaseUri => baseUri;
		public static TileList GetList ()
		{
			var url = $"{baseUri}/list.json";
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
		public static string WebpageUri => $"{BaseUri}/index_elder.html";
	}
}

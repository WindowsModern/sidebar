using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.IO;

namespace Sidebar
{
	public static class WebAPI
	{
		const string baseUri = "https://windowsmodern.github.io/sidebar-web-store";
		public static string BaseUri => baseUri;
		public static TileList GetList ()
		{
			var cacheFile = Path.Combine (App.AppData.FolderPath, "Temp\\Store\\list.json");
			var baseCacheDir = Path.GetDirectoryName (cacheFile);
			if (!Directory.Exists (baseCacheDir))
			{
				try
				{
					Directory.CreateDirectory (baseCacheDir);
				}
				catch { }
			}
			try
			{
				if (File.Exists (cacheFile) && DateTime.UtcNow - File.GetLastWriteTimeUtc (cacheFile) < TimeSpan.FromDays (3))
				{
					using (var file = File.OpenText (cacheFile))
					{
						var jsonstr = file.ReadToEnd ();
						return JsonConvert.DeserializeObject<TileList> (jsonstr);
					}
				}
			}
			catch { }
			var url = $"{baseUri}/list.json";
			using (var client = new WebClient ())
			{
				var data = client.DownloadData (url);
				var jsonstr = Encoding.UTF8.GetString (data);
				try
				{
					using (var file = File.CreateText (cacheFile))
					{
						file.Write (jsonstr);
					}
				}
				catch { }
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

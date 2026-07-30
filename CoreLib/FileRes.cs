using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Win32;

namespace Sidebar
{
	public class PathResource: ConcurrentDictionary<int, string>, IPathResource
	{
		public PathResource () { }
		internal string GetRequiredValue (IDictionary <int, string> dict, int scale)
		{
			var ret = "";
			if (dict.TryGetValue (scale, out ret)) return ret;
			var list = dict.ToList ();
			list.Sort ((a, b) => a.Key.CompareTo (b.Key));
			foreach (var kv in list)
			{
				if (kv.Key >= scale) return kv.Value;
			}
			list.Sort ((a, b) => b.Key.CompareTo (a.Key));
			if (list.Count > 0) return list [0].Value;
			return string.Empty;
		}
		public string SuitableValue (string fallback = null, int dpiScale = -1)
		{
			if (dpiScale < 0) dpiScale = UITheme.GetDPI ();
			var ret = GetRequiredValue (this, dpiScale);
			if (string.IsNullOrWhiteSpace (ret))
			{
				if (dpiScale != UITheme.GetDPI ())
				{
					ret = GetRequiredValue (this, UITheme.GetDPI ());
				}
			}
			if (string.IsNullOrWhiteSpace (ret)) ret = null;
			return ret ?? fallback;
		}
		public override string ToString ()
		{
			try { return SuitableValue (); } catch { return ""; }
		}
		public void CleanRedundantValues (int dpiScale = -1)
		{
			if (Count <= 2) return;
			var list = PathResources.RecommendRemainScales;
			if (dpiScale > 0 && !list.Contains (dpiScale))
			{
				var newer = new List<int> (list);
				newer.Add (dpiScale);
				list = newer;
			}
			foreach (var i in list)
			{
				string _ = null;
				if (!ContainsKey (i)) this.TryRemove (i, out _);
				if (Count <= 2) return;
			}
		}
	}
	public class PathResources: ConcurrentDictionary<string, IPathResource>, IPathResources
	{
		public PathResources () : base (StringComparer.OrdinalIgnoreCase) { }
		public IDictionary<string, IPathResource> AllResources => this;
		public IPathResource AllValues (string resourceName) => this [resourceName];
		public string SuitableResource (string resName, string fallback = null, int scale = -1)
		{
			try { return this [resName]?.SuitableValue (fallback, scale) ?? fallback; }
			catch { return fallback; }
		}
		public static PathResources CreateFromXml (XmlDocument doc)
		{
			var ret = new PathResources ();
			var root = doc.DocumentElement;
			var children = root.ChildNodes;
			foreach (XmlNode xn in children)
			{
				var lr = new PathResource ();
				var locales = xn.ChildNodes;
				foreach (XmlNode xnl in locales)
				{
					lr [Convert.ToInt32 (xnl.Attributes ["scale"].Value)] = xnl.InnerText;
				}
				ret [xn.Attributes ["id"].Value] = lr;
			}
			return ret;
		}
		public static PathResources CreateFromFile (string filepath)
		{
			using (var f = File.Open (filepath, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				var doc = new XmlDocument ();
				doc.Load (f);
				return CreateFromXml (doc);
			}
		}
		public static PathResources CreateFromUrl (string filename)
		{
			var doc = new XmlDocument ();
			doc.Load (filename);
			return CreateFromXml (doc);
		}
		public void CleanRedundantValues (int dpiScale = -1)
		{
			foreach (var kv in this)
			{
				kv.Value?.CleanRedundantValues (dpiScale);
			}
		}
		private static List<int> rrs = null;
		internal static IEnumerable <int> RecommendRemainScales
		{
			get
			{
				if (rrs == null)
				{
					rrs = new List<int> ();
					var curr = UITheme.GetDPI ();
					if (curr != 100 && curr > 0) rrs.Add (curr);
					rrs.Add (100);
				}
				return rrs;
			}
		}
	}
}

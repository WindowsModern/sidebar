using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SGReader
{
	static class Program
	{
		/// <summary>
		/// 应用程序的主入口点。
		/// </summary>
		[STAThread]
		static void Main (string [] args)
		{
			Application.EnableVisualStyles ();
			Application.SetCompatibleTextRenderingDefault (false);
			var form = new MainForm ();
			if (args.Length > 0 && File.Exists (args [0]))
				form.SetOpenedFile (args [0]);
			Application.Run (form);
		}
	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace WindowsModern.UserTile
{
	public class UserUtils
	{
		public static string GetUserName ()
		{
			//WindowsIdentity identity = WindowsIdentity.GetCurrent ();
			//if (identity != null) return identity.Name;
			return Environment.UserName;
		}
		public static string GetUser ()
		{
			WindowsIdentity identity = WindowsIdentity.GetCurrent ();
			if (identity != null) return identity.Name;
			return Environment.UserDomainName;
		}
		public static string GetUserSid ()
		{
			using (WindowsIdentity identity = WindowsIdentity.GetCurrent ())
			{
				return identity.User.Value;
			}
		}
		public static string GetAccountPicturePath (string sid = null)
		{
			sid = sid ?? GetUserSid ();
			var username = Environment.GetEnvironmentVariable ("USERNAME");
			try
			{
				using (RegistryKey key = Registry.LocalMachine.OpenSubKey (@"Software\Microsoft\Windows\CurrentVersion\AccountPicture\Users\" + sid))
				{
					if (key != null)
					{
						object value = key.GetValue ("Source");
						if (value != null) return value.ToString ();
						value = key.GetValue ("Image96");
						if (value != null) return value.ToString ();
						value = key.GetValue ("Image64");
						if (value != null) return value.ToString ();
						value = key.GetValue ("Image48");
						if (value != null) return value.ToString ();
						value = key.GetValue ("Image40");
						if (value != null) return value.ToString ();
						value = key.GetValue ("Image32");
						if (value != null) return value.ToString ();
					}
				}
			}
			catch { }
			try
			{
				using (RegistryKey key = Registry.LocalMachine.OpenSubKey (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Hints\" + GetUser ()))
				{
					if (key != null)
					{
						object value = key.GetValue ("PictureSource");
						if (value != null) return value.ToString ();
					}
				}
			}
			catch { }
			try
			{
				using (RegistryKey key = Registry.LocalMachine.OpenSubKey (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Hints\" + username))
				{
					if (key != null)
					{
						object value = key.GetValue ("PictureSource");
						if (value != null) return value.ToString ();
					}
				}
			}
			catch { }
			{
				string environmentVariable = Environment.GetEnvironmentVariable ("USERNAME");
				string folderPath = Environment.GetFolderPath (Environment.SpecialFolder.CommonApplicationData);
				string uriString = string.Format ("{0}\\Microsoft\\User Account Pictures\\{1}.bmp", folderPath, environmentVariable);
				if (File.Exists (uriString)) return uriString;
			}
			{
				try
				{
					var path = GetUserTilePath (GetUser ());
					if (!string.IsNullOrWhiteSpace (path) && File.Exists (path)) return path;
				}
				catch { }
			}
			{
				var imgPath = System.IO.Path.Combine (Path.GetTempPath (), GetUserName () + ".bmp");
				if (File.Exists (imgPath)) return imgPath;
				imgPath = System.IO.Path.Combine (Path.GetTempPath (), GetUserName () + ".png");
				if (File.Exists (imgPath)) return imgPath;
				imgPath = System.IO.Path.Combine (Path.GetTempPath (), GetUserName () + ".jpg");
				if (File.Exists (imgPath)) return imgPath;
			}
			{
				string environmentVariable2 = Environment.GetEnvironmentVariable ("USERNAME");
				string environmentVariable3 = Environment.GetEnvironmentVariable ("TEMP");
				string uriString2 = string.Format ("{0}\\{1}.bmp", environmentVariable3, environmentVariable2);
				if (File.Exists (uriString2)) return uriString2;
			}
			return null;
		}
		private static string GetUserTilePath (string username)
		{
			StringBuilder stringBuilder = new StringBuilder (1000);
			GetUserTilePath (username, 2147483648u, stringBuilder, stringBuilder.Capacity);
			return stringBuilder.ToString ();
		}
		[DllImport ("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "#261", PreserveSig = false)]
		private static extern void GetUserTilePath (string username, uint whatever, StringBuilder PicPath, int maxLength);
	}
	public static class UserSwitchHelper
	{
		// 导入 keybd_event 函数 (user32.dll)
		[DllImport ("user32.dll", SetLastError = true)]
		private static extern void keybd_event (byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

		// 虚拟键码常量
		private const byte VK_LWIN = 0x5B;   // 左 Win 键
		private const byte VK_L = 0x4C;      // L 键

		// 事件标志
		private const uint KEYEVENTF_KEYDOWN = 0x0000;
		private const uint KEYEVENTF_KEYUP = 0x0002;

		/// <summary>
		/// 模拟 Win + L 组合键，触发快速用户切换（锁屏并显示切换用户界面）。
		/// </summary>
		public static void SwitchUser ()
		{
			try
			{
				// 按下 Win 键
				keybd_event (VK_LWIN, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
				// 按下 L 键
				keybd_event (VK_L, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
				// 释放 L 键
				keybd_event (VK_L, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
				// 释放 Win 键
				keybd_event (VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
			}
			catch (Exception ex)
			{
				// 可记录日志，这里简单忽略或重新抛出
				throw new InvalidOperationException ("模拟按键失败，请确保程序有足够权限。", ex);
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Applications.Sidebar
{
	[Serializable]
	public enum PreferredDeveloperContact
	{
		Email,
		Website
	}
	[AttributeUsage (AttributeTargets.Assembly, AllowMultiple = false)]
	public sealed class SidebarDeveloperContactInfo: Attribute
	{
		public string email;
		public string eSubj;
		public PreferredDeveloperContact howContact;
		public string website;
		public SidebarDeveloperContactInfo (string emailAddress, string emailSubject, string websiteURL, PreferredDeveloperContact preferedContactMethod)
		{
			this.email = emailAddress;
			this.website = websiteURL;
			this.howContact = preferedContactMethod;
		}
	}
	[AttributeUsage (AttributeTargets.Assembly, AllowMultiple = false)]
	public sealed class SidebarTileInfo: Attribute
	{
		public string Author;
		public string Copyright;
		public string Description;
		public bool IsOuterTile;
		public string Title;
		public double Version;
		public SidebarTileInfo (string title, string author, string copyright, string description, double version, bool outertile)
		{
			this.Title = title;
			this.Author = author;
			this.Copyright = copyright;
			this.Description = description;
			this.Version = version;
			this.IsOuterTile = outertile;
		}
	}
}

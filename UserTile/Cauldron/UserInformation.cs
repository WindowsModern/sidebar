using System;
using System.IO;
using System.Security.Principal;

namespace Cauldron
{
	/// <summary>
	/// Represents information about the current user, such as name and account picture.
	/// </summary>
	public static class UserInformation
	{
		private static User currentUser;

		static UserInformation ()
		{
			currentUser = new User (WindowsIdentity.GetCurrent ().Name);
		}

		/// <summary>
		/// Gets the account picture for the user.
		/// </summary>
		/// <returns>The image of the user</returns>
		public static byte [] AccountPicture
		{
			get { return currentUser.AccountPicture; }
		}

		/// <summary>
		/// Gets the current user
		/// </summary>
		public static User CurrentUser
		{
			get { return currentUser; }
		}

		/// <summary>
		/// Gets the display name for the user account.
		/// </summary>
		/// <returns>The display name for the user account.</returns>
		public static string DisplayName
		{
			get { return currentUser.DisplayName; }
		}

		/// <summary>
		/// Gets the domain name for the user.
		/// </summary>
		/// <returns>A string that represents the domain name for the user.</returns>
		public static string DomainName
		{
			get { return currentUser.DomainName; }
		}

		/// <summary>
		/// Gets the user's email address.
		/// </summary>
		/// <returns>The user's email address name</returns>
		public static string EmailAddress
		{
			get { return currentUser.EmailAddress; }
		}

		/// <summary>
		/// Gets the user's first name.
		/// </summary>
		/// <returns>The user's first name.</returns>
		public static string FirstName
		{
			get { return currentUser.FirstName; }
		}

		/// <summary>
		/// Gets the user's home directory.
		/// </summary>
		/// <returns>The user's home directory</returns>
		public static DirectoryInfo HomeDirectory
		{
			get { return currentUser.HomeDirectory; }
		}

		/// <summary>
		/// Tests whether the current user is an elevated administrator.
		/// </summary>
		public static bool IsCurrentUserAnAdministrator
		{
			get
			{
				try
				{
					using (var user = WindowsIdentity.GetCurrent ())
						return new WindowsPrincipal (user).IsInRole (WindowsBuiltInRole.Administrator);
				}
				catch
				{
					return false;
				}
			}
		}

		/// <summary>
		/// Gets a value that indicates if the user account is local or domain.
		/// <para/>
		/// Returns true if the account is a local account, otherwise false
		/// </summary>
		public static bool IsLocalAccount
		{
			get { return currentUser.IsLocalAccount; }
		}

		/// <summary>
		/// Gets a value that indicates if the user is locked out or not
		/// </summary>
		/// <returns>Returns true if the user is locked out, otherwise false</returns>
		public static bool IsLockedOut
		{
			get { return currentUser.IsLockedOut; }
		}

		/// <summary>
		/// Gets the user's last name.
		/// </summary>
		/// <returns>The user's last name.</returns>
		public static string LastName
		{
			get { return currentUser.LastName; }
		}

		/// <summary>
		/// Gets the principal name for the user. This name is the User Principal Name (typically the
		/// user's address, although this is not always true.)
		/// </summary>
		/// <returns>The user's principal name.</returns>
		public static string PrincipalName
		{
			get { return currentUser.PrincipalName; }
		}

		/// <summary>
		/// Gets the telephone number of the user
		/// </summary>
		/// <returns>The telephone number of the user</returns>
		public static string TelephoneNumber
		{
			get { return currentUser.TelephoneNumber; }
		}

		/// <summary>
		/// Gets the user name of the user.
		/// </summary>
		/// <returns>A string that represents the user name of the user.</returns>
		public static string UserName
		{
			get { return currentUser.UserName; }
		}

		/// <summary>
		/// Gets a the user's Windows Terminal Service's client name. The value will fallback to <see cref="Environment.MachineName"/> if there is no client name available.
		/// </summary>
		public static string WTSClientName
		{
			get { return currentUser.WTSClientName; }
		}

		/// <summary>
		/// Gets the user information of the given user by its user name.
		/// </summary>
		/// <param name="userName">The domain and id of the user</param>
		/// <exception cref="ArgumentNullException"><paramref name="userName"/> is null</exception>
		/// <exception cref="ArgumentException"><paramref name="userName"/> is empty</exception>
		public static User GetUserInformation (string userName)
		{
			return new User (userName);
		}
	}
}
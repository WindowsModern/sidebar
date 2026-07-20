using System;
using System.IO;
using System.Security.Cryptography;

public static class FileHash
{
	public static string GenerateDigest (string filePath)
	{
		if (!File.Exists (filePath))
			throw new FileNotFoundException ("File doesn't exist.", filePath);
		byte [] md5Hash, sha1Hash;
		using (var md5 = new MD5CryptoServiceProvider ())
		using (var stream = File.OpenRead (filePath))
		{
			md5Hash = md5.ComputeHash (stream);
		}
		using (var sha1 = new SHA1CryptoServiceProvider ())
		using (var stream = File.OpenRead (filePath))
		{
			sha1Hash = sha1.ComputeHash (stream);
		}
		byte [] combined = new byte [md5Hash.Length + sha1Hash.Length];
		Buffer.BlockCopy (md5Hash, 0, combined, 0, md5Hash.Length);
		Buffer.BlockCopy (sha1Hash, 0, combined, md5Hash.Length, sha1Hash.Length);
		return Convert.ToBase64String (combined);
	}
	public static bool VerifyDigest (string filePath, string combinedHashBase64)
	{
		if (!File.Exists (filePath))
			return false;
		byte [] combined;
		try
		{
			combined = Convert.FromBase64String (combinedHashBase64);
		}
		catch (FormatException)
		{
			return false; 
		}
		if (combined.Length != 36)
			return false;
		byte [] expectedMd5 = new byte [16];
		byte [] expectedSha1 = new byte [20];
		Buffer.BlockCopy (combined, 0, expectedMd5, 0, 16);
		Buffer.BlockCopy (combined, 16, expectedSha1, 0, 20);
		byte [] currentMd5, currentSha1;
		using (var md5 = new MD5CryptoServiceProvider ())
		using (var stream = File.OpenRead (filePath))
		{
			currentMd5 = md5.ComputeHash (stream);
		}
		using (var sha1 = new SHA1CryptoServiceProvider ())
		using (var stream = File.OpenRead (filePath))
		{
			currentSha1 = sha1.ComputeHash (stream);
		}
		return CompareByteArrays (expectedMd5, currentMd5) &&
			   CompareByteArrays (expectedSha1, currentSha1);
	}
	private static bool CompareByteArrays (byte [] a, byte [] b)
	{
		if (a.Length != b.Length) return false;
		for (int i = 0; i < a.Length; i++)
		{
			if (a [i] != b [i]) return false;
		}
		return true;
	}
}
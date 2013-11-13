/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2013 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;
using System.Security;
using System.Security.Cryptography;
using System.IO;

using KeePassLib.Cryptography;
using KeePassLib.Resources;
using KeePassLib.Security;
using KeePassLib.Utility;

namespace KeePassLib.Keys
{
	/// <summary>
	/// A user key depending on the currently logged on Windows user account.
	/// </summary>
	public sealed class KcpUserAccount : IUserKey
	{
		private ProtectedBinary m_pbKeyData = null;

		// Constant initialization vector (unique for KeePass)
		private static readonly byte[] m_pbEntropy = new byte[]{
			0xDE, 0x13, 0x5B, 0x5F, 0x18, 0xA3, 0x46, 0x70,
			0xB2, 0x57, 0x24, 0x29, 0x69, 0x88, 0x98, 0xE6
		};

		private const string UserKeyFileName = "ProtectedUserKey.bin";

		/// <summary>
		/// Get key data. Querying this property is fast (it returns a
		/// reference to a cached <c>ProtectedBinary</c> object).
		/// If no key data is available, <c>null</c> is returned.
		/// </summary>
		public ProtectedBinary KeyData
		{
			get { return m_pbKeyData; }
		}

		/// <summary>
		/// Construct a user account key.
		/// </summary>
		public KcpUserAccount()
		{
			// Test if ProtectedData is supported -- throws an exception
			// when running on an old system (Windows 98 / ME).
			byte[] pbDummyData = new byte[128];
			ProtectedData.Protect(pbDummyData, m_pbEntropy,
				DataProtectionScope.CurrentUser);

			byte[] pbKey = LoadUserKey(false);
			if(pbKey == null) pbKey = CreateUserKey();
			if(pbKey == null) throw new SecurityException(KLRes.UserAccountKeyError);

			m_pbKeyData = new ProtectedBinary(true, pbKey);
			Array.Clear(pbKey, 0, pbKey.Length);
		}

		// public void Clear()
		// {
		//	m_pbKeyData = null;
		// }

		private static string GetUserKeyFilePath(bool bCreate)
		{
#if KeePassRT
			string strUserDir = Windows.Storage.ApplicationData.Current.RoamingFolder.Path;
#else
			string strUserDir = Environment.GetFolderPath(
				Environment.SpecialFolder.ApplicationData);
#endif

			strUserDir = UrlUtil.EnsureTerminatingSeparator(strUserDir, false);
			strUserDir += PwDefs.ShortProductName;

			if(bCreate && !Directory.Exists(strUserDir))
				Directory.CreateDirectory(strUserDir);

			strUserDir = UrlUtil.EnsureTerminatingSeparator(strUserDir, false);
			return strUserDir + UserKeyFileName;
		}

		private static byte[] LoadUserKey(bool bShowWarning)
		{
			byte[] pbKey = null;

#if !KeePassLibSD
			try
			{
				string strFilePath = GetUserKeyFilePath(false);
				byte[] pbProtectedKey = File.ReadAllBytes(strFilePath);

				pbKey = ProtectedData.Unprotect(pbProtectedKey, m_pbEntropy,
					DataProtectionScope.CurrentUser);

				Array.Clear(pbProtectedKey, 0, pbProtectedKey.Length);
			}
			catch(Exception exLoad)
			{
				if(bShowWarning) MessageService.ShowWarning(exLoad);

				pbKey = null;
			}
#endif

			return pbKey;
		}

		private static byte[] CreateUserKey()
		{
			byte[] pbKey = null;

#if !KeePassLibSD
			try
			{
				string strFilePath = GetUserKeyFilePath(true);

				byte[] pbRandomKey = CryptoRandom.Instance.GetRandomBytes(64);
				byte[] pbProtectedKey = ProtectedData.Protect(pbRandomKey,
					m_pbEntropy, DataProtectionScope.CurrentUser);

				File.WriteAllBytes(strFilePath, pbProtectedKey);

				Array.Clear(pbProtectedKey, 0, pbProtectedKey.Length);
				Array.Clear(pbRandomKey, 0, pbRandomKey.Length);

				pbKey = LoadUserKey(true);
			}
			catch(Exception) { pbKey = null; }
#endif

			return pbKey;
		}
	}
}

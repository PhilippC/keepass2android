/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2017 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using KeePassLib.Cryptography;
using KeePassLib.Cryptography.KeyDerivation;
using KeePassLib.Native;
using KeePassLib.Resources;
using KeePassLib.Security;
using KeePassLib.Utility;

namespace KeePassLib.Keys
{
	/// <summary>
	/// Represents a key. A key can be build up using several user key data sources
	/// like a password, a key file, the currently logged on user credentials,
	/// the current computer ID, etc.
	/// </summary>
	public sealed class CompositeKey
	{
		private List<IUserKey> m_vUserKeys = new List<IUserKey>();

		/// <summary>
		/// List of all user keys contained in the current composite key.
		/// </summary>
		public IEnumerable<IUserKey> UserKeys
		{
			get { return m_vUserKeys; }
		}

		public uint UserKeyCount
		{
			get { return (uint)m_vUserKeys.Count; }
		}

		/// <summary>
		/// Construct a new, empty key object.
		/// </summary>
		public CompositeKey()
		{
		}

		// /// <summary>
		// /// Deconstructor, clears up the key.
		// /// </summary>
		// ~CompositeKey()
		// {
		//	Clear();
		// }

		// /// <summary>
		// /// Clears the key. This function also erases all previously stored
		// /// user key data objects.
		// /// </summary>
		// public void Clear()
		// {
		//	foreach(IUserKey pKey in m_vUserKeys)
		//		pKey.Clear();
		//	m_vUserKeys.Clear();
		// }

		/// <summary>
		/// Add a user key.
		/// </summary>
		/// <param name="pKey">User key to add.</param>
		public void AddUserKey(IUserKey pKey)
		{
			Debug.Assert(pKey != null); if(pKey == null) throw new ArgumentNullException("pKey");

			m_vUserKeys.Add(pKey);
		}

		/// <summary>
		/// Remove a user key.
		/// </summary>
		/// <param name="pKey">User key to remove.</param>
		/// <returns>Returns <c>true</c> if the key was removed successfully.</returns>
		public bool RemoveUserKey(IUserKey pKey)
		{
			Debug.Assert(pKey != null); if(pKey == null) throw new ArgumentNullException("pKey");

			Debug.Assert(m_vUserKeys.IndexOf(pKey) >= 0);
			return m_vUserKeys.Remove(pKey);
		}

		/// <summary>
		/// Test whether the composite key contains a specific type of
		/// user keys (password, key file, ...). If at least one user
		/// key of that type is present, the function returns <c>true</c>.
		/// </summary>
		/// <param name="tUserKeyType">User key type.</param>
		/// <returns>Returns <c>true</c>, if the composite key contains
		/// a user key of the specified type.</returns>
		public bool ContainsType(Type tUserKeyType)
		{
			Debug.Assert(tUserKeyType != null);
			if(tUserKeyType == null) throw new ArgumentNullException("tUserKeyType");

			foreach(IUserKey pKey in m_vUserKeys)
			{
				if(pKey == null) { Debug.Assert(false); continue; }

#if KeePassUAP
				if(pKey.GetType() == tUserKeyType)
					return true;
#else
				if(tUserKeyType.IsInstanceOfType(pKey))
					return true;
#endif
			}

			return false;
		}

		/// <summary>
		/// Get the first user key of a specified type.
		/// </summary>
		/// <param name="tUserKeyType">Type of the user key to get.</param>
		/// <returns>Returns the first user key of the specified type
		/// or <c>null</c> if no key of that type is found.</returns>
		public IUserKey GetUserKey(Type tUserKeyType)
		{
			Debug.Assert(tUserKeyType != null);
			if(tUserKeyType == null) throw new ArgumentNullException("tUserKeyType");

			foreach(IUserKey pKey in m_vUserKeys)
			{
				if(pKey == null) { Debug.Assert(false); continue; }

#if KeePassUAP
				if(pKey.GetType() == tUserKeyType)
					return pKey;
#else
				if(tUserKeyType.IsInstanceOfType(pKey))
					return pKey;
#endif
			}

			return null;
		}

		/// <summary>
		/// Creates the composite key from the supplied user key sources (password,
		/// key file, user account, computer ID, etc.).
		/// </summary>
		private byte[] CreateRawCompositeKey32()
		{
			ValidateUserKeys();

			// Concatenate user key data
			List<byte[]> lData = new List<byte[]>();
			int cbData = 0;
			foreach(IUserKey pKey in m_vUserKeys)
			{
				ProtectedBinary b = pKey.KeyData;
				if(b != null)
				{
					byte[] pbKeyData = b.ReadData();
					lData.Add(pbKeyData);
					cbData += pbKeyData.Length;
				}
			}

			byte[] pbAllData = new byte[cbData];
			int p = 0;
			foreach(byte[] pbData in lData)
			{
				Array.Copy(pbData, 0, pbAllData, p, pbData.Length);
				p += pbData.Length;
				MemUtil.ZeroByteArray(pbData);
			}
			Debug.Assert(p == cbData);

			byte[] pbHash = CryptoUtil.HashSha256(pbAllData);
			MemUtil.ZeroByteArray(pbAllData);
			return pbHash;
		}

		public bool EqualsValue(CompositeKey ckOther)
		{
			if(ckOther == null) throw new ArgumentNullException("ckOther");

			byte[] pbThis = CreateRawCompositeKey32();
			byte[] pbOther = ckOther.CreateRawCompositeKey32();
			bool bResult = MemUtil.ArraysEqual(pbThis, pbOther);
			MemUtil.ZeroByteArray(pbOther);
			MemUtil.ZeroByteArray(pbThis);

			return bResult;
		}

		[Obsolete]
		public ProtectedBinary GenerateKey32(byte[] pbKeySeed32, ulong uNumRounds)
		{
			Debug.Assert(pbKeySeed32 != null);
			if(pbKeySeed32 == null) throw new ArgumentNullException("pbKeySeed32");
			Debug.Assert(pbKeySeed32.Length == 32);
			if(pbKeySeed32.Length != 32) throw new ArgumentException("pbKeySeed32");

			AesKdf kdf = new AesKdf();
			KdfParameters p = kdf.GetDefaultParameters();
			p.SetUInt64(AesKdf.ParamRounds, uNumRounds);
			p.SetByteArray(AesKdf.ParamSeed, pbKeySeed32);

			return GenerateKey32(p);
		}

		/// <summary>
		/// Generate a 32-byte (256-bit) key from the composite key.
		/// </summary>
		public ProtectedBinary GenerateKey32(KdfParameters p)
		{
			if(p == null) { Debug.Assert(false); throw new ArgumentNullException("p"); }

			byte[] pbRaw32 = CreateRawCompositeKey32();
			if((pbRaw32 == null) || (pbRaw32.Length != 32))
				{ Debug.Assert(false); return null; }

			KdfEngine kdf = KdfPool.Get(p.KdfUuid);
			if(kdf == null) // CryptographicExceptions are translated to "file corrupted"
				throw new Exception(KLRes.UnknownKdf + MessageService.NewParagraph +
					KLRes.FileNewVerOrPlgReq + MessageService.NewParagraph +
					"UUID: " + p.KdfUuid.ToHexString() + ".");

			byte[] pbTrf32 = kdf.Transform(pbRaw32, p);
			if(pbTrf32 == null) { Debug.Assert(false); return null; }

			if(pbTrf32.Length != 32)
			{
				Debug.Assert(false);
				pbTrf32 = CryptoUtil.HashSha256(pbTrf32);
			}

			ProtectedBinary pbRet = new ProtectedBinary(true, pbTrf32);
			MemUtil.ZeroByteArray(pbTrf32);
			MemUtil.ZeroByteArray(pbRaw32);
			return pbRet;
		}

		private void ValidateUserKeys()
		{
			int nAccounts = 0;

			foreach(IUserKey uKey in m_vUserKeys)
			{
				if(uKey is KcpUserAccount)
					++nAccounts;
			}

			if(nAccounts >= 2)
			{
				Debug.Assert(false);
				throw new InvalidOperationException();
			}
		}
	}

	public sealed class InvalidCompositeKeyException : Exception
	{
		public override string Message
		{
			get
			{
				return KLRes.InvalidCompositeKey + MessageService.NewParagraph +
					KLRes.InvalidCompositeKeyHint;
			}
		}

		/// <summary>
		/// Construct a new invalid composite key exception.
		/// </summary>
		public InvalidCompositeKeyException()
		{
		}
	}
}

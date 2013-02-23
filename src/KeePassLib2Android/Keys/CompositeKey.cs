/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2012 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

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
				if(tUserKeyType.IsInstanceOfType(pKey))
					return true;
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
				if(tUserKeyType.IsInstanceOfType(pKey))
					return pKey;
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
			MemoryStream ms = new MemoryStream();
			foreach(IUserKey pKey in m_vUserKeys)
			{
				ProtectedBinary b = pKey.KeyData;
				if(b != null)
				{
					byte[] pbKeyData = b.ReadData();
					ms.Write(pbKeyData, 0, pbKeyData.Length);
					MemUtil.ZeroByteArray(pbKeyData);
				}
			}

			SHA256Managed sha256 = new SHA256Managed();
			byte[] pbHash = sha256.ComputeHash(ms.ToArray());
			ms.Close();
			return pbHash;
		}

		public bool EqualsValue(CompositeKey ckOther)
		{
			if(ckOther == null) throw new ArgumentNullException("ckOther");

			byte[] pbThis = CreateRawCompositeKey32();
			byte[] pbOther = ckOther.CreateRawCompositeKey32();
			bool bResult = MemUtil.ArraysEqual(pbThis, pbOther);
			Array.Clear(pbOther, 0, pbOther.Length);
			Array.Clear(pbThis, 0, pbThis.Length);

			return bResult;
		}

		/// <summary>
		/// Generate a 32-bit wide key out of the composite key.
		/// </summary>
		/// <param name="pbKeySeed32">Seed used in the key transformation
		/// rounds. Must be a byte array containing exactly 32 bytes; must
		/// not be null.</param>
		/// <param name="uNumRounds">Number of key transformation rounds.</param>
		/// <returns>Returns a protected binary object that contains the
		/// resulting 32-bit wide key.</returns>
		public ProtectedBinary GenerateKey32(byte[] pbKeySeed32, ulong uNumRounds)
		{
			Debug.Assert(pbKeySeed32 != null);
			if(pbKeySeed32 == null) throw new ArgumentNullException("pbKeySeed32");
			Debug.Assert(pbKeySeed32.Length == 32);
			if(pbKeySeed32.Length != 32) throw new ArgumentException("pbKeySeed32");

			byte[] pbRaw32 = CreateRawCompositeKey32();
			if((pbRaw32 == null) || (pbRaw32.Length != 32))
				{ Debug.Assert(false); return null; }

			byte[] pbTrf32 = TransformKey(pbRaw32, pbKeySeed32, uNumRounds);
			if((pbTrf32 == null) || (pbTrf32.Length != 32))
				{ Debug.Assert(false); return null; }

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

		/// <summary>
		/// Transform the current key <c>uNumRounds</c> times.
		/// </summary>
		/// <param name="pbOriginalKey32">The original key which will be transformed.
		/// This parameter won't be modified.</param>
		/// <param name="pbKeySeed32">Seed used for key transformations. Must not
		/// be <c>null</c>. This parameter won't be modified.</param>
		/// <param name="uNumRounds">Transformation count.</param>
		/// <returns>256-bit transformed key.</returns>
		private static byte[] TransformKey(byte[] pbOriginalKey32, byte[] pbKeySeed32,
			ulong uNumRounds)
		{
			Debug.Assert((pbOriginalKey32 != null) && (pbOriginalKey32.Length == 32));
			if(pbOriginalKey32 == null) throw new ArgumentNullException("pbOriginalKey32");
			if(pbOriginalKey32.Length != 32) throw new ArgumentException();

			Debug.Assert((pbKeySeed32 != null) && (pbKeySeed32.Length == 32));
			if(pbKeySeed32 == null) throw new ArgumentNullException("pbKeySeed32");
			if(pbKeySeed32.Length != 32) throw new ArgumentException();

			byte[] pbNewKey = new byte[32];
			Array.Copy(pbOriginalKey32, pbNewKey, pbNewKey.Length);

			// Try to use the native library first
			if(NativeLib.TransformKey256(pbNewKey, pbKeySeed32, uNumRounds))
				return (new SHA256Managed()).ComputeHash(pbNewKey);

			if(TransformKeyManaged(pbNewKey, pbKeySeed32, uNumRounds) == false)
				return null;

			SHA256Managed sha256 = new SHA256Managed();
			return sha256.ComputeHash(pbNewKey);
		}

		public static bool TransformKeyManaged(byte[] pbNewKey32, byte[] pbKeySeed32,
			ulong uNumRounds)
		{
			byte[] pbIV = new byte[16];
			Array.Clear(pbIV, 0, pbIV.Length);

			RijndaelManaged r = new RijndaelManaged();
			if(r.BlockSize != 128) // AES block size
			{
				Debug.Assert(false);
				r.BlockSize = 128;
			}

			r.IV = pbIV;
			r.Mode = CipherMode.ECB;
			r.KeySize = 256;
			r.Key = pbKeySeed32;
			ICryptoTransform iCrypt = r.CreateEncryptor();

			// !iCrypt.CanReuseTransform -- doesn't work with Mono
			if((iCrypt == null) || (iCrypt.InputBlockSize != 16) ||
				(iCrypt.OutputBlockSize != 16))
			{
				Debug.Assert(false, "Invalid ICryptoTransform.");
				Debug.Assert((iCrypt.InputBlockSize == 16), "Invalid input block size!");
				Debug.Assert((iCrypt.OutputBlockSize == 16), "Invalid output block size!");
				return false;
			}

			for(ulong i = 0; i < uNumRounds; ++i)
			{
				iCrypt.TransformBlock(pbNewKey32, 0, 16, pbNewKey32, 0);
				iCrypt.TransformBlock(pbNewKey32, 16, 16, pbNewKey32, 16);
			}

			return true;
		}

		/// <summary>
		/// Benchmark the <c>TransformKey</c> method. Within
		/// <paramref name="uMilliseconds"/> ms, random keys will be transformed
		/// and the number of performed transformations are returned.
		/// </summary>
		/// <param name="uMilliseconds">Test duration in ms.</param>
		/// <param name="uStep">Stepping.
		/// <paramref name="uStep" /> should be a prime number. For fast processors
		/// (PCs) a value of <c>3001</c> is recommended, for slower processors (PocketPC)
		/// a value of <c>401</c> is recommended.</param>
		/// <returns>Number of transformations performed in the specified
		/// amount of time. Maximum value is <c>uint.MaxValue</c>.</returns>
		public static ulong TransformKeyBenchmark(uint uMilliseconds, ulong uStep)
		{
			ulong uRounds;

			// Try native method
			if(NativeLib.TransformKeyBenchmark256(uMilliseconds, out uRounds))
				return uRounds;

			byte[] pbIV = new byte[16];
			Array.Clear(pbIV, 0, pbIV.Length);

			byte[] pbKey = new byte[32];
			byte[] pbNewKey = new byte[32];
			for(int i = 0; i < pbKey.Length; ++i)
			{
				pbKey[i] = (byte)i;
				pbNewKey[i] = (byte)i;
			}

			RijndaelManaged r = new RijndaelManaged();
			if(r.BlockSize != 128) // AES block size
			{
				Debug.Assert(false);
				r.BlockSize = 128;
			}

			r.IV = pbIV;
			r.Mode = CipherMode.ECB;
			r.KeySize = 256;
			r.Key = pbKey;
			ICryptoTransform iCrypt = r.CreateEncryptor();

			// !iCrypt.CanReuseTransform -- doesn't work with Mono
			if((iCrypt == null) || (iCrypt.InputBlockSize != 16) ||
				(iCrypt.OutputBlockSize != 16))
			{
				Debug.Assert(false, "Invalid ICryptoTransform.");
				Debug.Assert(iCrypt.InputBlockSize == 16, "Invalid input block size!");
				Debug.Assert(iCrypt.OutputBlockSize == 16, "Invalid output block size!");
				return PwDefs.DefaultKeyEncryptionRounds;
			}

			DateTime dtStart = DateTime.Now;
			TimeSpan ts;
			double dblReqMillis = uMilliseconds;

			uRounds = 0;
			while(true)
			{
				for(ulong j = 0; j < uStep; ++j)
				{
					iCrypt.TransformBlock(pbNewKey, 0, 16, pbNewKey, 0);
					iCrypt.TransformBlock(pbNewKey, 16, 16, pbNewKey, 16);
				}

				uRounds += uStep;
				if(uRounds < uStep) // Overflow check
				{
					uRounds = ulong.MaxValue;
					break;
				}

				ts = DateTime.Now - dtStart;
				if(ts.TotalMilliseconds > dblReqMillis) break;
			}

			return uRounds;
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

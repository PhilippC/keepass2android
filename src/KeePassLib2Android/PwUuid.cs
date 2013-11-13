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
using System.Collections.Generic;
using System.Xml;
using System.Diagnostics;

using KeePassLib.Utility;

namespace KeePassLib
{
	// [ImmutableObject(true)]
	/// <summary>
	/// Represents an UUID of a password entry or group. Once created,
	/// <c>PwUuid</c> objects aren't modifyable anymore (immutable).
	/// </summary>
	public sealed class PwUuid : IComparable<PwUuid>, IEquatable<PwUuid>
	{
		/// <summary>
		/// Standard size in bytes of a UUID.
		/// </summary>
		public const uint UuidSize = 16;

		/// <summary>
		/// Zero UUID (all bytes are zero).
		/// </summary>
		public static readonly PwUuid Zero = new PwUuid(false);

		private byte[] m_pbUuid = null; // Never null after constructor

		/// <summary>
		/// Get the 16 UUID bytes.
		/// </summary>
		public byte[] UuidBytes
		{
			get { return m_pbUuid; }
		}

		/// <summary>
		/// Construct a new UUID object.
		/// </summary>
		/// <param name="bCreateNew">If this parameter is <c>true</c>, a new
		/// UUID is generated. If it is <c>false</c>, the UUID is initialized
		/// to zero.</param>
		public PwUuid(bool bCreateNew)
		{
			if(bCreateNew) CreateNew();
			else SetZero();
		}

		/// <summary>
		/// Construct a new UUID object.
		/// </summary>
		/// <param name="uuidBytes">Initial value of the <c>PwUuid</c> object.</param>
		public PwUuid(byte[] uuidBytes)
		{
			SetValue(uuidBytes);
		}

		/// <summary>
		/// Create a new, random UUID.
		/// </summary>
		/// <returns>Returns <c>true</c> if a random UUID has been generated,
		/// otherwise it returns <c>false</c>.</returns>
		private void CreateNew()
		{
			Debug.Assert(m_pbUuid == null); // Only call from constructor
			while(true)
			{
				m_pbUuid = Guid.NewGuid().ToByteArray();

				if((m_pbUuid == null) || (m_pbUuid.Length != (int)UuidSize))
				{
					Debug.Assert(false);
					throw new InvalidOperationException();
				}

				// Zero is a reserved value -- do not generate Zero
				if(!Equals(PwUuid.Zero)) break;
				Debug.Assert(false);
			}
		}

		private void SetValue(byte[] uuidBytes)
		{
			Debug.Assert((uuidBytes != null) && (uuidBytes.Length == (int)UuidSize));
			if(uuidBytes == null) throw new ArgumentNullException("uuidBytes");
			if(uuidBytes.Length != (int)UuidSize) throw new ArgumentException();

			Debug.Assert(m_pbUuid == null); // Only call from constructor
			m_pbUuid = new byte[UuidSize];

			Array.Copy(uuidBytes, m_pbUuid, (int)UuidSize);
		}

		private void SetZero()
		{
			Debug.Assert(m_pbUuid == null); // Only call from constructor
			m_pbUuid = new byte[UuidSize];

			// Array.Clear(m_pbUuid, 0, (int)UuidSize);
#if DEBUG
			List<byte> l = new List<byte>(m_pbUuid);
			Debug.Assert(l.TrueForAll(bt => (bt == 0)));
#endif
		}

		[Obsolete]
		public bool EqualsValue(PwUuid uuid)
		{
			return Equals(uuid);
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as PwUuid);
		}

		public bool Equals(PwUuid other)
		{
			if(other == null) { Debug.Assert(false); return false; }

			for(int i = 0; i < (int)UuidSize; ++i)
			{
				if(m_pbUuid[i] != other.m_pbUuid[i]) return false;
			}

			return true;
		}

		private int m_h = 0;
		public override int GetHashCode()
		{
			if(m_h == 0)
				m_h = (int)MemUtil.Hash32(m_pbUuid, 0, m_pbUuid.Length);
			return m_h;
		}

		public int CompareTo(PwUuid other)
		{
			if(other == null)
			{
				Debug.Assert(false);
				throw new ArgumentNullException("other");
			}

			for(int i = 0; i < (int)UuidSize; ++i)
			{
				if(m_pbUuid[i] < other.m_pbUuid[i]) return -1;
				if(m_pbUuid[i] > other.m_pbUuid[i]) return 1;
			}

			return 0;
		}

		/// <summary>
		/// Convert the UUID to its string representation.
		/// </summary>
		/// <returns>String containing the UUID value.</returns>
		public string ToHexString()
		{
			return MemUtil.ByteArrayToHexString(m_pbUuid);
		}

#if DEBUG
		public override string ToString()
		{
			return ToHexString();
		}
#endif
	}

	[Obsolete]
	public sealed class PwUuidComparable : IComparable<PwUuidComparable>
	{
		private byte[] m_pbUuid = new byte[PwUuid.UuidSize];

		public PwUuidComparable(PwUuid pwUuid)
		{
			if(pwUuid == null) throw new ArgumentNullException("pwUuid");

			Array.Copy(pwUuid.UuidBytes, m_pbUuid, (int)PwUuid.UuidSize);
		}

		public int CompareTo(PwUuidComparable other)
		{
			if(other == null) throw new ArgumentNullException("other");

			for(int i = 0; i < (int)PwUuid.UuidSize; ++i)
			{
				if(m_pbUuid[i] < other.m_pbUuid[i]) return -1;
				if(m_pbUuid[i] > other.m_pbUuid[i]) return 1;
			}

			return 0;
		}
	}
}

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
using System.Text;
using System.Diagnostics;
using System.Security.Cryptography;

using KeePassLib.Security;
using KeePassLib.Utility;

namespace KeePassLib.Keys
{
	public sealed class KcpCustomKey : IUserKey
	{
		private readonly string m_strName;
		private ProtectedBinary m_pbKey;

		/// <summary>
		/// Name of the provider that generated the custom key.
		/// </summary>
		public string Name
		{
			get { return m_strName; }
		}

		public ProtectedBinary KeyData
		{
			get { return m_pbKey; }
		}

		public KcpCustomKey(string strName, byte[] pbKeyData, bool bPerformHash)
		{
			Debug.Assert(strName != null); if(strName == null) throw new ArgumentNullException("strName");
			Debug.Assert(pbKeyData != null); if(pbKeyData == null) throw new ArgumentNullException("pbKeyData");

			m_strName = strName;

			if(bPerformHash)
			{
				SHA256Managed sha256 = new SHA256Managed();
				byte[] pbRaw = sha256.ComputeHash(pbKeyData);
				m_pbKey = new ProtectedBinary(true, pbRaw);
			}
			else m_pbKey = new ProtectedBinary(true, pbKeyData);
		}

		// public void Clear()
		// {
		//	m_pbKey = null;
		// }
	}
}

/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2016 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

using KeePassLib.Utility;

namespace KeePassLib.Keys
{
	public sealed class KeyValidatorPool : IEnumerable<KeyValidator>
	{
		private List<KeyValidator> m_vValidators = new List<KeyValidator>();

		public int Count
		{
			get { return m_vValidators.Count; }
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return m_vValidators.GetEnumerator();
		}

		public IEnumerator<KeyValidator> GetEnumerator()
		{
			return m_vValidators.GetEnumerator();
		}

		public void Add(KeyValidator v)
		{
			Debug.Assert(v != null); if(v == null) throw new ArgumentNullException("v");

			m_vValidators.Add(v);
		}

		public bool Remove(KeyValidator v)
		{
			Debug.Assert(v != null); if(v == null) throw new ArgumentNullException("v");

			return m_vValidators.Remove(v);
		}

		public string Validate(string strKey, KeyValidationType t)
		{
			Debug.Assert(strKey != null); if(strKey == null) throw new ArgumentNullException("strKey");

			foreach(KeyValidator v in m_vValidators)
			{
				string strResult = v.Validate(strKey, t);
				if(strResult != null) return strResult;
			}

			return null;
		}

		public string Validate(byte[] pbKeyUtf8, KeyValidationType t)
		{
			Debug.Assert(pbKeyUtf8 != null); if(pbKeyUtf8 == null) throw new ArgumentNullException("pbKeyUtf8");

			if(m_vValidators.Count == 0) return null;

			string strKey = StrUtil.Utf8.GetString(pbKeyUtf8, 0, pbKeyUtf8.Length);
			return Validate(strKey, t);
		}
	}
}

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

using KeePassLib.Interfaces;

#if KeePassLibSD
using KeePassLibSD;
#endif

namespace KeePassLib.Collections
{
	public sealed class StringDictionaryEx : IDeepCloneable<StringDictionaryEx>,
		IEnumerable<KeyValuePair<string, string>>
	{
		private SortedDictionary<string, string> m_vDict =
			new SortedDictionary<string, string>();

		public int Count
		{
			get { return m_vDict.Count; }
		}

		public StringDictionaryEx()
		{
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return m_vDict.GetEnumerator();
		}

		public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
		{
			return m_vDict.GetEnumerator();
		}

		public StringDictionaryEx CloneDeep()
		{
			StringDictionaryEx plNew = new StringDictionaryEx();

			foreach(KeyValuePair<string, string> kvpStr in m_vDict)
				plNew.Set(kvpStr.Key, kvpStr.Value);

			return plNew;
		}

		public string Get(string strName)
		{
			Debug.Assert(strName != null); if(strName == null) throw new ArgumentNullException("strName");

			string s;
			if(m_vDict.TryGetValue(strName, out s)) return s;

			return null;
		}

		public bool Exists(string strName)
		{
			Debug.Assert(strName != null); if(strName == null) throw new ArgumentNullException("strName");

			return m_vDict.ContainsKey(strName);
		}

		/// <summary>
		/// Set a string.
		/// </summary>
		/// <param name="strField">Identifier of the string field to modify.</param>
		/// <param name="strNewValue">New value. This parameter must not be <c>null</c>.</param>
		/// <exception cref="System.ArgumentNullException">Thrown if one of the input
		/// parameters is <c>null</c>.</exception>
		public void Set(string strField, string strNewValue)
		{
			Debug.Assert(strField != null); if(strField == null) throw new ArgumentNullException("strField");
			Debug.Assert(strNewValue != null); if(strNewValue == null) throw new ArgumentNullException("strNewValue");

			m_vDict[strField] = strNewValue;
		}

		/// <summary>
		/// Delete a string.
		/// </summary>
		/// <param name="strField">Name of the string field to delete.</param>
		/// <returns>Returns <c>true</c> if the field has been successfully
		/// removed, otherwise the return value is <c>false</c>.</returns>
		/// <exception cref="System.ArgumentNullException">Thrown if the input
		/// parameter is <c>null</c>.</exception>
		public bool Remove(string strField)
		{
			Debug.Assert(strField != null); if(strField == null) throw new ArgumentNullException("strField");

			return m_vDict.Remove(strField);
		}
	}
}

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
		IEnumerable<KeyValuePair<string, string>>, IEquatable<StringDictionaryEx>
	{
		private SortedDictionary<string, string> m_dict =
			new SortedDictionary<string, string>();

		public int Count
		{
			get { return m_dict.Count; }
		}

		public StringDictionaryEx()
		{
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return m_dict.GetEnumerator();
		}

		public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
		{
			return m_dict.GetEnumerator();
		}

		public StringDictionaryEx CloneDeep()
		{
			StringDictionaryEx sdNew = new StringDictionaryEx();

			foreach(KeyValuePair<string, string> kvp in m_dict)
				sdNew.m_dict[kvp.Key] = kvp.Value; // Strings are immutable

			return sdNew;
		}

		public bool Equals(StringDictionaryEx sdOther)
		{
			if(sdOther == null) { Debug.Assert(false); return false; }

			if(m_dict.Count != sdOther.m_dict.Count) return false;

			foreach(KeyValuePair<string, string> kvp in sdOther.m_dict)
			{
				string str = Get(kvp.Key);
				if((str == null) || (str != kvp.Value)) return false;
			}

			return true;
		}

		public string Get(string strName)
		{
			if(strName == null) { Debug.Assert(false); throw new ArgumentNullException("strName"); }

			string s;
			if(m_dict.TryGetValue(strName, out s)) return s;
			return null;
		}

		public bool Exists(string strName)
		{
			if(strName == null) { Debug.Assert(false); throw new ArgumentNullException("strName"); }

			return m_dict.ContainsKey(strName);
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
			if(strField == null) { Debug.Assert(false); throw new ArgumentNullException("strField"); }
			if(strNewValue == null) { Debug.Assert(false); throw new ArgumentNullException("strNewValue"); }

			m_dict[strField] = strNewValue;
		}

		/// <summary>
		/// Delete a string.
		/// </summary>
		/// <param name="strField">Name of the string field to delete.</param>
		/// <returns>Returns <c>true</c>, if the field has been successfully
		/// removed. Otherwise, the return value is <c>false</c>.</returns>
		/// <exception cref="System.ArgumentNullException">Thrown if the input
		/// parameter is <c>null</c>.</exception>
		public bool Remove(string strField)
		{
			if(strField == null) { Debug.Assert(false); throw new ArgumentNullException("strField"); }

			return m_dict.Remove(strField);
		}
	}
}

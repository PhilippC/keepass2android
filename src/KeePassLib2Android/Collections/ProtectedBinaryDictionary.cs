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
using KeePassLib.Security;

#if KeePassLibSD
using KeePassLibSD;
#endif

namespace KeePassLib.Collections
{
	/// <summary>
	/// A list of <c>ProtectedBinary</c> objects (dictionary).
	/// </summary>
	public sealed class ProtectedBinaryDictionary :
		IDeepCloneable<ProtectedBinaryDictionary>,
		IEnumerable<KeyValuePair<string, ProtectedBinary>>
	{
		private SortedDictionary<string, ProtectedBinary> m_vBinaries =
			new SortedDictionary<string, ProtectedBinary>();

		/// <summary>
		/// Get the number of binaries in this entry.
		/// </summary>
		public uint UCount
		{
			get { return (uint)m_vBinaries.Count; }
		}

		/// <summary>
		/// Construct a new list of protected binaries.
		/// </summary>
		public ProtectedBinaryDictionary()
		{
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return m_vBinaries.GetEnumerator();
		}

		public IEnumerator<KeyValuePair<string, ProtectedBinary>> GetEnumerator()
		{
			return m_vBinaries.GetEnumerator();
		}

		public void Clear()
		{
			m_vBinaries.Clear();
		}

		/// <summary>
		/// Clone the current <c>ProtectedBinaryList</c> object, including all
		/// stored protected strings.
		/// </summary>
		/// <returns>New <c>ProtectedBinaryList</c> object.</returns>
		public ProtectedBinaryDictionary CloneDeep()
		{
			ProtectedBinaryDictionary plNew = new ProtectedBinaryDictionary();

			foreach(KeyValuePair<string, ProtectedBinary> kvpBin in m_vBinaries)
			{
				// ProtectedBinary objects are immutable
				plNew.Set(kvpBin.Key, kvpBin.Value);
			}

			return plNew;
		}

		public bool EqualsDictionary(ProtectedBinaryDictionary dict)
		{
			if(dict == null) { Debug.Assert(false); return false; }

			if(m_vBinaries.Count != dict.m_vBinaries.Count) return false;

			foreach(KeyValuePair<string, ProtectedBinary> kvp in m_vBinaries)
			{
				ProtectedBinary pb = dict.Get(kvp.Key);
				if(pb == null) return false;
				if(!pb.Equals(kvp.Value)) return false;
			}

			return true;
		}

		/// <summary>
		/// Get one of the stored binaries.
		/// </summary>
		/// <param name="strName">Binary identifier.</param>
		/// <returns>Protected binary. If the binary identified by
		/// <paramref name="strName" /> cannot be found, the function
		/// returns <c>null</c>.</returns>
		/// <exception cref="System.ArgumentNullException">Thrown if the input
		/// parameter is <c>null</c>.</exception>
		public ProtectedBinary Get(string strName)
		{
			Debug.Assert(strName != null); if(strName == null) throw new ArgumentNullException("strName");

			ProtectedBinary pb;
			if(m_vBinaries.TryGetValue(strName, out pb)) return pb;

			return null;
		}

		/// <summary>
		/// Set a binary object.
		/// </summary>
		/// <param name="strField">Identifier of the binary field to modify.</param>
		/// <param name="pbNewValue">New value. This parameter must not be <c>null</c>.</param>
		/// <exception cref="System.ArgumentNullException">Thrown if any of the input
		/// parameters is <c>null</c>.</exception>
		public void Set(string strField, ProtectedBinary pbNewValue)
		{
			Debug.Assert(strField != null); if(strField == null) throw new ArgumentNullException("strField");
			Debug.Assert(pbNewValue != null); if(pbNewValue == null) throw new ArgumentNullException("pbNewValue");

			m_vBinaries[strField] = pbNewValue;
		}

		/// <summary>
		/// Remove a binary object.
		/// </summary>
		/// <param name="strField">Identifier of the binary field to remove.</param>
		/// <returns>Returns <c>true</c> if the object has been successfully
		/// removed, otherwise <c>false</c>.</returns>
		/// <exception cref="System.ArgumentNullException">Thrown if the input parameter
		/// is <c>null</c>.</exception>
		public bool Remove(string strField)
		{
			Debug.Assert(strField != null); if(strField == null) throw new ArgumentNullException("strField");

			return m_vBinaries.Remove(strField);
		}

		public string KeysToString()
		{
			if(m_vBinaries.Count == 0) return string.Empty;

			StringBuilder sb = new StringBuilder();
			foreach(KeyValuePair<string, ProtectedBinary> kvp in m_vBinaries)
			{
				if(sb.Length > 0) sb.Append(", ");
				sb.Append(kvp.Key);
			}

			return sb.ToString();
		}
	}
}

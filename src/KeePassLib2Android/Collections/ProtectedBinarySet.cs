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
using System.Diagnostics;
using System.Text;

using KeePassLib.Delegates;
using KeePassLib.Security;

namespace KeePassLib.Collections
{
	internal sealed class ProtectedBinarySet : IEnumerable<KeyValuePair<int, ProtectedBinary>>
	{
		private Dictionary<int, ProtectedBinary> m_d =
			new Dictionary<int, ProtectedBinary>();

		public ProtectedBinarySet()
		{
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return m_d.GetEnumerator();
		}

		public IEnumerator<KeyValuePair<int, ProtectedBinary>> GetEnumerator()
		{
			return m_d.GetEnumerator();
		}

		public void Clear()
		{
			m_d.Clear();
		}

		private int GetFreeID()
		{
			int i = m_d.Count;
			while(m_d.ContainsKey(i)) { ++i; }
			Debug.Assert(i == m_d.Count); // m_d.Count should be free
			return i;
		}

		public ProtectedBinary Get(int iID)
		{
			ProtectedBinary pb;
			if(m_d.TryGetValue(iID, out pb)) return pb;

			// Debug.Assert(false); // No assert
			return null;
		}

		public int Find(ProtectedBinary pb)
		{
			if(pb == null) { Debug.Assert(false); return -1; }

			// Fast search by reference
			foreach(KeyValuePair<int, ProtectedBinary> kvp in m_d)
			{
				if(object.ReferenceEquals(pb, kvp.Value))
				{
					Debug.Assert(pb.Equals(kvp.Value));
					return kvp.Key;
				}
			}

			// Slow search by content
			foreach(KeyValuePair<int, ProtectedBinary> kvp in m_d)
			{
				if(pb.Equals(kvp.Value)) return kvp.Key;
			}

			// Debug.Assert(false); // No assert
			return -1;
		}

		public void Set(int iID, ProtectedBinary pb)
		{
			if(iID < 0) { Debug.Assert(false); return; }
			if(pb == null) { Debug.Assert(false); return; }

			m_d[iID] = pb;
		}

		public void Add(ProtectedBinary pb)
		{
			if(pb == null) { Debug.Assert(false); return; }

			int i = Find(pb);
			if(i >= 0) return; // Exists already

			i = GetFreeID();
			m_d[i] = pb;
		}

		public void AddFrom(ProtectedBinaryDictionary d)
		{
			if(d == null) { Debug.Assert(false); return; }

			foreach(KeyValuePair<string, ProtectedBinary> kvp in d)
			{
				Add(kvp.Value);
			}
		}

		public void AddFrom(PwGroup pg)
		{
			if(pg == null) { Debug.Assert(false); return; }

			EntryHandler eh = delegate(PwEntry pe)
			{
				if(pe == null) { Debug.Assert(false); return true; }

				AddFrom(pe.Binaries);
				foreach(PwEntry peHistory in pe.History)
				{
					if(peHistory == null) { Debug.Assert(false); continue; }
					AddFrom(peHistory.Binaries);
				}

				return true;
			};

			pg.TraverseTree(TraversalMethod.PreOrder, null, eh);
		}

		public ProtectedBinary[] ToArray()
		{
			int n = m_d.Count;
			ProtectedBinary[] v = new ProtectedBinary[n];

			foreach(KeyValuePair<int, ProtectedBinary> kvp in m_d)
			{
				if((kvp.Key < 0) || (kvp.Key >= n))
				{
					Debug.Assert(false);
					throw new InvalidOperationException();
				}

				v[kvp.Key] = kvp.Value;
			}

			for(int i = 0; i < n; ++i)
			{
				if(v[i] == null)
				{
					Debug.Assert(false);
					throw new InvalidOperationException();
				}
			}

			return v;
		}
	}
}

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

using KeePassLib.Delegates;
using KeePassLib.Interfaces;

#if KeePassLibSD
using KeePassLibSD;
#endif

namespace KeePassLib.Collections
{
	public sealed class PwObjectPool
	{
		private SortedDictionary<PwUuid, IStructureItem> m_dict =
			new SortedDictionary<PwUuid, IStructureItem>();

		public static PwObjectPool FromGroupRecursive(PwGroup pgRoot, bool bEntries)
		{
			if(pgRoot == null) throw new ArgumentNullException("pgRoot");

			PwObjectPool p = new PwObjectPool();

			if(!bEntries) p.m_dict[pgRoot.Uuid] = pgRoot;
			GroupHandler gh = delegate(PwGroup pg)
			{
				p.m_dict[pg.Uuid] = pg;
				return true;
			};

			EntryHandler eh = delegate(PwEntry pe)
			{
				p.m_dict[pe.Uuid] = pe;
				return true;
			};

			pgRoot.TraverseTree(TraversalMethod.PreOrder, bEntries ? null : gh,
				bEntries ? eh : null);
			return p;
		}

		public IStructureItem Get(PwUuid pwUuid)
		{
			IStructureItem pItem;
			m_dict.TryGetValue(pwUuid, out pItem);
			return pItem;
		}

		public bool ContainsOnlyType(Type t)
		{
			foreach(KeyValuePair<PwUuid, IStructureItem> kvp in m_dict)
			{
				if(kvp.Value.GetType() != t) return false;
			}

			return true;
		}
	}

	internal sealed class PwObjectPoolEx
	{
		private Dictionary<PwUuid, ulong> m_dUuidToId =
			new Dictionary<PwUuid, ulong>();
		private Dictionary<ulong, IStructureItem> m_dIdToItem =
			new Dictionary<ulong, IStructureItem>();

		private PwObjectPoolEx()
		{
		}

		public static PwObjectPoolEx FromGroup(PwGroup pg)
		{
			PwObjectPoolEx p = new PwObjectPoolEx();

			if(pg == null) { Debug.Assert(false); return p; }

			ulong uFreeId = 2; // 0 = "not found", 1 is a hole

			p.m_dUuidToId[pg.Uuid] = uFreeId;
			p.m_dIdToItem[uFreeId] = pg;
			uFreeId += 2; // Make hole

			p.AddGroupRec(pg, ref uFreeId);
			return p;
		}

		private void AddGroupRec(PwGroup pg, ref ulong uFreeId)
		{
			if(pg == null) { Debug.Assert(false); return; }

			ulong uId = uFreeId;

			// Consecutive entries must have consecutive IDs
			foreach(PwEntry pe in pg.Entries)
			{
				Debug.Assert(!m_dUuidToId.ContainsKey(pe.Uuid));
				Debug.Assert(!m_dIdToItem.ContainsValue(pe));

				m_dUuidToId[pe.Uuid] = uId;
				m_dIdToItem[uId] = pe;
				++uId;
			}
			++uId; // Make hole

			// Consecutive groups must have consecutive IDs
			foreach(PwGroup pgSub in pg.Groups)
			{
				Debug.Assert(!m_dUuidToId.ContainsKey(pgSub.Uuid));
				Debug.Assert(!m_dIdToItem.ContainsValue(pgSub));

				m_dUuidToId[pgSub.Uuid] = uId;
				m_dIdToItem[uId] = pgSub;
				++uId;
			}
			++uId; // Make hole

			foreach(PwGroup pgSub in pg.Groups)
			{
				AddGroupRec(pgSub, ref uId);
			}

			uFreeId = uId;
		}

		public ulong GetIdByUuid(PwUuid pwUuid)
		{
			if(pwUuid == null) { Debug.Assert(false); return 0; }

			ulong uId;
			m_dUuidToId.TryGetValue(pwUuid, out uId);
			return uId;
		}

		public IStructureItem GetItemByUuid(PwUuid pwUuid)
		{
			if(pwUuid == null) { Debug.Assert(false); return null; }

			ulong uId;
			if(!m_dUuidToId.TryGetValue(pwUuid, out uId)) return null;
			Debug.Assert(uId != 0);

			return GetItemById(uId);
		}

		public IStructureItem GetItemById(ulong uId)
		{
			IStructureItem p;
			m_dIdToItem.TryGetValue(uId, out p);
			return p;
		}
	}

	internal sealed class PwObjectBlock<T> : IEnumerable<T>
		where T : class, ITimeLogger, IStructureItem, IDeepCloneable<T>
	{
		private List<T> m_l = new List<T>();

		public T PrimaryItem
		{
			get { return ((m_l.Count > 0) ? m_l[0] : null); }
		}

		private DateTime m_dtLocationChanged = DateTime.MinValue;
		public DateTime LocationChanged
		{
			get { return m_dtLocationChanged; }
		}

		private PwObjectPoolEx m_poolAssoc = null;
		public PwObjectPoolEx PoolAssoc
		{
			get { return m_poolAssoc; }
		}

		public PwObjectBlock()
		{
		}

#if DEBUG
		public override string ToString()
		{
			return ("PwObjectBlock, Count = " + m_l.Count.ToString());
		}
#endif

		IEnumerator IEnumerable.GetEnumerator()
		{
			return m_l.GetEnumerator();
		}

		public IEnumerator<T> GetEnumerator()
		{
			return m_l.GetEnumerator();
		}

		public void Add(T t, DateTime dtLoc, PwObjectPoolEx pool)
		{
			if(t == null) { Debug.Assert(false); return; }

			m_l.Add(t);

			if(dtLoc > m_dtLocationChanged)
			{
				m_dtLocationChanged = dtLoc;
				m_poolAssoc = pool;
			}
		}
	}
}

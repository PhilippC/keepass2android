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
		private SortedDictionary<PwUuidComparable, IStructureItem> m_dict =
			new SortedDictionary<PwUuidComparable, IStructureItem>();

		public static PwObjectPool FromGroupRecursive(PwGroup pgRoot, bool bEntries)
		{
			if(pgRoot == null) throw new ArgumentNullException("pgRoot");

			PwObjectPool p = new PwObjectPool();

			if(!bEntries) p.m_dict[new PwUuidComparable(pgRoot.Uuid)] = pgRoot;
			GroupHandler gh = delegate(PwGroup pg)
			{
				p.m_dict[new PwUuidComparable(pg.Uuid)] = pg;
				return true;
			};

			EntryHandler eh = delegate(PwEntry pe)
			{
				p.m_dict[new PwUuidComparable(pe.Uuid)] = pe;
				return true;
			};

			pgRoot.TraverseTree(TraversalMethod.PreOrder, bEntries ? null : gh,
				bEntries ? eh : null);
			return p;
		}

		public IStructureItem Get(PwUuid pwUuid)
		{
			IStructureItem pItem;
			m_dict.TryGetValue(new PwUuidComparable(pwUuid), out pItem);
			return pItem;
		}

		public bool ContainsOnlyType(Type t)
		{
			foreach(KeyValuePair<PwUuidComparable, IStructureItem> kvp in m_dict)
			{
				if(kvp.Value.GetType() != t) return false;
			}

			return true;
		}
	}
}

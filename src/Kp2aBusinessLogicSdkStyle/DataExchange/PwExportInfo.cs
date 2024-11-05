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

using KeePassLib;

namespace KeePass.DataExchange
{
	public sealed class PwExportInfo
	{
		private PwGroup m_pg;
		/// <summary>
		/// This group contains all entries and subgroups that should
		/// be exported. Is never <c>null</c>.
		/// </summary>
		public PwGroup DataGroup
		{
			get { return m_pg; }
		}

		private PwDatabase m_pd;
		/// <summary>
		/// Optional context database reference. May be <c>null</c>.
		/// </summary>
		public PwDatabase ContextDatabase
		{
			get { return m_pd; }
		}

		private bool m_bExpDel = true;
		/// <summary>
		/// Indicates whether deleted objects should be exported, if
		/// the data format supports it.
		/// </summary>
		public bool ExportDeletedObjects
		{
			get { return m_bExpDel; }
		}

		public PwExportInfo(PwGroup pgDataSource, PwDatabase pwContextInfo)
		{
			ConstructEx(pgDataSource, pwContextInfo, null);
		}

		public PwExportInfo(PwGroup pgDataSource, PwDatabase pwContextInfo,
			bool bExportDeleted)
		{
			ConstructEx(pgDataSource, pwContextInfo, bExportDeleted);
		}

		private void ConstructEx(PwGroup pgDataSource, PwDatabase pwContextInfo,
			bool? bExportDeleted)
		{
			if(pgDataSource == null) throw new ArgumentNullException("pgDataSource");
			// pwContextInfo may be null.

			m_pg = pgDataSource;
			m_pd = pwContextInfo;

			if(bExportDeleted.HasValue) m_bExpDel = bExportDeleted.Value;
		}
	}
}

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
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace KeePassLib.Keys
{
	public sealed class KeyProviderPool : IEnumerable<KeyProvider>
	{
		private List<KeyProvider> m_vProviders = new List<KeyProvider>();

		public int Count
		{
			get { return m_vProviders.Count; }
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return m_vProviders.GetEnumerator();
		}

		public IEnumerator<KeyProvider> GetEnumerator()
		{
			return m_vProviders.GetEnumerator();
		}

		public void Add(KeyProvider prov)
		{
			Debug.Assert(prov != null); if(prov == null) throw new ArgumentNullException("prov");

			m_vProviders.Add(prov);
		}

		public bool Remove(KeyProvider prov)
		{
			Debug.Assert(prov != null); if(prov == null) throw new ArgumentNullException("prov");

			return m_vProviders.Remove(prov);
		}

		public KeyProvider Get(string strProviderName)
		{
			if(strProviderName == null) throw new ArgumentNullException("strProviderName");

			foreach(KeyProvider prov in m_vProviders)
			{
				if(prov.Name == strProviderName) return prov;
			}

			return null;
		}

		public bool IsKeyProvider(string strName)
		{
			Debug.Assert(strName != null); if(strName == null) throw new ArgumentNullException("strName");

			foreach(KeyProvider prov in m_vProviders)
			{
				if(prov.Name == strName) return true;
			}

			return false;
		}

		internal byte[] GetKey(string strProviderName, KeyProviderQueryContext ctx,
			out bool bPerformHash)
		{
			Debug.Assert(strProviderName != null); if(strProviderName == null) throw new ArgumentNullException("strProviderName");

			bPerformHash = true;

			foreach(KeyProvider prov in m_vProviders)
			{
				if(prov.Name == strProviderName)
				{
					bPerformHash = !prov.DirectKey;
					return prov.GetKey(ctx);
				}
			}

			Debug.Assert(false);
			return null;
		}
	}
}

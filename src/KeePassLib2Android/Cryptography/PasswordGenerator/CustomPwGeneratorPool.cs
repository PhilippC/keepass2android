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

namespace KeePassLib.Cryptography.PasswordGenerator
{
	public sealed class CustomPwGeneratorPool : IEnumerable<CustomPwGenerator>
	{
		private List<CustomPwGenerator> m_vGens = new List<CustomPwGenerator>();

		public int Count
		{
			get { return m_vGens.Count; }
		}

		public CustomPwGeneratorPool()
		{
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return m_vGens.GetEnumerator();
		}

		public IEnumerator<CustomPwGenerator> GetEnumerator()
		{
			return m_vGens.GetEnumerator();
		}

		public void Add(CustomPwGenerator pwg)
		{
			if(pwg == null) throw new ArgumentNullException("pwg");

			PwUuid uuid = pwg.Uuid;
			if(uuid == null) throw new ArgumentException();

			int nIndex = FindIndex(uuid);

			if(nIndex >= 0) m_vGens[nIndex] = pwg; // Replace
			else m_vGens.Add(pwg);
		}

		public CustomPwGenerator Find(PwUuid uuid)
		{
			if(uuid == null) throw new ArgumentNullException("uuid");

			foreach(CustomPwGenerator pwg in m_vGens)
			{
				if(uuid.Equals(pwg.Uuid)) return pwg;
			}

			return null;
		}

		public CustomPwGenerator Find(string strName)
		{
			if(strName == null) throw new ArgumentNullException("strName");

			foreach(CustomPwGenerator pwg in m_vGens)
			{
				if(pwg.Name == strName) return pwg;
			}

			return null;
		}

		private int FindIndex(PwUuid uuid)
		{
			if(uuid == null) throw new ArgumentNullException("uuid");

			for(int i = 0; i < m_vGens.Count; ++i)
			{
				if(uuid.Equals(m_vGens[i].Uuid)) return i;
			}

			return -1;
		}

		public bool Remove(PwUuid uuid)
		{
			if(uuid == null) throw new ArgumentNullException("uuid");

			int nIndex = FindIndex(uuid);
			if(nIndex < 0) return false;

			m_vGens.RemoveAt(nIndex);
			return true;
		}
	}
}

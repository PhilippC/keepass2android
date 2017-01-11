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
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using KeePassLib.Utility;

namespace KeePassLib.Cryptography.KeyDerivation
{
	public static class KdfPool
	{
		private static List<KdfEngine> g_l = new List<KdfEngine>();

		public static IEnumerable<KdfEngine> Engines
		{
			get
			{
				EnsureInitialized();
				return g_l;
			}
		}

		private static void EnsureInitialized()
		{
			if(g_l.Count > 0) return;

			g_l.Add(new AesKdf());
			g_l.Add(new Argon2Kdf());
		}

		internal static KdfParameters GetDefaultParameters()
		{
			EnsureInitialized();
			return g_l[0].GetDefaultParameters();
		}

		public static KdfEngine Get(PwUuid pu)
		{
			if(pu == null) { Debug.Assert(false); return null; }

			EnsureInitialized();

			foreach(KdfEngine kdf in g_l)
			{
				if(pu.Equals(kdf.Uuid)) return kdf;
			}

			return null;
		}

		public static KdfEngine Get(string strName)
		{
			if(string.IsNullOrEmpty(strName)) { Debug.Assert(false); return null; }

			EnsureInitialized();

			foreach(KdfEngine kdf in g_l)
			{
				if(strName.Equals(kdf.Name, StrUtil.CaseIgnoreCmp)) return kdf;
			}

			return null;
		}

		public static void Add(KdfEngine kdf)
		{
			if(kdf == null) { Debug.Assert(false); return; }

			EnsureInitialized();

			if(Get(kdf.Uuid) != null) { Debug.Assert(false); return; }
			if(Get(kdf.Name) != null) { Debug.Assert(false); return; }

			g_l.Add(kdf);
		}
	}
}

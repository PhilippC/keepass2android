/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2020 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.IO;
using System.Text;

using KeePassLib.Collections;
using KeePassLib.Utility;

namespace KeePassLib.Cryptography.KeyDerivation
{
	public sealed class KdfParameters : VariantDictionary
	{
		private const string ParamUuid = @"$UUID";

		private readonly PwUuid m_puKdf;
		public PwUuid KdfUuid
		{
			get { return m_puKdf; }
		}

		public KdfParameters(PwUuid puKdf)
		{
			if(puKdf == null) throw new ArgumentNullException("puKdf");

			m_puKdf = puKdf;
			SetByteArray(ParamUuid, puKdf.UuidBytes);
		}

		/// <summary>
		/// Unsupported.
		/// </summary>
		public override object Clone()
		{
			throw new NotSupportedException();
		}

		public static byte[] SerializeExt(KdfParameters p)
		{
			return VariantDictionary.Serialize(p);
		}

		public static KdfParameters DeserializeExt(byte[] pb)
		{
			VariantDictionary d = VariantDictionary.Deserialize(pb);
			if(d == null) { Debug.Assert(false); return null; }

			byte[] pbUuid = d.GetByteArray(ParamUuid);
			if((pbUuid == null) || (pbUuid.Length != (int)PwUuid.UuidSize))
			{
				Debug.Assert(false);
				return null;
			}

			PwUuid pu = new PwUuid(pbUuid);
			KdfParameters p = new KdfParameters(pu);
			d.CopyTo(p);
			return p;
		}
	}
}

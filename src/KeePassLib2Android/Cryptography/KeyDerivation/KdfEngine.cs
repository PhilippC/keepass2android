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

namespace KeePassLib.Cryptography.KeyDerivation
{
	public abstract class KdfEngine
	{
		public abstract PwUuid Uuid
		{
			get;
		}

		public abstract string Name
		{
			get;
		}

		public virtual KdfParameters GetDefaultParameters()
		{
			return new KdfParameters(this.Uuid);
		}

		/// <summary>
		/// Generate random seeds and store them in <paramref name="p" />.
		/// </summary>
		public virtual void Randomize(KdfParameters p)
		{
			Debug.Assert(p != null);
			Debug.Assert(p.KdfUuid.Equals(this.Uuid));
		}

		public abstract byte[] Transform(byte[] pbMsg, KdfParameters p);

		public virtual KdfParameters GetBestParameters(uint uMilliseconds)
		{
			throw new NotImplementedException();
		}

		protected void MaximizeParamUInt64(KdfParameters p, string strName,
			ulong uMin, ulong uMax, uint uMilliseconds, bool bInterpSearch)
		{
			if(p == null) { Debug.Assert(false); return; }
			if(string.IsNullOrEmpty(strName)) { Debug.Assert(false); return; }
			if(uMin > uMax) { Debug.Assert(false); return; }

			if(uMax > (ulong.MaxValue >> 1))
			{
				Debug.Assert(false);
				uMax = ulong.MaxValue >> 1;

				if(uMin > uMax) { p.SetUInt64(strName, uMin); return; }
			}

			byte[] pbMsg = new byte[32];
			for(int i = 0; i < pbMsg.Length; ++i) pbMsg[i] = (byte)i;

			ulong uLow = uMin;
			ulong uHigh = uMin + 1UL;
			long tLow = 0;
			long tHigh = 0;
			long tTarget = (long)uMilliseconds;

			// Determine range
			while(uHigh <= uMax)
			{
				p.SetUInt64(strName, uHigh);

				// GC.Collect();
				Stopwatch sw = Stopwatch.StartNew();
				Transform(pbMsg, p);
				sw.Stop();

				tHigh = sw.ElapsedMilliseconds;
				if(tHigh > tTarget) break;

				uLow = uHigh;
				tLow = tHigh;
				uHigh <<= 1;
			}
			if(uHigh > uMax) { uHigh = uMax; tHigh = 0; }
			if(uLow > uHigh) uLow = uHigh; // Skips to end

			// Find optimal number of iterations
			while((uHigh - uLow) >= 2UL)
			{
				ulong u = (uHigh + uLow) >> 1; // Binary search
				// Interpolation search, if possible
				if(bInterpSearch && (tLow > 0) && (tHigh > tTarget) &&
					(tLow <= tTarget))
				{
					u = uLow + (((uHigh - uLow) * (ulong)(tTarget - tLow)) /
						(ulong)(tHigh - tLow));
					if((u >= uLow) && (u <= uHigh))
					{
						u = Math.Max(u, uLow + 1UL);
						u = Math.Min(u, uHigh - 1UL);
					}
					else
					{
						Debug.Assert(false);
						u = (uHigh + uLow) >> 1;
					}
				}

				p.SetUInt64(strName, u);

				// GC.Collect();
				Stopwatch sw = Stopwatch.StartNew();
				Transform(pbMsg, p);
				sw.Stop();

				long t = sw.ElapsedMilliseconds;
				if(t == tTarget) { uLow = u; break; }
				else if(t > tTarget) { uHigh = u; tHigh = t; }
				else { uLow = u; tLow = t; }
			}

			p.SetUInt64(strName, uLow);
		}
	}
}

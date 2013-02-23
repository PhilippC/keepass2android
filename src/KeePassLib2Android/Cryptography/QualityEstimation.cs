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
using System.Diagnostics;
using System.Text;

using KeePassLib.Utility;

namespace KeePassLib.Cryptography
{
	/// <summary>
	/// A class that offers static functions to estimate the quality of
	/// passwords.
	/// </summary>
	public static class QualityEstimation
	{
		private enum CharSpaceBits : uint
		{
			Control = 32,
			Alpha = 26,
			Number = 10,
			Special = 33,
			High = 112
		}

		/// <summary>
		/// Estimate the quality of a password.
		/// </summary>
		/// <param name="vPasswordChars">Password to check.</param>
		/// <returns>Estimated bit-strength of the password.</returns>
		/// <exception cref="System.ArgumentNullException">Thrown if the input
		/// parameter is <c>null</c>.</exception>
		public static uint EstimatePasswordBits(char[] vPasswordChars)
		{
			Debug.Assert(vPasswordChars != null);
			if(vPasswordChars == null) throw new ArgumentNullException("vPasswordChars");

			bool bChLower = false, bChUpper = false, bChNumber = false;
			bool bChSpecial = false, bChHigh = false, bChControl = false;
			Dictionary<char, uint> vCharCounts = new Dictionary<char, uint>();
			Dictionary<int, uint> vDifferences = new Dictionary<int, uint>();
			double dblEffectiveLength = 0.0;

			for(int i = 0; i < vPasswordChars.Length; ++i) // Get character types
			{
				char tch = vPasswordChars[i];

				if(tch < ' ') bChControl = true;
				else if((tch >= 'A') && (tch <= 'Z')) bChUpper = true;
				else if((tch >= 'a') && (tch <= 'z')) bChLower = true;
				else if((tch >= '0') && (tch <= '9')) bChNumber = true;
				else if((tch >= ' ') && (tch <= '/')) bChSpecial = true;
				else if((tch >= ':') && (tch <= '@')) bChSpecial = true;
				else if((tch >= '[') && (tch <= '`')) bChSpecial = true;
				else if((tch >= '{') && (tch <= '~')) bChSpecial = true;
				else if(tch > '~') bChHigh = true;

				double dblDiffFactor = 1.0;
				if(i >= 1)
				{
					int iDiff = (int)tch - (int)vPasswordChars[i - 1];

					uint uDiffCount;
					if(vDifferences.TryGetValue(iDiff, out uDiffCount))
					{
						++uDiffCount;
						vDifferences[iDiff] = uDiffCount;
						dblDiffFactor /= (double)uDiffCount;
					}
					else vDifferences.Add(iDiff, 1);
				}

				uint uCharCount;
				if(vCharCounts.TryGetValue(tch, out uCharCount))
				{
					++uCharCount;
					vCharCounts[tch] = uCharCount;
					dblEffectiveLength += dblDiffFactor * (1.0 / (double)uCharCount);
				}
				else
				{
					vCharCounts.Add(tch, 1);
					dblEffectiveLength += dblDiffFactor;
				}
			}

			uint uCharSpace = 0;
			if(bChControl) uCharSpace += (uint)CharSpaceBits.Control;
			if(bChUpper) uCharSpace += (uint)CharSpaceBits.Alpha;
			if(bChLower) uCharSpace += (uint)CharSpaceBits.Alpha;
			if(bChNumber) uCharSpace += (uint)CharSpaceBits.Number;
			if(bChSpecial) uCharSpace += (uint)CharSpaceBits.Special;
			if(bChHigh) uCharSpace += (uint)CharSpaceBits.High;

			if(uCharSpace == 0) return 0;

			double dblBitsPerChar = Math.Log((double)uCharSpace) / Math.Log(2.0);
			double dblRating = dblBitsPerChar * dblEffectiveLength;

#if !KeePassLibSD
			char[] vLowerCopy = new char[vPasswordChars.Length];
			for(int ilc = 0; ilc < vLowerCopy.Length; ++ilc)
				vLowerCopy[ilc] = char.ToLower(vPasswordChars[ilc]);
			if(PopularPasswords.IsPopularPassword(vLowerCopy)) dblRating /= 8.0;
			Array.Clear(vLowerCopy, 0, vLowerCopy.Length);
#endif

			return (uint)Math.Ceiling(dblRating);
		}

		/// <summary>
		/// Estimate the quality of a password.
		/// </summary>
		/// <param name="pbUnprotectedUtf8">Password to check, UTF-8 encoded.</param>
		/// <returns>Estimated bit-strength of the password.</returns>
		public static uint EstimatePasswordBits(byte[] pbUnprotectedUtf8)
		{
			if(pbUnprotectedUtf8 == null) { Debug.Assert(false); return 0; }

			char[] vChars = StrUtil.Utf8.GetChars(pbUnprotectedUtf8);
			uint uResult = EstimatePasswordBits(vChars);
			Array.Clear(vChars, 0, vChars.Length);

			return uResult;
		}
	}
}

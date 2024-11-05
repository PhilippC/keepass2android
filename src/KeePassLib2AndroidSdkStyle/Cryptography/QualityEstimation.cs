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
using System.Text;
using System.Diagnostics;

using KeePassLib.Cryptography.PasswordGenerator;
using KeePassLib.Utility;

namespace KeePassLib.Cryptography
{
	/// <summary>
	/// A class that offers static functions to estimate the quality of
	/// passwords.
	/// </summary>
	public static class QualityEstimation
	{
		private static class PatternID
		{
			public const char LowerAlpha = 'L';
			public const char UpperAlpha = 'U';
			public const char Digit = 'D';
			public const char Special = 'S';
			public const char High = 'H';
			public const char Other = 'X';

			public const char Dictionary = 'W';
			public const char Repetition = 'R';
			public const char Number = 'N';
			public const char DiffSeq = 'C';

			public const string All = "LUDSHXWRNC";
		}

		// private static class CharDistrib
		// {
		//	public static readonly ulong[] LowerAlpha = new ulong[26] {
		//		884, 211, 262, 249, 722, 98, 172, 234, 556, 124, 201, 447, 321,
		//		483, 518, 167, 18, 458, 416, 344, 231, 105, 80, 48, 238, 76
		//	};
		//	public static readonly ulong[] UpperAlpha = new ulong[26] {
		//		605, 188, 209, 200, 460, 81, 130, 163, 357, 122, 144, 332, 260,
		//		317, 330, 132, 18, 320, 315, 250, 137, 76, 60, 36, 161, 54
		//	};
		//	public static readonly ulong[] Digit = new ulong[10] {
		//		574, 673, 524, 377, 339, 336, 312, 310, 357, 386
		//	};
		// }

		private sealed class QeCharType
		{
			private readonly char m_chTypeID;
			public char TypeID { get { return m_chTypeID; } }

			private readonly string m_strAlph;
			public string Alphabet { get { return m_strAlph; } }

			private readonly int m_nChars;
			public int CharCount { get { return m_nChars; } }

			private readonly char m_chFirst;
			private readonly char m_chLast;

			private readonly double m_dblCharSize;
			public double CharSize { get { return m_dblCharSize; } }

			public QeCharType(char chTypeID, string strAlphabet, bool bIsConsecutive)
			{
				if(strAlphabet == null) throw new ArgumentNullException();
				if(strAlphabet.Length == 0) throw new ArgumentException();

				m_chTypeID = chTypeID;
				m_strAlph = strAlphabet;
				m_nChars = m_strAlph.Length;
				m_chFirst = (bIsConsecutive ? m_strAlph[0] : char.MinValue);
				m_chLast = (bIsConsecutive ? m_strAlph[m_nChars - 1] : char.MinValue);

				m_dblCharSize = Log2(m_nChars);

				Debug.Assert(((int)(m_chLast - m_chFirst) == (m_nChars - 1)) ||
					!bIsConsecutive);
			}

			public QeCharType(char chTypeID, int nChars) // Catch-none set
			{
				if(nChars <= 0) throw new ArgumentOutOfRangeException();

				m_chTypeID = chTypeID;
				m_strAlph = string.Empty;
				m_nChars = nChars;
				m_chFirst = char.MinValue;
				m_chLast = char.MinValue;

				m_dblCharSize = Log2(m_nChars);
			}

			public bool Contains(char ch)
			{
				if(m_chLast != char.MinValue)
					return ((ch >= m_chFirst) && (ch <= m_chLast));

				Debug.Assert(m_strAlph.Length > 0); // Don't call for catch-none set
				return (m_strAlph.IndexOf(ch) >= 0);
			}
		}

		private sealed class EntropyEncoder
		{
			private readonly string m_strAlph;
			private Dictionary<char, ulong> m_dHisto = new Dictionary<char, ulong>();
			private readonly ulong m_uBaseWeight;
			private readonly ulong m_uCharWeight;
			private readonly ulong m_uOccExclThreshold;

			public EntropyEncoder(string strAlphabet, ulong uBaseWeight,
				ulong uCharWeight, ulong uOccExclThreshold)
			{
				if(strAlphabet == null) throw new ArgumentNullException();
				if(strAlphabet.Length == 0) throw new ArgumentException();

				m_strAlph = strAlphabet;
				m_uBaseWeight = uBaseWeight;
				m_uCharWeight = uCharWeight;
				m_uOccExclThreshold = uOccExclThreshold;

#if DEBUG
				Dictionary<char, bool> d = new Dictionary<char, bool>();
				foreach(char ch in m_strAlph) { d[ch] = true; }
				Debug.Assert(d.Count == m_strAlph.Length); // No duplicates
#endif
			}

			public void Reset()
			{
				m_dHisto.Clear();
			}

			public void Write(char ch)
			{
				Debug.Assert(m_strAlph.IndexOf(ch) >= 0);

				ulong uOcc;
				m_dHisto.TryGetValue(ch, out uOcc);
				Debug.Assert(m_dHisto.ContainsKey(ch) || (uOcc == 0));
				m_dHisto[ch] = uOcc + 1;
			}

			public double GetOutputSize()
			{
				ulong uTotalWeight = m_uBaseWeight * (ulong)m_strAlph.Length;
				foreach(ulong u in m_dHisto.Values)
				{
					Debug.Assert(u >= 1);
					if(u > m_uOccExclThreshold)
						uTotalWeight += (u - m_uOccExclThreshold) * m_uCharWeight;
				}

				double dSize = 0.0, dTotalWeight = (double)uTotalWeight;
				foreach(ulong u in m_dHisto.Values)
				{
					ulong uWeight = m_uBaseWeight;
					if(u > m_uOccExclThreshold)
						uWeight += (u - m_uOccExclThreshold) * m_uCharWeight;

					dSize -= (double)u * Log2((double)uWeight / dTotalWeight);
				}

				return dSize;
			}
		}

		private sealed class MultiEntropyEncoder
		{
			private Dictionary<char, EntropyEncoder> m_dEncs =
				new Dictionary<char, EntropyEncoder>();

			public MultiEntropyEncoder()
			{
			}

			public void AddEncoder(char chTypeID, EntropyEncoder ec)
			{
				if(ec == null) { Debug.Assert(false); return; }

				Debug.Assert(!m_dEncs.ContainsKey(chTypeID));
				m_dEncs[chTypeID] = ec;
			}

			public void Reset()
			{
				foreach(EntropyEncoder ec in m_dEncs.Values) { ec.Reset(); }
			}

			public bool Write(char chTypeID, char chData)
			{
				EntropyEncoder ec;
				if(!m_dEncs.TryGetValue(chTypeID, out ec))
					return false;

				ec.Write(chData);
				return true;
			}

			public double GetOutputSize()
			{
				double d = 0.0;

				foreach(EntropyEncoder ec in m_dEncs.Values)
				{
					d += ec.GetOutputSize();
				}

				return d;
			}
		}

		private sealed class QePatternInstance
		{
			private readonly int m_iPos;
			public int Position { get { return m_iPos; } }

			private readonly int m_nLen;
			public int Length { get { return m_nLen; } }

			private readonly char m_chPatternID;
			public char PatternID { get { return m_chPatternID; } }

			private readonly double m_dblCost;
			public double Cost { get { return m_dblCost; } }

			private readonly QeCharType m_ctSingle;
			public QeCharType SingleCharType { get { return m_ctSingle; } }

			public QePatternInstance(int iPosition, int nLength, char chPatternID,
				double dblCost)
			{
				m_iPos = iPosition;
				m_nLen = nLength;
				m_chPatternID = chPatternID;
				m_dblCost = dblCost;
				m_ctSingle = null;
			}

			public QePatternInstance(int iPosition, int nLength, QeCharType ctSingle)
			{
				m_iPos = iPosition;
				m_nLen = nLength;
				m_chPatternID = ctSingle.TypeID;
				m_dblCost = ctSingle.CharSize;
				m_ctSingle = ctSingle;
			}
		}

		private sealed class QePathState
		{
			public readonly int Position;
			public readonly List<QePatternInstance> Path;

			public QePathState(int iPosition, List<QePatternInstance> lPath)
			{
				this.Position = iPosition;
				this.Path = lPath;
			}
		}

		private static object m_objSyncInit = new object();
		private static List<QeCharType> m_lCharTypes = null;

		private static void EnsureInitialized()
		{
			lock(m_objSyncInit)
			{
				if(m_lCharTypes == null)
				{
					string strSpecial = PwCharSet.PrintableAsciiSpecial;
					if(strSpecial.IndexOf(' ') >= 0) { Debug.Assert(false); }
					else strSpecial = strSpecial + " ";

					int nSp = strSpecial.Length;
					int nHi = PwCharSet.HighAnsiChars.Length;

					m_lCharTypes = new List<QeCharType>();

					m_lCharTypes.Add(new QeCharType(PatternID.LowerAlpha,
						PwCharSet.LowerCase, true));
					m_lCharTypes.Add(new QeCharType(PatternID.UpperAlpha,
						PwCharSet.UpperCase, true));
					m_lCharTypes.Add(new QeCharType(PatternID.Digit,
						PwCharSet.Digits, true));
					m_lCharTypes.Add(new QeCharType(PatternID.Special,
						strSpecial, false));
					m_lCharTypes.Add(new QeCharType(PatternID.High,
						PwCharSet.HighAnsiChars, false));
					m_lCharTypes.Add(new QeCharType(PatternID.Other,
						0x10000 - (2 * 26) - 10 - nSp - nHi));
				}
			}
		}

		/// <summary>
		/// Estimate the quality of a password.
		/// </summary>
		/// <param name="vPasswordChars">Password to check.</param>
		/// <returns>Estimated bit-strength of the password.</returns>
		public static uint EstimatePasswordBits(char[] vPasswordChars)
		{
			if(vPasswordChars == null) { Debug.Assert(false); return 0; }
			if(vPasswordChars.Length == 0) return 0;

			EnsureInitialized();

			int n = vPasswordChars.Length;
			List<QePatternInstance>[] vPatterns = new List<QePatternInstance>[n];
			for(int i = 0; i < n; ++i)
			{
				vPatterns[i] = new List<QePatternInstance>();

				QePatternInstance piChar = new QePatternInstance(i, 1,
					GetCharType(vPasswordChars[i]));
				vPatterns[i].Add(piChar);
			}

			FindRepetitions(vPasswordChars, vPatterns);
			FindNumbers(vPasswordChars, vPatterns);
			FindDiffSeqs(vPasswordChars, vPatterns);
			FindPopularPasswords(vPasswordChars, vPatterns);

			// Encoders must not be static, because the entropy estimation
			// may run concurrently in multiple threads and the encoders are
			// not read-only
			EntropyEncoder ecPattern = new EntropyEncoder(PatternID.All, 0, 1, 0);
			MultiEntropyEncoder mcData = new MultiEntropyEncoder();
			for(int i = 0; i < (m_lCharTypes.Count - 1); ++i)
			{
				// Let m be the alphabet size. In order to ensure that two same
				// characters cost at least as much as a single character, for
				// the probability p and weight w of the character it must hold:
				//     -log(1/m) >= -2*log(p)
				// <=> log(1/m) <= log(p^2) <=> 1/m <= p^2 <=> p >= sqrt(1/m);
				//     sqrt(1/m) = (1+w)/(m+w)
				// <=> m+w = (1+w)*sqrt(m) <=> m+w = sqrt(m) + w*sqrt(m)
				// <=> w*(1-sqrt(m)) = sqrt(m) - m <=> w = (sqrt(m)-m)/(1-sqrt(m))
				// <=> w = (sqrt(m)-m)*(1+sqrt(m))/(1-m)
				// <=> w = (sqrt(m)-m+m-m*sqrt(m))/(1-m) <=> w = sqrt(m)
				ulong uw = (ulong)Math.Sqrt((double)m_lCharTypes[i].CharCount);

				mcData.AddEncoder(m_lCharTypes[i].TypeID, new EntropyEncoder(
					m_lCharTypes[i].Alphabet, 1, uw, 1));
			}

			double dblMinCost = (double)int.MaxValue;
			int tStart = Environment.TickCount;

			Stack<QePathState> sRec = new Stack<QePathState>();
			sRec.Push(new QePathState(0, new List<QePatternInstance>()));
			while(sRec.Count > 0)
			{
				int tDiff = Environment.TickCount - tStart;
				if(tDiff > 500) break;

				QePathState s = sRec.Pop();

				if(s.Position >= n)
				{
					Debug.Assert(s.Position == n);

					double dblCost = ComputePathCost(s.Path, vPasswordChars,
						ecPattern, mcData);
					if(dblCost < dblMinCost) dblMinCost = dblCost;
				}
				else
				{
					List<QePatternInstance> lSubs = vPatterns[s.Position];
					for(int i = lSubs.Count - 1; i >= 0; --i)
					{
						QePatternInstance pi = lSubs[i];
						Debug.Assert(pi.Position == s.Position);
						Debug.Assert(pi.Length >= 1);

						List<QePatternInstance> lNewPath =
							new List<QePatternInstance>(s.Path.Count + 1);
						lNewPath.AddRange(s.Path);
						lNewPath.Add(pi);
						Debug.Assert(lNewPath.Capacity == (s.Path.Count + 1));

						QePathState sNew = new QePathState(s.Position +
							pi.Length, lNewPath);
						sRec.Push(sNew);
					}
				}
			}

			return (uint)Math.Ceiling(dblMinCost);
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
			MemUtil.ZeroArray<char>(vChars);

			return uResult;
		}

		private static QeCharType GetCharType(char ch)
		{
			int nTypes = m_lCharTypes.Count;
			Debug.Assert((nTypes > 0) && (m_lCharTypes[nTypes - 1].CharCount > 256));

			for(int i = 0; i < (nTypes - 1); ++i)
			{
				if(m_lCharTypes[i].Contains(ch))
					return m_lCharTypes[i];
			}

			return m_lCharTypes[nTypes - 1];
		}

		private static double ComputePathCost(List<QePatternInstance> l,
			char[] vPassword, EntropyEncoder ecPattern, MultiEntropyEncoder mcData)
		{
			ecPattern.Reset();
			for(int i = 0; i < l.Count; ++i)
				ecPattern.Write(l[i].PatternID);
			double dblPatternCost = ecPattern.GetOutputSize();

			mcData.Reset();
			double dblDataCost = 0.0;
			foreach(QePatternInstance pi in l)
			{
				QeCharType tChar = pi.SingleCharType;
				if(tChar != null)
				{
					char ch = vPassword[pi.Position];
					if(!mcData.Write(tChar.TypeID, ch))
						dblDataCost += pi.Cost;
				}
				else dblDataCost += pi.Cost;
			}
			dblDataCost += mcData.GetOutputSize();

			return (dblPatternCost + dblDataCost);
		}

		private static void FindPopularPasswords(char[] vPassword,
			List<QePatternInstance>[] vPatterns)
		{
			int n = vPassword.Length;

			char[] vLower = new char[n];
			char[] vLeet = new char[n];
			for(int i = 0; i < n; ++i)
			{
				char ch = vPassword[i];

				vLower[i] = char.ToLower(ch);
				vLeet[i] = char.ToLower(DecodeLeetChar(ch));
			}

			char chErased = default(char);
			Debug.Assert(chErased == char.MinValue);

			int nMaxLen = Math.Min(n, PopularPasswords.MaxLength);
			for(int nSubLen = nMaxLen; nSubLen >= 3; --nSubLen)
			{
				if(!PopularPasswords.ContainsLength(nSubLen)) continue;

				char[] vSub = new char[nSubLen];

				for(int i = 0; i <= (n - nSubLen); ++i)
				{
					if(Array.IndexOf<char>(vLower, chErased, i, nSubLen) >= 0)
						continue;

					Array.Copy(vLower, i, vSub, 0, nSubLen);
					if(!EvalAddPopularPasswordPattern(vPatterns, vPassword,
						i, vSub, 0.0))
					{
						Array.Copy(vLeet, i, vSub, 0, nSubLen);
						if(EvalAddPopularPasswordPattern(vPatterns, vPassword,
							i, vSub, 1.5))
						{
							Array.Clear(vLower, i, nSubLen); // Not vLeet
							Debug.Assert(vLower[i] == chErased);
						}
					}
					else
					{
						Array.Clear(vLower, i, nSubLen);
						Debug.Assert(vLower[i] == chErased);
					}
				}
			}
		}

		private static bool EvalAddPopularPasswordPattern(List<QePatternInstance>[] vPatterns,
			char[] vPassword, int i, char[] vSub, double dblCostPerMod)
		{
			ulong uDictSize;
			if(!PopularPasswords.IsPopularPassword(vSub, out uDictSize))
				return false;

			int n = vSub.Length;
			int d = HammingDist(vSub, 0, vPassword, i, n);

			double dblCost = Log2((double)uDictSize);

			// dblCost += log2(n binom d)
			int k = Math.Min(d, n - d);
			for(int j = n; j > (n - k); --j)
				dblCost += Log2(j);
			for(int j = k; j >= 2; --j)
				dblCost -= Log2(j);

			dblCost += dblCostPerMod * (double)d;

			vPatterns[i].Add(new QePatternInstance(i, n, PatternID.Dictionary,
				dblCost));
			return true;
		}

		private static char DecodeLeetChar(char chLeet)
		{
			if((chLeet >= '\u00C0') && (chLeet <= '\u00C6')) return 'a';
			if((chLeet >= '\u00C8') && (chLeet <= '\u00CB')) return 'e';
			if((chLeet >= '\u00CC') && (chLeet <= '\u00CF')) return 'i';
			if((chLeet >= '\u00D2') && (chLeet <= '\u00D6')) return 'o';
			if((chLeet >= '\u00D9') && (chLeet <= '\u00DC')) return 'u';
			if((chLeet >= '\u00E0') && (chLeet <= '\u00E6')) return 'a';
			if((chLeet >= '\u00E8') && (chLeet <= '\u00EB')) return 'e';
			if((chLeet >= '\u00EC') && (chLeet <= '\u00EF')) return 'i';
			if((chLeet >= '\u00F2') && (chLeet <= '\u00F6')) return 'o';
			if((chLeet >= '\u00F9') && (chLeet <= '\u00FC')) return 'u';

			char ch;
			switch(chLeet)
			{
				case '4':
				case '@':
				case '?':
				case '^':
				case '\u00AA': ch = 'a'; break;
				case '8':
				case '\u00DF': ch = 'b'; break;
				case '(':
				case '{':
				case '[':
				case '<':
				case '\u00A2':
				case '\u00A9':
				case '\u00C7':
				case '\u00E7': ch = 'c'; break;
				case '\u00D0':
				case '\u00F0': ch = 'd'; break;
				case '3':
				case '\u20AC':
				case '&':
				case '\u00A3': ch = 'e'; break;
				case '6':
				case '9': ch = 'g'; break;
				case '#': ch = 'h'; break;
				case '1':
				case '!':
				case '|':
				case '\u00A1':
				case '\u00A6': ch = 'i'; break;
				case '\u00D1':
				case '\u00F1': ch = 'n'; break;
				case '0':
				case '*':
				case '\u00A4': // Currency
				case '\u00B0': // Degree
				case '\u00D8':
				case '\u00F8': ch = 'o'; break;
				case '\u00AE': ch = 'r'; break;
				case '$':
				case '5':
				case '\u00A7': ch = 's'; break;
				case '+':
				case '7': ch = 't'; break;
				case '\u00B5': ch = 'u'; break;
				case '%':
				case '\u00D7': ch = 'x'; break;
				case '\u00A5':
				case '\u00DD':
				case '\u00FD':
				case '\u00FF': ch = 'y'; break;
				case '2': ch = 'z'; break;
				default: ch = chLeet; break;
			}

			return ch;
		}

		private static int HammingDist(char[] v1, int iOffset1,
			char[] v2, int iOffset2, int nLength)
		{
			int nDist = 0;
			for(int i = 0; i < nLength; ++i)
			{
				if(v1[iOffset1 + i] != v2[iOffset2 + i]) ++nDist;
			}

			return nDist;
		}

		private static void FindRepetitions(char[] vPassword,
			List<QePatternInstance>[] vPatterns)
		{
			int n = vPassword.Length;
			char[] v = new char[n];
			Array.Copy(vPassword, v, n);

			char chErased = char.MaxValue;
			for(int m = (n / 2); m >= 3; --m)
			{
				for(int x1 = 0; x1 <= (n - (2 * m)); ++x1)
				{
					bool bFoundRep = false;

					for(int x2 = (x1 + m); x2 <= (n - m); ++x2)
					{
						if(PartsEqual(v, x1, x2, m))
						{
							double dblCost = Log2(x1 + 1) + Log2(m);
							vPatterns[x2].Add(new QePatternInstance(x2, m,
								PatternID.Repetition, dblCost));

							ErasePart(v, x2, m, ref chErased);
							bFoundRep = true;
						}
					}

					if(bFoundRep) ErasePart(v, x1, m, ref chErased);
				}
			}
		}

		private static bool PartsEqual(char[] v, int x1, int x2, int nLength)
		{
			for(int i = 0; i < nLength; ++i)
			{
				if(v[x1 + i] != v[x2 + i]) return false;
			}

			return true;
		}

		private static void ErasePart(char[] v, int i, int n, ref char chErased)
		{
			for(int j = 0; j < n; ++j)
			{
				v[i + j] = chErased;
				--chErased;
			}
		}

		private static void FindNumbers(char[] vPassword,
			List<QePatternInstance>[] vPatterns)
		{
			int n = vPassword.Length;
			StringBuilder sb = new StringBuilder();
			for(int i = 0; i < n; ++i)
			{
				char ch = vPassword[i];
				if((ch >= '0') && (ch <= '9')) sb.Append(ch);
				else
				{
					AddNumberPattern(vPatterns, sb.ToString(), i - sb.Length);
					sb.Remove(0, sb.Length);
				}
			}
			AddNumberPattern(vPatterns, sb.ToString(), n - sb.Length);
		}

		private static void AddNumberPattern(List<QePatternInstance>[] vPatterns,
			string strNumber, int i)
		{
			if(strNumber.Length <= 2) return;

			int nZeros = 0;
			for(int j = 0; j < strNumber.Length; ++j)
			{
				if(strNumber[j] != '0') break;
				++nZeros;
			}

			double dblCost = Log2(nZeros + 1);
			if(nZeros < strNumber.Length)
			{
				string strNonZero = strNumber.Substring(nZeros);

#if KeePassLibSD
				try { dblCost += Log2(double.Parse(strNonZero)); }
				catch(Exception) { Debug.Assert(false); return; }
#else
				double d;
				if(double.TryParse(strNonZero, out d))
					dblCost += Log2(d);
				else { Debug.Assert(false); return; }
#endif
			}

			vPatterns[i].Add(new QePatternInstance(i, strNumber.Length,
				PatternID.Number, dblCost));
		}

		private static void FindDiffSeqs(char[] vPassword,
			List<QePatternInstance>[] vPatterns)
		{
			int d = int.MinValue, p = 0;
			string str = new string(vPassword) + new string(char.MaxValue, 1);

			for(int i = 1; i < str.Length; ++i)
			{
				int dCur = (int)str[i] - (int)str[i - 1];
				if(dCur != d)
				{
					if((i - p) >= 3) // At least 3 chars involved
					{
						QeCharType ct = GetCharType(str[p]);
						double dblCost = ct.CharSize + Log2(i - p - 1);

						vPatterns[p].Add(new QePatternInstance(p,
							i - p, PatternID.DiffSeq, dblCost));
					}

					d = dCur;
					p = i - 1;
				}
			}
		}

		private static double Log2(double dblValue)
		{
#if KeePassLibSD
			return (Math.Log(dblValue) / Math.Log(2.0));
#else
			return Math.Log(dblValue, 2.0);
#endif
		}
	}
}

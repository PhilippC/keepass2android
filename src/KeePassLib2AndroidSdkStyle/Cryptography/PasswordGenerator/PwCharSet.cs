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

namespace KeePassLib.Cryptography.PasswordGenerator
{
	public sealed class PwCharSet
	{
		public const string UpperCase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
		public const string LowerCase = "abcdefghijklmnopqrstuvwxyz";
		public const string Digits = "0123456789";

		public const string UpperConsonants = "BCDFGHJKLMNPQRSTVWXYZ";
		public const string LowerConsonants = "bcdfghjklmnpqrstvwxyz";
		public const string UpperVowels = "AEIOU";
		public const string LowerVowels = "aeiou";

		public const string Punctuation = @",.;:";
		public const string Brackets = @"[]{}()<>";

		public const string PrintableAsciiSpecial = "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";

		public const string UpperHex = "0123456789ABCDEF";
		public const string LowerHex = "0123456789abcdef";

		public const string Invalid = "\t\r\n";
		public const string LookAlike = @"O0l1I|";

		internal const string MenuAccels = PwCharSet.LowerCase + PwCharSet.Digits;

		private const int CharTabSize = (0x10000 / 8);

		private List<char> m_vChars = new List<char>();
		private byte[] m_vTab = new byte[CharTabSize];

		private static string m_strHighAnsi = null;
		public static string HighAnsiChars
		{
			get
			{
				if(m_strHighAnsi == null) { new PwCharSet(); } // Create string
				Debug.Assert(m_strHighAnsi != null);
				return m_strHighAnsi;
			}
		}

		private static string m_strSpecial = null;
		public static string SpecialChars
		{
			get
			{
				if(m_strSpecial == null) { new PwCharSet(); } // Create string
				Debug.Assert(m_strSpecial != null);
				return m_strSpecial;
			}
		}

		/// <summary>
		/// Create a new, empty character set collection object.
		/// </summary>
		public PwCharSet()
		{
			Initialize(true);
		}

		public PwCharSet(string strCharSet)
		{
			Initialize(true);
			Add(strCharSet);
		}

		private PwCharSet(bool bFullInitialize)
		{
			Initialize(bFullInitialize);
		}

		private void Initialize(bool bFullInitialize)
		{
			Clear();

			if(!bFullInitialize) return;

			if(m_strHighAnsi == null)
			{
				StringBuilder sbHighAnsi = new StringBuilder();
				// [U+0080, U+009F] are C1 control characters,
				// U+00A0 is non-breaking space
				for(char ch = '\u00A1'; ch <= '\u00AC'; ++ch)
					sbHighAnsi.Append(ch);
				// U+00AD is soft hyphen (format character)
				for(char ch = '\u00AE'; ch < '\u00FF'; ++ch)
					sbHighAnsi.Append(ch);
				sbHighAnsi.Append('\u00FF');

				m_strHighAnsi = sbHighAnsi.ToString();
			}

			if(m_strSpecial == null)
			{
				PwCharSet pcs = new PwCharSet(false);
				pcs.AddRange('!', '/');
				pcs.AddRange(':', '@');
				pcs.AddRange('[', '`');
				pcs.Add(@"|~");
				pcs.Remove(@"-_ ");
				pcs.Remove(PwCharSet.Brackets);

				m_strSpecial = pcs.ToString();
			}
		}

		/// <summary>
		/// Number of characters in this set.
		/// </summary>
		public uint Size
		{
			get { return (uint)m_vChars.Count; }
		}

		/// <summary>
		/// Get a character of the set using an index.
		/// </summary>
		/// <param name="uPos">Index of the character to get.</param>
		/// <returns>Character at the specified position. If the index is invalid,
		/// an <c>ArgumentOutOfRangeException</c> is thrown.</returns>
		public char this[uint uPos]
		{
			get
			{
				if(uPos >= (uint)m_vChars.Count)
					throw new ArgumentOutOfRangeException("uPos");

				return m_vChars[(int)uPos];
			}
		}

		/// <summary>
		/// Remove all characters from this set.
		/// </summary>
		public void Clear()
		{
			m_vChars.Clear();
			Array.Clear(m_vTab, 0, m_vTab.Length);
		}

		public bool Contains(char ch)
		{
			return (((m_vTab[ch / 8] >> (ch % 8)) & 1) != char.MinValue);
		}

		public bool Contains(string strCharacters)
		{
			Debug.Assert(strCharacters != null);
			if(strCharacters == null) throw new ArgumentNullException("strCharacters");

			foreach(char ch in strCharacters)
			{
				if(!Contains(ch)) return false;
			}

			return true;
		}

		/// <summary>
		/// Add characters to the set.
		/// </summary>
		/// <param name="ch">Character to add.</param>
		public void Add(char ch)
		{
			if(ch == char.MinValue) { Debug.Assert(false); return; }

			if(!Contains(ch))
			{
				m_vChars.Add(ch);
				m_vTab[ch / 8] |= (byte)(1 << (ch % 8));
			}
		}

		/// <summary>
		/// Add characters to the set.
		/// </summary>
		/// <param name="strCharSet">String containing characters to add.</param>
		public void Add(string strCharSet)
		{
			Debug.Assert(strCharSet != null);
			if(strCharSet == null) throw new ArgumentNullException("strCharSet");

			m_vChars.Capacity = m_vChars.Count + strCharSet.Length;

			foreach(char ch in strCharSet)
				Add(ch);
		}

		public void Add(string strCharSet1, string strCharSet2)
		{
			Add(strCharSet1);
			Add(strCharSet2);
		}

		public void Add(string strCharSet1, string strCharSet2, string strCharSet3)
		{
			Add(strCharSet1);
			Add(strCharSet2);
			Add(strCharSet3);
		}

		public void AddRange(char chMin, char chMax)
		{
			m_vChars.Capacity = m_vChars.Count + (chMax - chMin) + 1;

			for(char ch = chMin; ch < chMax; ++ch)
				Add(ch);

			Add(chMax);
		}

		public bool AddCharSet(char chCharSetIdentifier)
		{
			bool bResult = true;

			switch(chCharSetIdentifier)
			{
				case 'a': Add(PwCharSet.LowerCase, PwCharSet.Digits); break;
				case 'A': Add(PwCharSet.LowerCase, PwCharSet.UpperCase,
					PwCharSet.Digits); break;
				case 'U': Add(PwCharSet.UpperCase, PwCharSet.Digits); break;
				case 'c': Add(PwCharSet.LowerConsonants); break;
				case 'C': Add(PwCharSet.LowerConsonants,
					PwCharSet.UpperConsonants); break;
				case 'z': Add(PwCharSet.UpperConsonants); break;
				case 'd': Add(PwCharSet.Digits); break; // Digit
				case 'h': Add(PwCharSet.LowerHex); break;
				case 'H': Add(PwCharSet.UpperHex); break;
				case 'l': Add(PwCharSet.LowerCase); break;
				case 'L': Add(PwCharSet.LowerCase, PwCharSet.UpperCase); break;
				case 'u': Add(PwCharSet.UpperCase); break;
				case 'p': Add(PwCharSet.Punctuation); break;
				case 'b': Add(PwCharSet.Brackets); break;
				case 's': Add(PwCharSet.PrintableAsciiSpecial); break;
				case 'S': Add(PwCharSet.UpperCase, PwCharSet.LowerCase);
					Add(PwCharSet.Digits, PwCharSet.PrintableAsciiSpecial); break;
				case 'v': Add(PwCharSet.LowerVowels); break;
				case 'V': Add(PwCharSet.LowerVowels, PwCharSet.UpperVowels); break;
				case 'Z': Add(PwCharSet.UpperVowels); break;
				case 'x': Add(m_strHighAnsi); break;
				default: bResult = false; break;
			}

			return bResult;
		}

		public bool Remove(char ch)
		{
			m_vTab[ch / 8] &= (byte)(~(1 << (ch % 8)));
			return m_vChars.Remove(ch);
		}

		public bool Remove(string strCharacters)
		{
			Debug.Assert(strCharacters != null);
			if(strCharacters == null) throw new ArgumentNullException("strCharacters");

			bool bResult = true;
			foreach(char ch in strCharacters)
			{
				if(!Remove(ch)) bResult = false;
			}

			return bResult;
		}

		public bool RemoveIfAllExist(string strCharacters)
		{
			Debug.Assert(strCharacters != null);
			if(strCharacters == null) throw new ArgumentNullException("strCharacters");

			if(!Contains(strCharacters))
				return false;

			return Remove(strCharacters);
		}

		/// <summary>
		/// Convert the character set to a string containing all its characters.
		/// </summary>
		/// <returns>String containing all character set characters.</returns>
		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			foreach(char ch in m_vChars)
				sb.Append(ch);

			return sb.ToString();
		}

		public string PackAndRemoveCharRanges()
		{
			StringBuilder sb = new StringBuilder();

			sb.Append(RemoveIfAllExist(PwCharSet.UpperCase) ? 'U' : '_');
			sb.Append(RemoveIfAllExist(PwCharSet.LowerCase) ? 'L' : '_');
			sb.Append(RemoveIfAllExist(PwCharSet.Digits) ? 'D' : '_');
			sb.Append(RemoveIfAllExist(m_strSpecial) ? 'S' : '_');
			sb.Append(RemoveIfAllExist(PwCharSet.Punctuation) ? 'P' : '_');
			sb.Append(RemoveIfAllExist(@"-") ? 'm' : '_');
			sb.Append(RemoveIfAllExist(@"_") ? 'u' : '_');
			sb.Append(RemoveIfAllExist(@" ") ? 's' : '_');
			sb.Append(RemoveIfAllExist(PwCharSet.Brackets) ? 'B' : '_');
			sb.Append(RemoveIfAllExist(m_strHighAnsi) ? 'H' : '_');

			return sb.ToString();
		}

		public void UnpackCharRanges(string strRanges)
		{
			if(strRanges == null) { Debug.Assert(false); return; }
			if(strRanges.Length < 10) { Debug.Assert(false); return; }

			if(strRanges[0] != '_') Add(PwCharSet.UpperCase);
			if(strRanges[1] != '_') Add(PwCharSet.LowerCase);
			if(strRanges[2] != '_') Add(PwCharSet.Digits);
			if(strRanges[3] != '_') Add(m_strSpecial);
			if(strRanges[4] != '_') Add(PwCharSet.Punctuation);
			if(strRanges[5] != '_') Add('-');
			if(strRanges[6] != '_') Add('_');
			if(strRanges[7] != '_') Add(' ');
			if(strRanges[8] != '_') Add(PwCharSet.Brackets);
			if(strRanges[9] != '_') Add(m_strHighAnsi);
		}
	}
}

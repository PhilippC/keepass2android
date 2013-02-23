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

		private const int CharTabSize = (0x10000 / 8);

		private List<char> m_vChars = new List<char>();
		private byte[] m_vTab = new byte[CharTabSize];

		private string m_strHighAnsi = string.Empty;
		private string m_strSpecial = string.Empty;

		/// <summary>
		/// Create a new, empty character set collection object.
		/// </summary>
		public PwCharSet()
		{
			this.Initialize(true);
		}

		public PwCharSet(string strCharSet)
		{
			this.Initialize(true);
			this.Add(strCharSet);
		}

		private PwCharSet(bool bFullInitialize)
		{
			this.Initialize(bFullInitialize);
		}

		private void Initialize(bool bFullInitialize)
		{
			this.Clear();

			if(bFullInitialize == false) return;

			StringBuilder sbHighAnsi = new StringBuilder();
			for(char ch = '~'; ch < 255; ++ch)
				sbHighAnsi.Append(ch);
			m_strHighAnsi = sbHighAnsi.ToString();

			PwCharSet pcs = new PwCharSet(false);
			pcs.AddRange('!', '/');
			pcs.AddRange(':', '@');
			pcs.AddRange('[', '`');
			pcs.Remove(@"-_ ");
			pcs.Remove(PwCharSet.Brackets);
			m_strSpecial = pcs.ToString();
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

		public string SpecialChars { get { return m_strSpecial; } }
		public string HighAnsiChars { get { return m_strHighAnsi; } }

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
				if(this.Contains(ch) == false) return false;
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

			if(this.Contains(ch) == false)
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
				this.Add(ch);
		}

		public void Add(string strCharSet1, string strCharSet2)
		{
			this.Add(strCharSet1);
			this.Add(strCharSet2);
		}

		public void Add(string strCharSet1, string strCharSet2, string strCharSet3)
		{
			this.Add(strCharSet1);
			this.Add(strCharSet2);
			this.Add(strCharSet3);
		}

		public void AddRange(char chMin, char chMax)
		{
			m_vChars.Capacity = m_vChars.Count + (chMax - chMin) + 1;

			for(char ch = chMin; ch < chMax; ++ch)
				this.Add(ch);

			this.Add(chMax);
		}

		public bool AddCharSet(char chCharSetIdentifier)
		{
			bool bResult = true;

			switch(chCharSetIdentifier)
			{
				case 'a': this.Add(PwCharSet.LowerCase, PwCharSet.Digits); break;
				case 'A': this.Add(PwCharSet.LowerCase, PwCharSet.UpperCase,
					PwCharSet.Digits); break;
				case 'U': this.Add(PwCharSet.UpperCase, PwCharSet.Digits); break;
				case 'c': this.Add(PwCharSet.LowerConsonants); break;
				case 'C': this.Add(PwCharSet.LowerConsonants,
					PwCharSet.UpperConsonants); break;
				case 'z': this.Add(PwCharSet.UpperConsonants); break;
				case 'd': this.Add(PwCharSet.Digits); break; // Digit
				case 'h': this.Add(PwCharSet.LowerHex); break;
				case 'H': this.Add(PwCharSet.UpperHex); break;
				case 'l': this.Add(PwCharSet.LowerCase); break;
				case 'L': this.Add(PwCharSet.LowerCase, PwCharSet.UpperCase); break;
				case 'u': this.Add(PwCharSet.UpperCase); break;
				case 'p': this.Add(PwCharSet.Punctuation); break;
				case 'b': this.Add(PwCharSet.Brackets); break;
				case 's': this.Add(PwCharSet.PrintableAsciiSpecial); break;
				case 'S': this.Add(PwCharSet.UpperCase, PwCharSet.LowerCase);
					this.Add(PwCharSet.Digits, PwCharSet.PrintableAsciiSpecial); break;
				case 'v': this.Add(PwCharSet.LowerVowels); break;
				case 'V': this.Add(PwCharSet.LowerVowels, PwCharSet.UpperVowels); break;
				case 'Z': this.Add(PwCharSet.UpperVowels); break;
				case 'x': this.Add(m_strHighAnsi); break;
				default: bResult = false; break;
			}

			return bResult;
		}

		public bool Remove(char ch)
		{
			m_vTab[ch / 8] &= (byte)~(1 << (ch % 8));
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

			if(this.Contains(strCharacters) == false)
				return false;

			return this.Remove(strCharacters);
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

			sb.Append(this.RemoveIfAllExist(PwCharSet.UpperCase) ? 'U' : '_');
			sb.Append(this.RemoveIfAllExist(PwCharSet.LowerCase) ? 'L' : '_');
			sb.Append(this.RemoveIfAllExist(PwCharSet.Digits) ? 'D' : '_');
			sb.Append(this.RemoveIfAllExist(m_strSpecial) ? 'S' : '_');
			sb.Append(this.RemoveIfAllExist(PwCharSet.Punctuation) ? 'P' : '_');
			sb.Append(this.RemoveIfAllExist(@"-") ? 'm' : '_');
			sb.Append(this.RemoveIfAllExist(@"_") ? 'u' : '_');
			sb.Append(this.RemoveIfAllExist(@" ") ? 's' : '_');
			sb.Append(this.RemoveIfAllExist(PwCharSet.Brackets) ? 'B' : '_');
			sb.Append(this.RemoveIfAllExist(m_strHighAnsi) ? 'H' : '_');

			return sb.ToString();
		}

		public void UnpackCharRanges(string strRanges)
		{
			if(strRanges == null) { Debug.Assert(false); return; }
			if(strRanges.Length < 10) { Debug.Assert(false); return; }

			if(strRanges[0] != '_') this.Add(PwCharSet.UpperCase);
			if(strRanges[1] != '_') this.Add(PwCharSet.LowerCase);
			if(strRanges[2] != '_') this.Add(PwCharSet.Digits);
			if(strRanges[3] != '_') this.Add(m_strSpecial);
			if(strRanges[4] != '_') this.Add(PwCharSet.Punctuation);
			if(strRanges[5] != '_') this.Add('-');
			if(strRanges[6] != '_') this.Add('_');
			if(strRanges[7] != '_') this.Add(' ');
			if(strRanges[8] != '_') this.Add(PwCharSet.Brackets);
			if(strRanges[9] != '_') this.Add(m_strHighAnsi);
		}
	}
}

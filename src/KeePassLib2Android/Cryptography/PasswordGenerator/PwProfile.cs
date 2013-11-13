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
using System.Xml.Serialization;
using System.ComponentModel;
using System.Diagnostics;

using KeePassLib.Interfaces;
using KeePassLib.Security;
using KeePassLib.Utility;

namespace KeePassLib.Cryptography.PasswordGenerator
{
	/// <summary>
	/// Type of the password generator. Different types like generators
	/// based on given patterns, based on character sets, etc. are
	/// available.
	/// </summary>
	public enum PasswordGeneratorType
	{
		/// <summary>
		/// Generator based on character spaces/sets, i.e. groups
		/// of characters like lower-case, upper-case or numeric characters.
		/// </summary>
		CharSet = 0,

		/// <summary>
		/// Password generation based on a pattern. The user has provided
		/// a pattern, which describes how the generated password has to
		/// look like.
		/// </summary>
		Pattern = 1,

		Custom = 2
	}

	public sealed class PwProfile : IDeepCloneable<PwProfile>
	{
		private string m_strName = string.Empty;
		[DefaultValue("")]
		public string Name
		{
			get { return m_strName; }
			set { m_strName = value; }
		}

		private PasswordGeneratorType m_type = PasswordGeneratorType.CharSet;
		public PasswordGeneratorType GeneratorType
		{
			get { return m_type; }
			set { m_type = value; }
		}

		private bool m_bUserEntropy = false;
		[DefaultValue(false)]
		public bool CollectUserEntropy
		{
			get { return m_bUserEntropy; }
			set { m_bUserEntropy = value; }
		}

		private uint m_uLength = 20;
		public uint Length
		{
			get { return m_uLength; }
			set { m_uLength = value; }
		}

		private PwCharSet m_pwCharSet = new PwCharSet(PwCharSet.UpperCase +
			PwCharSet.LowerCase + PwCharSet.Digits);
		[XmlIgnore]
		public PwCharSet CharSet
		{
			get { return m_pwCharSet; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_pwCharSet = value;
			}
		}

		private string m_strCharSetRanges = string.Empty;
		[DefaultValue("")]
		public string CharSetRanges
		{
			get { this.UpdateCharSet(true); return m_strCharSetRanges; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strCharSetRanges = value;
				this.UpdateCharSet(false);
			}
		}

		private string m_strCharSetAdditional = string.Empty;
		[DefaultValue("")]
		public string CharSetAdditional
		{
			get { this.UpdateCharSet(true); return m_strCharSetAdditional; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strCharSetAdditional = value;
				this.UpdateCharSet(false);
			}
		}

		private string m_strPattern = string.Empty;
		[DefaultValue("")]
		public string Pattern
		{
			get { return m_strPattern; }
			set { m_strPattern = value; }
		}

		private bool m_bPatternPermute = false;
		[DefaultValue(false)]
		public bool PatternPermutePassword
		{
			get { return m_bPatternPermute; }
			set { m_bPatternPermute = value; }
		}

		private bool m_bNoLookAlike = false;
		[DefaultValue(false)]
		public bool ExcludeLookAlike
		{
			get { return m_bNoLookAlike; }
			set { m_bNoLookAlike = value; }
		}

		private bool m_bNoRepeat = false;
		[DefaultValue(false)]
		public bool NoRepeatingCharacters
		{
			get { return m_bNoRepeat; }
			set { m_bNoRepeat = value; }
		}

		private string m_strExclude = string.Empty;
		[DefaultValue("")]
		public string ExcludeCharacters
		{
			get { return m_strExclude; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strExclude = value;
			}
		}

		private string m_strCustomID = string.Empty;
		[DefaultValue("")]
		public string CustomAlgorithmUuid
		{
			get { return m_strCustomID; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strCustomID = value;
			}
		}

		private string m_strCustomOpt = string.Empty;
		[DefaultValue("")]
		public string CustomAlgorithmOptions
		{
			get { return m_strCustomOpt; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strCustomOpt = value;
			}
		}

		public PwProfile()
		{
		}

		public PwProfile CloneDeep()
		{
			PwProfile p = new PwProfile();

			p.m_strName = m_strName;
			p.m_type = m_type;
			p.m_bUserEntropy = m_bUserEntropy;
			p.m_uLength = m_uLength;
			p.m_pwCharSet = new PwCharSet(m_pwCharSet.ToString());
			p.m_strCharSetRanges = m_strCharSetRanges;
			p.m_strCharSetAdditional = m_strCharSetAdditional;
			p.m_strPattern = m_strPattern;
			p.m_bPatternPermute = m_bPatternPermute;
			p.m_bNoLookAlike = m_bNoLookAlike;
			p.m_bNoRepeat = m_bNoRepeat;
			p.m_strExclude = m_strExclude;
			p.m_strCustomID = m_strCustomID;
			p.m_strCustomOpt = m_strCustomOpt;

			return p;
		}

		private void UpdateCharSet(bool bSetXml)
		{
			if(bSetXml)
			{
				PwCharSet pcs = new PwCharSet(m_pwCharSet.ToString());
				m_strCharSetRanges = pcs.PackAndRemoveCharRanges();
				m_strCharSetAdditional = pcs.ToString();
			}
			else
			{
				PwCharSet pcs = new PwCharSet(m_strCharSetAdditional);
				pcs.UnpackCharRanges(m_strCharSetRanges);
				m_pwCharSet = pcs;
			}
		}

		public static PwProfile DeriveFromPassword(ProtectedString psPassword)
		{
			PwProfile pp = new PwProfile();
			Debug.Assert(psPassword != null); if(psPassword == null) return pp;

			byte[] pbUtf8 = psPassword.ReadUtf8();
			char[] vChars = StrUtil.Utf8.GetChars(pbUtf8);

			pp.GeneratorType = PasswordGeneratorType.CharSet;
			pp.Length = (uint)vChars.Length;

			PwCharSet pcs = pp.CharSet;
			pcs.Clear();

			foreach(char ch in vChars)
			{
				if((ch >= 'A') && (ch <= 'Z')) pcs.Add(PwCharSet.UpperCase);
				else if((ch >= 'a') && (ch <= 'z')) pcs.Add(PwCharSet.LowerCase);
				else if((ch >= '0') && (ch <= '9')) pcs.Add(PwCharSet.Digits);
				else if(PwCharSet.SpecialChars.IndexOf(ch) >= 0)
					pcs.Add(PwCharSet.SpecialChars);
				else if(ch == ' ') pcs.Add(' ');
				else if(ch == '-') pcs.Add('-');
				else if(ch == '_') pcs.Add('_');
				else if(PwCharSet.Brackets.IndexOf(ch) >= 0)
					pcs.Add(PwCharSet.Brackets);
				else if(PwCharSet.HighAnsiChars.IndexOf(ch) >= 0)
					pcs.Add(PwCharSet.HighAnsiChars);
				else pcs.Add(ch);
			}

			Array.Clear(vChars, 0, vChars.Length);
			MemUtil.ZeroByteArray(pbUtf8);
			return pp;
		}

		public bool HasSecurityReducingOption()
		{
			return (m_bNoLookAlike || m_bNoRepeat || (m_strExclude.Length > 0));
		}
	}
}

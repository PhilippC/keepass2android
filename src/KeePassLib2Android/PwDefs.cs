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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

using KeePassLib.Delegates;
using KeePassLib.Interfaces;
using KeePassLib.Serialization;

namespace KeePassLib
{
	/// <summary>
	/// Contains KeePassLib-global definitions and enums.
	/// </summary>
	public static class PwDefs
	{
		/// <summary>
		/// The product name.
		/// </summary>
		public const string ProductName = "KeePass Password Safe";

		/// <summary>
		/// A short, simple string representing the product name. The string
		/// should contain no spaces, directory separator characters, etc.
		/// </summary>
		public const string ShortProductName = "KeePass";

		internal const string UnixName = "keepass2";
		internal const string ResClass = "KeePass2"; // With initial capital

		/// <summary>
		/// Version, encoded as 32-bit unsigned integer.
		/// 2.00 = 0x02000000, 2.01 = 0x02000100, ..., 2.18 = 0x02010800.
		/// As of 2.19, the version is encoded component-wise per byte,
		/// e.g. 2.19 = 0x02130000.
		/// It is highly recommended to use <c>FileVersion64</c> instead.
		/// </summary>
		public const uint Version32 = 0x02220000;

		/// <summary>
		/// Version, encoded as 64-bit unsigned integer
		/// (component-wise, 16 bits per component).
		/// </summary>
		public const ulong FileVersion64 = 0x0002002200000000UL;

		/// <summary>
		/// Version, encoded as string.
		/// </summary>
		public const string VersionString = "2.34";

		public const string Copyright = @"Copyright © 2003-2016 Dominik Reichl";

		/// <summary>
		/// Product website URL. Terminated by a forward slash.
		/// </summary>
		public const string HomepageUrl = "http://keepass.info/";

		/// <summary>
		/// Product donations URL.
		/// </summary>
		public const string DonationsUrl = "http://keepass.info/donate.html";

		/// <summary>
		/// URL to the online plugins page.
		/// </summary>
		public const string PluginsUrl = "http://keepass.info/plugins.html";

		/// <summary>
		/// URL to the online translations page.
		/// </summary>
		public const string TranslationsUrl = "http://keepass.info/translations.html";

		/// <summary>
		/// URL to a TXT file (eventually compressed) that contains information
		/// about the latest KeePass version available on the website.
		/// </summary>
		public const string VersionUrl = "https://sslsites.de/keepass.info/update/version2x.txt.gz";
		// public const string VersionUrl = "http://keepass.info/update/version2x.txt.gz";

		/// <summary>
		/// URL to the root path of the online KeePass help. Terminated by
		/// a forward slash.
		/// </summary>
		public const string HelpUrl = "http://keepass.info/help/";

		/// <summary>
		/// A <c>DateTime</c> object that represents the time when the assembly
		/// was loaded.
		/// </summary>
		public static readonly DateTime DtDefaultNow = DateTime.Now;

		/// <summary>
		/// Default number of master key encryption/transformation rounds (making dictionary attacks harder).
		/// </summary>
		public const ulong DefaultKeyEncryptionRounds = 6000;

		/// <summary>
		/// Default identifier string for the title field. Should not contain
		/// spaces, tabs or other whitespace.
		/// </summary>
		public const string TitleField = "Title";

		/// <summary>
		/// Default identifier string for the user name field. Should not contain
		/// spaces, tabs or other whitespace.
		/// </summary>
		public const string UserNameField = "UserName";

		/// <summary>
		/// Default identifier string for the password field. Should not contain
		/// spaces, tabs or other whitespace.
		/// </summary>
		public const string PasswordField = "Password";

		/// <summary>
		/// Default identifier string for the URL field. Should not contain
		/// spaces, tabs or other whitespace.
		/// </summary>
		public const string UrlField = "URL";

		/// <summary>
		/// Default identifier string for the notes field. Should not contain
		/// spaces, tabs or other whitespace.
		/// </summary>
		public const string NotesField = "Notes";

		/// <summary>
		/// Default identifier string for the field which will contain TAN indices.
		/// </summary>
		public const string TanIndexField = UserNameField;

		/// <summary>
		/// Default title of an entry that is really a TAN entry.
		/// </summary>
		public const string TanTitle = @"<TAN>";

		/// <summary>
		/// Prefix of a custom auto-type string field.
		/// </summary>
		public const string AutoTypeStringPrefix = "S:";

		/// <summary>
		/// Default string representing a hidden password.
		/// </summary>
		public const string HiddenPassword = "********";

		/// <summary>
		/// Default auto-type keystroke sequence. If no custom sequence is
		/// specified, this sequence is used.
		/// </summary>
		public const string DefaultAutoTypeSequence = @"{USERNAME}{TAB}{PASSWORD}{ENTER}";

		/// <summary>
		/// Default auto-type keystroke sequence for TAN entries. If no custom
		/// sequence is specified, this sequence is used.
		/// </summary>
		public const string DefaultAutoTypeSequenceTan = @"{PASSWORD}";

		/// <summary>
		/// Check if a name is a standard field name.
		/// </summary>
		/// <param name="strFieldName">Input field name.</param>
		/// <returns>Returns <c>true</c>, if the field name is a standard
		/// field name (title, user name, password, ...), otherwise <c>false</c>.</returns>
		public static bool IsStandardField(string strFieldName)
		{
			Debug.Assert(strFieldName != null); if(strFieldName == null) return false;

			if(strFieldName.Equals(TitleField)) return true;
			if(strFieldName.Equals(UserNameField)) return true;
			if(strFieldName.Equals(PasswordField)) return true;
			if(strFieldName.Equals(UrlField)) return true;
			if(strFieldName.Equals(NotesField)) return true;

			return false;
		}

		public static List<string> GetStandardFields()
		{
			List<string> l = new List<string>();

			l.Add(TitleField);
			l.Add(UserNameField);
			l.Add(PasswordField);
			l.Add(UrlField);
			l.Add(NotesField);

			return l;
		}

		/// <summary>
		/// Check if an entry is a TAN.
		/// </summary>
		/// <param name="pe">Password entry.</param>
		/// <returns>Returns <c>true</c> if the entry is a TAN.</returns>
		public static bool IsTanEntry(PwEntry pe)
		{
			Debug.Assert(pe != null); if(pe == null) return false;

			return (pe.Strings.ReadSafe(PwDefs.TitleField) == TanTitle);
		}
	}

	// #pragma warning disable 1591 // Missing XML comments warning
	/// <summary>
	/// Search parameters for group and entry searches.
	/// </summary>
	public sealed class SearchParameters
	{
		private string m_strText = string.Empty;
		[DefaultValue("")]
		public string SearchString
		{
			get { return m_strText; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strText = value;
			}
		}

		private bool m_bRegex = false;
		[DefaultValue(false)]
		public bool RegularExpression
		{
			get { return m_bRegex; }
			set { m_bRegex = value; }
		}

		private bool m_bSearchInTitles = true;
		[DefaultValue(true)]
		public bool SearchInTitles
		{
			get { return m_bSearchInTitles; }
			set { m_bSearchInTitles = value; }
		}

		private bool m_bSearchInUserNames = true;
		[DefaultValue(true)]
		public bool SearchInUserNames
		{
			get { return m_bSearchInUserNames; }
			set { m_bSearchInUserNames = value; }
		}

		private bool m_bSearchInPasswords = false;
		[DefaultValue(false)]
		public bool SearchInPasswords
		{
			get { return m_bSearchInPasswords; }
			set { m_bSearchInPasswords = value; }
		}

		private bool m_bSearchInUrls = true;
		[DefaultValue(true)]
		public bool SearchInUrls
		{
			get { return m_bSearchInUrls; }
			set { m_bSearchInUrls = value; }
		}

		private bool m_bSearchInNotes = true;
		[DefaultValue(true)]
		public bool SearchInNotes
		{
			get { return m_bSearchInNotes; }
			set { m_bSearchInNotes = value; }
		}

		private bool m_bSearchInOther = true;
		[DefaultValue(true)]
		public bool SearchInOther
		{
			get { return m_bSearchInOther; }
			set { m_bSearchInOther = value; }
		}

		private bool m_bSearchInUuids = false;
		[DefaultValue(false)]
		public bool SearchInUuids
		{
			get { return m_bSearchInUuids; }
			set { m_bSearchInUuids = value; }
		}

		private bool m_bSearchInGroupNames = false;
		[DefaultValue(false)]
		public bool SearchInGroupNames
		{
			get { return m_bSearchInGroupNames; }
			set { m_bSearchInGroupNames = value; }
		}

		private bool m_bSearchInTags = true;
		[DefaultValue(true)]
		public bool SearchInTags
		{
			get { return m_bSearchInTags; }
			set { m_bSearchInTags = value; }
		}

#if KeePassUAP
		private StringComparison m_scType = StringComparison.OrdinalIgnoreCase;
#else
		private StringComparison m_scType = StringComparison.InvariantCultureIgnoreCase;
#endif
		/// <summary>
		/// String comparison type. Specifies the condition when the specified
		/// text matches a group/entry string.
		/// </summary>
		public StringComparison ComparisonMode
		{
			get { return m_scType; }
			set { m_scType = value; }
		}

		private bool m_bExcludeExpired = false;
		[DefaultValue(false)]
		public bool ExcludeExpired
		{
			get { return m_bExcludeExpired; }
			set { m_bExcludeExpired = value; }
		}

		private bool m_bRespectEntrySearchingDisabled = true;
		[DefaultValue(true)]
		public bool RespectEntrySearchingDisabled
		{
			get { return m_bRespectEntrySearchingDisabled; }
			set { m_bRespectEntrySearchingDisabled = value; }
		}

		private StrPwEntryDelegate m_fnDataTrf = null;
		[XmlIgnore]
		public StrPwEntryDelegate DataTransformationFn
		{
			get { return m_fnDataTrf; }
			set { m_fnDataTrf = value; }
		}

		private string m_strDataTrf = string.Empty;
		/// <summary>
		/// Only for serialization.
		/// </summary>
		[DefaultValue("")]
		public string DataTransformation
		{
			get { return m_strDataTrf; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strDataTrf = value;
			}
		}

		[XmlIgnore]
		public static SearchParameters None
		{
			get
			{
				SearchParameters sp = new SearchParameters();

				// sp.m_strText = string.Empty;
				// sp.m_bRegex = false;
				sp.m_bSearchInTitles = false;
				sp.m_bSearchInUserNames = false;
				// sp.m_bSearchInPasswords = false;
				sp.m_bSearchInUrls = false;
				sp.m_bSearchInNotes = false;
				sp.m_bSearchInOther = false;
				// sp.m_bSearchInUuids = false;
				// sp.SearchInGroupNames = false;
				sp.m_bSearchInTags = false;
				// sp.m_scType = StringComparison.InvariantCultureIgnoreCase;
				// sp.m_bExcludeExpired = false;
				// m_bRespectEntrySearchingDisabled = true;

				return sp;
			}
		}

		/// <summary>
		/// Construct a new search parameters object.
		/// </summary>
		public SearchParameters()
		{
		}

		public SearchParameters Clone()
		{
			return (SearchParameters)this.MemberwiseClone();
		}
	}
	// #pragma warning restore 1591 // Missing XML comments warning

	// #pragma warning disable 1591 // Missing XML comments warning
	/// <summary>
	/// Memory protection configuration structure (for default fields).
	/// </summary>
	public sealed class MemoryProtectionConfig : IDeepCloneable<MemoryProtectionConfig>
	{
		public bool ProtectTitle = false;
		public bool ProtectUserName = false;
		public bool ProtectPassword = true;
		public bool ProtectUrl = false;
		public bool ProtectNotes = false;

		// public bool AutoEnableVisualHiding = false;

		public MemoryProtectionConfig CloneDeep()
		{
			return (MemoryProtectionConfig)this.MemberwiseClone();
		}

		public bool GetProtection(string strField)
		{
			if(strField == PwDefs.TitleField) return this.ProtectTitle;
			if(strField == PwDefs.UserNameField) return this.ProtectUserName;
			if(strField == PwDefs.PasswordField) return this.ProtectPassword;
			if(strField == PwDefs.UrlField) return this.ProtectUrl;
			if(strField == PwDefs.NotesField) return this.ProtectNotes;

			return false;
		}
	}
	// #pragma warning restore 1591 // Missing XML comments warning

	public sealed class ObjectTouchedEventArgs : EventArgs
	{
		private object m_o;
		public object Object { get { return m_o; } }

		private bool m_bModified;
		public bool Modified { get { return m_bModified; } }

		private bool m_bParentsTouched;
		public bool ParentsTouched { get { return m_bParentsTouched; } }

		public ObjectTouchedEventArgs(object o, bool bModified,
			bool bParentsTouched)
		{
			m_o = o;
			m_bModified = bModified;
			m_bParentsTouched = bParentsTouched;
		}
	}

	public sealed class IOAccessEventArgs : EventArgs
	{
		private IOConnectionInfo m_ioc;
		public IOConnectionInfo IOConnectionInfo { get { return m_ioc; } }

		private IOConnectionInfo m_ioc2;
		public IOConnectionInfo IOConnectionInfo2 { get { return m_ioc2; } }

		private IOAccessType m_t;
		public IOAccessType Type { get { return m_t; } }

		public IOAccessEventArgs(IOConnectionInfo ioc, IOConnectionInfo ioc2,
			IOAccessType t)
		{
			m_ioc = ioc;
			m_ioc2 = ioc2;
			m_t = t;
		}
	}
}

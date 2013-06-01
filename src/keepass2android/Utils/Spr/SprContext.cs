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
using System.Diagnostics;

using KeePassLib;
using KeePassLib.Interfaces;

using SprRefsCache = System.Collections.Generic.Dictionary<string, string>;

namespace KeePass.Util.Spr
{
	[Flags]
	public enum SprCompileFlags
	{
		None = 0,

		AppPaths = 0x1, // Paths to IE, Firefox, Opera, ...
		PickChars = 0x2,
		EntryStrings = 0x4,
		EntryStringsSpecial = 0x8, // {URL:RMVSCM}, ...
		PasswordEnc = 0x10,
		Group = 0x20,
		Paths = 0x40, // App-dir, doc-dir, path sep, ...
		AutoType = 0x80, // Replacements like {CLEARFIELD}, ...
		DateTime = 0x100,
		References = 0x200,
		EnvVars = 0x400,
		NewPassword = 0x800,
		HmacOtp = 0x1000,
		Comments = 0x2000,

		ExtActive = 0x4000, // Active transformations provided by plugins
		ExtNonActive = 0x8000, // Non-active transformations provided by plugins

		// Next free: 0x10000
		All = 0xFFFF,

		// Internal:
		UIInteractive = SprCompileFlags.PickChars,
		StateChanging = (SprCompileFlags.NewPassword | SprCompileFlags.HmacOtp),

		Active = (SprCompileFlags.UIInteractive | SprCompileFlags.StateChanging |
			SprCompileFlags.ExtActive),
		NonActive = (SprCompileFlags.All & ~SprCompileFlags.Active),

		Deref = (SprCompileFlags.EntryStrings | SprCompileFlags.EntryStringsSpecial |
			SprCompileFlags.References)
	}

	public sealed class SprContext
	{
		private PwEntry m_pe = null;
		public PwEntry Entry
		{
			get { return m_pe; }
			set { m_pe = value; }
		}

		private PwDatabase m_pd = null;
		public PwDatabase Database
		{
			get { return m_pd; }
			set { m_pd = value; }
		}

		private bool m_bMakeAT = false;
		public bool EncodeAsAutoTypeSequence
		{
			get { return m_bMakeAT; }
			set { m_bMakeAT = value; }
		}

		private bool m_bMakeCmdQuotes = false;
		public bool EncodeQuotesForCommandLine
		{
			get { return m_bMakeCmdQuotes; }
			set { m_bMakeCmdQuotes = value; }
		}

		private bool m_bForcePlainTextPasswords = true;
		public bool ForcePlainTextPasswords
		{
			get { return m_bForcePlainTextPasswords; }
			set { m_bForcePlainTextPasswords = value; }
		}

		private SprCompileFlags m_flags = SprCompileFlags.All;
		public SprCompileFlags Flags
		{
			get { return m_flags; }
			set { m_flags = value; }
		}

		private SprRefsCache m_refsCache = new SprRefsCache();
		/// <summary>
		/// Used internally by <c>SprEngine</c>; don't modify it.
		/// </summary>
		internal SprRefsCache RefsCache
		{
			get { return m_refsCache; }
		}

		// private bool m_bNoUrlSchemeOnce = false;
		// /// <summary>
		// /// Used internally by <c>SprEngine</c>; don't modify it.
		// /// </summary>
		// internal bool UrlRemoveSchemeOnce
		// {
		//	get { return m_bNoUrlSchemeOnce; }
		//	set { m_bNoUrlSchemeOnce = value; }
		// }

		public SprContext() { }

		public SprContext(PwEntry pe, PwDatabase pd, SprCompileFlags fl)
		{
			Init(pe, pd, false, false, fl);
		}

		public SprContext(PwEntry pe, PwDatabase pd, SprCompileFlags fl,
			bool bEncodeAsAutoTypeSequence, bool bEncodeQuotesForCommandLine)
		{
			Init(pe, pd, bEncodeAsAutoTypeSequence, bEncodeQuotesForCommandLine, fl);
		}

		private void Init(PwEntry pe, PwDatabase pd, bool bAT, bool bCmdQuotes,
			SprCompileFlags fl)
		{
			m_pe = pe;
			m_pd = pd;
			m_bMakeAT = bAT;
			m_bMakeCmdQuotes = bCmdQuotes;
			m_flags = fl;
		}

		public SprContext Clone()
		{
			return (SprContext)this.MemberwiseClone();
		}

		/// <summary>
		/// Used by <c>SprEngine</c> internally; do not use.
		/// </summary>
		internal SprContext WithoutContentTransformations()
		{
			SprContext ctx = Clone();

			ctx.m_bMakeAT = false;
			ctx.m_bMakeCmdQuotes = false;
			// ctx.m_bNoUrlSchemeOnce = false;

			Debug.Assert(object.ReferenceEquals(m_pe, ctx.m_pe));
			Debug.Assert(object.ReferenceEquals(m_pd, ctx.m_pd));
			Debug.Assert(object.ReferenceEquals(m_refsCache, ctx.m_refsCache));
			return ctx;
		}
	}

	public sealed class SprEventArgs : EventArgs
	{
		private string m_str = string.Empty;
		public string Text
		{
			get { return m_str; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_str = value;
			}
		}

		private SprContext m_ctx = null;
		public SprContext Context
		{
			get { return m_ctx; }
		}

		public SprEventArgs() { }

		public SprEventArgs(string strText, SprContext ctx)
		{
			if(strText == null) throw new ArgumentNullException("strText");
			// ctx == null is allowed

			m_str = strText;
			m_ctx = ctx;
		}
	}
}

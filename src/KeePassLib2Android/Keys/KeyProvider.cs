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

using KeePassLib.Serialization;

namespace KeePassLib.Keys
{
	public sealed class KeyProviderQueryContext
	{
		private IOConnectionInfo m_ioInfo;
		public IOConnectionInfo DatabaseIOInfo
		{
			get { return m_ioInfo; }
		}

		public string DatabasePath
		{
			get { return m_ioInfo.Path; }
		}

		private bool m_bCreatingNewKey;
		public bool CreatingNewKey
		{
			get { return m_bCreatingNewKey; }
		}

		private bool m_bSecDesktop;
		public bool IsOnSecureDesktop
		{
			get { return m_bSecDesktop; }
		}

		public KeyProviderQueryContext(IOConnectionInfo ioInfo, bool bCreatingNewKey,
			bool bOnSecDesktop)
		{
			if(ioInfo == null) throw new ArgumentNullException("ioInfo");

			m_ioInfo = ioInfo.CloneDeep();
			m_bCreatingNewKey = bCreatingNewKey;
			m_bSecDesktop = bOnSecDesktop;
		}
	}

	public abstract class KeyProvider
	{
		/// <summary>
		/// Name of your key provider (should be unique).
		/// </summary>
		public abstract string Name
		{
			get;
		}

		/// <summary>
		/// Property indicating whether the provider is exclusive.
		/// If the provider is exclusive, KeePass doesn't allow other
		/// key sources (master password, Windows user account, ...)
		/// to be combined with the provider.
		/// Key providers typically should return <c>false</c>
		/// (to allow non-exclusive use), i.e. don't override this
		/// property.
		/// </summary>
		public virtual bool Exclusive
		{
			get { return false; }
		}

		/// <summary>
		/// Property that specifies whether the returned key data
		/// gets hashed by KeePass first or is written directly to
		/// the user key data stream.
		/// Standard key provider plugins should return <c>false</c>
		/// (i.e. don't overwrite this property). Returning <c>true</c>
		/// may cause severe security problems and is highly
		/// discouraged.
		/// </summary>
		public virtual bool DirectKey
		{
			get { return false; }
		}

		// public virtual PwIcon ImageIndex
		// {
		//	get { return PwIcon.UserKey; }
		// }

		/// <summary>
		/// This property specifies whether the <c>GetKey</c> method might
		/// show a form or dialog. If there is any chance that the method shows
		/// one, this property must return <c>true</c>. Only if it's guaranteed
		/// that the <c>GetKey</c> method doesn't show any form or dialog, this
		/// property should return <c>false</c>.
		/// </summary>
		public virtual bool GetKeyMightShowGui
		{
			get { return true; }
		}

		/// <summary>
		/// This property specifies whether the key provider is compatible
		/// with the secure desktop mode. This almost never is the case,
		/// so you usually won't override this property.
		/// </summary>
		public virtual bool SecureDesktopCompatible
		{
			get { return false; }
		}

		public abstract byte[] GetKey(KeyProviderQueryContext ctx);
	}

#if DEBUG
	public sealed class SampleKeyProvider : KeyProvider
	{
		public override string Name
		{
			get { return "Built-In Sample Key Provider"; }
		}

		// Do not just copy this to your own key provider class! See above.
		public override bool GetKeyMightShowGui
		{
			get { return false; }
		}

		public override byte[] GetKey(KeyProviderQueryContext ctx)
		{
			return new byte[]{ 2, 3, 5, 7, 11, 13 };
		}
	}
#endif
}

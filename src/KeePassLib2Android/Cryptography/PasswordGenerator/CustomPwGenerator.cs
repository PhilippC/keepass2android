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

using KeePassLib;
using KeePassLib.Security;

namespace KeePassLib.Cryptography.PasswordGenerator
{
	public abstract class CustomPwGenerator
	{
		/// <summary>
		/// Each custom password generation algorithm must have
		/// its own unique UUID.
		/// </summary>
		public abstract PwUuid Uuid { get; }

		/// <summary>
		/// Displayable name of the password generation algorithm.
		/// </summary>
		public abstract string Name { get; }

		public virtual bool SupportsOptions
		{
			get { return false; }
		}

		/// <summary>
		/// Password generation function.
		/// </summary>
		/// <param name="prf">Password generation options chosen
		/// by the user. This may be <c>null</c>, if the default
		/// options should be used.</param>
		/// <param name="crsRandomSource">Source that the algorithm
		/// can use to generate random numbers.</param>
		/// <returns>Generated password or <c>null</c> in case
		/// of failure. If returning <c>null</c>, the caller assumes
		/// that an error message has already been shown to the user.</returns>
		public abstract ProtectedString Generate(PwProfile prf,
			CryptoRandomStream crsRandomSource);

		public virtual string GetOptions(string strCurrentOptions)
		{
			return string.Empty;
		}
	}
}

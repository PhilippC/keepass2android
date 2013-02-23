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

namespace KeePassLib.Keys
{
	public enum KeyValidationType
	{
		MasterPassword = 0
	}

	public abstract class KeyValidator
	{
		/// <summary>
		/// Name of your key validator (should be unique).
		/// </summary>
		public abstract string Name
		{
			get;
		}

		/// <summary>
		/// Validate a key.
		/// </summary>
		/// <param name="strKey">Key to validate.</param>
		/// <param name="t">Type of the validation to perform.</param>
		/// <returns>Returns <c>null</c>, if the validation is successful.
		/// If there's a problem with the key, the returned string describes
		/// the problem.</returns>
		public abstract string Validate(string strKey, KeyValidationType t);
	}
}

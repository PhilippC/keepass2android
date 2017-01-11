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

namespace KeePassLib.Interfaces
{
	/// <summary>
	/// Interface for objects that support various times (creation time, last
	/// access time, last modification time and expiry time). Offers
	/// several helper functions (for example a function to touch the current
	/// object).
	/// </summary>
	public interface ITimeLogger
	{
		/// <summary>
		/// The date/time when the object was created.
		/// </summary>
		DateTime CreationTime
		{
			get;
			set;
		}

		/// <summary>
		/// The date/time when the object was last modified.
		/// </summary>
		DateTime LastModificationTime
		{
			get;
			set;
		}

		/// <summary>
		/// The date/time when the object was last accessed.
		/// </summary>
		DateTime LastAccessTime
		{
			get;
			set;
		}

		/// <summary>
		/// The date/time when the object expires.
		/// </summary>
		DateTime ExpiryTime
		{
			get;
			set;
		}

		/// <summary>
		/// Flag that determines if the object does expire.
		/// </summary>
		bool Expires
		{
			get;
			set;
		}

		/// <summary>
		/// Get or set the usage count of the object. To increase the usage
		/// count by one, use the <c>Touch</c> function.
		/// </summary>
		ulong UsageCount
		{
			get;
			set;
		}

		/// <summary>
		/// The date/time when the location of the object was last changed.
		/// </summary>
		DateTime LocationChanged
		{
			get;
			set;
		}

		/// <summary>
		/// Touch the object. This function updates the internal last access
		/// time. If the <paramref name="bModified" /> parameter is <c>true</c>,
		/// the last modification time gets updated, too. Each time you call
		/// <c>Touch</c>, the usage count of the object is increased by one.
		/// </summary>
		/// <param name="bModified">Update last modification time.</param>
		void Touch(bool bModified);
	}
}

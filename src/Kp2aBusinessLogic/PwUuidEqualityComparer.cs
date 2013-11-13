/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. 

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 2 of the License, or
  (at your option) any later version.

  Keepass2Android is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
  */

using System.Collections.Generic;
using KeePassLib;

namespace keepass2android
{
	/// <summary>
	/// EqualityComparer for PwUuid based on their value (instead of reference)
	/// </summary>
	public class PwUuidEqualityComparer: IEqualityComparer<PwUuid>
	{
		#region IEqualityComparer implementation			
		public bool Equals (PwUuid x, PwUuid y)
		{
			return x.Equals(y);
		}			
		public int GetHashCode (PwUuid obj)
		{
			return obj.ToHexString().GetHashCode();
		}			
#endregion
	}


}

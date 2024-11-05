/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 3 of the License, or
  (at your option) any later version.

  Keepass2Android is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
  */

using System;
using Android.Content;
using Android.Preferences;

namespace keepass2android
{
	/// <summary>
	/// Utility class to simplify access to the app preferences
	/// </summary>
	public class PrefsUtil {
		public static float GetListTextSize(Context ctx) {
			ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(ctx);

			return float.Parse(prefs.GetString(ctx.GetString(Resource.String.list_size_key), ctx.GetString(Resource.String.list_size_default)));
			
		}
		public static float GetListDetailTextSize(Context ctx) {
			return (float)Math.Round(GetListTextSize(ctx)*3.0f/4.0f);
			
		}
	}

}


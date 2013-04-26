/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace keepass2android
{
	
	public class Intents {
		public const String TIMEOUT = "keepass2android.timeout";
		
		public const String COPY_USERNAME = "keepass2android.copy_username";
		public const String COPY_PASSWORD = "keepass2android.copy_password";
		
		public const String FILE_BROWSE = "org.openintents.action.PICK_FILE";
		public const int REQUEST_CODE_FILE_BROWSE_FOR_OPEN = 987321;
		public const int REQUEST_CODE_FILE_BROWSE_FOR_CREATE = 987322;
		public const int REQUEST_CODE_FILE_BROWSE_FOR_BINARY = 987323;
		public const int REQUEST_CODE_FILE_BROWSE_FOR_KEYFILE = 987324;

		public const String SHOW_NOTIFICATION = "keepass2android.show_notification";
	}

}


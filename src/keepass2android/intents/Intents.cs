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

namespace keepass2android
{
	/// <summary>
	/// Contains constants to be used in intents
	/// </summary>
	public class Intents 
	{
		

		/// <summary>Broadcast this intent to lock the database (with quick unlock if enabled)</summary>
		public const String LockDatabase = "keepass2android."+AppNames.PackagePart+".lock_database";
        /// <summary>Broadcast this intent to lock the database (with quick unlock if enabled) after some timeout occurred. As the locking is not triggered explicitly by the user, we expect to show the QuickUnlock dialog instead of leaving the app</summary>
		public const String LockDatabaseByTimeout = "keepass2android." + AppNames.PackagePart + ".lock_database_by_timeout";

        /// <summary>Broadcast this intent to close the database (no quick unlock, full close)</summary>
		public const String CloseDatabase = "keepass2android." + AppNames.PackagePart + ".close_database";
		
		/// <summary>This intent will be broadcast once the database has been locked. Sensitive information displayed should be hidden and unloaded.</summary>
		public const String DatabaseLocked = "keepass2android." + AppNames.PackagePart + ".database_locked";

		/// <summary>This intent will be broadcast once the keyboard data has been cleared</summary>
		public const String KeyboardCleared = "keepass2android." + AppNames.PackagePart + ".keyboard_cleared";

		public const String CopyUsername = "keepass2android.copy_username";
		public const String CopyPassword = "keepass2android.copy_password";
	    public const String CopyTotp = "keepass2android.copy_totp";
        public const String CheckKeyboard = "keepass2android.check_keyboard";

		public const String StartWithOtp = "keepass2android.startWithOtp";
		public const String OtpExtraKey = "keepass2android.Otp";
		
		public const String FileBrowse = "org.openintents.action.PICK_FILE";
		public const int RequestCodeFileBrowseForOpen = 37321;
		public const int RequestCodeFileBrowseForCreate = 37322;
		public const int RequestCodeFileBrowseForBinary = 37323;
		public const int RequestCodeFileBrowseForKeyfile = 37324;

		public const String ShowNotification = "keepass2android.show_notification";
		public const String UpdateKeyboard = "keepass2android.update_keyboard";
		public const String CopyStringToClipboard = "keepass2android.copy_string_to_clipboard";
		public const String ActivateKeyboard = "keepass2android.activate_keyboard";
		public const String ClearNotificationsAndData = "keepass2android.ClearNotificationsAndData";
	}

}


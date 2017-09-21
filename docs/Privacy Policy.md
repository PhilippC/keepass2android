# Who we are

Philipp Crocoll
Wallonenstr. 4
76297 Stutensee
Germany

is the author of Keepass2Android and Keepass2Android Offline.

# What data is collected?

The contents of your password database is yours and is never collected by us. Keepass2Android stores this data on a location chosen by the user and encrypted in the Keepass database format. The app author does not have any access, neither to the files nor the contents. Depending on the user's choice of the storage location, the files may be stored on third-party servers like Dropbox or Google Drive. 

Keepass2Android does not collect personal identifiable information. After unexpected errors or crashes of the app, the user may be asked if he/she whants to send an error report (Keepass2Android regular only). Error reports do not contain database contents, except (depending on the error message) UUIDs of entries. They may contain file paths if the error was related to a failed file operation. Error reports sent from inside the app are sent using Xamarin Insights.

The app author does not pass any of this data to third parties.

# What Android permissions are required?

* **Internet** (Keepass2Android regular only): Required to allow the user to read/store password databases or key files on remote locations, e.g. Dropbox or via WebDav.
* **Contacts/Accounts** (Keepass2Android regular only): Required by the Google Drive SDK. If you want to access files on Google Drive, you are prompted to select one of the Google Accounts on your phone to use. The permission is required to query the list of Google accounts on the device. Keepass2Android does not access your personal contacts.
* **Storage**: Required to allow the user to read/store password databases or key files on the device locally.
* **Fingerprint**: Required if you want to use fingerprint unlock.
* **Vibrate**: Required by the built-in keyboard (vibrate on key press)
* **Bind Accessibility service**: Required to provide the Auto-Fill accessibility service.


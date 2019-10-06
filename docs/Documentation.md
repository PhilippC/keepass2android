**Note:** This is an incomplete and preliminary documentation. More documentation will be added as requests come in or when the app is more feature stable. 
If you want, I'd be happy if you contribute texts for this place!

If you think something is missing in the documentation, please create an issue at https://github.com/PhilippC/keepass2android/issues

# What you should know and think about
If you store important information using Keepass2Android, you should know a little bit about what's going on:
* Keepass2Android stores your password in an encrypted file. It is *your responsibility* to backup this file regularly and safely.
* There is no way for anyone, including the app's author, to access the information stored in your password database without 
  * having the database file
  * knowing the master password (and additional second factor if you chose one)
  This means that **if you forget the master password, your database is lost**! So make sure you remember the password and retain any second factor method (if one is used).
* You might also want to think about:
  * What happens if I have an accident? Should any trusted person be able to access my database?
  * What happens if my phone gets lost or stolen? Do I know how to recover my database from a backup or the cloud?


# Getting started

## Opening an existing database
Many users are already using Keepass 2 on Windows and thus have their passwords stored in a Keepass database, typically a file with ending .kdbx. For opening such an existing database, there are two main options:
* You can open the file directly if it is located on a webserver or in the cloud. Use "Open Database" on the startscreen. By default, files from the cloud or servers are cached in the application's cache directory after loading them once. This allows to access your files even when you're offline.
* If you don't have your database stored on a webserver or in the cloud (or if you're using KP2A Offline) you need to copy your kdbx-Database to your phone. I suggest to use a sync tool like FolderSync. Such a tool copies your database to your local storage, so you always have it accessible. FolderSync can access your database if you have it on a network share or use any other common storage. 

## Creating a new database
Select "Create new database" from the start screen. Tap the integrated help icons for more information. Note that by default, the database is created as a local file. Please consider making backups regularly or select a location in the cloud.

## Getting passwords into password fields
There are many ways how to enter the passwords from your database in the corresponding fields. By default, the clipboard as well as the KP2A keyboard are activated in the settings:
* The KP2A keyboard is the recommended way because it's safe against clipboard loggers: Whenever you select an entry, the KP2A keyboard notification will appear in the notification bar. Click it to activate the keyboard. (The first time you do this, you are required to enable the keyboard in the system settings. This must be done by the user for Android security reasons.) As soon as it's activated, you can tap a field where you want to enter data from the selected entry. The KP2A keyboard will come up. Click the KP2A key (on the bottom left) to select whether you want to enter Username/password etc. When you're done, click the Keyboard key (next to the KP2A key) to switch back to your favorite keyboard.
* You can enable the Keepass2Android Autofill service in the system's Autofill settings (Android 8+) which allows to fill data using Android's accessibility system. This works with many apps including Firefox browser but is not supported for Chrome (when writing this).
* The clipboard based approach can be used as well: Pull the notification bar down and select "Copy username/password to clipboard". Then long-tap the field where you want to paste the data. A small "paste" button should come up. Note, however, that information in the clipboard can be monitored by all apps on your device and clearing the clipboard is not always possible.

These options can be used in different workflows:
### Browser-based workflow
If you are browsing the web and need to enter crendentials for a webpage, a simple and powerful workflow is to use the "Share URL" option from the browser's menu. Then select Keepass2Android (or KP2A Offline). Open your database (if it's not already opened) and select the entry you want to enter (if KP2A did not already select the appropriate entry). Use the built-in keyboard or the clipboard to enter the password.
### Autofill service based workflow
If you have enabled the autofill service and open a (supported) app with a password field, a dropdown appears. Select "Fill with Keepass2Android" to select the appropriate entry. When you return to the app, the password and user field should be filled already.
### KP2A based workflow for websites
Open KP2A, open your database, select your entry (in this step, the notification bar items should show up already). Now click the URL link of the entry to open a browser window with the website. Use one of the methods described above to enter the credentials. 
### KP2A Keyboard based workflow
When you are in a text field, you can use the Android icon in the notification bar to switch to the KP2A keyboard. Hit the KP2A key to select an icon. After it's selected, hit the KP2A key again to enter the desired field.

## Creating a new account
Assume you want to create an account on a website. If you do not have a database yet, see above. As soon as you have a database, you may proceed as follows:
* Go to the website you want to create the account for
* Select Share/Share URL from the browser's menu and tap "Keepass2Android"
* Log in to your database (if it's not already unlocked)
* You will see the search result screen with "No search results"
* Tap "Create entry for URL"
* Choose the desired group, then tap the "+"-button to add an entry.
* Tap the "..." button next to the password field to launch the password generator, create your password and then select "Accept"
* Enter a name for the entry
* Enter the username you want to use for the entry
* Tap "Save" on the top
* You should see notifications like "Entry is available through KP2A keyboard" and/or  "Copy username/password to clipboard". If not, view the new entry by clicking it.
* Return back to the browser.
* Use the notifications to enter your new credentials. See "Getting passwords into the password fields" for more details.
* If the user name you entered is not available or valid, choose a different one but copy it to clipboard. After creating the account, don't forget to update the new entry.

# Keepass2Android vs Keepass2Android Offline vs Keepassdroid
What's the difference between these apps? There is a short comparison on [Comparison of Keepass apps for Android](Comparison-of-Keepass-apps-for-Android.md) to help you pick the best for you!

# Advanced topics
## YubiKey NEO support for One-Time-Passwords
Please see the [How to use Keepass2Android with YubiKey NEO](How-to-use-Keepass2Android-with-YubiKey-NEO.md) page.

## Advanced usage of the Keepass2Android keyboard
Please see the [Advanced usage of the Keepass2Android keyboard](Advanced-usage-of-the-Keepass2Android-keyboard.md) page.

# FAQ

## Should I use the KP2A keyboard for entering passwords?
The KP2A keyboard is meant to quickly "paste" or "type" values from your database to any text fields by using the KP2A icon. The QUERTY keyboard is just for convenience (if you just have the KP2A keyboard activated and need to enter a few letters). However, every other (trustworthy) keyboard is ok as well to enter sensitive information: Keyboard's aren't unsafe in Android. Only the clipboard is. Thus, the KP2A keyboard allows to get information out of the database without using the clipboard.
**You can use any keyboard when you enter the main database password**

## Is it safe to store my kdbx file in the cloud?
While it may happen that someone gets access to your kdbx file in the cloud, there is still no need to worry: the purpose of encryption is to protect the data even in case someone gets the kdbx file! As long as you are using a safe master key, you're safe! [Key files](https://keepass.info/help/base/keys.html#keyfiles) can help with securing the database even more. 

## Doesn't Keepass2Android create automatic backups?
Yes and no. Yes: Keepass2Android stores the last successfully opened file as a read-only backup locally on the phone (unless you disable this is in the settings). This should make sure that even if the file gets destroyed during a save operation or gets deleted by accident, you should always have a version that can be opened. (Don't mix this up with the internal file cache which is not meant as a backup and can easily be overwritten even with a corrupt file. This internal file cache is meant for providing writable access even when the original file is not reachable, e.g. when you're offline.)
No: The local backup has two shortcomings: It is only one backup and does not allow to revert to older versions. So if you deleted an entry from the database, it might be deleted in the local backup soon as well. The even more important shortcoming is that it is just a local backup. It won't help when your phone gets lost or broken. Please create additional backups on seperate storage!

## How do I backup the database?
If you have stored your database on the cloud, you might rely on your cloud storage providers backups. Make sure they allow you to revert to older revisions in case the file gets corrupted for some reason.
If you are working with a local database file, make sure you create regular backups. I suggest you have an aumotated mechanism, e.g. with FolderSync (Lite) which can copy local files from your device to other locations, e.g. your PC in a local network. You can also use USB or tools like MyPhoneExploror to transfer data to your PC. Or, you use a removable storage like an SD card which you keep in a safe place after making the backup. 
In all cases, you need to verify that your backup is readable! It's even best to test this on another device (e.g. a PC), so you simulate the case that you may lose your phone.

## I can open my database with fingerprint, but don't remember my master password!
It's time for action! As soon as possible, select Settings - Database - Export and choose unencrypted XML (don't put this on the cloud but on a local file). Transfer this file to a PC and import it to a new kdbx file, e.g. with Keepass2. Choose a new master password and make sure you don't forget this password! 

## How can I transfer data from one device to another?
  * If you are about to get a new Android device, you should make sure you're not losing your passwords in the transition! The first thing you need to make sure is that you can access your .kdbx file (which stores the passwords) on the new device. If it is already stored in the cloud, you only need to make sure you know how to setup the cloud storage on the new device (it might require a password, so make sure you have access to that!). 
  * If the .kdbx-file is stored locally on the old device, make sure you have an up-to-date backup (see above). You can then transfer that backup copy to the new device. (Note: transferring via USB causes data corruption in some cases, use MyPhoneExplorer or similar tools to be sure this does not happen.)
  * If you are securing your password database with a keyfile, also transfer this key file to the new device.
  * If you are opening your database with a fingerprint, make sure you also know the master password because fingerprint will not be available immediately on the new device.

## Why is Keepass2Android's apk so big?
Please see [Keepass2Android Apk](Keepass2Android-Apk.md) for more information.

# For developers
If you are interested in adding new features, you have two options:
Either your features can be implemented as a plug-in. Please see [How to create a plug-in?](How-to-create-a-plug-in_.md) for more information. Or you add the features directly in the source code of the projects and create a pull request.

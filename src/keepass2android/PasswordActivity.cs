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
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.Net;
using Android.Preferences;
using Java.IO;
using Android.Text;
using Android.Content.PM;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using keepass2android.Io;
using MemoryStream = System.IO.MemoryStream;

namespace keepass2android
{
	[Activity (Label = "@string/app_name", 
	           ConfigurationChanges=ConfigChanges.Orientation|ConfigChanges.KeyboardHidden, 
	           Theme="@style/Base")]

	public class PasswordActivity : LockingActivity {
		bool _showPassword;

		public const String KeyDefaultFilename = "defaultFileName";

		public const String KeyFilename = "fileName";
		private const String KeyKeyfile = "keyFile";
		public const String KeyServerusername = "serverCredUser";
		public const String KeyServerpassword = "serverCredPwd";
		public const String KeyServercredmode = "serverCredRememberMode";

		private const String ViewIntent = "android.intent.action.VIEW";
		private const string ShowpasswordKey = "ShowPassword";

		private Task<MemoryStream> _loadDbTask;
		private IOConnectionInfo _ioConnection;
		private String _keyFile;
		private bool _rememberKeyfile;
		ISharedPreferences _prefs;

		private bool _starting;

		public PasswordActivity (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}

		public PasswordActivity()
		{

		}


		public static void PutIoConnectionToIntent(IOConnectionInfo ioc, Intent i)
		{
			i.PutExtra(KeyFilename, ioc.Path);
			i.PutExtra(KeyServerusername, ioc.UserName);
			i.PutExtra(KeyServerpassword, ioc.Password);
			i.PutExtra(KeyServercredmode, (int)ioc.CredSaveMode);
		}
		
		public static void SetIoConnectionFromIntent(IOConnectionInfo ioc, Intent i)
		{
			ioc.Path = i.GetStringExtra(KeyFilename);
			ioc.UserName = i.GetStringExtra(KeyServerusername) ?? "";
			ioc.Password = i.GetStringExtra(KeyServerpassword) ?? "";
			ioc.CredSaveMode  = (IOCredSaveMode)i.GetIntExtra(KeyServercredmode, (int) IOCredSaveMode.NoSave);
		}

		public static void Launch(Activity act, String fileName, AppTask appTask)  {
			File dbFile = new File(fileName);
			if ( ! dbFile.Exists() ) {
				throw new FileNotFoundException();
			}
	
			
			Intent i = new Intent(act, typeof(PasswordActivity));
			i.PutExtra(KeyFilename, fileName);
			appTask.ToIntent(i);
			act.StartActivityForResult(i, 0);
			
		}
		

		public static void Launch(Activity act, IOConnectionInfo ioc, AppTask appTask)
		{
			if (ioc.IsLocalFile())
			{
				Launch(act, ioc.Path, appTask);
				return;
			}

			Intent i = new Intent(act, typeof(PasswordActivity));
			PutIoConnectionToIntent(ioc, i);

			appTask.ToIntent(i);

			act.StartActivityForResult(i, 0);
			
		}

		public void LaunchNextActivity()
		{
			AppTask.AfterUnlockDatabase(this);
		}

		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);

			Kp2aLog.Log("PasswordActivity.OnActivityResult "+resultCode+"/"+requestCode);

			//NOTE: original code from k eepassdroid used switch ((Android.App.Result)requestCode) { (but doesn't work here, although k eepassdroid works)
			switch(resultCode) {

				case KeePass.ExitNormal: // Returned to this screen using the Back key, treat as locking the database
					App.Kp2a.LockDatabase();
					break;
				case KeePass.ExitLock:
					// The database has already been locked, and the quick unlock screen will be shown if appropriate
					break;
				case KeePass.ExitCloseAfterTaskComplete:
					// Do not lock the database
					SetResult(KeePass.ExitCloseAfterTaskComplete);
					Finish();
					break;
				case KeePass.ExitClose:
					SetResult(KeePass.ExitClose);
					Finish();
					break;
				case KeePass.ExitReloadDb:
					//if the activity was killed, fill password/keyfile so the user can directly hit load again
					if (App.Kp2a.GetDb().Loaded)
					{
						if (App.Kp2a.GetDb().KpDatabase.MasterKey.ContainsType(typeof(KcpPassword)))
						{

							KcpPassword kcpPassword = (KcpPassword)App.Kp2a.GetDb().KpDatabase.MasterKey.GetUserKey(typeof(KcpPassword));
							String password = kcpPassword.Password.ReadString();

							SetEditText(Resource.Id.password, password);
						
						}
						if (App.Kp2a.GetDb().KpDatabase.MasterKey.ContainsType(typeof(KcpKeyFile)))
						{
							
							KcpKeyFile kcpKeyfile = (KcpKeyFile)App.Kp2a.GetDb().KpDatabase.MasterKey.GetUserKey(typeof(KcpKeyFile));

							SetEditText(Resource.Id.pass_keyfile, kcpKeyfile.Path);
						}
					}
					App.Kp2a.LockDatabase(false);
					break;
				case Result.Ok: // Key file browse dialog OK'ed.
					if (requestCode == Intents.RequestCodeFileBrowseForKeyfile) {
						string filename = Util.IntentToFilename(data, this);
						if (filename != null) {
							if (filename.StartsWith("file://")) {
								filename = filename.Substring(7);
							}
							
							filename = URLDecoder.Decode(filename);
							
							EditText fn = (EditText) FindViewById(Resource.Id.pass_keyfile);
							fn.Text = filename;
						}
					}
					break;
				case (Result)FileStorageResults.FileUsagePrepared:
					PeformLoadDatabase();
					break;
			}
			
		}

		internal AppTask AppTask;
		
		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			if (savedInstanceState != null)
				_showPassword = savedInstanceState.GetBoolean(ShowpasswordKey, false);
			
			Intent i = Intent;
			String action = i.Action;
			
			_prefs = PreferenceManager.GetDefaultSharedPreferences(this);
			_rememberKeyfile = _prefs.GetBoolean(GetString(Resource.String.keyfile_key), Resources.GetBoolean(Resource.Boolean.keyfile_default));

			_ioConnection = new IOConnectionInfo();


			if (action != null && action.Equals(ViewIntent))
			{
				_ioConnection.Path = i.DataString;
				
				if (! _ioConnection.Path.Substring(0, 7).Equals("file://"))
				{
					//TODO: this might no longer be required as we can handle http(s) and ftp as well (but we need server credentials therefore)
					Toast.MakeText(this, Resource.String.error_can_not_handle_uri, ToastLength.Long).Show();
					Finish();
					return;
				}

				_ioConnection.Path = URLDecoder.Decode(_ioConnection.Path.Substring(7));
				
				if (_ioConnection.Path.Length == 0)
				{
					// No file name
					Toast.MakeText(this, Resource.String.FileNotFound, ToastLength.Long).Show();
					Finish();
					return;
				}

				File dbFile = new File(_ioConnection.Path);
				if (! dbFile.Exists())
				{
					// File does not exist
					Toast.MakeText(this, Resource.String.FileNotFound, ToastLength.Long).Show();
					Finish();
					return;
				}
				
				_keyFile = GetKeyFile(_ioConnection.Path);
				
			} else
			{
				SetIoConnectionFromIntent(_ioConnection, i);
				_keyFile = i.GetStringExtra(KeyKeyfile);
				if (string.IsNullOrEmpty(_keyFile))
				{
					_keyFile = GetKeyFile(_ioConnection.Path);
				}
			}

			if (App.Kp2a.GetDb().Loaded && App.Kp2a.GetDb().Ioc != null &&
				App.Kp2a.GetDb().Ioc.GetDisplayName() != _ioConnection.GetDisplayName())
			{
				// A different database is currently loaded, unload it before loading the new one requested
				App.Kp2a.LockDatabase(false);
			}

			AppTask = AppTask.GetTaskInOnCreate(savedInstanceState, Intent);
			
			SetContentView(Resource.Layout.password);
			PopulateView();

			EditText passwordEdit = FindViewById<EditText>(Resource.Id.password);


			passwordEdit.RequestFocus();
			Window.SetSoftInputMode(SoftInput.StateVisible);

			Button confirmButton = (Button)FindViewById(Resource.Id.pass_ok);
			confirmButton.Click += (sender, e) =>
				{
					App.Kp2a.GetFileStorage(_ioConnection)
					   .PrepareFileUsage(new FileStorageSetupInitiatorActivity(this, OnActivityResult), _ioConnection, 0);
				};
			
			/*CheckBox checkBox = (CheckBox) FindViewById(Resource.Id.show_password);
			// Show or hide password
			checkBox.CheckedChange += delegate(object sender, CompoundButton.CheckedChangeEventArgs e) {

				TextView password = (TextView) FindViewById(Resource.Id.password);
				if ( e.IsChecked ) {
					password.InputType = InputTypes.ClassText | InputTypes.TextVariationVisiblePassword;
				} else {
					password.InputType = InputTypes.ClassText | InputTypes.TextVariationPassword;
				}
			};
			*/
			ImageButton btnTogglePassword = (ImageButton)FindViewById(Resource.Id.toggle_password);
			btnTogglePassword.Click += (sender, e) =>
				{
					_showPassword = !_showPassword;
					MakePasswordMaskedOrVisible();
				};
			
			
			
			ImageButton browse = (ImageButton)FindViewById(Resource.Id.browse_button);
			browse.Click += (sender, evt) => 
			{
				string filename = null;
				if (!String.IsNullOrEmpty(_ioConnection.Path))
				{
					File keyfile = new File(_ioConnection.Path);
					File parent = keyfile.ParentFile;
					if (parent != null)
					{
						filename = parent.AbsolutePath;
					}
				}
				Util.ShowBrowseDialog(filename, this, Intents.RequestCodeFileBrowseForKeyfile, false);

			};
			
			RetrieveSettings();
		}

		private void PeformLoadDatabase()
		{
			String pass = GetEditText(Resource.Id.password);
			String key = GetEditText(Resource.Id.pass_keyfile);
			if (pass.Length == 0 && key.Length == 0)
			{
				ErrorMessage(Resource.String.error_nopass);
				return;
			}

			CheckBox cbQuickUnlock = (CheckBox) FindViewById(Resource.Id.enable_quickunlock);
			App.Kp2a.SetQuickUnlockEnabled(cbQuickUnlock.Checked);

			//avoid password being visible while loading:
			_showPassword = false;
			MakePasswordMaskedOrVisible();

			Handler handler = new Handler();
			LoadDb task = new LoadDb(App.Kp2a, _ioConnection, _loadDbTask, pass, key, new AfterLoad(handler, this));
			_loadDbTask = null; // prevent accidental re-use

			SetNewDefaultFile();

			new ProgressTask(App.Kp2a, this, task).Run();
		}

		private void MakePasswordMaskedOrVisible()
		{
			TextView password = (TextView) FindViewById(Resource.Id.password);
			if (_showPassword)
			{
				password.InputType = InputTypes.ClassText | InputTypes.TextVariationVisiblePassword;
			}
			else
			{
				password.InputType = InputTypes.ClassText | InputTypes.TextVariationPassword;
			}
		}

		private void SetNewDefaultFile()
		{
//Don't allow the current file to be the default if we don't have stored credentials
			bool makeFileDefault;
			if ((_ioConnection.IsLocalFile() == false) && (_ioConnection.CredSaveMode != IOCredSaveMode.SaveCred))
			{
				makeFileDefault = false;
			}
			else
			{
				makeFileDefault = true;
			}
			String newDefaultFileName;

			if (makeFileDefault)
			{
				newDefaultFileName = _ioConnection.Path;
			}
			else
			{
				newDefaultFileName = "";
			}

			ISharedPreferencesEditor editor = _prefs.Edit();
			editor.PutString(KeyDefaultFilename, newDefaultFileName);
			EditorCompat.Apply(editor);
		}

		protected override void OnStart()
		{
			base.OnStart();
			_starting = true;

			ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(this);

			long usageCount = prefs.GetLong(GetString(Resource.String.UsageCount_key), 0);

			if ((DateTime.Now > new DateTime(2013, 09, 21))
			    && (DateTime.Now < new DateTime(2013, 10, 07))
				&& (usageCount > 5)
				)
			{
				const string donationOkt2013Key = "HasAskedForDonationOktoberfest2013";
				if (prefs.GetBoolean(donationOkt2013Key, false) == false)
				{
					ISharedPreferencesEditor edit = prefs.Edit();
					edit.PutBoolean(donationOkt2013Key, true);
					EditorCompat.Apply(edit);

					StartActivity(new Intent(this, typeof(DonateReminder)));
				}	
			}
			
		}

		private MemoryStream LoadDbFile()
		{
			if (KdbpFile.GetFormatToUse(_ioConnection) == KdbxFormat.ProtocolBuffers)
			{
				Kp2aLog.Log("Preparing kdbp serializer");				
				KdbpFile.PrepareSerializer();
			}

			Kp2aLog.Log("Pre-loading database file starting");
			var fileStorage = App.Kp2a.GetFileStorage(_ioConnection);
			var stream = fileStorage.OpenFileForRead(_ioConnection);

			var memoryStream = stream as MemoryStream;
			if (memoryStream == null)
			{
				// Read the file into memory
				int capacity = 4096; // Default initial capacity, if stream can't report it.
				if (stream.CanSeek)
				{
					capacity = (int)stream.Length;
				}
				memoryStream = new MemoryStream(capacity);
				stream.CopyTo(memoryStream);
				stream.Close();
				memoryStream.Seek(0, System.IO.SeekOrigin.Begin);
			}

			Kp2aLog.Log("Pre-loading database file completed");

			return memoryStream;
		}

		protected override void OnSaveInstanceState(Bundle outState)
		{
			base.OnSaveInstanceState(outState);
			AppTask.ToBundle(outState);
			outState.PutBoolean(ShowpasswordKey, _showPassword);
		}
		
		protected override void OnResume()
		{
			base.OnResume();

			MakePasswordMaskedOrVisible();

			// OnResume is run every time the activity comes to the foreground. This code should only run when the activity is started (OnStart), but must
			// be run in OnResume rather than OnStart so that it always occurrs after OnActivityResult (when re-creating a killed activity, OnStart occurs before OnActivityResult)
			if (_starting && !IsFinishing)  //use !IsFinishing to make sure we're not starting another activity when we're already finishing (e.g. due to TaskComplete in OnActivityResult)
			{
				_starting = false;
				if (App.Kp2a.DatabaseIsUnlocked)
				{
					LaunchNextActivity();
				}
				else if (App.Kp2a.QuickUnlockEnabled && App.Kp2a.QuickLocked)
				{
					var i = new Intent(this, typeof(QuickUnlock));
					PutIoConnectionToIntent(_ioConnection, i);
					Kp2aLog.Log("Starting QuickUnlock");
					StartActivityForResult(i, 0);
				}
				else 
				{
					//database not yet loaded.

					//check if FileStorage setup is all done. Usually this should not occur here because the setup is
					//performed in FileSelectActivity, but e.g. if the user unlinks from Dropbox saving might fail and 
					//the user is returned here.
					if (App.Kp2a.GetFileStorage(_ioConnection).RequiresSetup(_ioConnection))
					{
						GoToFileSelectActivity();
					}
					//check if pre-loading is enabled but wasn't started yet:
					else if (_loadDbTask == null && _prefs.GetBoolean(GetString(Resource.String.PreloadDatabaseEnabled_key), true))
					{
						// Create task to kick off file loading while the user enters the password
						_loadDbTask = Task.Factory.StartNew<MemoryStream>(LoadDbFile);
					}
				}
			}
		}
		
		private void RetrieveSettings() {
			CheckBox cbQuickUnlock = (CheckBox)FindViewById(Resource.Id.enable_quickunlock);
			cbQuickUnlock.Checked = _prefs.GetBoolean(GetString(Resource.String.QuickUnlockDefaultEnabled_key), true);
		}
		
		private String GetKeyFile(String filename) {
			if ( _rememberKeyfile ) {
                FileDbHelper dbHelp = App.Kp2a.FileDbHelper;
				
				String keyfile = dbHelp.GetFileByName(filename);
				
				return keyfile;
			} else {
				return "";
			}
		}
		
		private void PopulateView() {
			SetEditText(Resource.Id.filename, _ioConnection.GetDisplayName());
			SetEditText(Resource.Id.pass_keyfile, _keyFile);
		}
		
		/*
	private void errorMessage(CharSequence text)
	{
		Toast.MakeText(this, text, ToastLength.Long).Show();
	}
	*/
		
		private void ErrorMessage(int resId)
		{
			Toast.MakeText(this, resId, ToastLength.Long).Show();
		}
	
		private String GetEditText(int resId) {
			return Util.GetEditText(this, resId);
		}
		
		private void SetEditText(int resId, String str) {
			TextView te =  (TextView) FindViewById(resId);
			//assert(te == null);
			
			if (te != null) {
				te.Text = str;
			}
		}

		public override bool OnCreateOptionsMenu(IMenu menu) {
			base.OnCreateOptionsMenu(menu);
			
			MenuInflater inflate = MenuInflater;
			inflate.Inflate(Resource.Menu.password, menu);
			
			return true;
		}
		
		public override bool OnOptionsItemSelected(IMenuItem item) {
			switch ( item.ItemId ) {
				case Resource.Id.menu_about:
					AboutDialog dialog = new AboutDialog(this);
					dialog.Show();
					return true;
				
				case Resource.Id.menu_app_settings:
					AppSettingsActivity.Launch(this);
					return true;

				case Resource.Id.menu_change_db:
					GoToFileSelectActivity();
					return true;
			}
			
			return base.OnOptionsItemSelected(item);
		}

		private void GoToFileSelectActivity()
		{
			Intent intent = new Intent(this, typeof (FileSelectActivity));
			intent.PutExtra(FileSelectActivity.NoForwardToPasswordActivity, true);
			AppTask.ToIntent(intent);
			StartActivityForResult(intent, 0);
			Finish();
		}

		private class AfterLoad : OnFinish {
			readonly PasswordActivity _act;

			public AfterLoad(Handler handler, PasswordActivity act):base(handler)
			{
				_act = act;
			}


			public override void Run() {
				if ( Success ) 
				{
					_act.SetEditText(Resource.Id.password, "");

					_act.LaunchNextActivity();

					GC.Collect(); // Ensure temporary memory used while loading is collected - it will contain sensitive data such as username and password, and also the large data of the encrypted database file
				} 
				else
				{
					DisplayMessage(_act);
				}
			}
		}
		
	}

}


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
using Java.Net;
using Android.Preferences;
using Java.IO;
using Android.Text;
using Android.Content.PM;
using Android.Text.Method;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using Android.Views.InputMethods;

namespace keepass2android
{
	[Activity (Label = "@string/app_name", 
	           ConfigurationChanges=ConfigChanges.Orientation|ConfigChanges.KeyboardHidden, 
	           Theme="@style/Base")]

	public class PasswordActivity : LockingActivity {
		bool mShowPassword = false;

		public const String KEY_DEFAULT_FILENAME = "defaultFileName";

		public const String KEY_FILENAME = "fileName";
		private const String KEY_KEYFILE = "keyFile";
		private const String KEY_SERVERUSERNAME = "serverCredUser";
		private const String KEY_SERVERPASSWORD = "serverCredPwd";
		private const String KEY_SERVERCREDMODE = "serverCredRememberMode";

		private const String VIEW_INTENT = "android.intent.action.VIEW";
	
		private IOConnectionInfo mIoConnection;
		private String mKeyFile;
		private bool mRememberKeyfile;
		ISharedPreferences prefs;

		public PasswordActivity (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}

		public PasswordActivity()
		{

		}


		static void PutIoConnectionToIntent(IOConnectionInfo ioc, Android.Content.Intent i)
		{
			i.PutExtra(KEY_FILENAME, ioc.Path);
			i.PutExtra(KEY_SERVERUSERNAME, ioc.UserName);
			i.PutExtra(KEY_SERVERPASSWORD, ioc.Password);
			i.PutExtra(KEY_SERVERCREDMODE, (int)ioc.CredSaveMode);
		}
		
		public static void SetIoConnectionFromIntent(IOConnectionInfo ioc, Intent i)
		{
			ioc.Path = i.GetStringExtra(KEY_FILENAME);
			ioc.UserName = i.GetStringExtra(KEY_SERVERUSERNAME) ?? "";
			ioc.Password = i.GetStringExtra(KEY_SERVERPASSWORD) ?? "";
			ioc.CredSaveMode  = (IOCredSaveMode)i.GetIntExtra(KEY_SERVERCREDMODE, (int) IOCredSaveMode.NoSave);
		}

		public static void Launch(Activity act, String fileName, AppTask appTask)  {
			Java.IO.File dbFile = new Java.IO.File(fileName);
			if ( ! dbFile.Exists() ) {
				throw new Java.IO.FileNotFoundException();
			}
	
			
			Intent i = new Intent(act, typeof(PasswordActivity));
			i.PutExtra(KEY_FILENAME, fileName);
			appTask.ToIntent(i);
			act.StartActivityForResult(i, 0);
			
		}
		

		public static void Launch(Activity act, String fileName)  {
			Launch(act, IOConnectionInfo.FromPath(fileName), null);
			
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
			mAppTask.AfterUnlockDatabase(this);

		}

		void unloadDatabase()
		{
			App.getDB().Clear();
			StopService(new Intent(this, typeof(QuickUnlockForegroundService)));
		}

		void lockDatabase()
		{
			SetResult(KeePass.EXIT_LOCK);
			setEditText(Resource.Id.password, "");
			if (App.getDB().QuickUnlockEnabled)
				App.getDB().Locked = true;
			else
			{
				unloadDatabase();
			}
		}

		void lockAndClose()
		{
			lockDatabase();
			Finish();

		}

		bool tryStartQuickUnlock()
		{
			if (!App.getDB().QuickUnlockEnabled)
				return false;

			if (App.getDB().pm.MasterKey.ContainsType(typeof(KcpPassword)) == false)
				return false;
			KcpPassword kcpPassword = (KcpPassword)App.getDB().pm.MasterKey.GetUserKey(typeof(KcpPassword));
			String password = kcpPassword.Password.ReadString();

			if (password.Length == 0)
				return false;

			App.getDB().Locked = true;

			Intent i = new Intent(this, typeof(QuickUnlock));
			PutIoConnectionToIntent(mIoConnection, i);
			Android.Util.Log.Debug("DEBUG","Starting QuickUnlock");
			StartActivityForResult(i,0);
			return true;
		}

		public void StartQuickUnlockForegroundService()
		{
			if (App.getDB().QuickUnlockEnabled)
			{
				StartService(new Intent(this, typeof(QuickUnlockForegroundService)));
			}
		}

		bool startedWithActivityResult = false;

		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);

			startedWithActivityResult = true;
			Android.Util.Log.Debug("DEBUG","PasswordActivity.OnActivityResult "+resultCode+"/"+requestCode);

			if (resultCode != KeePass.EXIT_CLOSE_AFTER_TASK_COMPLETE)
			{
				//Stop service when app activity is left
				StopService(new Intent(this, typeof(CopyToClipboardService)));
			}

			//NOTE: original code from k eepassdroid used switch ((Android.App.Result)requestCode) { (but doesn't work here, although k eepassdroid works)
			switch(resultCode) {
				
				case KeePass.EXIT_NORMAL:
					if (!tryStartQuickUnlock())
					{
						setEditText(Resource.Id.password, "");
						;
					}
					break;
					
				case KeePass.EXIT_LOCK:
					if (!tryStartQuickUnlock())
					{
						lockAndClose();
					}
					break;
				case KeePass.EXIT_FORCE_LOCK:
					setEditText(Resource.Id.password, "");
					unloadDatabase();
					break;
				case KeePass.EXIT_FORCE_LOCK_AND_CHANGE_DB:
					unloadDatabase();
					Finish();
					break;
				case KeePass.EXIT_CHANGE_DB:
					lockAndClose();
					break;
				case KeePass.EXIT_CLOSE_AFTER_TASK_COMPLETE:
					SetResult(KeePass.EXIT_CLOSE_AFTER_TASK_COMPLETE);
					Finish();
					break;
				case KeePass.EXIT_QUICK_UNLOCK:
					App.getDB().Locked = false;
					LaunchNextActivity();
					break;
				case KeePass.EXIT_RELOAD_DB:
					//if the activity was killed, fill password/keyfile so the user can directly hit load again
					if (App.getDB().Loaded)
					{
						if (App.getDB().pm.MasterKey.ContainsType(typeof(KcpPassword)))
						{

							KcpPassword kcpPassword = (KcpPassword)App.getDB().pm.MasterKey.GetUserKey(typeof(KcpPassword));
							String password = kcpPassword.Password.ReadString();

							setEditText(Resource.Id.password, password);
						
						}
						if (App.getDB().pm.MasterKey.ContainsType(typeof(KcpKeyFile)))
						{
							
							KcpKeyFile kcpKeyfile = (KcpKeyFile)App.getDB().pm.MasterKey.GetUserKey(typeof(KcpKeyFile));

							setEditText(Resource.Id.pass_keyfile, kcpKeyfile.Path);
						}
					}
					unloadDatabase();
					break;
				case Android.App.Result.Ok:
					if (requestCode == Intents.REQUEST_CODE_FILE_BROWSE_FOR_KEYFILE) {
						string filename = Util.IntentToFilename(data);
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
			}
			
		}

		internal AppTask mAppTask;
		
		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			
			Intent i = Intent;
			String action = i.Action;
			
			prefs = PreferenceManager.GetDefaultSharedPreferences(this);
			mRememberKeyfile = prefs.GetBoolean(GetString(Resource.String.keyfile_key), Resources.GetBoolean(Resource.Boolean.keyfile_default));

			mIoConnection = new IOConnectionInfo();


			if (action != null && action.Equals(VIEW_INTENT))
			{
				mIoConnection.Path = i.DataString;
				
				if (! mIoConnection.Path.Substring(0, 7).Equals("file://"))
				{
					//TODO: this might no longer be required as we can handle http(s) and ftp as well (but we need server credentials therefore)
					Toast.MakeText(this, Resource.String.error_can_not_handle_uri, ToastLength.Long).Show();
					Finish();
					return;
				}

				mIoConnection.Path = URLDecoder.Decode(mIoConnection.Path.Substring(7));
				
				if (mIoConnection.Path.Length == 0)
				{
					// No file name
					Toast.MakeText(this, Resource.String.FileNotFound, ToastLength.Long).Show();
					Finish();
					return;
				}

				File dbFile = new File(mIoConnection.Path);
				if (! dbFile.Exists())
				{
					// File does not exist
					Toast.MakeText(this, Resource.String.FileNotFound, ToastLength.Long).Show();
					Finish();
					return;
				}
				
				mKeyFile = getKeyFile(mIoConnection.Path);
				
			} else
			{
				SetIoConnectionFromIntent(mIoConnection, i);
				mKeyFile = i.GetStringExtra(KEY_KEYFILE);
				if (mKeyFile == null || mKeyFile.Length == 0)
				{
					mKeyFile = getKeyFile(mIoConnection.Path);
				}
			}

			mAppTask = AppTask.GetTaskInOnCreate(savedInstanceState, Intent);
			
			SetContentView(Resource.Layout.password);
			populateView();

			EditText passwordEdit = FindViewById<EditText>(Resource.Id.password);


			passwordEdit.RequestFocus();
			Window.SetSoftInputMode(SoftInput.StateVisible);

			Button confirmButton = (Button)FindViewById(Resource.Id.pass_ok);
			confirmButton.Click += (object sender, EventArgs e) => 
			{
				String pass = GetEditText(Resource.Id.password);
				String key = GetEditText(Resource.Id.pass_keyfile);
				if (pass.Length == 0 && key.Length == 0)
				{
					errorMessage(Resource.String.error_nopass);
					return;
				}
				
				String fileName = GetEditText(Resource.Id.filename);
				
				
				// Clear before we load
				unloadDatabase();
				
				// Clear the shutdown flag
				App.clearShutdown();

				CheckBox cbQuickUnlock = (CheckBox)FindViewById(Resource.Id.enable_quickunlock);
				App.getDB().QuickUnlockEnabled = cbQuickUnlock.Checked;
				App.getDB().QuickUnlockKeyLength = int.Parse(prefs.GetString(GetString(Resource.String.QuickUnlockLength_key), GetString(Resource.String.QuickUnlockLength_default)));
				
				Handler handler = new Handler();
				LoadDB task = new LoadDB(App.getDB(), this, mIoConnection, pass, key, new AfterLoad(handler, this));
				ProgressTask pt = new ProgressTask(this, task, Resource.String.loading_database);
				pt.run();
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
			btnTogglePassword.Click += (object sender, EventArgs e) => {
				mShowPassword = !mShowPassword;
				TextView password = (TextView)FindViewById(Resource.Id.password);
				if (mShowPassword)
				{
					password.InputType = InputTypes.ClassText | InputTypes.TextVariationVisiblePassword;
				} else
				{
					password.InputType = InputTypes.ClassText | InputTypes.TextVariationPassword;
				}
			};
			
			CheckBox defaultCheck = (CheckBox)FindViewById(Resource.Id.default_database);
			///Don't allow the current file to be the default if we don't have stored credentials
			if ((mIoConnection.IsLocalFile() == false) && (mIoConnection.CredSaveMode != IOCredSaveMode.SaveCred))
			{
				defaultCheck.Enabled = false;
			} else
			{
				defaultCheck.Enabled = true;
			}
			defaultCheck.CheckedChange += (object sender, CompoundButton.CheckedChangeEventArgs e) => 
			{
				String newDefaultFileName;
				
				if (e.IsChecked)
				{
					newDefaultFileName = mIoConnection.Path;
				} else
				{
					newDefaultFileName = "";
				}
				
				ISharedPreferencesEditor editor = prefs.Edit();
				editor.PutString(KEY_DEFAULT_FILENAME, newDefaultFileName);
				EditorCompat.apply(editor);
			};
			
			ImageButton browse = (ImageButton)FindViewById(Resource.Id.browse_button);
			browse.Click += (object sender, EventArgs evt) => 
			{
				string filename = null;
				if (!String.IsNullOrEmpty(mIoConnection.Path))
				{
					File keyfile = new File(mIoConnection.Path);
					File parent = keyfile.ParentFile;
					if (parent != null)
					{
						filename = parent.AbsolutePath;
					}
				}
				Util.showBrowseDialog(filename, this, Intents.REQUEST_CODE_FILE_BROWSE_FOR_KEYFILE, false);

			};
			
			retrieveSettings();


		}

		protected override void OnSaveInstanceState(Bundle outState)
		{
			base.OnSaveInstanceState(outState);
			mAppTask.ToBundle(outState);
		}
		
		protected override void OnResume() {
			base.OnResume();
			
			// If the application was shutdown make sure to clear the password field, if it
			// was saved in the instance state
			if (App.isShutdown()) {
				lockDatabase();
			}

			// Clear the shutdown flag
			App.clearShutdown();

			if (startedWithActivityResult)
				return;

			if (App.getDB().Loaded && (App.getDB().mIoc != null)
			    && (mIoConnection != null) && (App.getDB().mIoc.GetDisplayName() == mIoConnection.GetDisplayName()))
			{
				if (App.getDB().Locked == false)
				{
					LaunchNextActivity();
				}
				else 
				{
					tryStartQuickUnlock();
				}
			}
		}
		
		private void retrieveSettings() {
			String defaultFilename = prefs.GetString(KEY_DEFAULT_FILENAME, "");
			if (!String.IsNullOrEmpty(mIoConnection.Path) && mIoConnection.Path.Equals(defaultFilename)) {
				CheckBox checkbox = (CheckBox) FindViewById(Resource.Id.default_database);
				checkbox.Checked = true;
			}
			CheckBox cbQuickUnlock = (CheckBox)FindViewById(Resource.Id.enable_quickunlock);
			cbQuickUnlock.Checked = prefs.GetBoolean(GetString(Resource.String.QuickUnlockDefaultEnabled_key), true);
		}
		
		private String getKeyFile(String filename) {
			if ( mRememberKeyfile ) {
				FileDbHelper dbHelp = App.fileDbHelper;
				
				String keyfile = dbHelp.getFileByName(filename);
				
				return keyfile;
			} else {
				return "";
			}
		}
		
		private void populateView() {
			setEditText(Resource.Id.filename, mIoConnection.GetDisplayName());
			setEditText(Resource.Id.pass_keyfile, mKeyFile);
		}
		
		/*
	private void errorMessage(CharSequence text)
	{
		Toast.MakeText(this, text, ToastLength.Long).Show();
	}
	*/
		
		private void errorMessage(int resId)
		{
			Toast.MakeText(this, resId, ToastLength.Long).Show();
		}
	
		private String GetEditText(int resId) {
			return Util.getEditText(this, resId);
		}
		
		private void setEditText(int resId, String str) {
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
			}
			
			return base.OnOptionsItemSelected(item);
		}
		
		private class AfterLoad : OnFinish {


			PasswordActivity act;
			public AfterLoad(Handler handler, PasswordActivity act):base(handler) {
				this.act = act;
			}
			

			public override void run() {
				if ( mSuccess ) {
					act.StartQuickUnlockForegroundService();
					act.LaunchNextActivity();
				} else {
					displayMessage(act);
				}
			}
		}
		
	}

}


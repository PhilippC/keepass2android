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
using Android.App;
using Android.Content;
using Android.OS;
using Android.Preferences;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Content.PM;
using KeePassLib.Serialization;
using keepass2android.Io;
using Environment = Android.OS.Environment;

namespace keepass2android
{
	/// <summary>
	/// Activity to select the file to use
	/// </summary>
	[Activity (Label = "@string/app_name", 
	           ConfigurationChanges=ConfigChanges.Orientation|
	           ConfigChanges.KeyboardHidden, 
	           Theme="@style/Base")]
	[IntentFilter(new [] { Intent.ActionSend }, 
		Label = "@string/kp2a_findUrl", 
		Categories=new[]{Intent.CategoryDefault}, 
		DataMimeType="text/plain")]
	public class FileSelectActivity : ListActivity
	{

		public FileSelectActivity (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}
		public FileSelectActivity()
		{
		}

		private const int CmenuClear = Menu.First;

		const string BundleKeyRecentMode = "RecentMode";

		private FileDbHelper _DbHelper;

		private bool _recentMode;
		view.FileSelectButtons _fileSelectButtons;

		internal AppTask AppTask;
		private IOConnectionInfo _iocToLaunch;

		void ShowFilenameDialog(bool showOpenButton, bool showCreateButton, bool showBrowseButton, string defaultFilename, string detailsText, int requestCodeBrowse)
		{
			AlertDialog.Builder builder = new AlertDialog.Builder(this);
			builder.SetView(LayoutInflater.Inflate(Resource.Layout.file_selection_filename, null));
			Dialog dialog = builder.Create();
			dialog.Show();

			Button openButton = (Button)dialog.FindViewById(Resource.Id.open);
			Button createButton = (Button)dialog.FindViewById(Resource.Id.create);
			TextView enterFilenameDetails = (TextView)dialog.FindViewById(Resource.Id.label_open_by_filename_details);
			openButton.Visibility = showOpenButton ? ViewStates.Visible : ViewStates.Gone;
			createButton.Visibility = showCreateButton ? ViewStates.Visible : ViewStates.Gone;
			// Set the initial value of the filename
			EditText editFilename = (EditText)dialog.FindViewById(Resource.Id.file_filename);
			editFilename.Text = defaultFilename;
			enterFilenameDetails.Text = detailsText;
			enterFilenameDetails.Visibility = enterFilenameDetails.Text == "" ? ViewStates.Gone : ViewStates.Visible;

			// Open button
			
			openButton.Click += ( sender, evt) => {
				String fileName = ((EditText)dialog.FindViewById(Resource.Id.file_filename)).Text;
				
				IOConnectionInfo ioc = new IOConnectionInfo
				    { 
					Path = fileName
				};
				
				LaunchPasswordActivityForIoc(ioc);
			};
			
			
			
			// Create button
			createButton.Click += (sender, evt) => {
				String filename = ((EditText)dialog.FindViewById(Resource.Id.file_filename)).Text;

				
				//TODO: allow non-local files?
				
				// Make sure file name exists
				if (filename.Length == 0)
				{
					Toast
						.MakeText(this,
						          Resource.String.error_filename_required,
						          ToastLength.Long).Show();
					return;
				}
				
				// Try to create the file
				Java.IO.File file = new Java.IO.File(filename);
				try
				{
					if (file.Exists())
					{
						Toast.MakeText(this,
						               Resource.String.error_database_exists,
						               ToastLength.Long).Show();
						return;
					}
					Java.IO.File parent = file.ParentFile;
					
					if (parent == null || (parent.Exists() && ! parent.IsDirectory))
					{
						Toast.MakeText(this,
						               Resource.String.error_invalid_path,
						               ToastLength.Long).Show();
						return;
					}
					
					if (! parent.Exists())
					{
						// Create parent dircetory
						if (! parent.Mkdirs())
						{
							Toast.MakeText(this,
							               Resource.String.error_could_not_create_parent,
							               ToastLength.Long).Show();
							return;
							
						}
					}
					
					file.CreateNewFile();
				} catch (Java.IO.IOException ex)
				{
					Toast.MakeText(
						this,
						GetText(Resource.String.error_file_not_create) + " "
						+ ex.LocalizedMessage,
						ToastLength.Long).Show();
					return;
				}
				
				// Prep an object to collect a password once the database has been created
				CollectPassword collectPassword = new CollectPassword(
					new LaunchGroupActivity(IOConnectionInfo.FromPath(filename), this), this);
				
				// Create the new database
				CreateDb create = new CreateDb(App.Kp2a, this, IOConnectionInfo.FromPath(filename), collectPassword, true);
				ProgressTask createTask = new ProgressTask(
                    App.Kp2a,
					this, create);
				createTask.Run();
				
				
			};
			
			Button cancelButton = (Button)dialog.FindViewById(Resource.Id.fnv_cancel);
			cancelButton.Click += (sender, e) => dialog.Dismiss();
			
			ImageButton browseButton = (ImageButton)dialog.FindViewById(Resource.Id.browse_button);
			if (!showBrowseButton)
			{
				browseButton.Visibility = ViewStates.Invisible;
			}
			browseButton.Click += (sender, evt) => {
				string filename = ((EditText)dialog.FindViewById(Resource.Id.file_filename)).Text;
				
				Util.ShowBrowseDialog(filename, this, requestCodeBrowse, showCreateButton);
				
			};

		}		

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			Kp2aLog.Log("FileSelect.OnCreate");
			Kp2aLog.Log("FileSelect:apptask="+Intent.GetStringExtra("KP2A_APPTASK"));

			if (Intent.Action == Intent.ActionSend)
			{
				AppTask = new SearchUrlTask { UrlToSearchFor = Intent.GetStringExtra(Intent.ExtraText) };
			}
			else
			{
				AppTask = AppTask.CreateFromIntent(Intent);
			}


			_DbHelper = App.Kp2a.FileDbHelper;
			if (ShowRecentFiles())
			{
				_recentMode = true;

				SetContentView(Resource.Layout.file_selection);
				_fileSelectButtons = new view.FileSelectButtons(this);
				((ListView)FindViewById(Android.Resource.Id.List)).AddFooterView(
					_fileSelectButtons);

			} else
			{
				SetContentView(Resource.Layout.file_selection_no_recent);
				_fileSelectButtons = (view.FileSelectButtons)FindViewById(Resource.Id.file_select);
			}




			Button openFileButton = (Button)FindViewById(Resource.Id.start_open_file);


			EventHandler openFileButtonClick = (sender, e) => 
			{
				string defaultFilename = Android.OS.Environment.ExternalStorageDirectory + GetString(Resource.String.default_file_path);
				const string detailsText = "";
				ShowFilenameDialog(true, false, true, defaultFilename, detailsText, Intents.RequestCodeFileBrowseForOpen);

				                   
			};
			openFileButton.Click += openFileButtonClick;
			//OPEN URL
			Button openUrlButton = (Button)FindViewById(Resource.Id.start_open_url);

#if NoNet
			openUrlButton.Visibility = ViewStates.Gone;
#endif

			//EventHandler openUrlButtonClick = (sender, e) => ShowFilenameDialog(true, false, false, "", GetString(Resource.String.enter_filename_details_url), Intents.RequestCodeFileBrowseForOpen);
			openUrlButton.Click += (sender, args) =>
				{
					Intent intent = new Intent(this, typeof(FileStorageSelectionActivity));
					StartActivityForResult(intent, 0);
				};

			//CREATE NEW
			Button createNewButton = (Button)FindViewById(Resource.Id.start_create);
			EventHandler createNewButtonClick = (sender, e) => ShowFilenameDialog(false, true, true, Android.OS.Environment.ExternalStorageDirectory + GetString(Resource.String.default_file_path), "", Intents.RequestCodeFileBrowseForCreate);
			createNewButton.Click += createNewButtonClick;

			/*//CREATE + IMPORT
			Button createImportButton = (Button)FindViewById(Resource.Id.start_create_import);
			createImportButton.Click += (object sender, EventArgs e) => 
			{
				openButton.Visibility = ViewStates.Gone;
				createButton.Visibility = ViewStates.Visible;
				enterFilenameDetails.Text = GetString(Resource.String.enter_filename_details_create_import);
				enterFilenameDetails.Visibility = enterFilenameDetails.Text == "" ? ViewStates.Gone : ViewStates.Visible;
				// Set the initial value of the filename
				EditText filename = (EditText)FindViewById(Resource.Id.file_filename);
				filename.Text = Android.OS.Environment.ExternalStorageDirectory + GetString(Resource.String.default_file_path);

			};*/

			FillData();
			
			RegisterForContextMenu(ListView);

			if (savedInstanceState != null)
			{
				AppTask = AppTask.CreateFromBundle(savedInstanceState);
				_recentMode = savedInstanceState.GetBoolean(BundleKeyRecentMode, _recentMode);

				string filenameToLaunch = savedInstanceState.GetString(PasswordActivity.KeyFilename);
				if (filenameToLaunch != null)
				{
					_iocToLaunch = new IOConnectionInfo()
						{
							Path = filenameToLaunch,
							UserName = savedInstanceState.GetString(PasswordActivity.KeyServerusername),
							Password = savedInstanceState.GetString(PasswordActivity.KeyServerpassword),
							CredSaveMode = (IOCredSaveMode) savedInstanceState.GetInt(PasswordActivity.KeyServercredmode)
						};
				}


			}

		}

		private bool ShowRecentFiles()
		{
			if (!RememberRecentFiles())
			{
				_DbHelper.DeleteAll();
			}

			return _DbHelper.HasRecentFiles();
		}

		private bool RememberRecentFiles()
		{
			return PreferenceManager.GetDefaultSharedPreferences(this).GetBoolean(GetString(Resource.String.RememberRecentFiles_key), Resources.GetBoolean(Resource.Boolean.RememberRecentFiles_default));
		}


		protected override void OnSaveInstanceState(Bundle outState)
		{
			base.OnSaveInstanceState(outState);
			AppTask.ToBundle(outState);
			outState.PutBoolean(BundleKeyRecentMode, _recentMode);
			
			if (_iocToLaunch != null)
			{
				outState.PutString(PasswordActivity.KeyFilename, _iocToLaunch.Path);
				outState.PutString(PasswordActivity.KeyServerusername, _iocToLaunch.UserName);
				outState.PutString(PasswordActivity.KeyServerpassword, _iocToLaunch.Password);
				outState.PutInt(PasswordActivity.KeyServercredmode, (int)_iocToLaunch.CredSaveMode);
			}
		}
		
		private class LaunchGroupActivity : FileOnFinish {
		    readonly FileSelectActivity _activity;
			private readonly IOConnectionInfo _ioc;
			
			public LaunchGroupActivity(IOConnectionInfo ioc, FileSelectActivity activity): base(null) {

				_activity = activity;
				_ioc = ioc;
			}
			
			public override void Run() {
				if (Success) {
					// Update the ongoing notification
					_activity.StartService(new Intent(_activity, typeof(OngoingNotificationsService)));


					if (_activity.RememberRecentFiles())
					{
						// Add to recent files
						FileDbHelper dbHelper = App.Kp2a.FileDbHelper;

					
						//TODO: getFilename always returns "" -> bug?
						dbHelper.CreateFile(_ioc, Filename);
					}

					GroupActivity.Launch(_activity, _activity.AppTask);
					
				} else {
					App.Kp2a.GetFileStorage(_ioc).Delete(_ioc);
					
				}
			}
		}
		
		private class CollectPassword: FileOnFinish {
		    readonly FileSelectActivity _activity;
		    readonly FileOnFinish _fileOnFinish;
			public CollectPassword(FileOnFinish finish,FileSelectActivity activity):base(finish) {
				_activity = activity;
				_fileOnFinish = finish;
			}
			
			public override void Run() {
				SetPasswordDialog password = new SetPasswordDialog(_activity, _fileOnFinish);
				password.Show();
			}
			
		}
		
		private void FillData()
		{
			// Get all of the rows from the database and create the item list
			Android.Database.ICursor filesCursor = _DbHelper.FetchAllFiles();
			StartManagingCursor(filesCursor);
			
			// Create an array to specify the fields we want to display in the list
			// (only TITLE)
			String[] from = new[] { FileDbHelper.KeyFileFilename };
			
			// and an array of the fields we want to bind those fields to (in this
			// case just text1)
			int[] to = new[] { Resource.Id.file_filename };
			
			// Now create a simple cursor adapter and set it to display
			SimpleCursorAdapter notes = new SimpleCursorAdapter(this,
			                                                    Resource.Layout.file_row, filesCursor, from, to);


			ListAdapter = notes;
		}


		void LaunchPasswordActivityForIoc(IOConnectionInfo ioc)
		{
			IFileStorage fileStorage = App.Kp2a.GetFileStorage(ioc);
			if (fileStorage.RequiredSetup != null)
			{
				if (!fileStorage.RequiredSetup.TrySetup(this))
				{
					//store ioc to launch. TrySetup hopefully launched another activity so we can check again in OnResume
					_iocToLaunch = ioc;
					return;
				}
			}

			if (fileStorage.RequiresCredentials(ioc))
			{
				//Build dialog to query credentials:
				AlertDialog.Builder builder = new AlertDialog.Builder(this);
				builder.SetTitle(GetString(Resource.String.credentials_dialog_title));
				builder.SetPositiveButton(GetString(Android.Resource.String.Ok), (dlgSender, dlgEvt) => 
				    {
				        Dialog dlg = (Dialog)dlgSender;
				        string username = ((EditText)dlg.FindViewById(Resource.Id.cred_username)).Text;
				        string password = ((EditText)dlg.FindViewById(Resource.Id.cred_password)).Text;
				        int credentialRememberMode = ((Spinner)dlg.FindViewById(Resource.Id.cred_remember_mode)).SelectedItemPosition;
				        ioc.UserName = username;
				        ioc.Password = password;
				        ioc.CredSaveMode = (IOCredSaveMode)credentialRememberMode;
				        PasswordActivity.Launch(this, ioc, AppTask);
						Finish();
				    });
				builder.SetView(LayoutInflater.Inflate(Resource.Layout.url_credentials, null));
				builder.SetNeutralButton(GetString(Android.Resource.String.Cancel), 
				                         (dlgSender, dlgEvt) => {});
				Dialog dialog = builder.Create();
				dialog.Show();
				((EditText)dialog.FindViewById(Resource.Id.cred_username)).Text = ioc.UserName;
				((EditText)dialog.FindViewById(Resource.Id.cred_password)).Text = ioc.Password;
				((Spinner)dialog.FindViewById(Resource.Id.cred_remember_mode)).SetSelection((int)ioc.CredSaveMode);
			}
			else
			{
				try
				{
					PasswordActivity.Launch(this, ioc, AppTask);
					Finish();
				} catch (Java.IO.FileNotFoundException)
				{
					Toast.MakeText(this, Resource.String.FileNotFound, ToastLength.Long).Show();
				} 
			}
		}
		
		protected override void OnListItemClick(ListView l, View v, int position, long id) {
			base.OnListItemClick(l, v, position, id);
			
			Android.Database.ICursor cursor = _DbHelper.FetchFile(id);
			StartManagingCursor(cursor);
			
			IOConnectionInfo ioc = _DbHelper.CursorToIoc(cursor);

			LaunchPasswordActivityForIoc(ioc);

		}



		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);

			if (resultCode == KeePass.ExitCloseAfterTaskComplete)
			{
				Finish();
				return;
			}
			
			FillData();

			if (resultCode == KeePass.ExitFileStorageSelectionOk)
			{
#if !EXCLUDE_FILECHOOSER
				Intent i = Keepass2android.Kp2afilechooser.Kp2aFileChooserBridge.GetLaunchFileChooserIntent(this, FileChooserFileProvider.TheAuthority, data.GetStringExtra("protocolId")+":///");

				StartActivityForResult(i, Intents.RequestCodeFileBrowseForOpen);
#else
				Toast.MakeText(this, "TODO: make this more flexible.", ToastLength.Long).Show();
				IOConnectionInfo ioc = new IOConnectionInfo
				{
					Path = Environment.ExternalStorageDirectory+"/keepass/keepass.kdbx"
				};

				LaunchPasswordActivityForIoc(ioc);
#endif
				
			}
			
			if ( (requestCode == Intents.RequestCodeFileBrowseForCreate
			      || requestCode == Intents.RequestCodeFileBrowseForOpen)
			    && resultCode == Result.Ok) {
				string filename = Util.IntentToFilename(data, this);
				if (filename != null) {
					if (filename.StartsWith("file://")) {
						filename = filename.Substring(7);
					}
					
					filename = Java.Net.URLDecoder.Decode(filename);

					if (requestCode == Intents.RequestCodeFileBrowseForOpen)
					{
						IOConnectionInfo ioc = new IOConnectionInfo
						    { 
							Path = filename
						};
						
						LaunchPasswordActivityForIoc(ioc);
					}

					if (requestCode == Intents.RequestCodeFileBrowseForCreate)
					{
						ShowFilenameDialog(false, true, true, filename, "", Intents.RequestCodeFileBrowseForCreate);
					}
				}
				
			}
		}


		protected override void OnResume()
		{
			base.OnResume();
			Kp2aLog.Log("FileSelect.OnResume");

			// Check to see if we need to change modes
			if (ShowRecentFiles() != _recentMode)
			{
				// Restart the activity
				Intent intent = Intent;
				StartActivity(intent);
				Finish();
				return;
			}

			//check if we are resuming after setting up the file storage:
			if (_iocToLaunch != null)
			{
				try
				{
					IOConnectionInfo iocToLaunch = _iocToLaunch;
					_iocToLaunch = null;

					IFileStorageSetupOnResume fsSetup = App.Kp2a.GetFileStorage(iocToLaunch).RequiredSetup as IFileStorageSetupOnResume;
					if ((fsSetup == null) || (fsSetup.TrySetupOnResume(this)))
					{
						LaunchPasswordActivityForIoc(iocToLaunch);
					}

				}
				catch (Exception e)
				{
					Toast.MakeText(this, e.Message, ToastLength.Long).Show();
				}	
			}

			_fileSelectButtons.UpdateExternalStorageWarning();


		}

		protected override void OnStart()
		{
			base.OnStart();
			Kp2aLog.Log("FileSelect.OnStart");

			var db = App.Kp2a.GetDb();
			if (db.Loaded)
			{
				LaunchPasswordActivityForIoc(db.Ioc);
			}
			else
			{
				//if no database is loaded: load the most recent database
				if (_DbHelper.HasRecentFiles())
				{
					Android.Database.ICursor filesCursor = _DbHelper.FetchAllFiles();
					StartManagingCursor(filesCursor);
					IOConnectionInfo ioc = _DbHelper.CursorToIoc(filesCursor);
					if (App.Kp2a.GetFileStorage(ioc).RequiredSetup == null)
					{
						LaunchPasswordActivityForIoc(ioc);
					}
				}
			}

			
		}
		public override bool OnCreateOptionsMenu(IMenu menu) {
			base.OnCreateOptionsMenu(menu);
			
			MenuInflater inflater = MenuInflater;
			inflater.Inflate(Resource.Menu.fileselect, menu);
			
			return true;
		}

		protected override void OnPause()
		{
			base.OnPause();
			Kp2aLog.Log("FileSelect.OnPause");
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			GC.Collect();
			Kp2aLog.Log("FileSelect.OnDestroy"+IsFinishing.ToString());
		}

		protected override void OnStop()
		{
			base.OnStop();
			Kp2aLog.Log("FileSelect.OnStop");
		}

		public override bool OnOptionsItemSelected(IMenuItem item) {
			switch (item.ItemId) {
			case Resource.Id.menu_donate:
				try {
						Util.GotoDonateUrl(this);
				} catch (ActivityNotFoundException) {
					Toast.MakeText(this, Resource.String.error_failed_to_launch_link, ToastLength.Long).Show();
					return false;
				}
				
				return true;
			
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
		
		public override void OnCreateContextMenu(IContextMenu menu, View v,
		                                         IContextMenuContextMenuInfo menuInfo) {
			base.OnCreateContextMenu(menu, v, menuInfo);
			
			menu.Add(0, CmenuClear, 0, Resource.String.remove_from_filelist);
		}
		
		public override bool OnContextItemSelected(IMenuItem item) {
			base.OnContextItemSelected(item);
			
			if ( item.ItemId == CmenuClear ) {
				AdapterView.AdapterContextMenuInfo acmi = (AdapterView.AdapterContextMenuInfo) item.MenuInfo;
				
				TextView tv = (TextView) acmi.TargetView;
				String filename = tv.Text;
				_DbHelper.DeleteFile(filename);

				RefreshList();
				
				
				return true;
			}
			
			return false;
		}
		
		private void RefreshList() {
			CursorAdapter ca = (CursorAdapter) ListAdapter;
			Android.Database.ICursor cursor = ca.Cursor;
			cursor.Requery();
		}
	}
}


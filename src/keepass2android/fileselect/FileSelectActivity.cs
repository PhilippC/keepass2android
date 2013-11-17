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
using Android.Database;
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

		private FileDbHelper _dbHelper;

		private bool _recentMode;
		view.FileSelectButtons _fileSelectButtons;

		internal AppTask AppTask;

		public const string NoForwardToPasswordActivity = "NoForwardToPasswordActivity";

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


			_dbHelper = App.Kp2a.FileDbHelper;
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
				Intent intent = new Intent(this, typeof(FileStorageSelectionActivity));
				intent.PutExtra(FileStorageSelectionActivity.AllowThirdPartyAppGet, true);
				StartActivityForResult(intent, 0);
				                   
			};
			openFileButton.Click += openFileButtonClick;
			//OPEN URL
			Button openUrlButton = (Button)FindViewById(Resource.Id.start_open_url);

			openUrlButton.Visibility = ViewStates.Gone;

			//EventHandler openUrlButtonClick = (sender, e) => ShowFilenameDialog(true, false, false, "", GetString(Resource.String.enter_filename_details_url), Intents.RequestCodeFileBrowseForOpen);

			//CREATE NEW
			Button createNewButton = (Button)FindViewById(Resource.Id.start_create);
			EventHandler createNewButtonClick = (sender, e) =>
				{
					//ShowFilenameDialog(false, true, true, Android.OS.Environment.ExternalStorageDirectory + GetString(Resource.String.default_file_path), "", Intents.RequestCodeFileBrowseForCreate)
					StartActivityForResult(typeof (CreateDatabaseActivity), 0);
				};
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
			}

		}

		private bool ShowRecentFiles()
		{
			if (!RememberRecentFiles())
			{
				_dbHelper.DeleteAll();
			}

			return _dbHelper.HasRecentFiles();
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
			
		}
		
		
		class MyViewBinder: Java.Lang.Object, SimpleCursorAdapter.IViewBinder
		{
			private Kp2aApp app;

			public MyViewBinder(Kp2aApp app)
			{
				this.app = app;
			}

			public bool SetViewValue(View view, ICursor cursor, int columnIndex)
			{
				if (columnIndex == 1)
				{
					String path = cursor.GetString(columnIndex);
					TextView textView = (TextView)view;
					IOConnectionInfo ioc = new IOConnectionInfo {Path = path};
					textView.Text = app.GetFileStorage(ioc).GetDisplayName(ioc);
					return true;
				}

				return false;
			}
		}
		
		private void FillData()
		{
			// Get all of the rows from the database and create the item list
			ICursor filesCursor = _dbHelper.FetchAllFiles();
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


			notes.ViewBinder = new MyViewBinder(App.Kp2a);

			ListAdapter = notes;
		}


		void LaunchPasswordActivityForIoc(IOConnectionInfo ioc)
		{
			IFileStorage fileStorage = App.Kp2a.GetFileStorage(ioc);

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
			
			ICursor cursor = _dbHelper.FetchFile(id);
			StartManagingCursor(cursor);
			
			IOConnectionInfo ioc = _dbHelper.CursorToIoc(cursor);
			
			App.Kp2a.GetFileStorage(ioc)
					   .PrepareFileUsage(new FileStorageSetupInitiatorActivity(this, OnActivityResult, null), ioc, 0, false);
		}
		private void OnOpenButton(String fileName)
		{
			
			IOConnectionInfo ioc = new IOConnectionInfo
			{
				Path = fileName
			};

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

				string protocolId = data.GetStringExtra("protocolId");

				if (protocolId == "androidget")
				{
					string defaultFilename = Environment.ExternalStorageDirectory +
					                         GetString(Resource.String.default_file_path);
					Util.ShowBrowseDialog(defaultFilename, this, Intents.RequestCodeFileBrowseForOpen, false);
				}
				else
				{
					App.Kp2a.GetFileStorage(protocolId).StartSelectFile(new FileStorageSetupInitiatorActivity(this, 
						OnActivityResult,
						defaultPath =>
							{

								Util.ShowFilenameDialog(this, OnOpenButton, null, false, defaultPath, GetString(Resource.String.enter_filename_details_url),
								                    Intents.RequestCodeFileBrowseForOpen);
							}
						), false, 0, protocolId);
				}

				
			}
			
			if ( (requestCode == Intents.RequestCodeFileBrowseForCreate
			      || requestCode == Intents.RequestCodeFileBrowseForOpen)
			    && resultCode == Result.Ok) {
				string filename = Util.IntentToFilename(data, this);
				if (filename != null) {
					if (filename.StartsWith("file://")) {
						filename = filename.Substring(7);
						filename = Java.Net.URLDecoder.Decode(filename);
					}
					
					if (requestCode == Intents.RequestCodeFileBrowseForOpen)
					{
						IOConnectionInfo ioc = new IOConnectionInfo
						    { 
							Path = filename
						};
						
						LaunchPasswordActivityForIoc(ioc);
					}

					
				}
				
			}

			if (resultCode == (Result) FileStorageResults.FileUsagePrepared)
			{
				IOConnectionInfo ioc = new IOConnectionInfo();
				PasswordActivity.SetIoConnectionFromIntent(ioc, data);
				LaunchPasswordActivityForIoc(ioc);
			}
			if (resultCode == (Result)FileStorageResults.FileChooserPrepared)
			{
				IOConnectionInfo ioc = new IOConnectionInfo();
				PasswordActivity.SetIoConnectionFromIntent(ioc, data);
#if !EXCLUDE_FILECHOOSER
				StartFileChooser(ioc.Path);
#else
				LaunchPasswordActivityForIoc(new IOConnectionInfo { Path = "/mnt/sdcard/keepass/yubi2.kdbx"});
#endif
			}
			if ((resultCode == Result.Canceled) && (data != null) && (data.HasExtra("EXTRA_ERROR_MESSAGE")))
			{
				Toast.MakeText(this, data.GetStringExtra("EXTRA_ERROR_MESSAGE"), ToastLength.Long).Show();
			}
		}
		#if !EXCLUDE_FILECHOOSER
		private void StartFileChooser(string defaultPath)
		{
			Kp2aLog.Log("FSA: defaultPath="+defaultPath);
			string fileProviderAuthority = FileChooserFileProvider.TheAuthority;
			if (defaultPath.StartsWith("file://"))
			{
				fileProviderAuthority = "keepass2android.keepass2android.android-filechooser.localfile";
			}
			Intent i = Keepass2android.Kp2afilechooser.Kp2aFileChooserBridge.GetLaunchFileChooserIntent(this, fileProviderAuthority,
			                                                                                            defaultPath);

			StartActivityForResult(i, Intents.RequestCodeFileBrowseForOpen);
		}

#endif
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
				if ( (Intent.GetBooleanExtra(NoForwardToPasswordActivity, false)==false) &&  _dbHelper.HasRecentFiles())
				{
					ICursor filesCursor = _dbHelper.FetchAllFiles();
					StartManagingCursor(filesCursor);
					filesCursor.MoveToFirst();
					IOConnectionInfo ioc = _dbHelper.CursorToIoc(filesCursor);
					if (App.Kp2a.GetFileStorage(ioc).RequiresSetup(ioc) == false)
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
				_dbHelper.DeleteFile(filename);

				RefreshList();
				
				
				return true;
			}
			
			return false;
		}
		
		private void RefreshList() {
			CursorAdapter ca = (CursorAdapter) ListAdapter;
			ICursor cursor = ca.Cursor;
			cursor.Requery();
		}
	}
}


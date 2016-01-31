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
using Android.App;
using Android.Content;
using Android.Database;
using Android.OS;
using Android.Preferences;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Content.PM;
using Android.Support.V7.App;
using KeePassLib.Serialization;
using Keepass2android.Pluginsdk;
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
               Theme = "@style/MyTheme_Blue")]
	[IntentFilter(new [] { Intent.ActionSend }, 
		Label = "@string/kp2a_findUrl", 
		Categories=new[]{Intent.CategoryDefault}, 
		DataMimeType="text/plain")]
	[IntentFilter(new[] { Strings.ActionStartWithTask },
		Categories = new[] { Intent.CategoryDefault })]
	public class FileSelectActivity : AppCompatActivity
	{
		private readonly ActivityDesign _design;
		public FileSelectActivity (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			_design = new ActivityDesign(this);
		}

		public FileSelectActivity()
		{
			_design = new ActivityDesign(this);
		}

		private const int CmenuClear = Menu.First;

		const string BundleKeyRecentMode = "RecentMode";

		private FileDbHelper _dbHelper;

		private bool _recentMode;
		
		internal AppTask AppTask;
		private const int RequestCodeSelectIoc = 456;

		public const string NoForwardToPasswordActivity = "NoForwardToPasswordActivity";

		protected override void OnCreate(Bundle savedInstanceState)
		{
			_design.ApplyTheme(); 
			base.OnCreate(savedInstanceState);
			

			Kp2aLog.Log("FileSelect.OnCreate");
			Kp2aLog.Log("FileSelect:apptask="+Intent.GetStringExtra("KP2A_APPTASK"));

			if (Intent.Action == Intent.ActionSend)
			{
				AppTask = new SearchUrlTask { UrlToSearchFor = Intent.GetStringExtra(Intent.ExtraText) };
			}
			else
			{
				//see PasswordActivity for an explanation
				if (Intent.Flags.HasFlag(ActivityFlags.LaunchedFromHistory))
				{
					AppTask = new NullTask();
				}
				else
				{
					AppTask = AppTask.GetTaskInOnCreate(savedInstanceState, Intent);
				}
			}


			_dbHelper = App.Kp2a.FileDbHelper;
            SetContentView(Resource.Layout.file_selection);
				

			if (ShowRecentFiles())
			{
				_recentMode = true;

				
                FindViewById(Resource.Id.recent_files).Visibility = ViewStates.Visible;
			    Android.Util.Log.Debug("KP2A", "Recent files visible");

			}
            else
			{
				FindViewById(Resource.Id.recent_files).Visibility = ViewStates.Invisible;
                Android.Util.Log.Debug("KP2A", "Recent files invisible");
#if NoNet
				ImageView imgView = FindViewById(Resource.Id.splashlogo) as ImageView;
				if (imgView != null)
				{
					imgView.SetImageDrawable(Resources.GetDrawable(Resource.Drawable.splashlogo_offline));
				}
#endif
			}

			Button openFileButton = (Button)FindViewById(Resource.Id.start_open_file);

			EventHandler openFileButtonClick = (sender, e) => 
			{
				Intent intent = new Intent(this, typeof(SelectStorageLocationActivity));
				intent.PutExtra(FileStorageSelectionActivity.AllowThirdPartyAppGet, true);
				intent.PutExtra(FileStorageSelectionActivity.AllowThirdPartyAppSend, false);
				intent.PutExtra(SelectStorageLocationActivity.ExtraKeyWritableRequirements, (int) SelectStorageLocationActivity.WritableRequirements.WriteDesired);
				intent.PutExtra(FileStorageSetupDefs.ExtraIsForSave, false);
				StartActivityForResult(intent, RequestCodeSelectIoc);	
				                   
			};
			openFileButton.Click += openFileButtonClick;
			
			//CREATE NEW
			Button createNewButton = (Button)FindViewById(Resource.Id.start_create);
			EventHandler createNewButtonClick = (sender, e) =>
				{
					//ShowFilenameDialog(false, true, true, Android.OS.Environment.ExternalStorageDirectory + GetString(Resource.String.default_file_path), "", Intents.RequestCodeFileBrowseForCreate)
					Intent i = new Intent(this, typeof (CreateDatabaseActivity));
					this.AppTask.ToIntent(i);
					StartActivityForResult(i, 0);
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
			private readonly Kp2aApp _app;

			public MyViewBinder(Kp2aApp app)
			{
				_app = app;
			}

			public bool SetViewValue(View view, ICursor cursor, int columnIndex)
			{
				if (columnIndex == 1)
				{
					String path = cursor.GetString(columnIndex);
					TextView textView = (TextView)view;
					IOConnectionInfo ioc = new IOConnectionInfo {Path = path};
					textView.Text = _app.GetFileStorage(ioc).GetDisplayName(ioc);
					textView.Tag = ioc.Path;
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
			SimpleCursorAdapter recentFilesAdapter = new SimpleCursorAdapter(this,
			                                                    Resource.Layout.file_row, filesCursor, from, to);


			recentFilesAdapter.ViewBinder = new MyViewBinder(App.Kp2a);

		    FragmentManager.FindFragmentById<RecentFilesFragment>(Resource.Id.recent_files).SetAdapter(recentFilesAdapter);

		    
		}


		void LaunchPasswordActivityForIoc(IOConnectionInfo ioc)
		{
			IFileStorage fileStorage = App.Kp2a.GetFileStorage(ioc);

			if (fileStorage.RequiresCredentials(ioc))
			{
				Util.QueryCredentials(ioc, AfterQueryCredentials, this);
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

		

		private void AfterQueryCredentials(IOConnectionInfo ioc)
		{
			PasswordActivity.Launch(this, ioc, AppTask);
			Finish();
		}

		public void OnListItemClick(ListView l, View v, int position, long id) {
			
			ICursor cursor = _dbHelper.FetchFile(id);
			StartManagingCursor(cursor);
			
			IOConnectionInfo ioc = _dbHelper.CursorToIoc(cursor);
			
			App.Kp2a.GetFileStorage(ioc)
					   .PrepareFileUsage(new FileStorageSetupInitiatorActivity(this, OnActivityResult, null), ioc, 0, false);
		}
		
        
		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);

			//update app task.
			//this is important even if we're about to close, because then we should get a NullTask here 
			//in order not to do the same task next time again!
			AppTask.TryGetFromActivityResult(data, ref AppTask);

			if (resultCode == KeePass.ExitCloseAfterTaskComplete)
			{
				//no need to set the result ExitCloseAfterTaskComplete here, there's no parent Activity on the stack
				Finish();
				return;
			}
			
			FillData();


			if (resultCode == (Result)FileStorageResults.FileUsagePrepared)
			{
				IOConnectionInfo ioc = new IOConnectionInfo();
				PasswordActivity.SetIoConnectionFromIntent(ioc, data);
				LaunchPasswordActivityForIoc(ioc);
			}

			if ((resultCode == Result.Ok) && (requestCode == RequestCodeSelectIoc))
			{
				IOConnectionInfo ioc = new IOConnectionInfo();
				PasswordActivity.SetIoConnectionFromIntent(ioc, data);
				LaunchPasswordActivityForIoc(ioc);
			}
			
		}

		protected override void OnResume()
		{
			base.OnResume();
			App.Kp2a.OfflineMode = false; //no matter what the preferences are, file selection or db creation is performed offline. PasswordActivity might set this to true.
			Kp2aLog.Log("FileSelect.OnResume");

			_design.ReapplyTheme();

			// Check to see if we need to change modes
			if (ShowRecentFiles() != _recentMode)
			{
				// Restart the activity
				Recreate();
				return;
			}



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
				return Util.GotoDonateUrl(this);
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
		
	}

    public class RecentFilesFragment : ListFragment
    {
        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate(Resource.Layout.recent_files, container, false);
            Android.Util.Log.Debug("KP2A", "OnCreateView");
            return view;
        }

        public void SetAdapter(BaseAdapter adapter)
        {
            ListAdapter = adapter;
            Android.Util.Log.Debug("KP2A", "SetAdapter");
        }

        public override void OnActivityCreated(Bundle savedInstanceState)
        {
			base.OnActivityCreated(savedInstanceState); 
			Android.Util.Log.Debug("KP2A", "OnActCreated");
            ListView.ItemClick += (sender, args) =>
            {
                ((FileSelectActivity) Activity).OnListItemClick((ListView) sender, args.View, args.Position, args.Id);
            };
            RefreshList();
	        RegisterForContextMenu(ListView);
            
        }

        public void RefreshList()
        {
            Android.Util.Log.Debug("KP2A", "RefreshList");
            CursorAdapter ca = (CursorAdapter)ListAdapter;
            ICursor cursor = ca.Cursor;
            cursor.Requery();
        }

	    public override void OnCreateContextMenu(IContextMenu menu, View v, IContextMenuContextMenuInfo menuInfo)
	    {
		    base.OnCreateContextMenu(menu, v, menuInfo);
			menu.Add(0, Menu.First, 0, Resource.String.remove_from_filelist);
	    }

	    public override bool OnContextItemSelected(IMenuItem item)
	    {
			if (item.ItemId == Menu.First)
			{
				AdapterView.AdapterContextMenuInfo acmi = (AdapterView.AdapterContextMenuInfo)item.MenuInfo;

				TextView tv = (TextView)acmi.TargetView;
				String filename = (string)tv.Tag;
				App.Kp2a.FileDbHelper.DeleteFile(filename);

				RefreshList();


				return true;
			}
			return base.OnContextItemSelected(item);

	    }
		
    }
}


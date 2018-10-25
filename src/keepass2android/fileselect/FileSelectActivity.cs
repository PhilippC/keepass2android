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
using System.Linq;
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
using Java.IO;
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
		
		
		private const int RequestCodeSelectIoc = 456;
	    private const int RequestCodeEditIoc = 457;

        public const string NoForwardToPasswordActivity = "NoForwardToPasswordActivity";

		protected override void OnCreate(Bundle savedInstanceState)
		{
			_design.ApplyTheme(); 
			base.OnCreate(savedInstanceState);
			

			Kp2aLog.Log("FileSelect.OnCreate");
			


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
					
				    i.SetFlags(ActivityFlags.ForwardResult);
					StartActivity(i);
				    Finish();
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

            FindViewById<Switch>(Resource.Id.local_backups_switch).CheckedChange += (sender, args) => {FillData();};

            FillData();
			
			if (savedInstanceState != null)
			{
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
		
			outState.PutBoolean(BundleKeyRecentMode, _recentMode);
			
		}

	    class MyCursorAdapter: CursorAdapter
	    {
	        private LayoutInflater cursorInflater;
	        private readonly FileSelectActivity _activity;
	        private IKp2aApp _app;

	        public MyCursorAdapter(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
	        {
	        }

	        public MyCursorAdapter(FileSelectActivity activity, ICursor c, IKp2aApp app) : base(activity, c)
	        {
	            _activity = activity;
	            _app = app;
	        }

	        public MyCursorAdapter(Context context, ICursor c, bool autoRequery) : base(context, c, autoRequery)
	        {
	        }

	        public MyCursorAdapter(Context context, ICursor c, CursorAdapterFlags flags) : base(context, c, flags)
	        {
	            
            }

	        public override void BindView(View view, Context context, ICursor cursor)
	        {
	            
	            String path = cursor.GetString(1);

	            TextView textView = view.FindViewById<TextView>(Resource.Id.file_filename);
	            IOConnectionInfo ioc = new IOConnectionInfo { Path = path };
	            var fileStorage = _app.GetFileStorage(ioc);

	            String displayName = cursor.GetString(6);
	            if (string.IsNullOrEmpty(displayName))
	            {
	                displayName = fileStorage.GetDisplayName(ioc);

	            }

                textView.Text = displayName;
	            textView.Tag = ioc.Path;
                
	        }

	        public override View NewView(Context context, ICursor cursor, ViewGroup parent)
	        {
                if (cursorInflater == null)
                    cursorInflater = (LayoutInflater)context.GetSystemService( Context.LayoutInflaterService);
                View view = cursorInflater.Inflate(Resource.Layout.file_row, parent, false);

	            view.FindViewById(Resource.Id.group_name_vdots).Click += (sender, args) =>
	            {
	                Handler handler = new Handler(Looper.MainLooper);
	                handler.Post(() =>
	                {
	                    PopupMenu popupMenu = new PopupMenu(context, view.FindViewById(Resource.Id.group_name_vdots));

	                    AccessManager.PreparePopup(popupMenu);
	                    int remove = 0;
	                    int edit = 1;
	                    popupMenu.Menu.Add(0, remove, 0, context.GetString(Resource.String.remove_from_filelist)).SetIcon(Resource.Drawable.ic_menu_delete_grey);

	                    TextView textView = view.FindViewById<TextView>(Resource.Id.file_filename);
                        
	                    String filename = (string)textView.Tag;
                        IOConnectionInfo ioc = new IOConnectionInfo { Path = filename };
	                    if (FileSelectHelper.CanEditIoc(ioc))
	                    {
	                        popupMenu.Menu.Add(0, edit, 0, context.GetString(Resource.String.edit)).SetIcon(Resource.Drawable.ic_menu_edit_grey);
                        }


	                    popupMenu.MenuItemClick += delegate(object sender2, PopupMenu.MenuItemClickEventArgs args2)
	                    {
	                        if (args2.Item.ItemId == remove)
	                        {
	                            if (new LocalFileStorage(App.Kp2a).IsLocalBackup(IOConnectionInfo.FromPath(filename)))
	                            {
	                                try
	                                {
	                                    Java.IO.File file = new File(filename);
	                                    file.Delete();
	                                }
	                                catch (Exception exception)
	                                {
	                                    Kp2aLog.LogUnexpectedError(exception);
	                                }
	                            }

	                            App.Kp2a.FileDbHelper.DeleteFile(filename);

	                            cursor.Requery();
	                        }
	                        if (args2.Item.ItemId == edit)
	                        {
	                            var fsh = new FileSelectHelper(_activity, false, RequestCodeEditIoc);
	                            fsh.OnOpen += (o, newConnectionInfo) =>
	                            {
	                                _activity.EditFileEntry(filename, newConnectionInfo);
	                            };
                                fsh.PerformManualFileSelect(filename);

	                        }
	                    };
	                    popupMenu.Show();
	                });
	            };

                return view;
	        }

	        
	    }

	    private void EditFileEntry(string filename, IOConnectionInfo newConnectionInfo)
	    {
            _dbHelper.CreateFile(newConnectionInfo, _dbHelper.GetKeyFileForFile(filename));
	        _dbHelper.DeleteFile(filename);

            LaunchPasswordActivityForIoc(newConnectionInfo);
	        
        }


	    private void FillData()
		{
			// Get all of the rows from the database and create the item list
			ICursor filesCursor = _dbHelper.FetchAllFiles();
			
			

		    if (FindViewById<Switch>(Resource.Id.local_backups_switch).Checked == false)
		    {
		        var fileStorage = new LocalFileStorage(App.Kp2a);
		        filesCursor = new FilteredCursor(filesCursor, cursor => !fileStorage.IsLocalBackup(IOConnectionInfo.FromPath(cursor.GetString(1))));
		    }

		    StartManagingCursor(filesCursor);

            FragmentManager.FindFragmentById<RecentFilesFragment>(Resource.Id.recent_files).SetAdapter(new MyCursorAdapter(this, filesCursor,App.Kp2a));

		    
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
					PasswordActivity.Launch(this, ioc, new ActivityLaunchModeForward());
					Finish();
				} catch (Java.IO.FileNotFoundException)
				{
					Toast.MakeText(this, Resource.String.FileNotFound, ToastLength.Long).Show();
				} 
			}
		}

		

		private void AfterQueryCredentials(IOConnectionInfo ioc)
		{
			PasswordActivity.Launch(this, ioc, new ActivityLaunchModeForward());
			Finish();
		}

		public void OnListItemClick(ListView l, View v, int position, long id)
        {
            ICursor cursor = _dbHelper.FetchFile(id);
			StartManagingCursor(cursor);
			
			IOConnectionInfo ioc = _dbHelper.CursorToIoc(cursor);
			
			App.Kp2a.GetFileStorage(ioc)
					   .PrepareFileUsage(new FileStorageSetupInitiatorActivity(this, OnActivityResult, null), ioc, 0, false);
		}
		
        
		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);

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
				Util.SetIoConnectionFromIntent(ioc, data);
				LaunchPasswordActivityForIoc(ioc);
			}

			if ((resultCode == Result.Ok) && (requestCode == RequestCodeSelectIoc))
			{
				IOConnectionInfo ioc = new IOConnectionInfo();
				Util.SetIoConnectionFromIntent(ioc, data);
				LaunchPasswordActivityForIoc(ioc);
			}
		    
		    if ((resultCode == Result.Ok) && (requestCode == RequestCodeEditIoc))
		    {
		        string filename = Util.IntentToFilename(data, this);
		        
		        LaunchPasswordActivityForIoc(IOConnectionInfo.FromPath(filename));
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

			
			//if no database is loaded: load the most recent database
			if ( (Intent.GetBooleanExtra(NoForwardToPasswordActivity, false)==false) &&  _dbHelper.HasRecentFiles() && !App.Kp2a.OpenDatabases.Any())
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

	    
		
    }
}


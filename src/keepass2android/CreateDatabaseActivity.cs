using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Preferences;
using Android.Support.V7.App;
using Android.Text;
using Android.Views;
using Android.Widget;
using Java.IO;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using keepass2android.Io;
using Environment = Android.OS.Environment;
using IOException = Java.IO.IOException;

namespace keepass2android
{
	[Activity(Label = "@string/app_name",
			   ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden,
               Theme = "@style/MyTheme_ActionBar")]
	public class CreateDatabaseActivity : AppCompatActivity
	{
		private IOConnectionInfo _ioc;
		private string _keyfileFilename;
		private bool _restoringInstanceState;
		private bool _showPassword;

		private readonly ActivityDesign _design;
		private AppTask _appTask;

		public CreateDatabaseActivity()
		{
			_design = new ActivityDesign(this);
		}


		private const int RequestCodeKeyFile = 0;
		private const int RequestCodeDbFilename = 1;
		private const string KeyfilefilenameBundleKey = "KeyfileFilename";

		protected override void OnSaveInstanceState(Bundle outState)
		{
			base.OnSaveInstanceState(outState);
			outState.PutString(PasswordActivity.KeyFilename, _ioc.Path);
			outState.PutString(PasswordActivity.KeyServerusername, _ioc.UserName);
			outState.PutString(PasswordActivity.KeyServerpassword, _ioc.Password);
			outState.PutInt(PasswordActivity.KeyServercredmode, (int)_ioc.CredSaveMode);

			if (_keyfileFilename != null)
				outState.PutString(KeyfilefilenameBundleKey, _keyfileFilename);
		}

		protected override void OnCreate(Bundle bundle)
		{
			_design.ApplyTheme(); 
			base.OnCreate(bundle);
			

            SupportActionBar.SetDisplayHomeAsUpEnabled(true);
            SupportActionBar.SetHomeButtonEnabled(true);

			SetContentView(Resource.Layout.create_database);
			_appTask = AppTask.GetTaskInOnCreate(bundle, Intent);

			SetDefaultIoc();

			FindViewById(Resource.Id.keyfile_filename).Visibility = ViewStates.Gone;
			

			var keyfileCheckbox = FindViewById<CheckBox>(Resource.Id.use_keyfile);

			if (bundle != null)
			{
				_keyfileFilename = bundle.GetString(KeyfilefilenameBundleKey, null);
				if (_keyfileFilename != null)
				{
					FindViewById<TextView>(Resource.Id.keyfile_filename).Text = ConvertFilenameToIocPath(_keyfileFilename);
					FindViewById(Resource.Id.keyfile_filename).Visibility = ViewStates.Visible;
					keyfileCheckbox.Checked = true;
				}

				if (bundle.GetString(PasswordActivity.KeyFilename, null) != null)
				{
					_ioc = new IOConnectionInfo
						{
							Path = bundle.GetString(PasswordActivity.KeyFilename),
							UserName = bundle.GetString(PasswordActivity.KeyServerusername),
							Password = bundle.GetString(PasswordActivity.KeyServerpassword),
							CredSaveMode = (IOCredSaveMode) bundle.GetInt(PasswordActivity.KeyServercredmode),
						};
				}
			}

			UpdateIocView();

			keyfileCheckbox.CheckedChange += (sender, args) =>
				{
					if (keyfileCheckbox.Checked)
					{
						if (_restoringInstanceState)
							return;

						string defaulFilename = _keyfileFilename;
						if (_keyfileFilename == null)
						{
							defaulFilename = _keyfileFilename = SdDir + "keyfile.txt";
							if (defaulFilename.StartsWith("file://") == false)
								defaulFilename = "file://" + defaulFilename;
						}

						StartFileChooser(defaulFilename, RequestCodeKeyFile, false);

					}
					else
					{
						FindViewById(Resource.Id.keyfile_filename).Visibility = ViewStates.Gone;
						_keyfileFilename = null;
					}
				};


			FindViewById(Resource.Id.btn_change_location).Click += (sender, args) =>
			{
				Intent intent = new Intent(this, typeof(FileStorageSelectionActivity));
				StartActivityForResult(intent, 0);
			};

			Button generatePassword = (Button)FindViewById(Resource.Id.generate_button);
			generatePassword.Click += (sender, e) =>
			{
				GeneratePasswordActivity.LaunchWithoutLockCheck(this);
			};

			FindViewById(Resource.Id.btn_create).Click += (sender, evt) => 
			{
				CreateDatabase();
			};

			ImageButton btnTogglePassword = (ImageButton)FindViewById(Resource.Id.toggle_password);
			btnTogglePassword.Click += (sender, e) =>
			{
				_showPassword = !_showPassword;
				MakePasswordMaskedOrVisible();
			};
			Android.Graphics.PorterDuff.Mode mMode = Android.Graphics.PorterDuff.Mode.SrcAtop;
			Android.Graphics.Color color = new Android.Graphics.Color (224, 224, 224);
			btnTogglePassword.SetColorFilter (color, mMode);

			
		}

		private void MakePasswordMaskedOrVisible()
		{
			TextView password = (TextView)FindViewById(Resource.Id.entry_password);
			TextView confpassword = (TextView)FindViewById(Resource.Id.entry_confpassword);
			if (_showPassword)
			{
				password.InputType = InputTypes.ClassText | InputTypes.TextVariationVisiblePassword;
				confpassword.Visibility = ViewStates.Gone;
			}
			else
			{
				password.InputType = InputTypes.ClassText | InputTypes.TextVariationPassword;
				confpassword.Visibility = ViewStates.Visible;
			}
			
		}

		private void CreateDatabase()
		{
			var keyfileCheckbox = FindViewById<CheckBox>(Resource.Id.use_keyfile);
			string password;
			if (!TryGetPassword(out password)) return;


			// Verify that a password or keyfile is set
			if (password.Length == 0 && !keyfileCheckbox.Checked)
			{
				Toast.MakeText(this, Resource.String.error_nopass, ToastLength.Long).Show();
				return;
			}

			//create the key
			CompositeKey newKey = new CompositeKey();
			if (String.IsNullOrEmpty(password) == false)
			{
				newKey.AddUserKey(new KcpPassword(password));
			}
			if (String.IsNullOrEmpty(_keyfileFilename) == false)
			{
				try
				{
					newKey.AddUserKey(new KcpKeyFile(_keyfileFilename));
				}
				catch (Exception)
				{
					Toast.MakeText(this, Resource.String.error_adding_keyfile, ToastLength.Long).Show();
					return;
				}
			}

			// Create the new database
			CreateDb create = new CreateDb(App.Kp2a, this, _ioc, new LaunchGroupActivity(_ioc, this), false, newKey);
			ProgressTask createTask = new ProgressTask(
				App.Kp2a,
				this, create);
			createTask.Run();
		}

		private bool TryGetPassword(out string pass)
		{
			TextView passView = (TextView)FindViewById(Resource.Id.entry_password);
			pass = passView.Text;

			if (_showPassword)
				return true;

			TextView passConfView = (TextView) FindViewById(Resource.Id.entry_confpassword);
			String confpass = passConfView.Text;

			// Verify that passwords match
			if (! pass.Equals(confpass))
			{
				// Passwords do not match
				Toast.MakeText(this, Resource.String.error_pass_match, ToastLength.Long).Show();
				return false;
			}
			return true;
		}

		protected override void OnResume()
		{
			base.OnResume();
			_design.ReapplyTheme();
		}

		protected override void OnRestoreInstanceState(Bundle savedInstanceState)
		{
			_restoringInstanceState = true;
			base.OnRestoreInstanceState(savedInstanceState);
			_restoringInstanceState = false;
		}

		private void StartFileChooser(string defaultPath, int requestCode, bool forSave)
		{
#if !EXCLUDE_FILECHOOSER
			Kp2aLog.Log("FSA: defaultPath=" + defaultPath);
			string fileProviderAuthority = FileChooserFileProvider.TheAuthority;
			if (defaultPath.StartsWith("file://"))
			{
				fileProviderAuthority = "keepass2android."+AppNames.PackagePart+".android-filechooser.localfile";
			}
			Intent i = Keepass2android.Kp2afilechooser.Kp2aFileChooserBridge.GetLaunchFileChooserIntent(this, fileProviderAuthority,
																										defaultPath);

			if (forSave)
			{
				i.PutExtra("group.pals.android.lib.ui.filechooser.FileChooserActivity.save_dialog", true);
				i.PutExtra("group.pals.android.lib.ui.filechooser.FileChooserActivity.default_file_ext", "kdbx");
			}

			StartActivityForResult(i, requestCode);
#endif
		}


		private void UpdateIocView()
		{
			string displayPath = App.Kp2a.GetFileStorage(_ioc).GetDisplayName(_ioc);
			int protocolSeparatorPos = displayPath.IndexOf("://", StringComparison.Ordinal);
			string protocolId = protocolSeparatorPos < 0 ?
				"file" : displayPath.Substring(0, protocolSeparatorPos);
			Drawable drawable = App.Kp2a.GetResourceDrawable("ic_storage_" + protocolId);
			FindViewById<ImageView>(Resource.Id.filestorage_logo).SetImageDrawable(drawable);

			String title = App.Kp2a.GetResourceString("filestoragename_" + protocolId);
			FindViewById<TextView>(Resource.Id.filestorage_label).Text = title;

			FindViewById<TextView>(Resource.Id.label_filename).Text = protocolSeparatorPos < 0 ?
				displayPath :
				displayPath.Substring(protocolSeparatorPos + 3);

		}

		private void SetDefaultIoc()
		{
			File directory = GetExternalFilesDir(null);
			if (directory == null)
				directory = FilesDir;

			string strDir = directory.CanonicalPath;
			if (!strDir.EndsWith(File.Separator))
				strDir += File.Separator;

			string filename = strDir + "keepass.kdbx";
			filename = ConvertFilenameToIocPath(filename);
			int count = 2;
			while (new File(filename).Exists())
			{
				filename = ConvertFilenameToIocPath(strDir + "keepass" + count + ".kdbx");
				count++;
			}
			
			_ioc = new IOConnectionInfo
				{
					Path = filename
				};
		}

		private static string SdDir
		{
			get
			{
				string sdDir = Environment.ExternalStorageDirectory.AbsolutePath;
				if (!sdDir.EndsWith("/"))
					sdDir += "/";
				if (!sdDir.StartsWith("file://"))
					sdDir = "file://" + sdDir;
				return sdDir;
			}
		}

		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);

			if (resultCode == KeePass.ResultOkPasswordGenerator)
			{
				String generatedPassword = data.GetStringExtra("keepass2android.password.generated_password");
				FindViewById<TextView>(Resource.Id.entry_password).Text = generatedPassword;
				FindViewById<TextView>(Resource.Id.entry_confpassword).Text = generatedPassword;
			}

			if (resultCode == KeePass.ExitFileStorageSelectionOk)
			{
				string protocolId = data.GetStringExtra("protocolId");
				if (protocolId == "content")
				{
					Util.ShowBrowseDialog(this, RequestCodeDbFilename, true, true);
				}
				else
				{
					App.Kp2a.GetFileStorage(protocolId).StartSelectFile(new FileStorageSetupInitiatorActivity(this,
							OnActivityResult,
							defaultPath =>
							{
								if (defaultPath.StartsWith("sftp://"))
									Util.ShowSftpDialog(this, OnReceiveSftpData, () => { });
								else
									Util.ShowFilenameDialog(this, OnCreateButton, null, null, false, defaultPath, GetString(Resource.String.enter_filename_details_url),
													Intents.RequestCodeFileBrowseForOpen);
							}
							), true, RequestCodeDbFilename, protocolId);	
				}
				
			}

			if (resultCode == Result.Ok)
			{
				if (requestCode == RequestCodeKeyFile)
				{
					string filename = Util.IntentToFilename(data, this);
					if (filename != null)
					{
						_keyfileFilename = ConvertFilenameToIocPath(filename);
						FindViewById<TextView>(Resource.Id.keyfile_filename).Text = _keyfileFilename;
						FindViewById(Resource.Id.keyfile_filename).Visibility = ViewStates.Visible;
					}
				}
				if (requestCode == RequestCodeDbFilename)
				{
					
					if (data.Data.Scheme == "content")
					{
						if ((int)Build.VERSION.SdkInt >= 19)
						{
							//try to take persistable permissions
							try
							{
								Kp2aLog.Log("TakePersistableUriPermission");
								var takeFlags = data.Flags
										& (ActivityFlags.GrantReadUriPermission
										| ActivityFlags.GrantWriteUriPermission);
								this.ContentResolver.TakePersistableUriPermission(data.Data, takeFlags);
							}
							catch (Exception e)
							{
								Kp2aLog.LogUnexpectedError(e);
							}

						}
					}

					
					string filename = Util.IntentToFilename(data, this);
					if (filename == null)
						filename = data.DataString;

					bool fileExists = data.GetBooleanExtra("group.pals.android.lib.ui.filechooser.FileChooserActivity.result_file_exists", true);

					if (fileExists)
					{
						_ioc = new IOConnectionInfo { Path = ConvertFilenameToIocPath(filename) };
						UpdateIocView();
					}
					else
					{
						var task = new CreateNewFilename(new ActionOnFinish((success, messageOrFilename) =>
							{
								if (!success)
								{
									Toast.MakeText(this, messageOrFilename, ToastLength.Long).Show();
									return;
								}
								_ioc = new IOConnectionInfo { Path = ConvertFilenameToIocPath(messageOrFilename) };
								UpdateIocView();
								
							}), filename);

						new ProgressTask(App.Kp2a, this, task).Run();
					}

				}
				
			}
			if (resultCode == (Result)FileStorageResults.FileUsagePrepared)
			{
				_ioc = new IOConnectionInfo();
				PasswordActivity.SetIoConnectionFromIntent(_ioc, data);
				UpdateIocView();
			}
			if (resultCode == (Result)FileStorageResults.FileChooserPrepared)
			{
				IOConnectionInfo ioc = new IOConnectionInfo();
				PasswordActivity.SetIoConnectionFromIntent(ioc, data);
				StartFileChooser(ioc.Path, RequestCodeDbFilename, true);
			}

		}

		private bool OnReceiveSftpData(string filename)
		{
			StartFileChooser(filename, RequestCodeDbFilename, true);
			return true;
		}

		private static string ConvertFilenameToIocPath(string filename)
		{
			if ((filename != null) && (filename.StartsWith("file://")))
			{
				filename = filename.Substring(7);
				filename = Java.Net.URLDecoder.Decode(filename);
			}
			return filename;
		}

		private bool OnCreateButton(string filename, Dialog dialog)
		{
			// Make sure file name exists
			if (filename.Length == 0)
			{
				Toast.MakeText(this,
								Resource.String.error_filename_required,
								ToastLength.Long).Show();
				return false;
			}

			IOConnectionInfo ioc = new IOConnectionInfo { Path = filename };
			IFileStorage fileStorage;
			try
			{
				fileStorage = App.Kp2a.GetFileStorage(ioc);
			}
			catch (NoFileStorageFoundException)
			{
				Toast.MakeText(this,
								"Unexpected scheme in "+filename,
								ToastLength.Long).Show();
				return false;
			}

			if (ioc.IsLocalFile())
			{
				// Try to create the file
				File file = new File(filename);
				try
				{
					File parent = file.ParentFile;

					if (parent == null || (parent.Exists() && !parent.IsDirectory))
					{
						Toast.MakeText(this,
							            Resource.String.error_invalid_path,
							            ToastLength.Long).Show();
						return false;
					}

					if (!parent.Exists())
					{
						// Create parent dircetory
						if (!parent.Mkdirs())
						{
							Toast.MakeText(this,
								            Resource.String.error_could_not_create_parent,
								            ToastLength.Long).Show();
							return false;

						}
					}
					System.IO.File.Create(filename);

				}
				catch (IOException ex)
				{
					Toast.MakeText(
						this,
						GetText(Resource.String.error_file_not_create) + " "
						+ ex.LocalizedMessage,
						ToastLength.Long).Show();
					return false;
				}

			}
			if (fileStorage.RequiresCredentials(ioc))
			{
				Util.QueryCredentials(ioc, AfterQueryCredentials, this);
			}
			else
			{
				_ioc = ioc;
				UpdateIocView();	
			}
			

			return true;
		}

		private void AfterQueryCredentials(IOConnectionInfo ioc)
		{
			_ioc = ioc;
			UpdateIocView();	
		}

		private class LaunchGroupActivity : FileOnFinish
		{
			readonly CreateDatabaseActivity _activity;
			private readonly IOConnectionInfo _ioc;

			public LaunchGroupActivity(IOConnectionInfo ioc, CreateDatabaseActivity activity)
				: base(null)
			{
				_activity = activity;
				_ioc = ioc;
			}

			public override void Run()
			{
				if (Success)
				{
					// Update the ongoing notification
					App.Kp2a.UpdateOngoingNotification();

					if (PreferenceManager.GetDefaultSharedPreferences(_activity).GetBoolean(_activity.GetString(Resource.String.RememberRecentFiles_key), _activity.Resources.GetBoolean(Resource.Boolean.RememberRecentFiles_default))) 
					{
						// Add to recent files
						FileDbHelper dbHelper = App.Kp2a.FileDbHelper;


						//TODO: getFilename always returns "" -> bug?
						dbHelper.CreateFile(_ioc, Filename);
					}

					GroupActivity.Launch(_activity, _activity._appTask);
					_activity.Finish();

				}
				else
				{
					DisplayMessage(_activity);
					try
					{
						App.Kp2a.GetFileStorage(_ioc).Delete(_ioc);
					}
					catch (Exception e)
					{
						//not nice, but not a catastrophic failure if we can't delete the file:
						Kp2aLog.Log("couldn't delete file after failure! " + e);
					}
				}
			}
		}


	    public override bool OnOptionsItemSelected(IMenuItem item)
	    {
	        switch (item.ItemId)
	        {
	            case Android.Resource.Id.Home:
	                OnBackPressed();
	                return true;
	        }
	        return false;
	    }
	}
}
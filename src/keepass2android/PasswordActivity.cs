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
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Android.App;
using Android.Content;
using Android.Database;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.Net;
using Android.Preferences;
using Android.Text;
using Android.Content.PM;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using OtpKeyProv;
using keepass2android.Io;
using keepass2android.Utils;
using Exception = System.Exception;
using File = Java.IO.File;
using FileNotFoundException = Java.IO.FileNotFoundException;
using MemoryStream = System.IO.MemoryStream;
using Object = Java.Lang.Object;
using Process = Android.OS.Process;
using String = System.String;

namespace keepass2android
{
	[Activity(Label = "@string/app_name",
		ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden,
		LaunchMode = LaunchMode.SingleInstance,
		Theme = "@style/Base")]
	public class PasswordActivity : LockingActivity {

		enum KeyProviders
		{
			//int values correspond to indices in passwordSpinner
			None = 0,
			KeyFile = 1,
			Otp = 2,
			OtpRecovery = 3
		}

		public const String KeyDefaultFilename = "defaultFileName";

		public const String KeyFilename = "fileName";
		private const String KeyKeyfile = "keyFile";
		private const String KeyPassword = "password";
		public const String KeyServerusername = "serverCredUser";
		public const String KeyServerpassword = "serverCredPwd";
		public const String KeyServercredmode = "serverCredRememberMode";

		private const String ViewIntent = "android.intent.action.VIEW";
		private const string ShowpasswordKey = "ShowPassword";
		private const string KeyProviderIdOtp = "KP2A-OTP";
		private const string KeyProviderIdOtpRecovery = "KP2A-OTPSecret";

		private const int RequestCodePrepareDbFile = 1000;
		private const int RequestCodePrepareOtpAuxFile = 1001;


		private Task<MemoryStream> _loadDbTask;
		private IOConnectionInfo _ioConnection;
		private String _keyFileOrProvider;
		bool _showPassword;

		internal AppTask AppTask;
		private bool _killOnDestroy;
		private string _password = "";
		//OTPs which should be entered into the OTP fields as soon as these become visible
		private List<String> _pendingOtps = new List<string>();



		KeyProviders KeyProviderType
		{
			get
			{
				if (_keyFileOrProvider == null)
					return KeyProviders.None;
				if (_keyFileOrProvider == KeyProviderIdOtp)
					return KeyProviders.Otp;
				if (_keyFileOrProvider == KeyProviderIdOtpRecovery)
					return KeyProviders.OtpRecovery;
				return KeyProviders.KeyFile;
			}
		}

		private bool _rememberKeyfile;
		ISharedPreferences _prefs;

		private bool _starting;
		private OtpInfo _otpInfo;
		private readonly int[] _otpTextViewIds = new[] {Resource.Id.otp1, Resource.Id.otp2, Resource.Id.otp3, Resource.Id.otp4, Resource.Id.otp5, Resource.Id.otp6};
		private const string OtpInfoKey = "OtpInfoKey";
		private const string EnteredOtpsKey = "EnteredOtpsKey";
		private const string PendingOtpsKey = "PendingOtpsKey";
		private const string PasswordKey = "PasswordKey";
		private const string KeyFileOrProviderKey = "KeyFileOrProviderKey";

		private ActivityDesign _design;
		private bool _performingLoad;

		public PasswordActivity (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			_design = new ActivityDesign(this);
		}

		public PasswordActivity()
		{
			_design = new ActivityDesign(this);
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
			i.SetFlags(ActivityFlags.ClearTask | ActivityFlags.NewTask);
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
			i.SetFlags(ActivityFlags.ClearTask | ActivityFlags.ForwardResult);

			appTask.ToIntent(i);

			act.StartActivity(i);
			
		}

		public void LaunchNextActivity()
		{
			AppTask.AfterUnlockDatabase(this);
		}

		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);

			Kp2aLog.Log("PasswordActivity.OnActivityResult "+resultCode+"/"+requestCode);

			AppTask.TryGetFromActivityResult(data, ref AppTask);

			//NOTE: original code from k eepassdroid used switch ((Android.App.Result)requestCode) { (but doesn't work here, although k eepassdroid works)
			switch(resultCode) {

				case KeePass.ExitNormal: // Returned to this screen using the Back key
					if (PreferenceManager.GetDefaultSharedPreferences(this)
						                 .GetBoolean(GetString(Resource.String.LockWhenNavigateBack_key), false))
					{
						App.Kp2a.LockDatabase();	
					}
					//by leaving the app with the back button, the user probably wants to cancel the task
					//The activity might be resumed (through Android's recent tasks list), then use a NullTask:
					AppTask = new NullTask();
					Finish();
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
					if (requestCode == RequestCodePrepareDbFile)
						PerformLoadDatabase();
					if (requestCode == RequestCodePrepareOtpAuxFile)
						LoadOtpFile();
					break;
			}
			
		}


		private void LoadOtpFile()
		{
			new LoadingDialog<object, object, object>(this, true, 
				//doInBackground
				delegate
				{
					try
					{
						_otpInfo = OathHotpKeyProv.LoadOtpInfo(new KeyProviderQueryContext(_ioConnection, false, false));
					}
					catch (Exception e)
					{
						Kp2aLog.Log(e.ToString());
					}
					
					return null;
				},
				//onPostExecute
				delegate
					{
						if (_otpInfo == null)
						{
							Toast.MakeText(this,
								GetString(Resource.String.CouldntLoadOtpAuxFile) + " " + GetString(Resource.String.CouldntLoadOtpAuxFile_Hint)
								, ToastLength.Long).Show();
							return;
						}

						IList<string> prefilledOtps = _pendingOtps;
						ShowOtpEntry(prefilledOtps);
						_pendingOtps.Clear();
						
					}
			).Execute();
		}

		private void ShowOtpEntry(IList<string> prefilledOtps)
		{
			FindViewById(Resource.Id.otpInitView).Visibility = ViewStates.Gone;
			FindViewById(Resource.Id.otpEntry).Visibility = ViewStates.Visible;
			int c = 0;

			foreach (int otpId in _otpTextViewIds)
			{
				c++;
				var otpTextView = FindViewById<EditText>(otpId);
				if (c <= prefilledOtps.Count)
				{
					otpTextView.Text = prefilledOtps[c - 1];
				}
				else
				{
					otpTextView.Text = "";
				}
				otpTextView.Hint = GetString(Resource.String.otp_hint, new Object[] {c});
				otpTextView.SetFilters(new IInputFilter[] {new InputFilterLengthFilter((int) _otpInfo.OtpLength)});
				if (c > _otpInfo.OtpsRequired)
				{
					otpTextView.Visibility = ViewStates.Gone;
				}
				else
				{
					otpTextView.TextChanged += (sender, args) => { UpdateOkButtonState(); };
				}
			}
		}

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			_design.ApplyTheme();

			Intent i = Intent;

			//only load the AppTask if this is the "first" OnCreate (not because of kill/resume, i.e. savedInstanceState==null)
			// and if the activity is not launched from history (i.e. recent tasks) because this would mean that
			// the Activity was closed already (user cancelling the task or task complete) but is restarted due recent tasks.
			// Don't re-start the task (especially bad if tak was complete already)
			if ((savedInstanceState == null) && (Intent.Flags.HasFlag(ActivityFlags.LaunchedFromHistory)))
			{
				AppTask = new NullTask();
			}
			else
			{
				AppTask = AppTask.GetTaskInOnCreate(savedInstanceState, Intent);	
			}
			

			String action = i.Action;
			
			_prefs = PreferenceManager.GetDefaultSharedPreferences(this);
			_rememberKeyfile = _prefs.GetBoolean(GetString(Resource.String.keyfile_key), Resources.GetBoolean(Resource.Boolean.keyfile_default));

			_ioConnection = new IOConnectionInfo();


			if (action != null && action.Equals(ViewIntent))
			{
				if (!GetIocFromViewIntent(i)) return;
			} 
			else if ((action != null) && (action.Equals(Intents.StartWithOtp)))
			{
				if (!GetIocFromOtpIntent(savedInstanceState, i)) return;
			}
			else
			{
				SetIoConnectionFromIntent(_ioConnection, i);
				_keyFileOrProvider = i.GetStringExtra(KeyKeyfile);
				_password = i.GetStringExtra(KeyPassword) ?? "";
				if (string.IsNullOrEmpty(_keyFileOrProvider))
				{
					_keyFileOrProvider = GetKeyFile(_ioConnection.Path);
				}
			}

			if (App.Kp2a.GetDb().Loaded && App.Kp2a.GetDb().Ioc != null &&
				App.Kp2a.GetDb().Ioc.GetDisplayName() != _ioConnection.GetDisplayName())
			{
				// A different database is currently loaded, unload it before loading the new one requested
				App.Kp2a.LockDatabase(false);
			}

			
			
			SetContentView(Resource.Layout.password);
			InitializeFilenameView();

			if (KeyProviderType == KeyProviders.KeyFile)
				SetEditText(Resource.Id.pass_keyfile, _keyFileOrProvider);

			FindViewById<EditText>(Resource.Id.pass_keyfile).TextChanged +=
				(sender, args) =>
				{
					_keyFileOrProvider = FindViewById<EditText>(Resource.Id.pass_keyfile).Text;
					UpdateOkButtonState();
				};

			FindViewById<EditText>(Resource.Id.password).TextChanged +=
				(sender, args) =>
				{
					_password = FindViewById<EditText>(Resource.Id.password).Text;
					UpdateOkButtonState();
				};

			FindViewById<EditText>(Resource.Id.pass_otpsecret).TextChanged += (sender, args) => UpdateOkButtonState();


			EditText passwordEdit = FindViewById<EditText>(Resource.Id.password);
			passwordEdit.Text = _password;
			passwordEdit.RequestFocus();
			Window.SetSoftInputMode(SoftInput.StateVisible);

			InitializeOkButton();

			InitializePasswordModeSpinner();

			InitializeOtpSecretSpinner();

			UpdateOkButtonState();
			
			InitializeTogglePasswordButton();
			InitializeKeyfileBrowseButton();

			InitializeQuickUnlockCheckbox();

			RestoreState(savedInstanceState);

			if (i.GetBooleanExtra("launchImmediately", false))
			{
				App.Kp2a.GetFileStorage(_ioConnection)
					   .PrepareFileUsage(new FileStorageSetupInitiatorActivity(this, OnActivityResult, null), _ioConnection,
										 RequestCodePrepareDbFile, false);
			}
		}

		private void InitializeOtpSecretSpinner()
		{
			Spinner spinner = FindViewById<Spinner>(Resource.Id.otpsecret_format_spinner);
			ArrayAdapter<String> spinnerArrayAdapter = new ArrayAdapter<String>(this, Android.Resource.Layout.SimpleSpinnerDropDownItem, EncodingUtil.Formats);
			spinner.Adapter = spinnerArrayAdapter;
		}

		private bool GetIocFromOtpIntent(Bundle savedInstanceState, Intent i)
		{
			//create called after detecting an OTP via NFC
			//this means the Activity was not on the back stack before, i.e. no database has been selected

			_ioConnection = null;

			//see if we can get a database from recent:
			if (App.Kp2a.FileDbHelper.HasRecentFiles())
			{
				ICursor filesCursor = App.Kp2a.FileDbHelper.FetchAllFiles();
				StartManagingCursor(filesCursor);
				filesCursor.MoveToFirst();
				IOConnectionInfo ioc = App.Kp2a.FileDbHelper.CursorToIoc(filesCursor);
				if (App.Kp2a.GetFileStorage(ioc).RequiresSetup(ioc) == false)
				{
					IFileStorage fileStorage = App.Kp2a.GetFileStorage(ioc);

					if (!fileStorage.RequiresCredentials(ioc))
					{
						//ok, we can use this file
						_ioConnection = ioc;
					}
				}
			}

			if (_ioConnection == null)
			{
				//We need to go to FileSelectActivity first.
				//For security reasons: discard the OTP (otherwise the user might not select a database now and forget 
				//about the OTP, but it would still be stored in the Intents and later be passed to PasswordActivity again.

				Toast.MakeText(this, GetString(Resource.String.otp_discarded_because_no_db), ToastLength.Long).Show();
				GoToFileSelectActivity();
				return false;
			}

			//user obviously wants to use OTP:
			_keyFileOrProvider = KeyProviderIdOtp;

			if (savedInstanceState == null) //only when not re-creating
			{
				//remember the OTP for later use
				_pendingOtps.Add(i.GetStringExtra(Intents.OtpExtraKey));
				i.RemoveExtra(Intents.OtpExtraKey);
			}
			return true;
		}

		private bool GetIocFromViewIntent(Intent i)
		{
			//started from "view" intent (e.g. from file browser)
			_ioConnection.Path = i.DataString;

			if (! _ioConnection.Path.Substring(0, 7).Equals("file://"))
			{
				//TODO: this might no longer be required as we can handle http(s) and ftp as well (but we need server credentials therefore)
				Toast.MakeText(this, Resource.String.error_can_not_handle_uri, ToastLength.Long).Show();
				Finish();
				return false;
			}

			_ioConnection.Path = URLDecoder.Decode(_ioConnection.Path.Substring(7));

			if (_ioConnection.Path.Length == 0)
			{
				// No file name
				Toast.MakeText(this, Resource.String.FileNotFound, ToastLength.Long).Show();
				Finish();
				return false;
			}

			File dbFile = new File(_ioConnection.Path);
			if (! dbFile.Exists())
			{
				// File does not exist
				Toast.MakeText(this, Resource.String.FileNotFound, ToastLength.Long).Show();
				Finish();
				return false;
			}

			_keyFileOrProvider = GetKeyFile(_ioConnection.Path);
			return true;
		}

		private void InitializeOkButton()
		{
			Button confirmButton = (Button) FindViewById(Resource.Id.pass_ok);
			confirmButton.Click += (sender, e) =>
				{
					App.Kp2a.GetFileStorage(_ioConnection)
					   .PrepareFileUsage(new FileStorageSetupInitiatorActivity(this, OnActivityResult, null), _ioConnection,
					                     RequestCodePrepareDbFile, false);
				};
		}

		private void InitializeTogglePasswordButton()
		{
			ImageButton btnTogglePassword = (ImageButton) FindViewById(Resource.Id.toggle_password);
			btnTogglePassword.Click += (sender, e) =>
				{
					_showPassword = !_showPassword;
					MakePasswordMaskedOrVisible();
				};
		}

		private void InitializeKeyfileBrowseButton()
		{
			ImageButton browse = (ImageButton) FindViewById(Resource.Id.browse_button);
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
		}

		private void InitializePasswordModeSpinner()
		{
			Spinner passwordModeSpinner = FindViewById<Spinner>(Resource.Id.password_mode_spinner);
			if (passwordModeSpinner != null)
			{
				UpdateKeyProviderUiState();
				passwordModeSpinner.SetSelection((int) KeyProviderType);
				passwordModeSpinner.ItemSelected += (sender, args) =>
					{
						switch (args.Position)
						{
							case 0:
								_keyFileOrProvider = null;
								break;
							case 1:
								//don't set to "" to prevent losing the filename. (ItemSelected is also called during recreation!)
								_keyFileOrProvider = FindViewById<EditText>(Resource.Id.pass_keyfile).Text;
								break;
							case 2:
								_keyFileOrProvider = KeyProviderIdOtp;
								break;
							case 3:
								_keyFileOrProvider = KeyProviderIdOtpRecovery;
								break;
							default:
								throw new Exception("Unexpected position " + args.Position + " / " +
								                    ((ICursor) ((AdapterView) sender).GetItemAtPosition(args.Position)).GetString(1));
						}
						UpdateKeyProviderUiState();
					};
				FindViewById(Resource.Id.init_otp).Click += (sender, args) =>
					{
						App.Kp2a.GetOtpAuxFileStorage(_ioConnection)
						   .PrepareFileUsage(new FileStorageSetupInitiatorActivity(this, OnActivityResult, null), _ioConnection,
						                     RequestCodePrepareOtpAuxFile, false);
					};
			}
			else
			{
				//android 2.x 
				//TODO test
			}
		}

		private void RestoreState(Bundle savedInstanceState)
		{
			if (savedInstanceState != null)
			{
				_showPassword = savedInstanceState.GetBoolean(ShowpasswordKey, false);
				MakePasswordMaskedOrVisible();

				_keyFileOrProvider = FindViewById<EditText>(Resource.Id.pass_keyfile).Text = savedInstanceState.GetString(KeyFileOrProviderKey);
				_password = FindViewById<EditText>(Resource.Id.password).Text = savedInstanceState.GetString(PasswordKey);

				_pendingOtps = new List<string>(savedInstanceState.GetStringArrayList(PendingOtpsKey));
				
				string otpInfoString = savedInstanceState.GetString(OtpInfoKey);
				if (otpInfoString != null)
				{
					
					XmlSerializer xs = new XmlSerializer(typeof(OtpInfo));
					_otpInfo = (OtpInfo)xs.Deserialize(new StringReader(otpInfoString));

					var enteredOtps = savedInstanceState.GetStringArrayList(EnteredOtpsKey);

					ShowOtpEntry(enteredOtps);
				}
				
				UpdateKeyProviderUiState();
				
			}
		}

		private void UpdateOkButtonState()
		{
			switch (KeyProviderType)
			{
				case KeyProviders.None:
					FindViewById(Resource.Id.pass_ok).Enabled = true;
					break;
				case KeyProviders.KeyFile:
					FindViewById(Resource.Id.pass_ok).Enabled = _keyFileOrProvider != "" || _password != "";
					break;
				case KeyProviders.Otp:
					
					bool enabled = true;
					if (_otpInfo == null)
						enabled = false;
					else
					{
						int c = 0;
						foreach (int otpId in _otpTextViewIds)
						{
							c++;
							var otpTextView = FindViewById<EditText>(otpId);
							if ((c <= _otpInfo.OtpsRequired) && (otpTextView.Text == ""))
							{
								enabled = false;
								break;
							}
						}	
					}
					
					FindViewById(Resource.Id.pass_ok).Enabled = enabled;
					break;
				case KeyProviders.OtpRecovery:
					FindViewById(Resource.Id.pass_ok).Enabled = FindViewById<EditText>(Resource.Id.pass_otpsecret).Text != "" && _password != "";
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private void UpdateKeyProviderUiState()
		{
			FindViewById(Resource.Id.keyfileLine).Visibility = KeyProviderType == KeyProviders.KeyFile
				                                                   ? ViewStates.Visible
				                                                   : ViewStates.Gone;
			FindViewById(Resource.Id.otpView).Visibility = KeyProviderType == KeyProviders.Otp
				                                               ? ViewStates.Visible
				                                               : ViewStates.Gone;

			FindViewById(Resource.Id.otpSecretLine).Visibility = KeyProviderType == KeyProviders.OtpRecovery
															   ? ViewStates.Visible
															   : ViewStates.Gone;
			if (KeyProviderType == KeyProviders.Otp)
			{
				FindViewById(Resource.Id.otps_pending).Visibility = _pendingOtps.Count > 0 ? ViewStates.Visible : ViewStates.Gone;
			}
			UpdateOkButtonState();
		}

		private void PerformLoadDatabase()
		{
			
			//no need to check for validity of password because if this method is called, the Ok button was enabled (i.e. there was a valid password)
			CompositeKey compositeKey = new CompositeKey();
			compositeKey.AddUserKey(new KcpPassword(_password));
			if ((KeyProviderType == KeyProviders.KeyFile) && (_keyFileOrProvider != ""))
			{
				try
				{
					compositeKey.AddUserKey(new KcpKeyFile(_keyFileOrProvider));
				}
				catch (Exception e)
				{
					Kp2aLog.Log(e.ToString());
					Toast.MakeText(this, App.Kp2a.GetResourceString(UiStringKey.keyfile_does_not_exist), ToastLength.Long).Show();
					return;
				}
			}
			else if (KeyProviderType == KeyProviders.Otp)
			{

				try
				{
					var lOtps = GetOtpsFromUi();
					OathHotpKeyProv.CreateOtpSecret(lOtps, _otpInfo);
				}
				catch (Exception)
				{

					Toast.MakeText(this, GetString(Resource.String.OtpKeyError), ToastLength.Long).Show();

					return;
				}
				compositeKey.AddUserKey(new KcpCustomKey(OathHotpKeyProv.Name, _otpInfo.Secret, true));
			}
			else if (KeyProviderType == KeyProviders.OtpRecovery)
			{
				Spinner stpDataFmtSpinner = FindViewById<Spinner>(Resource.Id.otpsecret_format_spinner);
				EditText secretEdit = FindViewById<EditText>(Resource.Id.pass_otpsecret);

				byte[] pbSecret = EncodingUtil.ParseKey(secretEdit.Text, (OtpDataFmt)stpDataFmtSpinner.SelectedItemPosition);
				if (pbSecret != null)
				{
					compositeKey.AddUserKey(new KcpCustomKey(OathHotpKeyProv.Name, pbSecret, true));
				}
				else
				{
					Toast.MakeText(this, Resource.String.CouldntParseOtpSecret, ToastLength.Long).Show();
					return;
				}
			}

			CheckBox cbQuickUnlock = (CheckBox) FindViewById(Resource.Id.enable_quickunlock);
			App.Kp2a.SetQuickUnlockEnabled(cbQuickUnlock.Checked);

			//avoid password being visible while loading:
			_showPassword = false;
			MakePasswordMaskedOrVisible();

			Handler handler = new Handler();
			OnFinish onFinish = new AfterLoad(handler, this);
			_performingLoad = true;	 
			LoadDb task = (KeyProviderType == KeyProviders.Otp) ?
				new SaveOtpAuxFileAndLoadDb(App.Kp2a, _ioConnection, _loadDbTask, compositeKey, _keyFileOrProvider, onFinish, this)
				:
				new LoadDb(App.Kp2a, _ioConnection, _loadDbTask, compositeKey, _keyFileOrProvider, onFinish);
			_loadDbTask = null; // prevent accidental re-use

			SetNewDefaultFile();

			new ProgressTask(App.Kp2a, this, task).Run();
		}

		private List<string> GetOtpsFromUi()
		{
			List<string> lOtps = new List<string>();
			foreach (int otpId in _otpTextViewIds)
			{
				string otpText = FindViewById<EditText>(otpId).Text;
				if (!String.IsNullOrEmpty(otpText))
					lOtps.Add(otpText);
			}
			return lOtps;
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

			if (usageCount > 5)
			{
				DonateReminder.ShowDonateReminderIfAppropriate(this);
			}
			
		}

		private MemoryStream PreloadDbFile()
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

			outState.PutString(KeyFileOrProviderKey, _keyFileOrProvider);
			outState.PutString(PasswordKey, _password);
			outState.PutStringArrayList(PendingOtpsKey, _pendingOtps);
			if (_otpInfo != null)
			{
				outState.PutStringArrayList(EnteredOtpsKey, GetOtpsFromUi());

				var sw = new StringWriter();

				var xws = OtpInfo.XmlWriterSettings();

				XmlWriter xw = XmlWriter.Create(sw, xws);

				XmlSerializer xs = new XmlSerializer(typeof(OtpInfo));
				xs.Serialize(xw, _otpInfo);

				xw.Close();

				outState.PutString(OtpInfoKey, sw.ToString());
			}
			
			//more OTP TODO:
			// * Caching of aux file
			// *  -> implement IFileStorage in JavaFileStorage based on ListFiles
			// *  -> Sync
		}

		protected override void OnNewIntent(Intent intent)
		{
			base.OnNewIntent(intent);

			//this method is called from the NfcOtpActivity's startActivity() if the activity is already running
			//note: it's not called in other cases because OnNewIntent requires the activity to be on top already 
			//which is never the case when started from another activity (in the same task).
			//NfcOtpActivity sets the ClearTop flag to get OnNewIntent called.
			if ((intent != null) && (intent.HasExtra(Intents.OtpExtraKey)))
			{
				string otp = intent.GetStringExtra(Intents.OtpExtraKey);

				if (_otpInfo == null)
				{
					//Entering OTPs not yet initialized:
					_pendingOtps.Add(otp);
					UpdateKeyProviderUiState();
				}
				else
				{
					//Entering OTPs is initialized. Write OTP into first empty field:
					bool foundEmptyField = false;
					foreach (int otpId in _otpTextViewIds)
					{
						EditText otpEdit = FindViewById<EditText>(otpId);
						if ((otpEdit.Visibility == ViewStates.Visible) && String.IsNullOrEmpty(otpEdit.Text))
						{
							otpEdit.Text = otp;
							foundEmptyField = true;
							break;
						}
					}
					//did we find a field?
					if (!foundEmptyField)
					{
						Toast.MakeText(this, GetString(Resource.String.otp_discarded_no_space), ToastLength.Long).Show();
					}
				}

				Spinner passwordModeSpinner = FindViewById<Spinner>(Resource.Id.password_mode_spinner);
				if (passwordModeSpinner.SelectedItemPosition != (int) KeyProviders.Otp)
				{
					passwordModeSpinner.SetSelection((int)KeyProviders.Otp);
				}
			}
	
		}
		
		protected override void OnResume()
		{
			base.OnResume();

			_design.ReapplyTheme();

			View killButton = FindViewById(Resource.Id.kill_app);
			if (PreferenceManager.GetDefaultSharedPreferences(this)
								 .GetBoolean(GetString(Resource.String.show_kill_app_key), false))
			{
				killButton.Click += (sender, args) =>
				{
					_killOnDestroy = true;
					Finish();

				};
				killButton.Visibility = ViewStates.Visible;

			}
			else
			{
				killButton.Visibility = ViewStates.Gone;
			}

			MakePasswordMaskedOrVisible();

			UpdateOkButtonState();

			// OnResume is run every time the activity comes to the foreground. This code should only run when the activity is started (OnStart), but must
			// be run in OnResume rather than OnStart so that it always occurrs after OnActivityResult (when re-creating a killed activity, OnStart occurs before OnActivityResult)
			//use !IsFinishing to make sure we're not starting another activity when we're already finishing (e.g. due to TaskComplete in OnActivityResult)
			//use !performingLoad to make sure we're not already loading the database (after ActivityResult from File-Prepare-Activity; this would cause _loadDbTask to exist when we reload later!)
			if (_starting && !IsFinishing && !_performingLoad)  
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

					//check if pre-loading is enabled but wasn't started yet:
					if (_loadDbTask == null && _prefs.GetBoolean(GetString(Resource.String.PreloadDatabaseEnabled_key), true))
					{
						// Create task to kick off file loading while the user enters the password
						_loadDbTask = Task.Factory.StartNew<MemoryStream>(PreloadDbFile);
					}
				}
			}
		}
		
		private void InitializeQuickUnlockCheckbox() {
			CheckBox cbQuickUnlock = (CheckBox)FindViewById(Resource.Id.enable_quickunlock);
			cbQuickUnlock.Checked = _prefs.GetBoolean(GetString(Resource.String.QuickUnlockDefaultEnabled_key), true);
		}
		
		private String GetKeyFile(String filename) {
			if ( _rememberKeyfile ) {
				string keyfile = App.Kp2a.FileDbHelper.GetKeyFileForFile(filename);
				if (keyfile == "")
					return null; //signal no key file
				return keyfile;
			} else {
				return null;
			}
		}
		
		private void InitializeFilenameView() {
			SetEditText(Resource.Id.filename, App.Kp2a.GetFileStorage(_ioConnection).GetDisplayName(_ioConnection));
			if (App.Kp2a.FileDbHelper.NumberOfRecentFiles() < 2)
			{
				FindViewById(Resource.Id.filename_group).Visibility = ViewStates.Gone;
			}
			else
			{
				FindViewById(Resource.Id.filename_group).Visibility = ViewStates.Visible;
			}
			
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			if (_killOnDestroy)
				Process.KillProcess(Process.MyPid());
		}
		
		/*
	private void errorMessage(CharSequence text)
	{
		Toast.MakeText(this, text, ToastLength.Long).Show();
	}
	*/

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
			intent.AddFlags(ActivityFlags.ForwardResult);
			StartActivity(intent);
			Finish();
		}

		private class AfterLoad : OnFinish {
			readonly PasswordActivity _act;

			public AfterLoad(Handler handler, PasswordActivity act):base(handler)
			{
				_act = act;
			}
				

			public override void Run()
			{
				if ( Success ) 
				{
					_act.SetEditText(Resource.Id.password, "");
					_act.SetEditText(Resource.Id.pass_otpsecret, "");
					foreach (int otpId in  _act._otpTextViewIds)
					{
						_act.SetEditText(otpId, "");
					}

					_act.LaunchNextActivity();


					GC.Collect(); // Ensure temporary memory used while loading is collected
				} 
				DisplayMessage(_act);

				_act._performingLoad = false;

			}
		}

		class SaveOtpAuxFileAndLoadDb : LoadDb
		{
			private readonly PasswordActivity _act;


			public SaveOtpAuxFileAndLoadDb(IKp2aApp app, IOConnectionInfo ioc, Task<MemoryStream> databaseData, CompositeKey compositeKey, string keyfileOrProvider, OnFinish finish, PasswordActivity act) : base(app, ioc, databaseData, compositeKey, keyfileOrProvider, finish)
			{
				_act = act;
			}

			public override void Run()
			{
				try
				{
					StatusLogger.UpdateMessage(UiStringKey.SavingOtpAuxFile);

					KeyProviderQueryContext ctx = new KeyProviderQueryContext(_act._ioConnection, false, false);
					IOConnectionInfo auxFileIoc = OathHotpKeyProv.GetAuxFileIoc(_act._ioConnection);
					if (!OathHotpKeyProv.CreateAuxFile(_act._otpInfo, ctx, auxFileIoc))
						Toast.MakeText(_act, _act.GetString(Resource.String.ErrorUpdatingOtpAuxFile), ToastLength.Long).Show();

					App.Kp2a.GetDb().OtpAuxFileIoc = auxFileIoc;
				}
				catch (Exception e)
				{
					Kp2aLog.Log(e.Message);

					Toast.MakeText(_act, _act.GetString(Resource.String.ErrorUpdatingOtpAuxFile) + " " + e.Message,
								   ToastLength.Long).Show();
				}


				base.Run();

			}
		}
		
	}
}


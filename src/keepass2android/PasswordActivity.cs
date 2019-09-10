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
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Android;
using Android.App;
using Android.Content;
using Android.Database;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using Java.Net;
using Android.Preferences;
using Android.Text;
using Android.Content.PM;
using Android.Graphics;
using Android.Support.Design.Widget;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Java.Lang;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using Keepass2android.Pluginsdk;
using OtpKeyProv;
using keepass2android.Io;
using keepass2android.Utils;

using File = Java.IO.File;
using FileNotFoundException = Java.IO.FileNotFoundException;

using Object = Java.Lang.Object;
using Process = Android.OS.Process;

using KeeChallenge;
using AlertDialog = Android.App.AlertDialog;
using ClipboardManager = Android.Content.ClipboardManager;
using Enum = System.Enum;
using Exception = System.Exception;
using String = System.String;

namespace keepass2android
{
    [Activity(Label = "@string/app_name",
		ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden,
		LaunchMode = LaunchMode.SingleInstance,
		WindowSoftInputMode = SoftInput.AdjustResize,
		Theme = "@style/MyTheme_Blue")] 
	public class PasswordActivity : LockingActivity, IFingerprintAuthCallback
	{

		enum KeyProviders
		{
			KeyFile = 1,
			Otp = 2,
			OtpRecovery = 3,
			Challenge = 4,
			ChalRecovery = 5,
			ChallengeXC = 6, //KeepassXC compatible Challenge-Response
        }

		public const String KeyDefaultFilename = "defaultFileName";

	    public const String KeyKeyfile = "keyFile";
	    public const String KeyPassword = "password";

		private const string ShowpasswordKey = "ShowPassword";
		private const string KeyProviderIdOtp = "KP2A-OTP";
		private const string KeyProviderIdOtpRecovery = "KP2A-OTPSecret";
		private const string KeyProviderIdChallenge = "KP2A-Chal";
		private const string KeyProviderIdChallengeRecovery = "KP2A-ChalSecret";
		private const string KeyProviderIdChallengeXC = "KP2A-ChalXC";


        private const int RequestCodePrepareDbFile = 1000;
		private const int RequestCodePrepareOtpAuxFile = 1001;
        
		private const int RequestCodeSelectKeyfile = 1003;
		private const int RequestCodePrepareKeyFile = 1004;
		private const int RequestCodeSelectAuxFile = 1005;

	    public const int ResultSelectOtherFile = (int) Result.FirstUser;


        private Task<MemoryStream> _loadDbFileTask;
		private bool _loadDbTaskOffline; //indicate if preloading was started with offline mode

		private IOConnectionInfo _ioConnection;
		private String _keyFile;
		bool _showPassword;

		private bool _killOnDestroy;
		private string _password = "";
		//OTPs which should be entered into the OTP fields as soon as these become visible
		private List<String> _pendingOtps = new List<string>();


	    private HashSet<KeyProviders> KeyProviderTypes = new HashSet<KeyProviders>();
		
		private bool _rememberKeyfile;
		ISharedPreferences _prefs;

		private bool _starting;
		private OtpInfo _otpInfo;
		private IOConnectionInfo _otpAuxIoc;
        private ChallengeInfo _chalInfo;
        private byte[] _challengeSecret;
        private KeeChallengeProv _challengeProv;
		private readonly int[] _otpTextViewIds = new[] {Resource.Id.otp1, Resource.Id.otp2, Resource.Id.otp3, Resource.Id.otp4, Resource.Id.otp5, Resource.Id.otp6};
		private const string OtpInfoKey = "OtpInfoKey";
		private const string EnteredOtpsKey = "EnteredOtpsKey";
		private const string PendingOtpsKey = "PendingOtpsKey";
		private const string PasswordKey = "PasswordKey";
		private const string KeyFileOrProviderKey = "KeyFileOrProviderKey";


		private bool _performingLoad;
		private bool _keepPasswordInOnResume;
	    private DateTime _lastOnPauseTime = DateTime.MinValue;
	    

        private ActionBarDrawerToggle mDrawerToggle;
	    private DrawerLayout _drawerLayout;


	    public PasswordActivity (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
	    {
		    _activityDesign = new ActivityDesign(this);
	    }

		public PasswordActivity()
		{
			_activityDesign = new ActivityDesign(this);
		}


        //can be set before launching the Activity. Will be used once to immediately open the database
        static CompositeKey compositeKeyForImmediateLoad = null;


        public static void Launch(Activity act, IOConnectionInfo ioc, CompositeKey compositeKey, ActivityLaunchMode launchMode, bool makeCurrent)
	    {
	        compositeKeyForImmediateLoad = compositeKey;
	        Launch(act, ioc, launchMode, makeCurrent);
	    }

		

		public static void Launch(Activity act, IOConnectionInfo ioc, ActivityLaunchMode launchMode, bool makeCurrent)
		{
			Intent i = new Intent(act, typeof(PasswordActivity));
		    Util.PutIoConnectionToIntent(ioc, i);
		    i.PutExtra("MakeCurrent", makeCurrent);

		    launchMode.Launch(act, i);
		}

		public void LaunchNextActivity()
		{
            //StackBaseActivity will launch the next activity
            Intent data = new Intent();
		    data.PutExtra("ioc", IOConnectionInfo.SerializeToString(_ioConnection));

		    SetResult(Result.Ok, data);

		    Finish();
		}

		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);
			_keepPasswordInOnResume = true; 
			Kp2aLog.Log("PasswordActivity.OnActivityResult "+resultCode+"/"+requestCode);

			switch(resultCode) {

				
				case Result.Ok:
					if (requestCode == RequestCodeSelectKeyfile) 
					{
						IOConnectionInfo ioc = new IOConnectionInfo();
					    Util.SetIoConnectionFromIntent(ioc, data);
						_keyFile = IOConnectionInfo.SerializeToString(ioc);
						UpdateKeyfileIocView();
					}
					break;
				case (Result)FileStorageResults.FileUsagePrepared:
					if (requestCode == RequestCodePrepareDbFile)
					{
						if (KeyProviderTypes.Contains(KeyProviders.KeyFile))
						{

                            //if the user has not yet selected a keyfile, _keyFile is empty 
                            if (string.IsNullOrEmpty(_keyFile) == false)
							{
								var iocKeyfile = IOConnectionInfo.UnserializeFromString(_keyFile);

								App.Kp2a.GetFileStorage(iocKeyfile)
									.PrepareFileUsage(new FileStorageSetupInitiatorActivity(this, OnActivityResult, null), iocKeyfile,
											 RequestCodePrepareKeyFile, false);	
							}
							
						}
						else
							PerformLoadDatabase();
					}
					if (requestCode == RequestCodePrepareKeyFile)
					{
						PerformLoadDatabase();
					}
					if (requestCode == RequestCodePrepareOtpAuxFile)
					{
						GetAuxFileLoader().LoadAuxFile(true);
					}
					break;              
			}
			if (requestCode == RequestCodeSelectAuxFile && resultCode == Result.Ok)
			{
				IOConnectionInfo auxFileIoc = new IOConnectionInfo();
			    Util.SetIoConnectionFromIntent(auxFileIoc, data);
				
				PreferenceManager.GetDefaultSharedPreferences(this).Edit()
				                 .PutString("KP2A.PasswordAct.AuxFileIoc" + IOConnectionInfo.SerializeToString(_ioConnection), 
											IOConnectionInfo.SerializeToString(auxFileIoc))
				                 .Apply();
				
				GetAuxFileLoader().LoadAuxFile(false);
			}
		    if (requestCode == RequestCodeChallengeYubikey)
		    {
		        if (CurrentlyWaitingKey != null)
		        {
                    //ActivityResult was handled in base class already
		            return;
		        }

                if (resultCode == Result.Ok)
		        {

		            try
		            {
		                byte[] challengeResponse = data.GetByteArrayExtra("response");
		                _challengeProv = new KeeChallengeProv();
		                _challengeSecret = _challengeProv.GetSecret(_chalInfo, challengeResponse);
		                Array.Clear(challengeResponse, 0, challengeResponse.Length);
		            }
		            catch (Exception e)
		            {
		                Kp2aLog.Log(e.ToString());
		                Toast.MakeText(this, "Error: " + e.Message, ToastLength.Long).Show();
		                return;
		            }

		            UpdateOkButtonState();
		            FindViewById(Resource.Id.otpInitView).Visibility = ViewStates.Gone;

		            if (_challengeSecret != null)
		            {
		                new LoadingDialog<object, object, object>(this, true,
		                    //doInBackground
		                    delegate
		                    {
		                        //save aux file
		                        try
		                        {
		                            ChallengeInfo temp = _challengeProv.Encrypt(_challengeSecret);
		                            if (!temp.Save(_otpAuxIoc))
		                            {
		                                Toast.MakeText(this, Resource.String.ErrorUpdatingChalAuxFile, ToastLength.Long)
		                                    .Show();
		                                return false;
		                            }

		                        }
		                        catch (Exception e)
		                        {
		                            Kp2aLog.LogUnexpectedError(e);
		                        }
		                        return null;
		                    }
		                    , delegate
		                    {

		                    }).Execute();

		            }
		            else
		            {
		                Toast.MakeText(this, Resource.String.bad_resp, ToastLength.Long).Show();
		            }
		        }
		    }
		}

	    

	    private AuxFileLoader GetAuxFileLoader()
		{
		    if (KeyProviderTypes.Contains(KeyProviders.Challenge))
            {
				return new ChallengeAuxFileLoader(this);
			}
			else
			{
				return new OtpAuxFileLoader(this);
			}
		}
		private void UpdateKeyfileIocView()
		{
			//store keyfile in the view so that we can show the selected keyfile again if the user switches to another key provider and back to key file
			FindViewById<TextView>(Resource.Id.label_keyfilename).Tag = _keyFile;
			if (string.IsNullOrEmpty(_keyFile))
			{
				FindViewById<TextView>(Resource.Id.filestorage_label).Visibility = ViewStates.Gone;
				FindViewById<ImageView>(Resource.Id.filestorage_logo).Visibility = ViewStates.Gone;
				FindViewById<TextView>(Resource.Id.label_keyfilename).Text = Resources.GetString(Resource.String.no_keyfile_selected);
			
				return;
			}
			var ioc = IOConnectionInfo.UnserializeFromString(_keyFile);
			string displayPath = App.Kp2a.GetFileStorage(ioc).GetDisplayName(ioc);
			int protocolSeparatorPos = displayPath.IndexOf("://", StringComparison.Ordinal);
			string protocolId = protocolSeparatorPos < 0 ?
				"file" : displayPath.Substring(0, protocolSeparatorPos);
			Drawable drawable = App.Kp2a.GetResourceDrawable("ic_storage_" + protocolId);
			FindViewById<ImageView>(Resource.Id.filestorage_logo).SetImageDrawable(drawable);
			FindViewById<ImageView>(Resource.Id.filestorage_logo).Visibility = ViewStates.Visible;
			

			String title = App.Kp2a.GetResourceString("filestoragename_" + protocolId);
			FindViewById<TextView>(Resource.Id.filestorage_label).Text = title;
			FindViewById<TextView>(Resource.Id.filestorage_label).Visibility = ViewStates.Visible;

			FindViewById<TextView>(Resource.Id.label_keyfilename).Text = protocolSeparatorPos < 0 ?
				displayPath :
				displayPath.Substring(protocolSeparatorPos + 3);

		}


		
		private abstract class AuxFileLoader
		{
			protected readonly PasswordActivity Activity;

			protected AuxFileLoader(PasswordActivity activity)
			{
				Activity = activity;
			}

			public void LoadAuxFile(bool triggerSelectAuxManuallyOnFailure)
			{
				new LoadingDialog<object, object, object>(Activity, true,
					//doInBackground
						delegate
						{

							try
							{
								var iocAux = GetDefaultAuxLocation();
								LoadFile(iocAux);
							}
							catch (Exception e)
							{
								//this can happen e.g. if the file storage does not support GetParentPath
								Kp2aLog.Log(e.ToString());
								//retry with saved ioc
								try
								{
									var savedManualIoc = IOConnectionInfo.UnserializeFromString(
										PreferenceManager.GetDefaultSharedPreferences(Activity).GetString(
											"KP2A.PasswordAct.AuxFileIoc" + IOConnectionInfo.SerializeToString(Activity._ioConnection), null));

									LoadFile((savedManualIoc));
								}
								catch (Exception e2)
								{
									Kp2aLog.LogUnexpectedError(e2);
								}

							}
							return null;
						}
						, delegate
							{
								if (!AuxDataLoaded)
								{
									if (triggerSelectAuxManuallyOnFailure)
									{
										Intent intent = new Intent(Activity, typeof(SelectStorageLocationActivity));
										intent.PutExtra(FileStorageSelectionActivity.AllowThirdPartyAppGet, true);
										intent.PutExtra(FileStorageSelectionActivity.AllowThirdPartyAppSend, false);
										intent.PutExtra(FileStorageSetupDefs.ExtraIsForSave, false);
										intent.PutExtra(SelectStorageLocationActivity.ExtraKeyWritableRequirements, (int)SelectStorageLocationActivityBase.WritableRequirements.WriteDemanded);
										Activity.StartActivityForResult(intent, RequestCodeSelectAuxFile);
									}
									else
									{
										Toast.MakeText(Activity,GetErrorMessage(), ToastLength.Long).Show();
									}
									return;

								}
								HandleSuccess();
							}).Execute();

			}

			protected abstract bool AuxDataLoaded { get; }

			protected abstract void LoadFile(IOConnectionInfo iocAux);

			protected abstract void HandleSuccess();

			protected abstract string GetErrorMessage();

			protected abstract IOConnectionInfo GetDefaultAuxLocation();

			
			
			
		}

		private class OtpAuxFileLoader : AuxFileLoader
		{
			public OtpAuxFileLoader(PasswordActivity activity) : base(activity)
			{
			}

			protected override bool AuxDataLoaded
			{
				get { return Activity._otpInfo != null; }
			}

			protected override void LoadFile(IOConnectionInfo iocAux)
			{
				Activity._otpInfo = OtpInfo.Load(iocAux);
				Activity._otpAuxIoc = iocAux;
			}

			private static IOConnectionInfo GetAuxFileIoc(IOConnectionInfo databaseIoc)
			{
				IFileStorage fileStorage = App.Kp2a.GetOtpAuxFileStorage(databaseIoc);
				var parentPath = fileStorage.GetParentPath(databaseIoc);
				var filename = fileStorage.GetFilenameWithoutPathAndExt(databaseIoc) + OathHotpKeyProv.AuxFileExt;
				IOConnectionInfo iocAux = fileStorage.GetFilePath(parentPath, filename);
				return iocAux;
			}
            

			protected override void HandleSuccess()
			{
				IList<string> prefilledOtps = Activity._pendingOtps;
				Activity.ShowOtpEntry(prefilledOtps);
				Activity._pendingOtps.Clear();

			}

			protected override string GetErrorMessage()
			{
				return Activity.GetString(Resource.String.CouldntLoadOtpAuxFile) + " " +
					Activity.GetString(Resource.String.CouldntLoadOtpAuxFile_Hint);
			}

			protected override IOConnectionInfo GetDefaultAuxLocation()
			{
				return GetAuxFileIoc(Activity._ioConnection);
			}
		}

		private class ChallengeAuxFileLoader : AuxFileLoader
		{
			public ChallengeAuxFileLoader(PasswordActivity activity) : base(activity)
			{
			}

			protected override void HandleSuccess()
			{
			    var chalIntent = Activity.TryGetYubichallengeIntentOrPrompt(Activity._chalInfo.Challenge, true);

                if (chalIntent != null)
			    {
			        Activity.StartActivityForResult(chalIntent, RequestCodeChallengeYubikey);
                }
			}

			protected override string GetErrorMessage()
			{
				return Activity.GetString(Resource.String.CouldntLoadChalAuxFile) +
					   " " +
					   Activity.GetString(
						   Resource.String.CouldntLoadChalAuxFile_Hint);
			}

			protected override bool AuxDataLoaded
			{
				get { return Activity._chalInfo != null; }
			}

			protected override void LoadFile(IOConnectionInfo iocAux)
			{
				Activity._chalInfo = ChallengeInfo.Load(iocAux);
				Activity._otpAuxIoc = iocAux;
			}


			protected override IOConnectionInfo GetDefaultAuxLocation()
			{
				IFileStorage fileStorage =
					App.Kp2a.GetOtpAuxFileStorage(Activity._ioConnection);
				IOConnectionInfo iocAux =
					fileStorage.GetFilePath(
						fileStorage.GetParentPath(Activity._ioConnection),
						fileStorage.GetFilenameWithoutPathAndExt(Activity._ioConnection) +
						".xml");
				return iocAux;
			}
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

		int count = 1;

	    
		private string mDrawerTitle;
		private MeasuringRelativeLayout.MeasureArgs _measureArgs;
		private ActivityDesign _activityDesign;
		private FingerprintDecryption _fingerprintDec;
		private bool _fingerprintPermissionGranted;
		private PasswordActivityBroadcastReceiver _intentReceiver;
		private int _appnameclickCount;


		internal class MyActionBarDrawerToggle : ActionBarDrawerToggle
		{
			PasswordActivity owner;

			public MyActionBarDrawerToggle(PasswordActivity activity, DrawerLayout layout, int openRes, int closeRes)
				: base(activity, layout, openRes, closeRes)
			{
				owner = activity;
			}

			public override void OnDrawerClosed(View drawerView)
			{
				owner.SupportActionBar.Title = owner.Title;
				owner.InvalidateOptionsMenu();
			}

			public override void OnDrawerOpened(View drawerView)
			{
				owner.SupportActionBar.Title = owner.mDrawerTitle;
				owner.InvalidateOptionsMenu();
			}
		}
		private void UncollapseToolbar()
		{
			AppBarLayout appbarLayout = FindViewById<AppBarLayout>(Resource.Id.appbar);
			var tmp = appbarLayout.LayoutParameters;
			CoordinatorLayout.LayoutParams p = tmp.JavaCast<CoordinatorLayout.LayoutParams>();
			var tmp2 = p.Behavior;
			var behavior = tmp2.JavaCast<AppBarLayout.Behavior>();
			if (behavior == null)
			{
				p.Behavior = behavior = new AppBarLayout.Behavior();
			}
			behavior.OnNestedFling(FindViewById<CoordinatorLayout>(Resource.Id.main_content), appbarLayout, null, 0, -10000, false);
		}

		private void CollapseToolbar()
		{
			AppBarLayout appbarLayout = FindViewById<AppBarLayout>(Resource.Id.appbar);
			ViewGroup.LayoutParams tmp = appbarLayout.LayoutParameters;
			CoordinatorLayout.LayoutParams p = tmp.JavaCast<CoordinatorLayout.LayoutParams>();
			var tmp2 = p.Behavior;
			var behavior = tmp2.JavaCast<AppBarLayout.Behavior>();
			if (behavior == null)
			{
				p.Behavior = behavior = new AppBarLayout.Behavior();
			}
			behavior.OnNestedFling(FindViewById<CoordinatorLayout>(Resource.Id.main_content), appbarLayout, null, 0, 200, true);
		}

	    readonly PasswordFont _passwordFont = new PasswordFont();

		protected override void OnCreate(Bundle savedInstanceState)
		{
			_activityDesign.ApplyTheme();
			base.OnCreate(savedInstanceState);

		    _intentReceiver = new PasswordActivityBroadcastReceiver(this);
			IntentFilter filter = new IntentFilter();
			filter.AddAction(Intent.ActionScreenOff);
			RegisterReceiver(_intentReceiver, filter);
			
			
			//use FlagSecure to make sure the last (revealed) character of the master password is not visible in recent apps
			if (PreferenceManager.GetDefaultSharedPreferences(this).GetBoolean(
				GetString(Resource.String.ViewDatabaseSecure_key), true))
			{
				Window.SetFlags(WindowManagerFlags.Secure, WindowManagerFlags.Secure);
			}

			Intent i = Intent;


			String action = i.Action;

			_prefs = PreferenceManager.GetDefaultSharedPreferences(this);
			_rememberKeyfile = _prefs.GetBoolean(GetString(Resource.String.keyfile_key), Resources.GetBoolean(Resource.Boolean.keyfile_default));

			_ioConnection = new IOConnectionInfo();


            if ((action != null) && (action.Equals(Intents.StartWithOtp)))
			{
				if (!GetIocFromOtpIntent(savedInstanceState, i)) return;
				_keepPasswordInOnResume = true;
			}
			else
			{
			    Util.SetIoConnectionFromIntent(_ioConnection, i);
				var keyFileFromIntent = i.GetStringExtra(KeyKeyfile);
				if (keyFileFromIntent != null)
				{
					Kp2aLog.Log("try get keyfile from intent");
				    _keyFile = IOConnectionInfo.SerializeToString(IOConnectionInfo.FromPath(keyFileFromIntent));
                    KeyProviderTypes.Clear();
				    KeyProviderTypes.Add(KeyProviders.KeyFile);
					Kp2aLog.Log("try get keyfile from intent ok");
				}
				else
				{
					_keyFile = null;
				    KeyProviderTypes.Clear();
                }
				_password = i.GetStringExtra(KeyPassword) ?? "";
				if (!KeyProviderTypes.Any())
				{
                    SetKeyProviderFromString(LoadKeyProviderStringForIoc(_ioConnection.Path));
				}
				if ((!string.IsNullOrEmpty(_keyFile)) || (_password != ""))
				{
					_keepPasswordInOnResume = true;
				}

			}

		    App.Kp2a.RegisterOpenAttempt(_ioConnection);

			SetContentView(Resource.Layout.password);

			InitializeToolbar();

			InitializeFilenameView();

			if (KeyProviderTypes.Contains(KeyProviders.KeyFile))
			{
				UpdateKeyfileIocView();
			}

			var passwordEdit = FindViewById<EditText>(Resource.Id.password_edit);
			passwordEdit.TextChanged +=
				(sender, args) =>
				{
					_password = passwordEdit.Text;
					UpdateOkButtonState();
				};
			passwordEdit.EditorAction += (sender, args) =>
			{
				if ((args.ActionId == ImeAction.Done) || ((args.ActionId == ImeAction.ImeNull) && (args.Event.Action == KeyEventActions.Down)))
					OnOk();
			};


			FindViewById<EditText>(Resource.Id.pass_otpsecret).TextChanged += (sender, args) => UpdateOkButtonState();

			passwordEdit.Text = _password;

			var passwordFont = Typeface.CreateFromAsset(Assets, "SourceCodePro-Regular.ttf");
			passwordEdit.Typeface = passwordFont;


			InitializeBottomBarButtons();

			InitializePasswordModeSpinner();

			InitializeOtpSecretSpinner();

			InitializeNavDrawerButtons();

			UpdateOkButtonState();

			InitializeTogglePasswordButton();
			InitializeKeyfileBrowseButton();

			InitializeOptionCheckboxes();

			RestoreState(savedInstanceState);

			if (i.GetBooleanExtra("launchImmediately", false))
			{
				App.Kp2a.GetFileStorage(_ioConnection)
					   .PrepareFileUsage(new FileStorageSetupInitiatorActivity(this, OnActivityResult, null), _ioConnection,
										 RequestCodePrepareDbFile, false);
			}


			mDrawerTitle = Title;
			InitializeToolbarCollapsing();

		    if ((int)Build.VERSION.SdkInt >= 23)
				RequestPermissions(new[] { Manifest.Permission.UseFingerprint }, FingerprintPermissionRequestCode);

            
		    if (App.Kp2a.TrySelectCurrentDb(_ioConnection))
            { 
                //database already opened. return the ioc and we're good.
		        LaunchNextActivity();
		    }
			
		}

	    private void InitializeToolbarCollapsing()
	    {
	        var rootview = FindViewById<MeasuringRelativeLayout>(Resource.Id.relative_layout);
	        rootview.ViewTreeObserver.GlobalLayout += (sender, args2) =>
	        {
	            Android.Util.Log.Debug("KP2A", "GlobalLayout");
	            if (_measureArgs == null)
	                return;
	            Android.Util.Log.Debug("KP2A", "ActualHeight=" + _measureArgs.ActualHeight);
	            Android.Util.Log.Debug("KP2A", "ProposedHeight=" + _measureArgs.ProposedHeight);
	            if (_measureArgs.ActualHeight < _measureArgs.ProposedHeight)
	                UncollapseToolbar();
	            if (_measureArgs.ActualHeight > _measureArgs.ProposedHeight)
	                CollapseToolbar();
	        };
	        rootview.MeasureEvent += (sender, args) => { _measureArgs = args; };
	    }

	    private const string Kp2aKeyProviderStringPrefix = "_KP2A_KEYTYPES:";

        private string GetKeyProviderString()
	    {
	        if (!KeyProviderTypes.Any())
	            return null;
	        string result = Kp2aKeyProviderStringPrefix;
	        foreach (KeyProviders type in KeyProviderTypes)
	        {
	            result += type.ToString();

                if (type == KeyProviders.KeyFile)
	            {
	                result += WebUtility.UrlEncode(_keyFile) + ";";
	            }
	        }
	        return result;
	    }

        private void SetKeyProviderFromString(string keyProviderString)
	    {
	        KeyProviderTypes.Clear();
            if (string.IsNullOrEmpty(keyProviderString))
            {
                _keyFile = null;
                return;
            }

	        if (keyProviderString.StartsWith(Kp2aKeyProviderStringPrefix))
	        {
	            keyProviderString = keyProviderString.Substring(Kp2aKeyProviderStringPrefix.Length);
	            foreach (string type in keyProviderString.Split(';'))
	            {
	                if (!type.Trim().Any())
	                    continue;
	                if (type.StartsWith(KeyProviders.KeyFile.ToString()))
	                {
	                    _keyFile = WebUtility.UrlDecode(type.Substring(KeyProviders.KeyFile.ToString().Length));
	                    KeyProviderTypes.Add(KeyProviders.KeyFile);
	                    continue;
	                }
	                foreach (KeyProviders providerType in Enum.GetValues(typeof(KeyProviders)))
	                {
	                    if (type == providerType.ToString())
	                    {
	                        KeyProviderTypes.Add(providerType);
	                        break;
	                    }
	                }
	            }
            }
            else
            {

                //legacy mode
                _keyFile = null;
                
                if (keyProviderString == KeyProviderIdOtp)
                    KeyProviderTypes.Add(KeyProviders.Otp);
                else if (keyProviderString == KeyProviderIdOtpRecovery)
                    KeyProviderTypes.Add(KeyProviders.OtpRecovery);
                else if (keyProviderString == KeyProviderIdChallenge)
                    KeyProviderTypes.Add(KeyProviders.Challenge);
                else if (keyProviderString == KeyProviderIdChallengeRecovery)
                    KeyProviderTypes.Add(KeyProviders.ChalRecovery);
                else if (keyProviderString == KeyProviderIdChallengeXC)
                    KeyProviderTypes.Add(KeyProviders.ChallengeXC);
                else
                {
                    KeyProviderTypes.Add(KeyProviders.KeyFile);
                    _keyFile = keyProviderString;
                }
                


                if (KeyProviderTypes.Contains(KeyProviders.KeyFile))
                {
                    //test if the filename is properly encoded. 
                    try
                    {
                        Kp2aLog.Log("test if stored filename is ok");
                        IOConnectionInfo.UnserializeFromString(_keyFile);
                        Kp2aLog.Log("...ok");
                    }
                    catch (Exception e)
                    {
                        //it's not. This is probably because we're upgrading from app version <= 45
                        //where the keyfile was stored plain text and not serialized
                        Kp2aLog.Log("no, it's not: " + e.GetType().Name);
                        var serializedKeyFile = IOConnectionInfo.SerializeToString(IOConnectionInfo.FromPath(_keyFile));
                        Kp2aLog.Log("now it is!");
                        _keyFile = serializedKeyFile;

                    }
                }
            }
        }

	    const int FingerprintPermissionRequestCode = 99;
	    

	    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
		{
			if ((requestCode == FingerprintPermissionRequestCode) && (grantResults.Length > 0) && (grantResults[0] == Permission.Granted))
			{
				var btn = FindViewById<ImageButton>(Resource.Id.fingerprintbtn);
				btn.Click += (sender, args) =>
				{
					AlertDialog.Builder b = new AlertDialog.Builder(this);
					b.SetTitle(Resource.String.fingerprint_prefs);
					b.SetMessage(btn.Tag.ToString());
					b.SetPositiveButton(Android.Resource.String.Ok, (o, eventArgs) => ((Dialog)o).Dismiss());
					if (_fingerprintDec != null)
					{
						b.SetNegativeButton(Resource.String.disable_sensor, (senderAlert, alertArgs) =>
						{
							btn.SetImageResource(Resource.Drawable.ic_fingerprint_error);
							_fingerprintDec?.StopListening();
							_fingerprintDec = null;
						});
					}
					else
					{
						b.SetNegativeButton(Resource.String.enable_sensor, (senderAlert, alertArgs) =>
						{
							InitFingerprintUnlock();
						});
					}
					b.Show();
				};
				_fingerprintPermissionGranted = true;
			}
		}

		private void ClearFingerprintUnlockData()
		{
			ISharedPreferencesEditor edit = PreferenceManager.GetDefaultSharedPreferences(this).Edit();
			edit.PutString(Database.GetFingerprintPrefKey(_ioConnection), "");
			edit.PutString(Database.GetFingerprintModePrefKey(_ioConnection), FingerprintUnlockMode.Disabled.ToString());
			edit.Commit();
		}

		public void OnFingerprintError(string message)
		{
			var btn = FindViewById<ImageButton>(Resource.Id.fingerprintbtn);

			btn.SetImageResource(Resource.Drawable.ic_fingerprint_error);
			btn.PostDelayed(() =>
			{
				btn.SetImageResource(Resource.Drawable.ic_fp_40px);
				btn.Tag = GetString(Resource.String.fingerprint_unlock_hint);
			}, 1300);
			Toast.MakeText(this, message, ToastLength.Long).Show();
		}

		public void OnFingerprintAuthSucceeded()
		{
			var btn = FindViewById<ImageButton>(Resource.Id.fingerprintbtn);

			btn.SetImageResource(Resource.Drawable.ic_fingerprint_success);

			try
			{
				var masterPassword = _fingerprintDec.DecryptStored(Database.GetFingerprintPrefKey(_ioConnection));
				_password = FindViewById<EditText>(Resource.Id.password_edit).Text = masterPassword;
			    FindViewById<EditText>(Resource.Id.password_edit).Enabled = false; //prevent accidental modification of password

            }
			catch (Java.Security.GeneralSecurityException)
			{
				HandleFingerprintKeyInvalidated();
				return;
			}
			
			btn.PostDelayed(() =>
			{
                //fire
                OnOk(true);
			    FindViewById<EditText>(Resource.Id.password_edit).Enabled = true;
			}, 500);

        }

		private void InitializeNavDrawerButtons()
	    {
	        FindViewById(Resource.Id.btn_nav_change_db).Click += (sender, args) =>
	        {
                GoToFileSelectActivity();
	        };

	        FindViewById(Resource.Id.btn_nav_donate).Click += (sender, args) =>
	        {
                Util.GotoDonateUrl(this);
	        };
			FindViewById(Resource.Id.btn_nav_donate).Visibility =
				PreferenceManager.GetDefaultSharedPreferences(this)
					.GetBoolean(GetString(Resource.String.NoDonateOption_key), false)
					? ViewStates.Gone
					: ViewStates.Visible;
	        FindViewById(Resource.Id.btn_nav_about).Click += (sender, args) =>
	        {
	            AboutDialog dialog = new AboutDialog(this);
	            dialog.Show();
	        };

	        FindViewById(Resource.Id.btn_nav_settings).Click += (sender, args) =>
	        {
                AppSettingsActivity.Launch(this);
	        };

			FindViewById(Resource.Id.nav_app_name).Click += (sender, args) =>
			{
				_appnameclickCount++;
				if (_appnameclickCount == 6)
				{
					Kp2aLog.LogUnexpectedError(new Exception("some blabla"));
					Toast.MakeText(this, "Once again and the app will crash.", ToastLength.Long).Show();
				}
					
				if (_appnameclickCount == 7)
				{
					throw new Exception("this is an easter egg crash (to test uncaught exceptions.)");
				}
					

			};

	    }

	    private void InitializeToolbar()
	    {
	        var toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.mytoolbar);

	        SetSupportActionBar(toolbar);

            var collapsingToolbar = FindViewById<CollapsingToolbarLayout>(Resource.Id.collapsing_toolbar);
            collapsingToolbar.SetTitle(GetString(Resource.String.unlock_database_title));

            _drawerLayout = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            mDrawerToggle = new ActionBarDrawerToggle(this, _drawerLayout,
                Resource.String.menu_open,
                Resource.String.menu_close);
            

            _drawerLayout?.SetDrawerListener(mDrawerToggle);

	        	        
            SupportActionBar.SetDisplayHomeAsUpEnabled(true);
            SupportActionBar.SetHomeButtonEnabled(true);
            mDrawerToggle.SyncState();

	    }

	    public override void OnBackPressed()
	    {
	        if (_drawerLayout != null)
	        {
	            if (_drawerLayout.IsDrawerOpen((int) GravityFlags.Start))
	            {
	                _drawerLayout.CloseDrawer((int) GravityFlags.Start);
	                return;
	            }
	        }
	        base.OnBackPressed();
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

			//assume user wants to use OTP (for static password, they need to open KP2A first and select the key provider type, then see OnNewIntent)
		    KeyProviderTypes.Clear();
		    KeyProviderTypes.Add(KeyProviders.Otp);

            if (savedInstanceState == null) //only when not re-creating
			{
				//remember the OTP for later use
				_pendingOtps.Add(i.GetStringExtra(Intents.OtpExtraKey));
				i.RemoveExtra(Intents.OtpExtraKey);
			}
			return true;
		}


		private void InitializeBottomBarButtons()
		{
			Button confirmButton = (Button) FindViewById(Resource.Id.pass_ok);
			confirmButton.Click += (sender, e) =>
				{
					OnOk();
				};

			var changeDbButton = FindViewById<Button>(Resource.Id.change_db);
			string label = changeDbButton.Text;
			if (label.EndsWith("\u2026"))
				changeDbButton.Text = label.Substring(0, label.Length - 1);
		    changeDbButton.Click += (sender, args) => GoToFileSelectActivity();

			Util.MoveBottomBarButtons(Resource.Id.change_db, Resource.Id.pass_ok, Resource.Id.bottom_bar, this);
		}

		private void OnOk(bool usedFingerprintUnlock = false)
		{
			UsedFingerprintUnlock = usedFingerprintUnlock;
			App.Kp2a.GetFileStorage(_ioConnection)
			   .PrepareFileUsage(new FileStorageSetupInitiatorActivity(this, OnActivityResult, null), _ioConnection,
			                     RequestCodePrepareDbFile, false);
		}

		public bool UsedFingerprintUnlock { get; set; }

		private void InitializeTogglePasswordButton()
		{
			ImageButton btnTogglePassword = (ImageButton) FindViewById(Resource.Id.toggle_password);
			btnTogglePassword.Click += (sender, e) =>
				{
					_showPassword = !_showPassword;
					MakePasswordMaskedOrVisible();
				};
			Android.Graphics.PorterDuff.Mode mMode = Android.Graphics.PorterDuff.Mode.SrcAtop;
			Color color = new Color (224, 224, 224);
			btnTogglePassword.SetColorFilter (color, mMode);
		}

		private void InitializeKeyfileBrowseButton()
		{
			var browseButton = (Button)FindViewById(Resource.Id.btn_change_location);
			browseButton.Click += (sender, evt) =>
				{
					Intent intent = new Intent(this, typeof(SelectStorageLocationActivity));
					intent.PutExtra(FileStorageSelectionActivity.AllowThirdPartyAppGet, true);
					intent.PutExtra(FileStorageSelectionActivity.AllowThirdPartyAppSend, false);
					intent.PutExtra(FileStorageSetupDefs.ExtraIsForSave, false);
					StartActivityForResult(intent, RequestCodeSelectKeyfile);
				};
		}

		private void InitializePasswordModeSpinner()
		{
			Spinner passwordModeSpinner = FindViewById<Spinner>(Resource.Id.password_mode_spinner);
			if (passwordModeSpinner != null)
			{
				UpdateKeyProviderUiState();

			    int spinnerPos = 0;
			    if (KeyProviderTypes.Contains(KeyProviders.KeyFile))
			    {
			        if (KeyProviderTypes.Contains(KeyProviders.ChallengeXC))
			            spinnerPos = 7;
			        else spinnerPos = 1;
			    }
                else if (KeyProviderTypes.Contains(KeyProviders.Otp))
			        spinnerPos = 2;
			    else if (KeyProviderTypes.Contains(KeyProviders.OtpRecovery))
			        spinnerPos = 3;
			    else if (KeyProviderTypes.Contains(KeyProviders.Challenge))
			        spinnerPos = 4;
			    else if (KeyProviderTypes.Contains(KeyProviders.ChalRecovery))
			        spinnerPos = 5;
			    else if (KeyProviderTypes.Contains(KeyProviders.ChallengeXC))
			        spinnerPos = 6;

                passwordModeSpinner.SetSelection(spinnerPos);
				passwordModeSpinner.ItemSelected += (sender, args) =>
				{
				    KeyProviderTypes.Clear();
				    _keyFile = null;
						switch (args.Position)
						{
							case 0:
								break;
							case 1:
                            //don't set to "" to prevent losing the filename. (ItemSelected is also called during recreation!)
							    _keyFile = (FindViewById(Resource.Id.label_keyfilename).Tag ?? "").ToString();
							    KeyProviderTypes.Add(KeyProviders.KeyFile);
								break;
							case 2:
							    KeyProviderTypes.Add(KeyProviders.Otp);
                            break;
							case 3:
							    KeyProviderTypes.Add(KeyProviders.OtpRecovery);
                            break;
							case 4:
							    KeyProviderTypes.Add(KeyProviders.Challenge);
                            break;
							case 5:
							    KeyProviderTypes.Add(KeyProviders.ChalRecovery);
                            break;
							case 6:
							    KeyProviderTypes.Add(KeyProviders.ChallengeXC);
							    break;
						    case 7:
						        KeyProviderTypes.Add(KeyProviders.ChallengeXC);
						        KeyProviderTypes.Add(KeyProviders.KeyFile);
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
			
		}

		private void RestoreState(Bundle savedInstanceState)
		{
			if (savedInstanceState != null)
			{
				_showPassword = savedInstanceState.GetBoolean(ShowpasswordKey, false);
				MakePasswordMaskedOrVisible();

                SetKeyProviderFromString(savedInstanceState.GetString(KeyFileOrProviderKey));
				_password = FindViewById<EditText>(Resource.Id.password_edit).Text = savedInstanceState.GetString(PasswordKey);

				_pendingOtps = new List<string>(savedInstanceState.GetStringArrayList(PendingOtpsKey));
				
				string otpInfoString = savedInstanceState.GetString(OtpInfoKey);
				if (otpInfoString != null)
				{
					
					XmlSerializer xs = new XmlSerializer(typeof(OtpInfo));

					XmlReaderSettings settings = new XmlReaderSettings() { XmlResolver = null, DtdProcessing = DtdProcessing.Ignore };
					var reader = XmlReader.Create(new StringReader(otpInfoString), settings);
				
					_otpInfo = (OtpInfo)xs.Deserialize(reader);

					var enteredOtps = savedInstanceState.GetStringArrayList(EnteredOtpsKey);

					ShowOtpEntry(enteredOtps);
				}
				
				UpdateKeyProviderUiState();
				
			}
		}

		private void UpdateOkButtonState()
		{
		    bool enabled = true;
				if (KeyProviderTypes.Contains(KeyProviders.KeyFile))
					enabled &= _keyFile != "" || _password != "";

		    if (KeyProviderTypes.Contains(KeyProviders.Otp))
		    {
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
		    }


		    if (KeyProviderTypes.Contains(KeyProviders.OtpRecovery) || KeyProviderTypes.Contains(KeyProviders.ChalRecovery))
		    {
		        enabled &= FindViewById<EditText>(Resource.Id.pass_otpsecret).Text != "";
		    }

		    if (KeyProviderTypes.Contains(KeyProviders.Challenge))
                    enabled &= _challengeSecret != null;
			
            FindViewById(Resource.Id.pass_ok).Enabled = enabled;
            
		}

		private void UpdateKeyProviderUiState()
		{
			FindViewById(Resource.Id.keyfileLine).Visibility = KeyProviderHasKeyFile()
				                                                   ? ViewStates.Visible
				                                                   : ViewStates.Gone;
			if (KeyProviderHasKeyFile())
			{
				UpdateKeyfileIocView();
			}
			
			FindViewById(Resource.Id.otpView).Visibility = KeyProviderTypes.Contains(KeyProviders.Otp)
				                                               ? ViewStates.Visible
				                                               : ViewStates.Gone;

			FindViewById(Resource.Id.otpSecretLine).Visibility = (KeyProviderTypes.Contains(KeyProviders.OtpRecovery) || KeyProviderTypes.Contains(KeyProviders.ChalRecovery))
															   ? ViewStates.Visible
															   : ViewStates.Gone;
			if (KeyProviderTypes.Contains(KeyProviders.Otp))
			{
				FindViewById(Resource.Id.otps_pending).Visibility = _pendingOtps.Count > 0 ? ViewStates.Visible : ViewStates.Gone;
			}

			if (KeyProviderTypes.Contains(KeyProviders.Challenge))
			{
				FindViewById (Resource.Id.otpView).Visibility = ViewStates.Visible;
				FindViewById(Resource.Id.otps_pending).Visibility = ViewStates.Gone;
			}
			UpdateOkButtonState();
		}

	    private bool KeyProviderHasKeyFile()
	    {
	        return KeyProviderTypes.Contains(KeyProviders.KeyFile);
	    }

	    private void PerformLoadDatabase()
		{
		    if (_performingLoad)
		        return;
		    CurrentlyWaitingKey = null;
            _performingLoad = true;
            //put loading into background thread to allow loading the key file (potentially over network)
            new SimpleLoadingDialog(this, GetString(Resource.String.loading),
			                        true, () =>
				                        {
					                        CompositeKey compositeKey;
					                        string errorMessage;
					                        if (!CreateCompositeKey(out compositeKey, out errorMessage)) return (() =>
						                        {
							                        Toast.MakeText(this, errorMessage, ToastLength.Long).Show();
						                            _performingLoad = false;
                                                });
											return () => { PerformLoadDatabaseWithCompositeKey(compositeKey); };
				                        }).Execute();
			
			
		}

		private void PerformLoadDatabaseWithCompositeKey(CompositeKey compositeKey)
		{
			CheckBox cbQuickUnlock = (CheckBox) FindViewById(Resource.Id.enable_quickunlock);
			if (cbQuickUnlock == null)
				throw new NullPointerException("cpQuickUnlock");
			App.Kp2a.SetQuickUnlockEnabled(cbQuickUnlock.Checked);

			if (App.Kp2a.OfflineMode != _loadDbTaskOffline)
			{
				if (_loadDbFileTask == null)
					throw new NullPointerException("_loadDbFileTask");
				if (App.Kp2a == null)
					throw new NullPointerException("App.Kp2a");
				//keep the loading result if we loaded in online-mode (now offline) and the task is completed
				if (!App.Kp2a.OfflineMode || !_loadDbFileTask.IsCompleted)
				{
					//discard the pre-loading task
					_loadDbFileTask = null;	
				}
				
			}

			//avoid password being visible while loading:
			_showPassword = false;
			try
			{
				MakePasswordMaskedOrVisible();

				Handler handler = new Handler();
				OnFinish onFinish = new AfterLoad(handler, this, _ioConnection);
				LoadDb task = (KeyProviderTypes.Contains(KeyProviders.Otp))
					? new SaveOtpAuxFileAndLoadDb(App.Kp2a, _ioConnection, _loadDbFileTask, compositeKey, GetKeyProviderString(),
						onFinish, this, true, Intent.GetBooleanExtra("MakeCurrent", true))
					: new LoadDb(this, App.Kp2a, _ioConnection, _loadDbFileTask, compositeKey, GetKeyProviderString(), onFinish,true, Intent.GetBooleanExtra("MakeCurrent",true));
				_loadDbFileTask = null; // prevent accidental re-use

			    new ProgressTask(App.Kp2a, this, task).Run();
			}
			catch (Exception e)
			{
				Kp2aLog.LogUnexpectedError(new Exception("cannot load database: "+e + ", c: " + (compositeKey != null) + (_ioConnection != null) + (_keyFile != null), e));
				throw;
			}
			
		}

		private bool CreateCompositeKey(out CompositeKey compositeKey, out string errorMessage)
		{
			errorMessage = null;
//no need to check for validity of password because if this method is called, the Ok button was enabled (i.e. there was a valid password)
			compositeKey = new CompositeKey();
			compositeKey.AddUserKey(new KcpPassword(_password));
			if (KeyProviderTypes.Contains(KeyProviders.KeyFile))
			{
				try
				{
					if (_keyFile == "")
						throw new System.IO.FileNotFoundException();
					var ioc = IOConnectionInfo.UnserializeFromString(_keyFile);
					using (var stream = App.Kp2a.GetFileStorage(ioc).OpenFileForRead(ioc))
					{
						byte[] keyfileData = Util.StreamToMemoryStream(stream).ToArray();
						compositeKey.AddUserKey(new KcpKeyFile(keyfileData, ioc, true));
					}
				}
				catch (System.IO.FileNotFoundException e)
				{
					Kp2aLog.Log(e.ToString());
					errorMessage = App.Kp2a.GetResourceString(UiStringKey.keyfile_does_not_exist);
					return false;
				}
				catch (Exception e)
				{
					Kp2aLog.LogUnexpectedError(e);
					errorMessage = e.Message;
					return false;
				}
			}
			if (KeyProviderTypes.Contains(KeyProviders.Otp))
			{
				try
				{
					var lOtps = GetOtpsFromUi();
					Kp2aLog.Log("received " + lOtps.Count + " otps.");
					OathHotpKeyProv.CreateOtpSecret(lOtps, _otpInfo);
				}
				catch (Exception e)
				{
					Kp2aLog.LogUnexpectedError(e);
					errorMessage = GetString(Resource.String.OtpKeyError);

					return false;
				}
				compositeKey.AddUserKey(new KcpCustomKey(OathHotpKeyProv.Name, _otpInfo.Secret, true));
			}
			if ((KeyProviderTypes.Contains(KeyProviders.OtpRecovery)) || (KeyProviderTypes.Contains(KeyProviders.ChalRecovery)))
			{
				Spinner stpDataFmtSpinner = FindViewById<Spinner>(Resource.Id.otpsecret_format_spinner);
				EditText secretEdit = FindViewById<EditText>(Resource.Id.pass_otpsecret);

				byte[] pbSecret = EncodingUtil.ParseKey(secretEdit.Text, (OtpDataFmt) stpDataFmtSpinner.SelectedItemPosition);
				if (pbSecret != null)
				{
					compositeKey.AddUserKey(new KcpCustomKey(OathHotpKeyProv.Name, pbSecret, true));
				}
				else
				{
					errorMessage = GetString(Resource.String.CouldntParseOtpSecret);
					return false;
				}
			}
			if (KeyProviderTypes.Contains(KeyProviders.Challenge))
			{
				compositeKey.AddUserKey(new KcpCustomKey(KeeChallengeProv.Name, _challengeSecret, true));
			} 
			if (KeyProviderTypes.Contains(KeyProviders.ChallengeXC))
			{
				CurrentlyWaitingKey = new ChallengeXCKey(this, RequestCodeChallengeYubikey);
				compositeKey.AddUserKey(CurrentlyWaitingKey);
			}
			return true;
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
		    EditText password = (EditText) FindViewById(Resource.Id.password_edit);
			int selStart = password.SelectionStart, selEnd = password.SelectionEnd;
			if (_showPassword)
			{
				password.InputType = InputTypes.ClassText | InputTypes.TextVariationVisiblePassword;
                _passwordFont.ApplyTo(password);
			}
			else
			{
				password.InputType = InputTypes.ClassText | InputTypes.TextVariationPassword;
			}
			password.SetSelection(selStart, selEnd);
		}

		protected override void OnPause()
		{
		    _fingerprintDec?.StopListening();
		    _lastOnPauseTime = DateTime.Now;

            base.OnPause();
		}

	    protected override void OnStart()
		{
			base.OnStart();
			_starting = true;

		    if (PreferenceManager.GetDefaultSharedPreferences(this)
		        .GetBoolean(GetString(Resource.String.UseKp2aKeyboardInKp2a_key), false))
		    {
		        CopyToClipboardService.ActivateKeyboard(this);
		    }

            DonateReminder.ShowDonateReminderIfAppropriate(this);
			
			
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

			var memoryStream = Util.StreamToMemoryStream(stream);

			Kp2aLog.Log("Pre-loading database file completed");

			return memoryStream;
		}

		protected override void OnSaveInstanceState(Bundle outState)
		{
			base.OnSaveInstanceState(outState);
			outState.PutBoolean(ShowpasswordKey, _showPassword);

			outState.PutString(KeyFileOrProviderKey, GetKeyProviderString());
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
				_keepPasswordInOnResume = true;
				if (KeyProviderTypes.Contains(KeyProviders.Otp))
				{
				
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
					if (passwordModeSpinner.SelectedItemPosition != (int)KeyProviders.Otp)
					{
						passwordModeSpinner.SetSelection((int)KeyProviders.Otp);
					}	
				}
				else
				{
					//assume the key should be used as static password
					FindViewById<EditText>(Resource.Id.password_edit).Text += otp;
				}

				
			}
	
		}
		
		protected override void OnResume()
		{
			base.OnResume();
			_activityDesign.ReapplyTheme();

			Kp2aLog.Log("starting: " + _starting + ", Finishing: " + IsFinishing + ", _performingLoad: " + _performingLoad);

			CheckBox cbOfflineMode = (CheckBox)FindViewById(Resource.Id.work_offline);
			App.Kp2a.OfflineMode = cbOfflineMode.Checked = App.Kp2a.OfflineModePreference; //this won't overwrite new user settings because every change is directly saved in settings
			LinearLayout offlineModeContainer = FindViewById<LinearLayout>(Resource.Id.work_offline_container);
			var cachingFileStorage = App.Kp2a.GetFileStorage(_ioConnection) as CachingFileStorage;
			if ((cachingFileStorage != null) && cachingFileStorage.IsCached(_ioConnection))
			{	
				offlineModeContainer.Visibility = ViewStates.Visible;
			}
			else
			{
				offlineModeContainer.Visibility = ViewStates.Gone;
				App.Kp2a.OfflineMode = false;
			}
			

			

			View killButton = FindViewById(Resource.Id.kill_app);
			if (PreferenceManager.GetDefaultSharedPreferences(this)
								 .GetBoolean(GetString(Resource.String.show_kill_app_key), false))
			{
				killButton.Click += (sender, args) =>
				{
					_killOnDestroy = true;
                    SetResult(Result.Canceled);
					Finish();

				};
				killButton.Visibility = ViewStates.Visible;

			}
			else
			{
				killButton.Visibility = ViewStates.Gone;
			}

		    TryGetOtpFromClipboard();

		    if (!_keepPasswordInOnResume)
		    {
		        if (
                    _lastOnPauseTime < DateTime.Now - TimeSpan.FromSeconds(5) //only clear when user left the app for more than 5 seconds (allows to use Yubiclip, also allows to switch shortly to another app)
                    && 
		            PreferenceManager.GetDefaultSharedPreferences(this)
		                .GetBoolean(GetString(Resource.String.ClearPasswordOnLeave_key), true))
		        {
		            ClearEnteredPassword();
		        }

		    }



			_keepPasswordInOnResume = false;

			MakePasswordMaskedOrVisible();

			UpdateOkButtonState();

			if (KeyProviderTypes.Contains(KeyProviders.Challenge))
			{
				FindViewById(Resource.Id.otpInitView).Visibility = _challengeSecret == null ? ViewStates.Visible : ViewStates.Gone;
			}

			//use !IsFinishing to make sure we're not starting another activity when we're already finishing (e.g. due to TaskComplete in OnActivityResult)
			//use !performingLoad to make sure we're not already loading the database (after ActivityResult from File-Prepare-Activity; this would cause _loadDbFileTask to exist when we reload later!)
			if ( !IsFinishing && !_performingLoad)  
			{
				
				
			    // OnResume is run every time the activity comes to the foreground. This code should only run when the activity is started (OnStart), but must
				// be run in OnResume rather than OnStart so that it always occurrs after OnActivityResult (when re-creating a killed activity, OnStart occurs before OnActivityResult)
			    if (_starting)
			    {

			        _starting = false;

			        //database not yet loaded.

			        //check if pre-loading is enabled but wasn't started yet:
			        if (_loadDbFileTask == null &&
			            _prefs.GetBoolean(GetString(Resource.String.PreloadDatabaseEnabled_key), true))
			        {
			            // Create task to kick off file loading while the user enters the password
			            _loadDbFileTask = Task.Factory.StartNew(PreloadDbFile);
			            _loadDbTaskOffline = App.Kp2a.OfflineMode;
			        }
			    }

			}

		    if (compositeKeyForImmediateLoad != null)
		    {
		        //reload the database (without most other stuff performed in PerformLoadDatabase.
		        // We're assuming that the db file (and if appropriate also the key file) are still available 
		        // and there's no need to re-init the file storage. if it is, loading will fail and the user has 
		        // to retry with typing the full password, but that's intended to avoid showing the password to a 
		        // a potentially unauthorized user (feature request https://keepass2android.codeplex.com/workitem/274)
		        Handler handler = new Handler();
		        OnFinish onFinish = new AfterLoad(handler, this, _ioConnection);
		        _performingLoad = true;
		        LoadDb task = new LoadDb(this, App.Kp2a, _ioConnection, _loadDbFileTask, compositeKeyForImmediateLoad, GetKeyProviderString(),
		            onFinish, false, Intent.GetBooleanExtra("MakeCurrent",false));
		        _loadDbFileTask = null; // prevent accidental re-use
		        new ProgressTask(App.Kp2a, this, task).Run();
		        compositeKeyForImmediateLoad = null; //don't reuse or keep in memory

		    }
		    else
		    {


		        bool showKeyboard = (Util.GetShowKeyboardDuringFingerprintUnlock(this));


		        if (_fingerprintPermissionGranted)
		        {
		            if (!InitFingerprintUnlock())
		                showKeyboard = true;
		        }
		        else
		        {
		            FindViewById<ImageButton>(Resource.Id.fingerprintbtn).Visibility = ViewStates.Gone;
		            showKeyboard = true;
		        }


		        EditText pwd = (EditText) FindViewById(Resource.Id.password_edit);
		        pwd.PostDelayed(() =>
		        {
		            InputMethodManager keyboard = (InputMethodManager) GetSystemService(InputMethodService);
		            if (showKeyboard)
		                keyboard.ShowSoftInput(pwd, 0);
		            else
		                keyboard.HideSoftInputFromWindow(pwd.WindowToken, HideSoftInputFlags.ImplicitOnly);
		        }, 50);
		    }
        }

	    private void TryGetOtpFromClipboard()
	    {
	        if (_otpInfo != null)
	        {
	            if ((int) Build.VERSION.SdkInt >= 26)
	            {
	                Android.Content.ClipboardManager clipboardManager = (ClipboardManager)GetSystemService(Context.ClipboardService);
	                if (clipboardManager?.PrimaryClip?.Description == null || (clipboardManager.PrimaryClip.Description.Timestamp <
	                    Java.Lang.JavaSystem.CurrentTimeMillis() - 5000))
	                    return; //data older than 5 seconds
	            }
	            string clipboardContent = Util.GetClipboard(this);
	            if (clipboardContent == null || (_otpInfo.OtpLength != clipboardContent.Length))
	            {
	                return;
	            }
	            foreach (char c in clipboardContent)
	            {
	                if (c < '0' || c > '9')
	                    return;
	            }
	            string otp = clipboardContent;
                
	            EditText lastNonEmptyOtpEdit = null;

                foreach (int otpId in _otpTextViewIds)
	            {
	                EditText otpEdit = FindViewById<EditText>(otpId);
	                if (otpEdit?.Visibility == ViewStates.Visible)
	                {
	                    if (string.IsNullOrEmpty(otpEdit.Text))
	                    {
	                        if ((lastNonEmptyOtpEdit != null) && (lastNonEmptyOtpEdit.Text == otp))
	                            return; //otp was already set.

                            //otp ok. use it:
                            otpEdit.Text = otp;
	                        break;
                        }

                        lastNonEmptyOtpEdit = otpEdit;

	                }
	                
	            }
	            
	        }
        }

	    private bool InitFingerprintUnlock()
		{
			var btn = FindViewById<ImageButton>(Resource.Id.fingerprintbtn);
			try
			{
				FingerprintUnlockMode um;
				Enum.TryParse(_prefs.GetString(Database.GetFingerprintModePrefKey(_ioConnection), ""), out um);
				btn.Visibility = (um == FingerprintUnlockMode.FullUnlock) ? ViewStates.Visible : ViewStates.Gone;

				if (um != FingerprintUnlockMode.FullUnlock)
				{
					return false;
				}

				FingerprintModule fpModule = new FingerprintModule(this);
				_fingerprintDec = new FingerprintDecryption(fpModule, Database.GetFingerprintPrefKey(_ioConnection), this,
					Database.GetFingerprintPrefKey(_ioConnection));

				btn.Tag = GetString(Resource.String.fingerprint_unlock_hint);

				if (_fingerprintDec.Init())
				{
					btn.SetImageResource(Resource.Drawable.ic_fp_40px);
					_fingerprintDec.StartListening(new FingerprintAuthCallbackAdapter(this, this));
					return true;
				}
				else
				{
					HandleFingerprintKeyInvalidated();
					return false;
				}
			}
			catch (Exception e)
			{
                //exception can happen here if the app was restored from Google Backup (including preferences) but no fingerprint data is there.
				btn.SetImageResource(Resource.Drawable.ic_fingerprint_error);
			    Kp2aLog.Log("failed to init fingerprint unlock:" + e);
                string error = GetString(Resource.String.FingerprintInitFailed) + " " +
			                   GetString(Resource.String.fingerprint_reenable2);
			    
                btn.Tag = error;

			    Toast.MakeText(this, Resource.String.fingerprint_reenable2, ToastLength.Long).Show();

				_fingerprintDec = null;
				return false;
			}
				
				
		}

		private void HandleFingerprintKeyInvalidated()
		{
			var btn = FindViewById<ImageButton>(Resource.Id.fingerprintbtn);
//key invalidated permanently
			btn.SetImageResource(Resource.Drawable.ic_fingerprint_error);
			btn.Tag = GetString(Resource.String.fingerprint_unlock_failed) + " " + GetString(Resource.String.fingerprint_reenable2);
			_fingerprintDec = null;

			ClearFingerprintUnlockData();
		}

		private void InitializeOptionCheckboxes() {
			CheckBox cbQuickUnlock = (CheckBox)FindViewById(Resource.Id.enable_quickunlock);
			cbQuickUnlock.Checked = _prefs.GetBoolean(GetString(Resource.String.QuickUnlockDefaultEnabled_key), true);

			CheckBox cbOfflineMode = (CheckBox)FindViewById(Resource.Id.work_offline);
			cbOfflineMode.CheckedChange += (sender, args) =>
			{
				App.Kp2a.OfflineModePreference = App.Kp2a.OfflineMode = args.IsChecked;
			};
			
		}
			
		private String LoadKeyProviderStringForIoc(String filename) {
			if ( _rememberKeyfile ) {
				string keyfile = App.Kp2a.FileDbHelper.GetKeyFileForFile(filename);
				if (String.IsNullOrEmpty(keyfile))
					return null; //signal no key file
                
				return keyfile;
			} else {
				return null;
			}
		}
		
		private void InitializeFilenameView() {
			SetEditText(Resource.Id.filename, App.Kp2a.GetFileStorage(_ioConnection).GetDisplayName(_ioConnection));
						
		}

		protected override void OnDestroy()
		{
		    UnregisterReceiver(_intentReceiver);
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
		
		public override bool OnOptionsItemSelected(IMenuItem item) {
			switch ( item.ItemId ) {
				
                case Android.Resource.Id.Home:
                    _drawerLayout.OpenDrawer(Android.Support.V4.View.GravityCompat.Start);
                    return true;
			}
			
			return base.OnOptionsItemSelected(item);
		}

		private void GoToFileSelectActivity()
		{
		    SetResult((Result) ResultSelectOtherFile);
            Finish();
		}

		private class AfterLoad : OnFinish {
			readonly PasswordActivity _act;
		    private readonly IOConnectionInfo _ioConnection;

		    public AfterLoad(Handler handler, PasswordActivity act, IOConnectionInfo ioConnection):base(act, handler)
		    {
		        _act = act;
		        _ioConnection = ioConnection;
		    }
				

			public override void Run()
			{
				if (Success)
				{

					_act.ClearEnteredPassword();
					_act.BroadcastOpenDatabase();
					_act.InvalidCompositeKeyCount = 0;
				    _act.LoadingErrorCount = 0;


                    GC.Collect(); // Ensure temporary memory used while loading is collected
				}

			    if (Exception != null)
			    {
			        _act.LoadingErrorCount++;
			    }

			    if ((Exception != null) && (Exception.Message == KeePassLib.Resources.KLRes.FileCorrupted))
			    {
			        Message = _act.GetString(Resource.String.CorruptDatabaseHelp);
			    }

                if (Exception is InvalidCompositeKeyException)
			    {
			        _act.InvalidCompositeKeyCount++;
			        if (_act.UsedFingerprintUnlock)
			        {
			            //disable fingerprint unlock if master password changed
			            _act.ClearFingerprintUnlockData();
			            _act.InitFingerprintUnlock();

			            Message = _act.GetString(Resource.String.fingerprint_disabled_wrong_masterkey) + " " +
			                      _act.GetString(Resource.String.fingerprint_reenable2);
			        }
			        else
			        {
			            if (_act.InvalidCompositeKeyCount > 1)
			            {
			                Message = _act.GetString(Resource.String.RepeatedInvalidCompositeKeyHelp);
			                if (_act._prefs.GetBoolean(IoUtil.GetIocPrefKey(_ioConnection, "has_local_backup"), false))
			                {
			                    Java.Lang.Object changeDb = _act.GetString(Resource.String.menu_change_db);
			                    Message += _act.GetString(Resource.String.HintLocalBackupInvalidCompositeKey, new Java.Lang.Object[] {changeDb});
			                }
			            }
			            else
			            {
			                Message = _act.GetString(Resource.String.FirstInvalidCompositeKeyError);
			            }
			        }


			    }
			    else if (_act.LoadingErrorCount > 1)
			    {
			        if (_act._prefs.GetBoolean(IoUtil.GetIocPrefKey(_ioConnection, "has_local_backup"), false))
			        {
			            Object changeDb = _act.GetString(Resource.String.menu_change_db);
			            Message += _act.GetString(Resource.String.HintLocalBackupOtherError, changeDb);
			        }

                }
				
				
				
				if ((Message != null) && (Message.Length > 150)) //show long messages as dialog
				{
					new AlertDialog.Builder(_act).SetMessage(Message)
					                             .SetPositiveButton(Android.Resource.String.Ok,
					                                                (sender, args) =>
						                                                {
							                                                ((Dialog) sender).Dismiss();
						                                                    if (Success)
						                                                    {
						                                                        _act.LaunchNextActivity();
						                                                    }
                                                                        })
												.SetCancelable(false)
												.Show();
					
				}
				else
				{
					DisplayMessage(_act);
				    if (Success)
				    {
				        _act.LaunchNextActivity();
				    }

				}

			    //re-init fingerprint unlock in case something went wrong with opening the database 
                if (!Success)
                    _act.InitFingerprintUnlock();


                _act._performingLoad = false;

			}
		}

		public int InvalidCompositeKeyCount
		{
			get; set;
		}
	    public int LoadingErrorCount
	    {
	        get; set;
	    }

        private void BroadcastOpenDatabase()
		{
			App.Kp2a.BroadcastDatabaseAction(this, Strings.ActionOpenDatabase);
		}

		private void ClearEnteredPassword()
		{
			SetEditText(Resource.Id.password_edit, "");
			SetEditText(Resource.Id.pass_otpsecret, "");
			foreach (int otpId in _otpTextViewIds)
			{
				SetEditText(otpId, "");
			}
			if (_challengeSecret != null)
			{
				Array.Clear(_challengeSecret, 0, _challengeSecret.Length);
				_challengeSecret = null;	
			}
			
		}


		class SaveOtpAuxFileAndLoadDb : LoadDb
		{
			private readonly PasswordActivity _act;


			public SaveOtpAuxFileAndLoadDb(IKp2aApp app, IOConnectionInfo ioc, Task<MemoryStream> databaseData, CompositeKey compositeKey, string keyfileOrProvider, OnFinish finish, PasswordActivity act, bool updateLastUsageTimestamp, bool makeCurrent) : base(act, app, ioc, databaseData, compositeKey, keyfileOrProvider, finish,updateLastUsageTimestamp,makeCurrent)
			{
				_act = act;
			}

			public override void Run()
			{
				try
				{
					StatusLogger.UpdateMessage(UiStringKey.SavingOtpAuxFile);

					KeyProviderQueryContext ctx = new KeyProviderQueryContext(_act._ioConnection, false, false);
					
					if (!OathHotpKeyProv.CreateAuxFile(_act._otpInfo, ctx, _act._otpAuxIoc))
						Toast.MakeText(_act, _act.GetString(Resource.String.ErrorUpdatingOtpAuxFile), ToastLength.Long).Show();

					
				}
				catch (Exception e)
				{
					Kp2aLog.LogUnexpectedError(e);

					Toast.MakeText(_act, _act.GetString(Resource.String.ErrorUpdatingOtpAuxFile) + " " + e.Message,
								   ToastLength.Long).Show();
				}


				base.Run();

			    if (success)
			    {
			        App.Kp2a.CurrentDb.OtpAuxFileIoc = _act._otpAuxIoc;
                }

                
            }
            
		}
		private class PasswordActivityBroadcastReceiver : BroadcastReceiver
		{
			readonly PasswordActivity _activity;
			public PasswordActivityBroadcastReceiver(PasswordActivity activity)
			{
				_activity = activity;
			}

			public override void OnReceive(Context context, Intent intent)
			{
				switch (intent.Action)
				{
					case Intent.ActionScreenOff:
						_activity.OnScreenLocked();
						break;
				}
			}
		}

		private void OnScreenLocked()
		{
			if (_fingerprintDec != null)
				_fingerprintDec.StopListening();
			
		}

	    
	}



	
}


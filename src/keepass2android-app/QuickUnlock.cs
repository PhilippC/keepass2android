﻿/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. 

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
using Android.OS;
using Android.Views;
using Android.Widget;
using Android.Content.PM;
using KeePassLib.Keys;
using Android.Preferences;
using Android.Provider;
using Android.Runtime;

using Android.Views.InputMethods;
using Google.Android.Material.AppBar;
using Google.Android.Material.Dialog;
using keepass2android;
using KeePassLib;
using KeePassLib.Serialization;
using Toolbar = AndroidX.AppCompat.Widget.Toolbar;
using AndroidX.Core.Content;
using keepass2android.Utils;

namespace keepass2android
{
	[Activity(Label = "@string/app_name", 
		ConfigurationChanges = ConfigChanges.Orientation,
		WindowSoftInputMode = SoftInput.AdjustResize,
		MainLauncher = false,
        Theme = "@style/Kp2aTheme_BlueNoActionBar")]
	public class QuickUnlock : LifecycleAwareActivity, IBiometricAuthCallback
	{
		private IOConnectionInfo _ioc;
		private QuickUnlockBroadcastReceiver _intentReceiver;
		private ActivityDesign _design;
        private IBiometricIdentifier _biometryIdentifier;
		private int _quickUnlockLength;

        private int numFailedAttempts = 0;
        private int maxNumFailedAttempts = int.MaxValue;

        public QuickUnlock()
		{
			_design = new ActivityDesign(this);
		}

		protected override void OnCreate(Bundle bundle)
		{
			_design.ApplyTheme();
			base.OnCreate(bundle);
			
			//use FlagSecure to make sure the last (revealed) character of the password is not visible in recent apps
		    Util.MakeSecureDisplay(this);

			_ioc = App.Kp2a.GetDbForQuickUnlock()?.Ioc;



            if (_ioc == null)
			{
				Finish();
				return;
			}

			SetContentView(Resource.Layout.QuickUnlock);

            Util.InsetListener.ForBottomElement(FindViewById(Resource.Id.bottom_bar)).Apply();
            Util.InsetListener.ForTopElement(FindViewById(Resource.Id.appbar)).Apply();


            var collapsingToolbar = FindViewById<CollapsingToolbarLayout>(Resource.Id.collapsing_toolbar);
			collapsingToolbar.SetTitle(GetString(Resource.String.QuickUnlock_prefs));
            SetSupportActionBar(FindViewById<Toolbar>(Resource.Id.toolbar));
            
            if (App.Kp2a.GetDbForQuickUnlock().KpDatabase.Name != "")
			{
				FindViewById(Resource.Id.filename_label).Visibility = ViewStates.Visible;
				((TextView) FindViewById(Resource.Id.filename_label)).Text = App.Kp2a.GetDbForQuickUnlock().KpDatabase.Name;
			}
			else
			{
				if (
					PreferenceManager.GetDefaultSharedPreferences(this)
					                 .GetBoolean(GetString(Resource.String.RememberRecentFiles_key),
					                             Resources.GetBoolean(Resource.Boolean.RememberRecentFiles_default)))
				{
					((TextView) FindViewById(Resource.Id.filename_label)).Text = App.Kp2a.GetFileStorage(_ioc).GetDisplayName(_ioc);
				}
				else
				{
					((TextView) FindViewById(Resource.Id.filename_label)).Text = "*****";
				}

			}


			TextView txtLabel = (TextView) FindViewById(Resource.Id.QuickUnlock_label);

			_quickUnlockLength = App.Kp2a.QuickUnlockKeyLength;

			bool useUnlockKeyFromDatabase = 
                QuickUnlockFromDatabaseEnabled
				&& FindQuickUnlockEntry() != null;
			

            if (useUnlockKeyFromDatabase || PreferenceManager.GetDefaultSharedPreferences(this)
		        .GetBoolean(GetString(Resource.String.QuickUnlockHideLength_key), false))
		    {
		        txtLabel.Text = GetString(Resource.String.QuickUnlock_label_secure);
            }
		    else
		    {
		        txtLabel.Text = GetString(Resource.String.QuickUnlock_label, new Java.Lang.Object[] { _quickUnlockLength });
            }
			

			EditText pwd = (EditText) FindViewById(Resource.Id.QuickUnlock_password);
			pwd.SetEms(_quickUnlockLength);
			Util.MoveBottomBarButtons(Resource.Id.QuickUnlock_buttonLock, Resource.Id.QuickUnlock_button, Resource.Id.bottom_bar, this);

			Button btnUnlock = (Button) FindViewById(Resource.Id.QuickUnlock_button);
			btnUnlock.Click += (object sender, EventArgs e) =>
				{
					OnUnlock(pwd);
				};

		    

			Button btnLock = (Button) FindViewById(Resource.Id.QuickUnlock_buttonLock);
			btnLock.Text = btnLock.Text.Replace("ß", "ss");
			btnLock.Click += (object sender, EventArgs e) =>
				{
					App.Kp2a.Lock(false);
					Finish();
				};
			pwd.EditorAction += (sender, args) =>
				{
					if ((args.ActionId == ImeAction.Done) || ((args.ActionId == ImeAction.ImeNull) && (args.Event.Action == KeyEventActions.Down)))
						OnUnlock(pwd);
				};

			_intentReceiver = new QuickUnlockBroadcastReceiver(this);
			IntentFilter filter = new IntentFilter();
			filter.AddAction(Intents.DatabaseLocked);
            ContextCompat.RegisterReceiver(this, _intentReceiver, filter, (int)ReceiverFlags.Exported);

            Util.SetNoPersonalizedLearning(FindViewById<EditText>(Resource.Id.QuickUnlock_password));

            if (bundle != null)
                numFailedAttempts = bundle.GetInt(NumFailedAttemptsKey, 0);

            FindViewById(Resource.Id.QuickUnlock_buttonEnableLock).Click += (object sender, EventArgs e) =>
            {
				Intent intent = new Intent(Settings.ActionSecuritySettings);
                StartActivity(intent);

            };

            FindViewById(Resource.Id.QuickUnlock_buttonCloseDb).Click += (object sender, EventArgs e) =>
            {
                App.Kp2a.Lock(false);
            };

            if (App.Kp2a.ScreenLockWasEnabledWhenOpeningDatabase == false && App.Kp2a.QuickUnlockBlockedWhenDeviceNotSecureWhenOpeningDatabase)
            {
				FindViewById(Resource.Id.QuickUnlockForm).Visibility = ViewStates.Gone;
                FindViewById(Resource.Id.QuickUnlockBlocked).Visibility = ViewStates.Visible;
            }
            else
            {
                FindViewById(Resource.Id.QuickUnlockForm).Visibility = ViewStates.Visible;
                FindViewById(Resource.Id.QuickUnlockBlocked).Visibility = ViewStates.Gone;
            }



        }

        private bool QuickUnlockFromDatabaseEnabled =>
            PreferenceManager.GetDefaultSharedPreferences(this)
                .GetBoolean(GetString(Resource.String.QuickUnlockKeyFromDatabase_key), false);

        private static PwEntry FindQuickUnlockEntry()
        {
            return App.Kp2a.GetDbForQuickUnlock()?.KpDatabase?.RootGroup?.Entries.SingleOrDefault(e => e.Strings.GetSafe(PwDefs.TitleField).ReadString() == "QuickUnlock");
        }

        private const string NumFailedAttemptsKey = "FailedAttempts";

        protected override void OnSaveInstanceState(Bundle outState)
        {
            base.OnSaveInstanceState(outState);
            outState.PutInt(NumFailedAttemptsKey, numFailedAttempts);
            
        }

        protected override void OnStart()
		{
			base.OnStart();
			DonateReminder.ShowDonateReminderIfAppropriate(this);
			
		}

		

		public void OnBiometricError(string message)
		{
			Kp2aLog.Log("fingerprint error: " + message);
			var btn = FindViewById<ImageButton>(Resource.Id.fingerprintbtn);

			btn.SetImageResource(Resource.Drawable.ic_fingerprint_error);
			btn.PostDelayed(() =>
			{
				btn.SetImageResource(Resource.Drawable.baseline_fingerprint_24);
				
			}, 1300);
			App.Kp2a.ShowMessage(this, message,  MessageSeverity.Error);
		}

        
        public void OnBiometricAttemptFailed(string message)
        {
            numFailedAttempts++;
            if (numFailedAttempts >= maxNumFailedAttempts)
            {
                FindViewById<ImageButton>(Resource.Id.fingerprintbtn).Visibility = ViewStates.Gone;
                _biometryIdentifier.StopListening();
            }
        }

        public void OnBiometricAuthSucceeded()
		{
			Kp2aLog.Log("OnFingerprintAuthSucceeded");
			_biometryIdentifier.StopListening();
			var btn = FindViewById<ImageButton>(Resource.Id.fingerprintbtn);

			btn.SetImageResource(Resource.Drawable.ic_fingerprint_success);

			EditText pwd = (EditText)FindViewById(Resource.Id.QuickUnlock_password);
			pwd.Text = ExpectedPasswordPart;
			
			btn.PostDelayed(() =>
            {
				UnlockAndSyncAndClose();
			}, 500);


		}
		private bool InitFingerprintUnlock()
		{
			Kp2aLog.Log("InitFingerprintUnlock");

			if (_biometryIdentifier != null)
			{
				Kp2aLog.Log("Already listening for fingerprint!");
				return true;
			}


			var btn = FindViewById<ImageButton>(Resource.Id.fingerprintbtn);
			try
			{
				FingerprintUnlockMode um;
				Enum.TryParse(PreferenceManager.GetDefaultSharedPreferences(this).GetString(App.Kp2a.GetDbForQuickUnlock().CurrentFingerprintModePrefKey, ""), out um);
				btn.Visibility = (um != FingerprintUnlockMode.Disabled) ? ViewStates.Visible : ViewStates.Gone;

				if (um == FingerprintUnlockMode.Disabled)
				{
					_biometryIdentifier = null;
					return false;
				}



                if (um == FingerprintUnlockMode.QuickUnlock && Util.GetCloseDatabaseAfterFailedBiometricQuickUnlock(this))
                {
                    maxNumFailedAttempts = 3;
                }

                BiometricModule fpModule = new BiometricModule(this);
				Kp2aLog.Log("fpModule.IsHardwareAvailable=" + fpModule.IsHardwareAvailable);
				if (fpModule.IsHardwareAvailable) //see FingerprintSetupActivity
					_biometryIdentifier = new BiometricDecryption(fpModule, App.Kp2a.GetDbForQuickUnlock().CurrentFingerprintPrefKey, this,
						App.Kp2a.GetDbForQuickUnlock().CurrentFingerprintPrefKey);
				
				
			    if (_biometryIdentifier == null)
			    {
			        FindViewById<ImageButton>(Resource.Id.fingerprintbtn).Visibility = ViewStates.Gone;
			        return false;
                }
                

				if (_biometryIdentifier.Init())
				{
					Kp2aLog.Log("successfully initialized fingerprint.");
					btn.SetImageResource(Resource.Drawable.baseline_fingerprint_24);
					_biometryIdentifier.StartListening(this);
					return true;
				}
				else
				{
					Kp2aLog.Log("failed to initialize fingerprint.");
					HandleFingerprintKeyInvalidated();
				}
			}
			catch (Exception e)
			{
				Kp2aLog.Log("Error initializing Fingerprint Unlock: " + e);
				btn.SetImageResource(Resource.Drawable.ic_fingerprint_error);
				btn.Tag = "Error initializing Fingerprint Unlock: " + e;

				_biometryIdentifier = null;
			}
			return false;

		}

		private void HandleFingerprintKeyInvalidated()
		{
			var btn = FindViewById<ImageButton>(Resource.Id.fingerprintbtn);
//key invalidated permanently
			btn.SetImageResource(Resource.Drawable.ic_fingerprint_error);
		    btn.Tag = GetString(Resource.String.fingerprint_unlock_failed) + " " + GetString(Resource.String.fingerprint_reenable2);
            _biometryIdentifier = null;
		}

	    private void OnUnlock(EditText pwd)
		{
			var expectedPasswordPart = ExpectedPasswordPart;
			if (pwd.Text == expectedPasswordPart)
            {
                UnlockAndSyncAndClose();
            }
			else
			{
				Kp2aLog.Log("QuickUnlock not successful!");
				App.Kp2a.Lock(false);
				App.Kp2a.ShowMessage(this, GetString(Resource.String.QuickUnlock_fail),  MessageSeverity.Error);
                Finish();
			}
			
		}

        private void UnlockAndSyncAndClose()
        {
            App.Kp2a.UnlockDatabase();

			if (PreferenceManager.GetDefaultSharedPreferences(this)
									 .GetBoolean(GetString(Resource.String.SyncAfterQuickUnlock_key), false))
			{
				new SyncUtil(this).SynchronizeDatabase(Finish);
			}
			else
				Finish();

			
            
        }

        private string ExpectedPasswordPart
		{
			get
            {
                if (QuickUnlockFromDatabaseEnabled)
                {
                    var quickUnlockEntry = FindQuickUnlockEntry();
                    if (quickUnlockEntry != null)
                    {
                        return quickUnlockEntry.Strings.ReadSafe(PwDefs.PasswordField).ToString();
                    }
				}

                
				KcpPassword kcpPassword = (KcpPassword) App.Kp2a.GetDbForQuickUnlock().KpDatabase.MasterKey.GetUserKey(typeof (KcpPassword));
				String password = kcpPassword.Password.ReadString();

			    var passwordStringInfo = new System.Globalization.StringInfo(password);

			    int passwordLength = passwordStringInfo.LengthInTextElements;
                
                String expectedPasswordPart = passwordStringInfo.SubstringByTextElements(Math.Max(0, passwordLength - _quickUnlockLength),
                    Math.Min(passwordLength, _quickUnlockLength));
				return expectedPasswordPart;
			}
		}

		private void OnLockDatabase()
		{
			CheckIfUnloaded();
		}

		protected override void OnResume()
		{
			base.OnResume();
			_design.ReapplyTheme();
            App.Kp2a.MessagePresenter = new ChainedSnackbarPresenter(FindViewById(Resource.Id.main_content));

            CheckIfUnloaded();

            InitFingerprintUnlock();

            bool showKeyboard = true;

			EditText pwd = (EditText)FindViewById(Resource.Id.QuickUnlock_password);
			pwd.PostDelayed(() =>
            {
                pwd.RequestFocus();
				InputMethodManager keyboard = (InputMethodManager)GetSystemService(Context.InputMethodService);
				if (showKeyboard)
					keyboard.ShowSoftInput(pwd, ShowFlags.Implicit);
				else
					keyboard.HideSoftInputFromWindow(pwd.WindowToken, HideSoftInputFlags.ImplicitOnly);
			}, 50);


            var btn = FindViewById<ImageButton>(Resource.Id.fingerprintbtn);
            btn.Click += (sender, args) =>
            {
                if ((_biometryIdentifier != null) && ((_biometryIdentifier.HasUserInterface)|| string.IsNullOrEmpty((string)btn.Tag) ))
                {
                    _biometryIdentifier.StartListening(this);
                }
                else
                {
                    MaterialAlertDialogBuilder b = new MaterialAlertDialogBuilder(this);
                    b.SetTitle(Resource.String.fingerprint_prefs);
                    b.SetMessage(btn.Tag.ToString());
                    b.SetPositiveButton(Android.Resource.String.Ok, (o, eventArgs) => ((Dialog)o).Dismiss());
                    if (_biometryIdentifier != null)
                    {
                        b.SetNegativeButton(Resource.String.disable_sensor, (senderAlert, alertArgs) =>
                        {
                            btn.SetImageResource(Resource.Drawable.ic_fingerprint_error);
                            _biometryIdentifier?.StopListening();
                            _biometryIdentifier = null;
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
                }

                
            };
            
            





        }

		

		protected override void OnPause()
		{
            App.Kp2a.MessagePresenter = new NonePresenter();
            if (_biometryIdentifier != null)
			{
				Kp2aLog.Log("FP: Stop listening");
				_biometryIdentifier.StopListening();
            }

			base.OnPause();
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			try
			{
				UnregisterReceiver(_intentReceiver);
			}
			catch (Exception e)
			{
				Kp2aLog.LogUnexpectedError(e);
			}
			
		}

		private void CheckIfUnloaded()
		{
			if (App.Kp2a.OpenDatabases.Any() == false)
			{
				Finish();
			}
		}

		public override void OnBackPressed()
		{
			SetResult(KeePass.ExitClose);
			base.OnBackPressed();
		}

		private class QuickUnlockBroadcastReceiver : BroadcastReceiver
		{
			readonly QuickUnlock _activity;
			public QuickUnlockBroadcastReceiver(QuickUnlock activity)
			{
				_activity = activity;
			}

			public override void OnReceive(Context context, Intent intent)
			{
				switch (intent.Action)
				{
					case Intents.DatabaseLocked:
						_activity.OnLockDatabase();
						break;
				}
			}
		}
	}
}


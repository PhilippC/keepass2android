using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Hardware.Fingerprints;
using Android.OS;
using Android.Preferences;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.Lang;
using KeePassLib.Keys;
using KeePassLib.Utility;
using Kotlin.Text;
using Enum = System.Enum;
using Exception = System.Exception;

namespace keepass2android
{
	[Activity(Label = "@string/app_name",
	    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden,
		Theme = "@style/MyTheme_ActionBar", MainLauncher = false)]
	[IntentFilter(new[] { "kp2a.action.FingerprintSetupActivity" }, Categories = new[] { Intent.CategoryDefault })]
	public class BiometricSetupActivity : LockCloseActivity, IBiometricAuthCallback
	{
		private readonly ActivityDesign _activityDesign;

		public BiometricSetupActivity(IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
	    {
		    _activityDesign = new ActivityDesign(this);
	    }
		public BiometricSetupActivity()
		{
			_activityDesign = new ActivityDesign(this);
		}

		

		private FingerprintUnlockMode _unlockMode = FingerprintUnlockMode.Disabled;
		private FingerprintUnlockMode _desiredUnlockMode;
		private BiometricEncryption _enc;
		private RadioButton[] _radioButtons;
		public override bool OnOptionsItemSelected(IMenuItem item)
		{
			switch (item.ItemId)
			{

				case Android.Resource.Id.Home:
					Finish();
					return true;
			}

			return base.OnOptionsItemSelected(item);
		}

		protected override void OnCreate(Bundle savedInstanceState)
		{
			_activityDesign.ApplyTheme();
			base.OnCreate(savedInstanceState);
			SetContentView(Resource.Layout.fingerprint_setup);

			Enum.TryParse(
				PreferenceManager.GetDefaultSharedPreferences(this).GetString(App.Kp2a.CurrentDb.CurrentFingerprintModePrefKey, ""),
				out _unlockMode);

			_fpIcon = FindViewById<ImageView>(Resource.Id.fingerprint_icon);
			_fpTextView = FindViewById<TextView>(Resource.Id.fingerprint_status);

            
			SupportActionBar.SetDisplayHomeAsUpEnabled(true);
			SupportActionBar.SetHomeButtonEnabled(true);

			int[] radioButtonIds =
			{
				Resource.Id.radio_fingerprint_quickunlock, Resource.Id.radio_fingerprint_unlock,
				Resource.Id.radio_fingerprint_disabled
			};
			_radioButtons = radioButtonIds.Select(FindViewById<RadioButton>).ToArray();
			_radioButtons[0].Tag = FingerprintUnlockMode.QuickUnlock.ToString();
			_radioButtons[1].Tag = FingerprintUnlockMode.FullUnlock.ToString();
			_radioButtons[2].Tag = FingerprintUnlockMode.Disabled.ToString();
			foreach (RadioButton r in _radioButtons)
			{
				r.CheckedChange += (sender, args) =>
				{
					var rbSender = ((RadioButton) sender);
					if (!rbSender.Checked) return;
					foreach (RadioButton rOther in _radioButtons)
					{
						if (rOther == sender) continue;
						rOther.Checked = false;
					}
					FingerprintUnlockMode newMode;
					Enum.TryParse(rbSender.Tag.ToString(), out newMode);
					ChangeUnlockMode(_unlockMode, newMode);
					
				};
			}

			CheckCurrentRadioButton();

			int errorId = Resource.String.fingerprint_os_error;
			SetError(errorId);

			FindViewById(Resource.Id.cancel_button).Click += (sender, args) =>
			{
				_enc.StopListening();
				_unlockMode = FingerprintUnlockMode.Disabled; //cancelling a FingerprintEncryption means a new key has been created but not been authenticated to encrypt something. We can't keep the previous state.
				StoreUnlockMode();
				FindViewById(Resource.Id.radio_buttons).Visibility = ViewStates.Visible;
				FindViewById(Resource.Id.fingerprint_auth_container).Visibility = ViewStates.Gone;
				_enc = null;
				CheckCurrentRadioButton();
			};

			FindViewById(Resource.Id.radio_buttons).Visibility = ViewStates.Gone;
			FindViewById(Resource.Id.fingerprint_auth_container).Visibility = ViewStates.Gone;
			FindViewById<CheckBox>(Resource.Id.show_keyboard_while_fingerprint).Checked =
				Util.GetShowKeyboardDuringFingerprintUnlock(this);

			FindViewById<CheckBox>(Resource.Id.show_keyboard_while_fingerprint).CheckedChange += (sender, args) =>
			{
				PreferenceManager.GetDefaultSharedPreferences(this)
					.Edit()
					.PutBoolean(GetString(Resource.String.ShowKeyboardWhileFingerprint_key), args.IsChecked)
					.Commit();
			};
				
            
			UpdateKeyboardCheckboxVisibility();
			
			
		}

		private void UpdateKeyboardCheckboxVisibility()
		{
			FindViewById(Resource.Id.show_keyboard_while_fingerprint).Visibility = ViewStates.Gone;
		}

		private bool TrySetupSamsung()
		{
			try
			{
				//try to create a Samsung ID object 
				_samsungBiometry = new BiometrySamsungIdentifier(this);
				if (!_samsungBiometry.Init())
				{
					SetError(Resource.String.fingerprint_no_enrolled);
				}
				ShowRadioButtons();
				FindViewById(Resource.Id.container_fingerprint_unlock).Visibility = _samsungBiometry == null
					? ViewStates.Visible
					: ViewStates.Gone;
				return true;
			}
			catch (Exception)
			{
				_samsungBiometry = null;
				return false;
			}
		}

		string CurrentPreferenceKey
		{
			get { return App.Kp2a.CurrentDb.CurrentFingerprintPrefKey; }
		}

		private void StoreUnlockMode()
		{
			ISharedPreferencesEditor edit = PreferenceManager.GetDefaultSharedPreferences(this).Edit();
			if (_unlockMode == FingerprintUnlockMode.Disabled)
			{
				edit.PutString(CurrentPreferenceKey, "");
			}
			else
			{
			    try
			    {
			        if (_unlockMode == FingerprintUnlockMode.FullUnlock)
			        {
			            var userKey = App.Kp2a.CurrentDb.KpDatabase.MasterKey.GetUserKey<KcpPassword>();
			            _enc.StoreEncrypted(userKey != null ? userKey.Password.ReadString() : "", CurrentPreferenceKey, edit);
			        }
			        else
			            _enc.StoreEncrypted("QuickUnlock" /*some dummy data*/, CurrentPreferenceKey, edit);
                }
			    catch (Exception e)
			    {
			        new AlertDialog.Builder(this)
			            .SetTitle(GetString(Resource.String.ErrorOcurred))
			            .SetMessage(GetString(Resource.String.FingerprintSetupFailed))
                        .SetCancelable(false)
                        .SetPositiveButton(Android.Resource.String.Ok, (sender, args) => { })
                        .Show();

                }

            }
			edit.PutString(App.Kp2a.CurrentDb.CurrentFingerprintModePrefKey, _unlockMode.ToString());
			edit.Commit();
		}

		private void CheckCurrentRadioButton()
		{
			
			foreach (RadioButton r in _radioButtons)
			{
				FingerprintUnlockMode um;
				Enum.TryParse(r.Tag.ToString(), out um);
				if (um == _unlockMode)
					r.Checked = true;
			}
		}

		private void SetError(int errorId)
		{
			var tv = FindViewById<TextView>(Resource.Id.tvFatalError);
			tv.Text = GetString(Resource.String.fingerprint_fatal) + " " + GetString(errorId);
			tv.Visibility = ViewStates.Visible;
		}

	
		private void ShowRadioButtons()
		{
			FindViewById<TextView>(Resource.Id.tvFatalError).Visibility = ViewStates.Gone;
			FindViewById(Resource.Id.radio_buttons).Visibility = ViewStates.Visible;
			FindViewById(Resource.Id.fingerprint_auth_container).Visibility = ViewStates.Gone;
		}

        private void HideRadioButtons()
        {
            FindViewById<TextView>(Resource.Id.tvFatalError).Visibility = ViewStates.Gone;
            FindViewById(Resource.Id.radio_buttons).Visibility = ViewStates.Gone;
            FindViewById(Resource.Id.fingerprint_auth_container).Visibility = ViewStates.Gone;
        }


        private void ChangeUnlockMode(FingerprintUnlockMode oldMode, FingerprintUnlockMode newMode)
		{
			if (oldMode == newMode)
				return;

				
			if (_samsungBiometry != null)
			{
				_unlockMode = newMode;
				UpdateKeyboardCheckboxVisibility();
			
				ISharedPreferencesEditor edit = PreferenceManager.GetDefaultSharedPreferences(this).Edit();
				edit.PutString(App.Kp2a.CurrentDb.CurrentFingerprintModePrefKey, _unlockMode.ToString());
				edit.Commit();
				return;
			}

			if (newMode == FingerprintUnlockMode.Disabled)
			{
				_unlockMode = newMode;
				UpdateKeyboardCheckboxVisibility();
			
				StoreUnlockMode();
				return;
			}

			_desiredUnlockMode = newMode;
			FindViewById(Resource.Id.radio_buttons).Visibility = ViewStates.Gone;
			FindViewById(Resource.Id.show_keyboard_while_fingerprint).Visibility = ViewStates.Gone;

			FindViewById(Resource.Id.fingerprint_auth_container).Visibility = ViewStates.Visible;
            try
			{
                _enc = new BiometricEncryption(new BiometricModule(this), CurrentPreferenceKey);
                if (!_enc.Init())
					throw new Exception("Failed to initialize cipher");
				ResetErrorTextRunnable();
                
                _enc.StartListening(new BiometricAuthCallbackAdapter(this, this));
            }
			catch (Exception e)
			{
				CheckCurrentRadioButton();
				Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
				FindViewById(Resource.Id.radio_buttons).Visibility = ViewStates.Visible;
				FindViewById(Resource.Id.fingerprint_auth_container).Visibility = ViewStates.Gone;
			}
			

		}

		static readonly long ERROR_TIMEOUT_MILLIS = 1600;
		static readonly long SUCCESS_DELAY_MILLIS = 1300;
		private ImageView _fpIcon;
		private TextView _fpTextView;
		
		private BiometrySamsungIdentifier _samsungBiometry;

		public void OnBiometricAuthSucceeded()
		{
			_unlockMode = _desiredUnlockMode;

			_fpTextView.RemoveCallbacks(ResetErrorTextRunnable);
			_fpIcon.SetImageResource(Resource.Drawable.ic_fingerprint_success);
			_fpTextView.SetTextColor(_fpTextView.Resources.GetColor(Resource.Color.success_color, null));
			_fpTextView.Text = _fpTextView.Resources.GetString(Resource.String.fingerprint_success);
			_fpIcon.PostDelayed(() =>
			{
				FindViewById(Resource.Id.radio_buttons).Visibility = ViewStates.Visible;
				FindViewById(Resource.Id.fingerprint_auth_container).Visibility = ViewStates.Gone;

				StoreUnlockMode();
				UpdateKeyboardCheckboxVisibility();
			

			}, SUCCESS_DELAY_MILLIS);

			
		}


		
		public void OnBiometricError(string error)
		{
			_fpIcon.SetImageResource(Resource.Drawable.ic_fingerprint_error);
			_fpTextView.Text = error;
			_fpTextView.SetTextColor(
				_fpTextView.Resources.GetColor(Resource.Color.warning_color, null));
			_fpTextView.RemoveCallbacks(ResetErrorTextRunnable);
			_fpTextView.PostDelayed(ResetErrorTextRunnable, ERROR_TIMEOUT_MILLIS);
		}

		void ResetErrorTextRunnable()
		{
			_fpTextView.SetTextColor(
				_fpTextView.Resources.GetColor(Resource.Color.hint_color, null));
			_fpTextView.Text = _fpTextView.Resources.GetString(Resource.String.fingerprint_hint);
			_fpIcon.SetImageResource(Resource.Drawable.ic_fp_40px);
		}

		protected override void OnResume()
		{
			base.OnResume();

            BiometricModule fpModule = new BiometricModule(this);
            HideRadioButtons();
            if (!fpModule.IsHardwareAvailable)
            {
                //seems like not all Samsung Devices (e.g. Note 4) don't support the Android 6 fingerprint API
                if (!TrySetupSamsung())
                    SetError(Resource.String.fingerprint_hardware_error);
                UpdateKeyboardCheckboxVisibility();
                return;
            }
            if (!fpModule.IsAvailable)
            {
                SetError(Resource.String.fingerprint_no_enrolled);
                return;
            }
            ShowRadioButtons();
            UpdateKeyboardCheckboxVisibility();
            
        }

		protected override void OnPause()
		{
			base.OnPause();
			if (_enc != null)
				_enc.StopListening();
		}
	}

}
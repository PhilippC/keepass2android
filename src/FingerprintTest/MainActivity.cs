using System;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Hardware.Fingerprints;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Preferences;
using Android.Support.V7.App;
using keepass2android;

namespace FingerprintTest
{
	[Activity(Label = "FingerprintTest", MainLauncher = true, Icon = "@drawable/icon", Theme = "@style/Theme.AppCompat")]
	public class MainActivity : AppCompatActivity
	{
		int count = 1;
		private string _keyId = "mykeyid";
		const int FINGERPRINT_PERMISSION_REQUEST_CODE = 0;

		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

			// Set our view from the "main" layout resource
			SetContentView(Resource.Layout.Main);

			// Get our button from the layout resource,
			// and attach an event to it
			Button button = FindViewById<Button>(Resource.Id.MyButton);
			button.Visibility = ViewStates.Gone;
			
			RequestPermissions(new[] { Manifest.Permission.UseFingerprint }, FINGERPRINT_PERMISSION_REQUEST_CODE);
		}
		string prefKey = "enc_pref_key";

		public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
		{
			if (requestCode == FINGERPRINT_PERMISSION_REQUEST_CODE && grantResults[0] == Android.Content.PM.Permission.Granted)
			{
				Button encButton = FindViewById<Button>(Resource.Id.MyButton);
				Button decButton = FindViewById<Button>(Resource.Id.Decrypt);
				encButton.Visibility = ViewStates.Visible;
				encButton.Enabled = true;
				var fingerprint = new keepass2android.FingerprintModule(this);

				encButton.Click += (sender, args) =>
				{
					
					var fingerprintEnc = new FingerprintEncryption(fingerprint, _keyId);


					if (fingerprintEnc.InitCipher())
					{
						fingerprintEnc.StartListening(new EncryptionCallback(this, fingerprintEnc, prefKey));


					}
					else
					{
						Toast.MakeText(this, "Error initiating cipher", ToastLength.Long).Show();
					}

				};

				decButton.Click += (sender, args) =>
				{
					
					var fingerprintDec = new FingerprintDecryption(fingerprint, _keyId, this, prefKey);


					if (fingerprintDec.InitCipher())
					{
						fingerprintDec.StartListening(new DecryptionCallback(this, fingerprintDec,prefKey));


					}
					else
					{
						Toast.MakeText(this, "Error initiating cipher", ToastLength.Long).Show();
					}

				};

				if (!fingerprint.KeyguardManager.IsKeyguardSecure)
				{
					encButton.Enabled = false;
					// Show a message that the user hasn't set up a fingerprint or lock screen.
					Toast.MakeText(this, "Secure lock screen hasn't set up.\n"
						+ "Go to 'Settings -> Security -> Fingerprint' to set up a fingerprint", ToastLength.Long).Show();
					return;
				}


				if (!fingerprint.FingerprintManager.HasEnrolledFingerprints)
				{
					encButton.Enabled = false;
					// This happens when no fingerprints are registered.
					Toast.MakeText(this, "Go to 'Settings -> Security -> Fingerprint' " +
						"and register at least one fingerprint", ToastLength.Long).Show();
					return;
				}
				
			}
		}
	}

	public class DecryptionCallback : FingerprintManager.AuthenticationCallback
	{
		private readonly Context _context;
		private readonly FingerprintDecryption _fingerprintDec;
		private readonly string _prefKey;

		public DecryptionCallback(Context context, FingerprintDecryption fingerprintDec, string prefKey)
		{
			_context = context;
			_fingerprintDec = fingerprintDec;
			_prefKey = prefKey;
		}

		public override void OnAuthenticationSucceeded(FingerprintManager.AuthenticationResult result)
		{
			var prefs = PreferenceManager.GetDefaultSharedPreferences(Application.Context);
			Toast.MakeText(_context, _fingerprintDec.DecryptStored(_prefKey), ToastLength.Long).Show();
		}
	}

	public class EncryptionCallback : FingerprintManager.AuthenticationCallback
	{
		private readonly FingerprintCrypt _fingerprintEnc;
		private readonly string _prefKey;

		public EncryptionCallback(Context context, FingerprintCrypt fingerprintEnc, string prefKey)
		{
			_fingerprintEnc = fingerprintEnc;
			_prefKey = prefKey;
		}

		public override void OnAuthenticationSucceeded(FingerprintManager.AuthenticationResult result)
		{
			
			_fingerprintEnc.StoreEncrypted("some töst data", _prefKey, Application.Context);
		}

		
	}
}


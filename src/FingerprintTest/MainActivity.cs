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
	[Activity(Label = "FingerprintTest", MainLauncher = true, Icon = "@drawable/icon")]
	public class MainActivity : AppCompatActivity
	{
		int count = 1;
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

		public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
		{
			if (requestCode == FINGERPRINT_PERMISSION_REQUEST_CODE && grantResults[0] == Android.Content.PM.Permission.Granted)
			{
				Button button = FindViewById<Button>(Resource.Id.MyButton);
				button.Visibility = ViewStates.Visible;
				button.Enabled = true;
				var fingerprint = new keepass2android.FingerprintModule(this);

				button.Click += (sender, args) =>
				{

				};
				if (!fingerprint.KeyguardManager.IsKeyguardSecure)
				{
					button.Enabled = false;
					// Show a message that the user hasn't set up a fingerprint or lock screen.
					Toast.MakeText(this, "Secure lock screen hasn't set up.\n"
						+ "Go to 'Settings -> Security -> Fingerprint' to set up a fingerprint", ToastLength.Long).Show();
					return;
				}


				if (!fingerprint.FingerprintManager.HasEnrolledFingerprints)
				{
					button.Enabled = false;
					// This happens when no fingerprints are registered.
					Toast.MakeText(this, "Go to 'Settings -> Security -> Fingerprint' " +
						"and register at least one fingerprint", ToastLength.Long).Show();
					return;
				}
				var fingerprintEnc = new FingerprintEncryptionModule(fingerprint, "abc");

				
					if (fingerprintEnc.InitCipher())
					{
						fingerprintEnc.StartListening(new EncryptionCallback(this, fingerprintEnc));
						
						
					}
					else
					{
						Toast.MakeText(this, "Error initiating cipher", ToastLength.Long).Show();
					}
				
			}
		}
	}

	public class EncryptionCallback : FingerprintManager.AuthenticationCallback
	{
		private readonly FingerprintEncryptionModule _fingerprintEnc;

		public EncryptionCallback(Context context, FingerprintEncryptionModule fingerprintEnc)
		{
			_fingerprintEnc = fingerprintEnc;
		}

		public override void OnAuthenticationSucceeded(FingerprintManager.AuthenticationResult result)
		{
			_fingerprintEnc.Encrypt("abc");
			var edit = PreferenceManager.GetDefaultSharedPreferences(Application.Context).Edit();
			edit.PutString("encrypted", );
		}
	}
}


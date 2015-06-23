using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Nfc;
using Android.OS;
using Android.Widget;
using Java.Util;
using Java.Util.Regex;
using Keepass2android.Yubiclip.Scancode;

namespace keepass2android
{
	[Activity(Label = "@string/app_name",
			   ConfigurationChanges = ConfigChanges.Orientation |
			   ConfigChanges.KeyboardHidden,
			   NoHistory = true,
			   ExcludeFromRecents = true,
			   Theme = "@android:style/Theme.Dialog")]
	[IntentFilter(new[] { "android.nfc.action.NDEF_DISCOVERED" },
		Label = "@string/app_name",
		Categories = new[] { Intent.CategoryDefault },
		DataHost = "my.yubico.com",
		DataPathPrefix = "/neo",
		DataScheme = "https")]
	public class NfcOtpActivity : Activity
	{
		private String GetOtpFromIntent(Intent intent)
		{
			String data = intent.DataString;
			Matcher matcher = OtpPattern.Matcher(data);
			if (matcher.Matches())
			{
				String otp = matcher.Group(1);
				return otp;
			}
			else
			{
				IParcelable[] raw = Intent.GetParcelableArrayExtra(NfcAdapter.ExtraNdefMessages);
				
				byte[] bytes = ((NdefMessage) raw[0]).ToByteArray();
				bytes = Arrays.CopyOfRange(bytes, DATA_OFFSET, bytes.Length);
				String layout = "US";
				KeyboardLayout kbd = KeyboardLayout.ForName(layout);
				String otp = kbd.FromScanCodes(bytes);
				return otp;
			}
			return null;
		}


		//private static readonly Java.Util.Regex.Pattern OtpPattern = Java.Util.Regex.Pattern.Compile("^https://my\\.yubico\\.com/neo/(.+)$");
		private static readonly Java.Util.Regex.Pattern OtpPattern = Java.Util.Regex.Pattern.Compile("^https://my\\.yubico\\.com/neo/([a-zA-Z0-9!]+)$");
		private const int DATA_OFFSET = 23;

		private ActivityDesign _design;

		public NfcOtpActivity()
		{
			_design = new ActivityDesign(this);
		}

		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);
			_design.ApplyTheme();

			Intent i = new Intent(this, typeof (PasswordActivity));
			i.SetAction(Intents.StartWithOtp);
			
			//things to consider:
			// PasswordActivity should be resumed if currently active -> this is why single top is used and why PasswordActivity is started
			// If PasswordActivity is not open already, it may be the wrong place to send our OTP to because maybe the user first needs to select 
			//  a file (which might require UI action like entering credentials, all of which is handled in FileSelectActivity)
			// FileSelectActivity is not on the back stack, it finishes itself.
			// -> PasswordActivity needs to handle this and return to FSA.
			
			
			i.SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
			try
			{
				string otp = GetOtpFromIntent(Intent);
				if (otp == null)
					throw new Exception("Otp must not be null!");
				i.PutExtra(Intents.OtpExtraKey, otp);
			}
			catch (Exception e)
			{
				Kp2aLog.Log(e.ToString());
				Toast.MakeText(this, "No Yubikey OTP found!", ToastLength.Long).Show();
				Finish();
				return;
			}

			if (App.Kp2a.GetDb().Loaded)
			{
				Toast.MakeText(this, GetString(Resource.String.otp_discarded_because_db_open), ToastLength.Long).Show();
			}
			else
			{
				StartActivity(i);				
			}

			Finish();

		}

		protected override void OnResume()
		{
			base.OnResume();
			_design.ReapplyTheme();
		}
	}
}
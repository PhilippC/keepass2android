using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Java.Util.Regex;

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
			return null;
		}


		private static readonly Java.Util.Regex.Pattern OtpPattern = Java.Util.Regex.Pattern.Compile("^https://my\\.yubico\\.com/neo/(.+)$");



		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

			Intent i = new Intent(this, typeof (PasswordActivity));
			i.SetAction(Intents.StartWithOtp);
			
			//things to consider:
			// PasswordActivity should be resumed if currently active -> this is why single top is used and why PasswordActivity is started
			// If PasswordActivity is not open already, it may be the wrong place to send our OTP to because maybe the user first needs to select 
			//  a file (which might require UI action like entering credentials, all of which is handled in FileSelectActivity)
			// FileSelectActivity is not on the back stack, it finishes itself.
			// -> PasswordActivity needs to handle this and return to FSA.
			
			
			i.SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
			i.PutExtra(Intents.OtpExtraKey, GetOtpFromIntent(Intent));
			StartActivity(i);
			Finish();

		}
	}
}
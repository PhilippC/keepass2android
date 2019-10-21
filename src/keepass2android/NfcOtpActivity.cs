using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Nfc;
using Android.OS;
using Android.Widget;
using Java.Util;
using Java.Util.Regex;
using Keepass2android.Yubiclip.Scancode;
using Pattern = Java.Util.Regex.Pattern;

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
	[IntentFilter(new[] { "android.nfc.action.NDEF_DISCOVERED", Android.Content.Intent.ActionView },
	    Label = "@string/app_name",
	    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
	    DataHost = "keepass2android.crocoll.net",
	    DataPathPrefix = "/neo",
	    DataScheme = "https",
        AutoVerify=true)]
    public class NfcOtpActivity : Activity
	{
		private String GetOtpFromIntent(Intent intent)
		{
		    var patterns = new List<Pattern>{OTP_PATTERN, OTP_PATTERN2};
			foreach (var pattern in patterns)
		    {

		        Matcher matcher = pattern.Matcher(intent.DataString);
		        if (matcher.Matches())
		        {
		            String otp = matcher.Group(1);
		            return otp;
		        }
		        else
		        {
		            IParcelable[] raw = intent.GetParcelableArrayExtra(NfcAdapter.ExtraNdefMessages);
		            byte[] bytes = ((NdefMessage)raw[0]).ToByteArray();
		            if (bytes[0] == URL_NDEF_RECORD && Arrays.Equals(URL_PREFIX_BYTES, Arrays.CopyOfRange(bytes, 3, 3 + URL_PREFIX_BYTES.Length)))
		            {
		                if (Arrays.Equals(new Java.Lang.String("/neo/").GetBytes(), Arrays.CopyOfRange(bytes, 18, 18 + 5)))
		                {
		                    bytes[22] = (byte)'#';
		                }
		                for (int i = 0; i < bytes.Length; i++)
		                {
		                    if (bytes[i] == '#')
		                    {
		                        bytes = Arrays.CopyOfRange(bytes, i + 1, bytes.Length);
		                        String layout = "US";
		                        KeyboardLayout kbd = KeyboardLayout.ForName(layout);
		                        String otp = kbd.FromScanCodes(bytes);
		                        return otp;
		                    }
		                }
		            }
		        }
            }
            /*
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
			}*/
			return null;
		}


	    private static String URL_PREFIX = "https://my.yubico.com/";
	    private static byte URL_NDEF_RECORD = (byte)0xd1;
	    private static byte[] URL_PREFIX_BYTES = new byte[URL_PREFIX.Length + 2 - 8];

	    private static Pattern OTP_PATTERN = Pattern.Compile("^https://my\\.yubico\\.com/[a-z]+/#?([a-zA-Z0-9!]+)$");
	    private static Pattern OTP_PATTERN2 = Pattern.Compile("^https://keepass2android\\.crocoll\\.net/[a-z]+/#?([a-zA-Z0-9!]+)$");

        static NfcOtpActivity()
	    {
	        URL_PREFIX_BYTES[0] = 85;
	        URL_PREFIX_BYTES[1] = 4;
	        Java.Lang.JavaSystem.Arraycopy(new Java.Lang.String(URL_PREFIX.Substring(8)).GetBytes(), 0, URL_PREFIX_BYTES, 2, URL_PREFIX_BYTES.Length - 2);
        }

	    
	    

    private ActivityDesign _design;

		public NfcOtpActivity()
		{
			_design = new ActivityDesign(this);
		}

		protected override void OnCreate(Bundle bundle)
		{
			_design.ApplyTheme();
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
			try
			{
				string otp = GetOtpFromIntent(Intent);
				if (otp == null)
					throw new Exception("Otp must not be null!");
				i.PutExtra(Intents.OtpExtraKey, otp);
			}
			catch (Exception e)
			{
				Kp2aLog.LogUnexpectedError(e);
				Toast.MakeText(this, "No Yubikey OTP found!", ToastLength.Long).Show();
				Finish();
				return;
			}

			StartActivity(i);				
			Finish();

		}

		protected override void OnResume()
		{
			base.OnResume();
			_design.ReapplyTheme();
		}

	}
}
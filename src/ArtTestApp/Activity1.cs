using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using KeeChallenge;
using KeePassLib.Serialization;
using keepass2android;
using keepass2android.Io;

namespace ArtTestApp
{
	[Activity(Label = "ArtTestApp", MainLauncher = true, Icon = "@drawable/icon")]
	public class Activity1 : Activity
	{
		private ChallengeInfo _chalInfo;
		private byte[] _challengeSecret;
		private IOConnectionInfo _ioConnectionInfo;
		private IOConnectionInfo _ioConnectionInfoOut;
		private const int RequestCodeChallengeYubikey = 98;

		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

			// Set our view from the "main" layout resource
			SetContentView(Resource.Layout.Main);

			_ioConnectionInfo = new IOConnectionInfo() { Path = "/mnt/sdcard/keepass/keechallenge.xml" };
			_ioConnectionInfoOut = new IOConnectionInfo() { Path = "/mnt/sdcard/keepass/keechallengeOut.xml" };
						

			// Get our button from the layout resource,
			// and attach an event to it
			FindViewById<Button>(Resource.Id.MyButton1).Click += (sender, args) =>
				{
					Decrypt(_ioConnectionInfo);

				};
			FindViewById<Button>(Resource.Id.MyButton2).Click += (sender, args) =>
				{
					Decrypt(_ioConnectionInfoOut);
				};
				

			FindViewById<Button>(Resource.Id.MyButton3).Click += (sender, args) => StartActivityForResult(typeof(PrefActivity), 1);
			
		}

		private void Decrypt(IOConnectionInfo ioConnectionInfo)
		{
			try
			{
				//StartActivity(typeof (Activity2));
				_chalInfo = ChallengeInfo.Load(ioConnectionInfo);
				Intent chalIntent = new Intent("com.yubichallenge.NFCActivity.CHALLENGE");
				chalIntent.PutExtra("challenge", _chalInfo.Challenge);
				chalIntent.PutExtra("slot", 2);
				chalIntent.AddFlags(ActivityFlags.SingleTop);
				IList<ResolveInfo> activities = PackageManager.QueryIntentActivities(chalIntent, 0);
				bool isIntentSafe = activities.Count > 0;
				if (isIntentSafe)
				{
					StartActivityForResult(chalIntent, RequestCodeChallengeYubikey);
				}

			}
			catch (Exception ex)
			{
				Toast.MakeText(this, ex.ToString(), ToastLength.Long).Show();
			}
		}

		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);

			if (requestCode == RequestCodeChallengeYubikey && resultCode == Result.Ok)
			{
				try
				{
					byte[] challengeResponse = data.GetByteArrayExtra("response");
					_challengeSecret = KeeChallengeProv.GetSecret(_chalInfo, challengeResponse);
					Array.Clear(challengeResponse, 0, challengeResponse.Length);
				}
				catch (Exception e)
				{
					Kp2aLog.Log(e.ToString());
					Toast.MakeText(this, "Error: " + e.Message, ToastLength.Long).Show();
					return;
				}

				if (_challengeSecret != null)
				{
					Toast.MakeText(this, "OK!", ToastLength.Long).Show();
					ChallengeInfo temp = KeeChallengeProv.Encrypt(_challengeSecret, _chalInfo.IV);
					if (!temp.Save(_ioConnectionInfoOut))
					{
						Toast.MakeText(this, "error writing file", ToastLength.Long).Show();
						return;
					}
				}
				else
				{
					Toast.MakeText(this, "Not good :-(", ToastLength.Long).Show();
					return;
				}
			}
		}
	}
}


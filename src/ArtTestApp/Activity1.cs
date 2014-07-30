using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;

//using KeePassLib.Serialization;
using MasterPassword;
//using keepass2android;
//using keepass2android.Io;

namespace ArtTestApp
{
	[Activity(Label = "ArtTestApp", MainLauncher = true, Icon = "@drawable/icon")]
	public class Activity1 : Activity
	{
		//private IOConnectionInfo _ioConnectionInfo;
		//private IOConnectionInfo _ioConnectionInfoOut;
		private const int RequestCodeChallengeYubikey = 98;

		private static byte[] HashHMAC(byte[] key, byte[] message)
		{
			var hash = new HMACSHA256(key);
			return hash.ComputeHash(message);
		}

		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

			// Set our view from the "main" layout resource
			SetContentView(Resource.Layout.Main);

			/*_ioConnectionInfo = new IOConnectionInfo() { Path = "/mnt/sdcard/keepass/keechallenge.xml" };
			_ioConnectionInfoOut = new IOConnectionInfo() { Path = "/mnt/sdcard/keepass/keechallengeOut.xml" };
			*/

			FindViewById<Button>(Resource.Id.MyButton1).Text = "";
			Stopwatch sw = new Stopwatch();
			sw.Start();
			var key = new MpAlgorithm().GetKeyForPassword("u", "test");
			sw.Stop(); 
			string password = MpAlgorithm.GenerateContent("Long Password", "strn", key, 1, HashHMAC);
			
			FindViewById<Button>(Resource.Id.MyButton1).Text = password;
			FindViewById<Button>(Resource.Id.MyButton2).Text = sw.ElapsedMilliseconds.ToString();


			// Get our button from the layout resource,
			// and attach an event to it
			FindViewById<Button>(Resource.Id.MyButton1).Click += (sender, args) =>
				{
				//	Decrypt(_ioConnectionInfo);

				};
			FindViewById<Button>(Resource.Id.MyButton2).Click += (sender, args) =>
				{
					//Decrypt(_ioConnectionInfoOut);
				};
				

			FindViewById<Button>(Resource.Id.MyButton3).Click += (sender, args) => StartActivityForResult(typeof(PrefActivity), 1);
			
		}

		/*private void Decrypt(IOConnectionInfo ioConnectionInfo)
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
		}*/

	}
}


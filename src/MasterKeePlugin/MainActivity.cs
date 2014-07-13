using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Preferences;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Keepass2android.Pluginsdk;

namespace MasterKeePlugin
{
	[Activity(Label = "MasterKee", MainLauncher = true, Icon = "@drawable/ic_launcher")]
	public class MainActivity : Activity
	{
		private const string KEY_MK_ENTRY_ID = "KEY_MK_ENTRY_ID";


		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

			// Set our view from the "main" layout resource
			SetContentView(Resource.Layout.Main);

			
			Button buttonSelect = FindViewById<Button>(Resource.Id.btnSelectMaster);

			buttonSelect.Click += delegate {
												var intent = Kp2aControl.QueryEntryIntentForOwnPackage;
				                               try
				                               {
												   StartActivityForResult(intent, 123);
				                               }
				                               catch (ActivityNotFoundException)
				                               {
					                               Toast.MakeText(this, "No Keepass2Android host app found. Please install Keepass2Android 0.9.4 or above!", ToastLength.Long).Show();
				                               }
					
				};

			Button buttonCreateMaster = FindViewById<Button>(Resource.Id.btnSetupMaster);

			buttonCreateMaster.Click += delegate
				{
					StartActivityForResult(new Intent(this, typeof (SetupMasterEntryActivity)), 124);
				};

			FindViewById(Resource.Id.btnAddEntry).Click += delegate 
			{
				//StartActivityForResult(new Intent(this, typeof()), 125);	
				var prefs = PreferenceManager.GetDefaultSharedPreferences(this);
				Dictionary<string, string> fields = new Dictionary<string, string>();
				fields["MK_Site_Counter"] = "1";
				fields["MK_Type"] = "Long Password";
				fields[KeepassDefs.PasswordField] = "{MASTERKEE}";
				fields["MK_MasterKey"] = "{REF:P@I:" + prefs.GetString(KEY_MK_ENTRY_ID, "") + "}";

				try
				{
					StartActivityForResult(Kp2aControl.GetAddEntryIntent(fields, new List<string>() { "MK_MasterKey" }), 125);
				}
				catch (ActivityNotFoundException)
				{
					Toast.MakeText(this, "No Keepass2Android host app found. Please install Keepass2Android 0.9.4 or above!", ToastLength.Long).Show();
				}
			};

		}

		protected override void OnResume()
		{
			base.OnResume();
			var prefs = PreferenceManager.GetDefaultSharedPreferences(this);

			if (prefs.Contains(KEY_MK_ENTRY_ID))
			{
				Button buttonCreateMaster = FindViewById<Button>(Resource.Id.btnSetupMaster);
				buttonCreateMaster.Visibility = ViewStates.Gone;
				Button buttonSelect = FindViewById<Button>(Resource.Id.btnSelectMaster);
				buttonSelect.Visibility = ViewStates.Gone;

				FindViewById(Resource.Id.btnAddEntry).Visibility = ViewStates.Visible;
			}
			else
			{
				FindViewById(Resource.Id.btnAddEntry).Visibility = ViewStates.Gone;
			}
		}

		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);

			if ((requestCode == 123) && (resultCode == Result.Ok))
			{
				string masterEntryId = data.GetStringExtra(Strings.ExtraEntryId);
				if (!String.IsNullOrEmpty(masterEntryId))
				{
					var prefs = PreferenceManager.GetDefaultSharedPreferences(this);
					prefs.Edit().PutString(KEY_MK_ENTRY_ID, masterEntryId).Commit();
				}
				
			}

		}
	}
}


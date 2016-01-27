using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Preferences;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace keepass2android
{
	[Activity(Label = AppNames.AppName, Theme = "@style/MyTheme_ActionBar")]
	public class ActivateAutoFillActivity : LifecycleDebugActivity
	{
		
		protected override void OnCreate(Bundle savedInstanceState)
		{
			new ActivityDesign(this).ApplyTheme();

			base.OnCreate(savedInstanceState);

			ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(this);
			App.Kp2a.AskYesNoCancel(UiStringKey.ActivateAutoFillService_title,
								UiStringKey.ActivateAutoFillService_message,
								UiStringKey.ActivateAutoFillService_btnKeyboard,
								UiStringKey.ActivateAutoFillService_btnAutoFill, 
								delegate
								{
									//yes
									CopyToClipboardService.ActivateKeyboard(this);
									Finish();
								},
								delegate
								{
									//no 
									Intent intent = new Intent(Android.Provider.Settings.ActionAccessibilitySettings);
									StartActivity(intent);
									prefs.Edit().PutBoolean(GetString(Resource.String.OpenKp2aKeyboardAutomatically_key), false).Commit();
									Toast.MakeText(this, Resource.String.ActivateAutoFillService_toast, ToastLength.Long).Show();
									Finish(); 
								},
								delegate
								{
									//cancel
									Finish();
								},
								(sender, args) => Finish() //dismiss
								,this);
			
		}
	}
}
using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;

namespace ArtTestApp
{
	[Activity(Label = "ArtTestApp", MainLauncher = true, Icon = "@drawable/icon")]
	public class Activity1 : Activity
	{
		
		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

			// Set our view from the "main" layout resource
			SetContentView(Resource.Layout.Main);

			// Get our button from the layout resource,
			// and attach an event to it
			FindViewById<Button>(Resource.Id.MyButton1).Click += (sender, args) =>
				{
					try
					{
						StartActivity(typeof (Activity2));
					}
					catch (Exception ex)
					{
						Toast.MakeText(this, ex.ToString(), ToastLength.Long).Show();
					}
					
				};
			FindViewById<Button>(Resource.Id.MyButton2).Click += (sender, args) => StartActivityForResult(typeof(Activity2),1);

			FindViewById<Button>(Resource.Id.MyButton3).Click += (sender, args) => StartActivityForResult(typeof(PrefActivity), 1);


		}
	}
}


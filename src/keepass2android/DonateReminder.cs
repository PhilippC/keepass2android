using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using KeePassLib.Utility;

namespace keepass2android
{
	[Activity(Label = AppNames.AppName, Theme = "@style/Base")]
	public class DonateReminder : Activity
	{
		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

			SetContentView(Resource.Layout.donate);

			FindViewById(Resource.Id.ok_donate).Click += (sender, args) => { Util.GotoDonateUrl(this);Finish(); };
			FindViewById(Resource.Id.no_donate).Click += (sender, args) => { Finish(); };
		}
	}
}
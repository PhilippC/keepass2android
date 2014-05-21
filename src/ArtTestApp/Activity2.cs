using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace ArtTestApp
{
	[Activity(Label = "Activity2",
			ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden)]
	[IntentFilter(new[] { "blabla" },
		Categories = new[] { Intent.CategoryDefault })]
	
	public class Activity2 : Activity
	{
		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

			// Create your application here
		}
	}
}
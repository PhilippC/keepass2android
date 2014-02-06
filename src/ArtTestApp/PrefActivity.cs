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

namespace ArtTestApp
{
	[Activity(Label = "My PrefActivity")]
	public class PrefActivity : PreferenceActivity
	{
		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

			FragmentManager.BeginTransaction().Replace(Android.Resource.Id.Content, new AppPreferenceFragment()).Commit();
		}

		public class AppPreferenceFragment : PreferenceFragment
		{
			public override void OnCreate(Bundle savedInstanceState)
			{
				base.OnCreate(savedInstanceState);

				AddPreferencesFromResource(Resource.Xml.pref);
			}
		}	
	}
}
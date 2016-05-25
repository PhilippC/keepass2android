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
using KeePassLib.Utility;

namespace keepass2android
{
    [Activity(Label = AppNames.AppName, Theme = "@style/MyTheme_ActionBar")]
	public class DonateReminder : Activity
	{
		class Reminder
		{
			public DateTime From, To;
			public int ResourceToShow;
			public string Key;
		}

		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

			foreach (Reminder r in GetReminders())
			{
				if ((DateTime.Now >= r.From)
				    && (DateTime.Now < r.To))
				{
					SetContentView(r.ResourceToShow);
				}
			}

			FindViewById(Resource.Id.ok_donate).Click += (sender, args) => { Util.GotoDonateUrl(this);Finish(); };
			FindViewById(Resource.Id.no_donate).Click += (sender, args) =>
			{
				ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(this);
					
				ISharedPreferencesEditor edit = prefs.Edit();
				edit.PutBoolean("DismissedDonateReminder", true);
				EditorCompat.Apply(edit);

				Finish();
			};
		}
		static IEnumerable<Reminder> GetReminders()
		{
			yield return new Reminder
			{
				From = new DateTime(2015, 09, 19),
				To = new DateTime(2015, 10, 05),
				Key = "DonationOktoberfest2015"
					,ResourceToShow = Resource.Layout.donate
			};

			yield return new Reminder
			{
				From = new DateTime(2016, 09, 17), 
				To = new DateTime(2016, 10, 04),
				Key = "DonationOktoberfest2016"
					,ResourceToShow = Resource.Layout.donate
			};
			yield return new Reminder
			{
				From = new DateTime(2017, 09, 16),
				To = new DateTime(2017, 09, 25),
				Key = "DonationOktoberfest2017b"//b because year was incorrectly set to 2015 in 0.9.8b
					,
				ResourceToShow = Resource.Layout.donate
			};
			yield return new Reminder
			{
				From = new DateTime(2017, 09, 25),
				To = new DateTime(2017, 10, 04),
				Key = "DonationOktoberfest2017b-2"
					,
				ResourceToShow = Resource.Layout.donate
			};
			yield return new Reminder
			{
				From = new DateTime(2018, 09, 22),
				To = new DateTime(2018, 03, 30),
				Key = "DonationOktoberfest2018b"//b because year was incorrectly set to 2015 in 0.9.8b
					,ResourceToShow = Resource.Layout.donate
			};
			yield return new Reminder
			{
				From = new DateTime(2018, 09, 30),
				To = new DateTime(2018, 10, 08),
				Key = "DonationOktoberfest2018b-2"
					,
				ResourceToShow = Resource.Layout.donate
			};

			int thisYear = DateTime.Now.Year;
			
			yield return new Reminder
			{
				From = new DateTime(thisYear, 05, 10),
				To = new DateTime(thisYear, 05, 11),
				Key = "DonationBirthday" + thisYear,
				ResourceToShow = Resource.Layout.donate_bday
			};
			yield return new Reminder
			{
				From = new DateTime(thisYear, 05, 11),
				To = new DateTime(thisYear, 05, 16),
				Key = "DonationBirthday" + thisYear,
				ResourceToShow = Resource.Layout.donate_bdaymissed
			};
		}

		public static void ShowDonateReminderIfAppropriate(Activity context)
		{
			ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(context);
			if (prefs.GetBoolean(context.GetString(Resource.String.NoDonationReminder_key), false))
				return;

			long usageCount = prefs.GetLong(context.GetString(Resource.String.UsageCount_key), 0);

			if (usageCount <= 5)
				return;
					
			foreach (Reminder r in GetReminders())
			{
				if ((DateTime.Now >= r.From )
					&& (DateTime.Now < r.To))
				{
					if (prefs.GetBoolean(r.Key, false) == false)
					{
						ISharedPreferencesEditor edit = prefs.Edit();
						edit.PutBoolean(r.Key, true);
						EditorCompat.Apply(edit);

						context.StartActivity(new Intent(context, typeof(DonateReminder)));
						break;
					}
				}
			}
			
		}
	}
}
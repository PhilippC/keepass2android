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
	[Activity(Label = AppNames.AppName, Theme = "@style/Base")]
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
				if ((DateTime.Now > r.From)
				    && (DateTime.Now < r.To))
				{
					SetContentView(r.ResourceToShow);
				}
			}

			FindViewById(Resource.Id.ok_donate).Click += (sender, args) => { Util.GotoDonateUrl(this);Finish(); };
			FindViewById(Resource.Id.no_donate).Click += (sender, args) => { Finish(); };
		}
		static IEnumerable<Reminder> GetReminders()
		{
			yield return new Reminder
				{
					From = new DateTime(2014, 09, 20),
					To = new DateTime(2014, 10, 06),
					Key = "DonationOktoberfest2014"
					,ResourceToShow = Resource.Layout.donate
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
			foreach (Reminder r in GetReminders())
			{
				if ((DateTime.Now >= r.From )
					&& (DateTime.Now < r.To))
				{
					ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(context);
					if (prefs.GetBoolean(r.Key, false) == false)
					{
						ISharedPreferencesEditor edit = prefs.Edit();
						edit.PutBoolean(r.Key, true);
						EditorCompat.Apply(edit);

						context.StartActivity(new Intent(context, typeof(DonateReminder)));
					}
				}
			}
			
		}
	}
}
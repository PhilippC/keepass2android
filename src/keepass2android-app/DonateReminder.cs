// This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
//
//   Keepass2Android is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   Keepass2Android is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.

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
using keepass2android;
using KeePassLib.Utility;

namespace keepass2android
{
    [Activity(Label = AppNames.AppName, Theme = "@style/Kp2aTheme_ActionBar")]
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

            FindViewById(Resource.Id.ok_donate).Click += (sender, args) => { Util.GotoDonateUrl(this); Finish(); };
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
                From = new DateTime(2024, 09, 21),
                To = new DateTime(2024, 09, 28),
                Key = "DonationOktoberfest2024"
                ,
                ResourceToShow = Resource.Layout.donate
            };

            yield return new Reminder
            {
                From = new DateTime(2024, 09, 28),
                To = new DateTime(2024, 10, 08),
                Key = "DonationOktoberfest2024-2"
                ,
                ResourceToShow = Resource.Layout.donate
            };


            yield return new Reminder
            {
                From = new DateTime(2025, 09, 20),
                To = new DateTime(2025, 09, 28),
                Key = "DonationOktoberfest2025-1"
                ,
                ResourceToShow = Resource.Layout.donate
            };

            yield return new Reminder
            {
                From = new DateTime(2025, 09, 28),
                To = new DateTime(2025, 10, 07),
                Key = "DonationOktoberfest2025-2"
                ,
                ResourceToShow = Resource.Layout.donate
            };


            yield return new Reminder
            {
                From = new DateTime(2026, 09, 19),
                To = new DateTime(2026, 09, 28),
                Key = "DonationOktoberfest2026-1"
                ,
                ResourceToShow = Resource.Layout.donate
            };

            yield return new Reminder
            {
                From = new DateTime(2026, 09, 28),
                To = new DateTime(2026, 10, 06),
                Key = "DonationOktoberfest2026-2"
                ,
                ResourceToShow = Resource.Layout.donate
            };

            yield return new Reminder
            {
                From = new DateTime(2027, 09, 18),
                To = new DateTime(2027, 09, 26),
                Key = "DonationOktoberfest2027-1"
                ,
                ResourceToShow = Resource.Layout.donate
            };

            yield return new Reminder
            {
                From = new DateTime(2027, 09, 26),
                To = new DateTime(2027, 10, 05),
                Key = "DonationOktoberfest2027-2"
                ,
                ResourceToShow = Resource.Layout.donate
            };


            yield return new Reminder
            {
                From = new DateTime(2028, 09, 16),
                To = new DateTime(2028, 09, 26),
                Key = "DonationOktoberfest2028-1"
                ,
                ResourceToShow = Resource.Layout.donate
            };

            yield return new Reminder
            {
                From = new DateTime(2028, 09, 26),
                To = new DateTime(2028, 10, 05),
                Key = "DonationOktoberfest2028-2"
                ,
                ResourceToShow = Resource.Layout.donate
            };

            if (DateTime.Now.Year > 2028)
            {
                yield return new Reminder
                {
                    From = new DateTime(DateTime.Now.Year, 09, 20),
                    To = new DateTime(DateTime.Now.Year, 09, 26),
                    Key = $"DonationOktoberfest{DateTime.Now.Year}-1"
                    ,
                    ResourceToShow = Resource.Layout.donate
                };

                yield return new Reminder
                {
                    From = new DateTime(DateTime.Now.Year, 09, 26),
                    To = new DateTime(DateTime.Now.Year, 10, 08),
                    Key = $"DonationOktoberfest{DateTime.Now.Year}-2"
                    ,
                    ResourceToShow = Resource.Layout.donate
                };
            }







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
                To = new DateTime(thisYear, 05, 18),
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
                if ((DateTime.Now >= r.From)
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
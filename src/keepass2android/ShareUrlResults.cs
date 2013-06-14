/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. 

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 2 of the License, or
  (at your option) any later version.

  Keepass2Android is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
  */


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
using keepass2android.view;
using Android.Content.PM;

namespace keepass2android
{
	[Activity (Label = "@string/kp2a_findUrl", ConfigurationChanges=ConfigChanges.Orientation|ConfigChanges.KeyboardHidden, Theme="@style/Base")]		
	public class ShareUrlResults : GroupBaseActivity
	{

		public ShareUrlResults (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}

		public ShareUrlResults()
		{
		}

		public static void Launch(Activity act, SearchUrlTask task)
		{
			Intent i = new Intent(act, typeof(ShareUrlResults));
			task.ToIntent(i);
			act.StartActivityForResult(i, 0);
		}


		private Database mDb;

		
		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			SetResult(KeePass.EXIT_CLOSE_AFTER_TASK_COMPLETE);

			mDb = App.Kp2a.GetDb();

			String searchUrl = ((SearchUrlTask)mAppTask).UrlToSearchFor;
			
			if (!mDb.Loaded)
			{
				Intent intent = new Intent(this, typeof(FileSelectActivity));
				mAppTask.ToIntent(intent);
				StartActivityForResult(intent, 0);

				Finish();
			}
			else if (mDb.Locked)
			{
				PasswordActivity.Launch(this,mDb.mIoc, mAppTask);
				Finish();
			}
			else
			{
				query(searchUrl);
			}
			
		}

		protected override void OnSaveInstanceState(Bundle outState)
		{
			base.OnSaveInstanceState(outState);
			mAppTask.ToBundle(outState);
		}

		public override void LaunchActivityForEntry(KeePassLib.PwEntry pwEntry, int pos)
		{
			base.LaunchActivityForEntry(pwEntry, pos);
			Finish();
		}
		
		private void query(String url)
		{
			//first: search for exact url
			try
			{
				mGroup = mDb.SearchForExactUrl(url);
			} catch (Exception e)
			{
				Toast.MakeText(this, e.Message, ToastLength.Long).Show();
				Finish();
				return;
			}
			//if no results, search for host (e.g. "accounts.google.com")
			if (mGroup.Entries.Count() == 0)
			{
				try
				{
					mGroup = mDb.SearchForHost(url, false);
				} catch (Exception e)
				{
					Toast.MakeText(this, e.Message, ToastLength.Long).Show();
					Finish();
					return;
				}
			}
			//if still no results, search for host, allowing subdomains ("www.google.com" in entry is ok for "accounts.google.com" in search (but not the other way around)
			if (mGroup.Entries.Count() == 0)
			{
				try
				{
					mGroup = mDb.SearchForHost(url, true);
				} catch (Exception e)
				{
					Toast.MakeText(this, e.Message, ToastLength.Long).Show();
					Finish();
					return;
				}
			}
			//if there is exactly one match: open the entry
			if (mGroup.Entries.Count() == 1)
			{
				LaunchActivityForEntry(mGroup.Entries.Single(),0);
				return;
			}

			//show results:
			if (mGroup == null || (mGroup.Entries.Count() < 1))
			{
				//SetContentView(new GroupEmptyView(this));
				SetContentView(Resource.Layout.searchurlresults_empty);
			} else
			{
				SetContentView(Resource.Layout.searchurlresults);
				//SetContentView(new GroupViewOnlyView(this));
			}
			
			setGroupTitle();
			
			ListAdapter = new PwGroupListAdapter(this, mGroup);

			View selectOtherEntry = FindViewById (Resource.Id.select_other_entry);
			selectOtherEntry.Click += (object sender, EventArgs e) => {
				GroupActivity.Launch (this, new SelectEntryTask());
			};

			
			View createUrlEntry = FindViewById (Resource.Id.add_url_entry);
			createUrlEntry.Click += (object sender, EventArgs e) => {
				GroupActivity.Launch (this, new CreateEntryThenCloseTask() { Url = url } );
				Toast.MakeText(this, GetString(Resource.String.select_group_then_add, new Java.Lang.Object[]{GetString(Resource.String.add_entry)}), ToastLength.Long ).Show();
			};


		}
		
		private String getSearchUrl(Intent queryIntent) {
			String queryAction = queryIntent.Action;
			return queryIntent.GetStringExtra(Intent.ExtraText);
			
		}

		public override bool OnSearchRequested()
		{
			if (base.OnSearchRequested())
			{
				Finish();
				return true;
			} else
			{
				return false;
			}
		}
	}}


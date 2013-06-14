/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

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
using KeePassLib;
using Android.Util;
using KeePassLib.Utility;
using keepass2android.view;
using Android.Content.PM;

namespace keepass2android
{
	[Activity (Label = "@string/app_name", ConfigurationChanges=ConfigChanges.Orientation|ConfigChanges.KeyboardHidden , Theme="@style/NoTitleBar")]		
	[MetaData("android.app.default_searchable",Value="keepass2android.search.SearchResults")]
	public class GroupActivity : GroupBaseActivity {
		
		public const int UNINIT = -1;
		
		protected bool addGroupEnabled = false;
		protected bool addEntryEnabled = false;
		
		private const String TAG = "Group Activity:";
		
		public static void Launch(Activity act, AppTask appTask) {
			Launch(act, null, appTask);
		}
		
		public static void Launch (Activity act, PwGroup g, AppTask appTask)
		{
			Intent i;
			
			// Need to use PwDatabase since group may be null
            PwDatabase db = App.Kp2a.GetDb().pm;

			if (db == null) {
				// Reached if db is null
				Log.Debug (TAG, "Tried to launch with null db");
				return;
			}

			i = new Intent(act, typeof(GroupActivity));
				
			if ( g != null ) {
				i.PutExtra(KEY_ENTRY, g.Uuid.ToHexString());
			}
			appTask.ToIntent(i);

			act.StartActivityForResult(i,0);
		}

		protected PwUuid retrieveGroupId(Intent i)
		{
			String uuid = i.GetStringExtra(KEY_ENTRY);
			
			if ( String.IsNullOrEmpty(uuid) ) {
				return null;
			}
			return new PwUuid(MemUtil.HexStringToByteArray(uuid));
		}
		
		protected void setupButtons()
		{
			addGroupEnabled = true;
			addEntryEnabled = !mGroup.Uuid.EqualsValue(App.Kp2a.GetDb().root.Uuid);
		}
		
		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
			
			if (IsFinishing) {
				return;
			}
			
			SetResult (KeePass.EXIT_NORMAL);
			
			Log.Warn (TAG, "Creating group view");
			Intent intent = Intent;
			
			PwUuid id = retrieveGroupId (intent);
			
			Database db = App.Kp2a.GetDb();
			if (id == null) {
				mGroup = db.root;
			} else {
				mGroup = db.groups[id];
			}
			
			Log.Warn (TAG, "Retrieved group");
			if (mGroup == null) {
				Log.Warn (TAG, "Group was null");
				return;
			}
			
			setupButtons ();
			
			if (addGroupEnabled && addEntryEnabled) {
				SetContentView (new GroupAddEntryView (this));
			} else if (addGroupEnabled) {
				SetContentView (new GroupRootView (this));
			} else if (addEntryEnabled) {
				throw new Exception ("This mode is not supported.");
			} else {
				SetContentView (new GroupViewOnlyView (this));
			}
			if (addGroupEnabled) {
				// Add Group button
				View addGroup = FindViewById (Resource.Id.add_group);
				addGroup.Click += (object sender, EventArgs e) => {
					GroupEditActivity.Launch (this, mGroup);
				};
			}
			
			if (addEntryEnabled) {
				// Add Entry button
				View addEntry = FindViewById (Resource.Id.add_entry);
				addEntry.Click += (object sender, EventArgs e) => {
					EntryEditActivity.Launch (this, mGroup, mAppTask);

				};
			}
			
			setGroupTitle();
			setGroupIcon();
			
			ListAdapter = new PwGroupListAdapter(this, mGroup);
			RegisterForContextMenu(ListView);
			Log.Warn(TAG, "Finished creating group");
			
		}

		public override void OnCreateContextMenu(IContextMenu menu, View v,
		                                         IContextMenuContextMenuInfo  menuInfo) {
			
			AdapterView.AdapterContextMenuInfo acmi = (AdapterView.AdapterContextMenuInfo) menuInfo;
			ClickView cv = (ClickView) acmi.TargetView;
			cv.OnCreateMenu(menu, menuInfo);
		}
		
		public override void OnBackPressed()
		{
			base.OnBackPressed();
			if ((mGroup != null) && (mGroup.ParentGroup != null))
				OverridePendingTransition(Resource.Animation.anim_enter_back, Resource.Animation.anim_leave_back);
		}
		
		public override bool OnContextItemSelected(IMenuItem item) {
			Android.Widget.AdapterView.AdapterContextMenuInfo acmi = (Android.Widget.AdapterView.AdapterContextMenuInfo)item.MenuInfo;
			ClickView cv = (ClickView) acmi.TargetView;
			
			return cv.OnContextItemSelected(item);
		}
		
		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			switch (resultCode)
			{
			case Result.Ok:
				String GroupName = data.Extras.GetString(GroupEditActivity.KEY_NAME);
				int GroupIconID = data.Extras.GetInt(GroupEditActivity.KEY_ICON_ID);
				GroupBaseActivity act = this;
				Handler handler = new Handler();
				AddGroup task = AddGroup.getInstance(this, App.Kp2a.GetDb(), GroupName, GroupIconID, mGroup, new RefreshTask(handler, this), false);
                ProgressTask pt = new ProgressTask(App.Kp2a, act, task, UiStringKey.saving_database);
				pt.run();
				break;
				
				case Result.Canceled:
				default:
					base.OnActivityResult(requestCode, resultCode, data);
				break;
			}
		}
	}
}


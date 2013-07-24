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
using Android.App;
using Android.Content;
using Android.OS;
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
		
		public const int Uninit = -1;
		
		protected bool AddGroupEnabled = false;
		protected bool AddEntryEnabled = false;
		
		private const String Tag = "Group Activity:";
		
		public static void Launch(Activity act, AppTask appTask) {
			Launch(act, null, appTask);
		}
		
		public static void Launch (Activity act, PwGroup g, AppTask appTask)
		{
			// Need to use PwDatabase since group may be null
            PwDatabase db = App.Kp2a.GetDb().KpDatabase;

			if (db == null) {
				// Reached if db is null
				Log.Debug (Tag, "Tried to launch with null db");
				return;
			}

			Intent i = new Intent(act, typeof(GroupActivity));
				
			if ( g != null ) {
				i.PutExtra(KeyEntry, g.Uuid.ToHexString());
			}
			appTask.ToIntent(i);

			act.StartActivityForResult(i,0);
		}

		protected PwUuid RetrieveGroupId(Intent i)
		{
			String uuid = i.GetStringExtra(KeyEntry);
			
			if ( String.IsNullOrEmpty(uuid) ) {
				return null;
			}
			return new PwUuid(MemUtil.HexStringToByteArray(uuid));
		}
		
		protected void SetupButtons()
		{
			AddGroupEnabled = true;
			AddEntryEnabled = true;
		}
		
		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
			
			if (IsFinishing) {
				return;
			}
			
			SetResult (KeePass.ExitNormal);
			
			Log.Warn (Tag, "Creating group view");
			Intent intent = Intent;
			
			PwUuid id = RetrieveGroupId (intent);
			
			Database db = App.Kp2a.GetDb();
			if (id == null) {
				Group = db.Root;
			} else {
				Group = db.Groups[id];
			}
			
			Log.Warn (Tag, "Retrieved group");
			if (Group == null) {
				Log.Warn (Tag, "Group was null");
				return;
			}
			
			SetupButtons ();
			
			if (AddGroupEnabled && AddEntryEnabled) {
				SetContentView (new GroupAddEntryView (this));
			} else if (AddGroupEnabled) {
				SetContentView (new GroupRootView (this));
			} else if (AddEntryEnabled) {
				throw new Exception ("This mode is not supported.");
			} else {
				SetContentView (new GroupViewOnlyView (this));
			}
			if (AddGroupEnabled) {
				// Add Group button
				View addGroup = FindViewById (Resource.Id.add_group);
				addGroup.Click += (sender, e) => {
					GroupEditActivity.Launch (this, Group);
				};
			}
			
			if (AddEntryEnabled) {
				// Add Entry button
				View addEntry = FindViewById (Resource.Id.add_entry);
				addEntry.Click += (sender, e) => {
					EntryEditActivity.Launch (this, Group, AppTask);

				};
			}
			
			SetGroupTitle();
			SetGroupIcon();
			
			ListAdapter = new PwGroupListAdapter(this, Group);
			RegisterForContextMenu(ListView);
			Log.Warn(Tag, "Finished creating group");
			
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
			if ((Group != null) && (Group.ParentGroup != null))
				OverridePendingTransition(Resource.Animation.anim_enter_back, Resource.Animation.anim_leave_back);
		}
		
		public override bool OnContextItemSelected(IMenuItem item) {
			AdapterView.AdapterContextMenuInfo acmi = (AdapterView.AdapterContextMenuInfo)item.MenuInfo;
			ClickView cv = (ClickView) acmi.TargetView;
			
			return cv.OnContextItemSelected(item);
		}
		
		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			switch (resultCode)
			{
			case Result.Ok:
				String groupName = data.Extras.GetString(GroupEditActivity.KeyName);
				int groupIconId = data.Extras.GetInt(GroupEditActivity.KeyIconId);
				GroupBaseActivity act = this;
				Handler handler = new Handler();
				AddGroup task = AddGroup.GetInstance(this, App.Kp2a, groupName, groupIconId, Group, new RefreshTask(handler, this), false);
                ProgressTask pt = new ProgressTask(App.Kp2a, act, task);
				pt.Run();
				break;
				
			case Result.Canceled:
				break;
			default:
				base.OnActivityResult(requestCode, resultCode, data);
				break;
			}
		}
	}
}


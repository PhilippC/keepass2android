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
using Android.Runtime;
using Android.Views;
using Android.Widget;
using KeePassLib;
using Android.Preferences;
using keepass2android.view;
using Android.Graphics.Drawables;

namespace keepass2android
{
	
	public abstract class GroupBaseActivity : LockCloseListActivity {
		public const String KeyEntry = "entry";
		public const String KeyMode = "mode";

		public virtual void LaunchActivityForEntry(PwEntry pwEntry, int pos)
		{
			EntryActivity.Launch(this, pwEntry, pos, AppTask);
		}

		protected GroupBaseActivity ()
		{

		}

		protected GroupBaseActivity (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}

		protected override void OnSaveInstanceState(Bundle outState)
		{
			base.OnSaveInstanceState(outState);
			AppTask.ToBundle(outState);
		}

		
		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);

			if (resultCode == KeePass.ExitCloseAfterTaskComplete)
			{
				SetResult(KeePass.ExitCloseAfterTaskComplete);
				Finish();
			}

		}
		
		private ISharedPreferences _prefs;
		
		protected PwGroup Group;

		internal AppTask AppTask;
		
		protected override void OnResume() {
			base.OnResume();
			
			RefreshIfDirty();
		}

		public override bool OnSearchRequested()
		{
			StartActivityForResult(typeof(SearchActivity), 0);
			return true;
		}

		public void RefreshIfDirty() {
			Database db = App.Kp2a.GetDb();
			if ( db.Dirty.Contains(Group) ) {
				db.Dirty.Remove(Group);
				BaseAdapter adapter = (BaseAdapter) ListAdapter;
				adapter.NotifyDataSetChanged();
				
			}
		}
		
		protected override void OnListItemClick(ListView l, View v, int position, long id) {
			base.OnListItemClick(l, v, position, id);
			
			IListAdapter adapt = ListAdapter;
			ClickView cv = (ClickView) adapt.GetView(position, null, null);
			cv.OnClick();
			
		}
		
		protected override void OnCreate(Bundle savedInstanceState) {
			base.OnCreate(savedInstanceState);

			AppTask = AppTask.GetTaskInOnCreate(savedInstanceState, Intent);
			
			// Likely the app has been killed exit the activity 
			if ( ! App.Kp2a.GetDb().Loaded ) {
				Finish();
				return;
			}
			
			_prefs = PreferenceManager.GetDefaultSharedPreferences(this);
			
			SetContentView(new GroupViewOnlyView(this));
			SetResult(KeePass.ExitNormal);
			
			StyleScrollBars();
			
		}
		
		protected void StyleScrollBars() {
			ListView lv = ListView;
			lv.ScrollBarStyle =ScrollbarStyles.InsideInset;
			lv.TextFilterEnabled = true;
			
		}
		
		
		protected void SetGroupTitle()
		{
			String name = Group.Name;
			String titleText;
			bool clickable = (Group != null) && (Group.IsVirtual == false) && (Group.ParentGroup != null);
			if (!String.IsNullOrEmpty(name))
			{
				titleText = name;
			} else
			{
				titleText = GetText(Resource.String.root);
			}

			//see if the button for SDK Version < 11 is there
			Button tv = (Button)FindViewById(Resource.Id.group_name);
			if (tv != null)
			{
				if (Group != null)
				{
					tv.Text = titleText;
				}

				if (clickable)
				{
					tv.Click += (sender, e) => 
					{
						Finish();
					};
				} else
				{
					tv.SetCompoundDrawables(null, null, null, null);
					tv.Clickable = false;
				}
			}
			//ICS?
			if (Util.HasActionBar(this))
			{
				ActionBar.Title = titleText;
				if (clickable)
					ActionBar.SetDisplayHomeAsUpEnabled(true);
			}
		}

		
		protected void SetGroupIcon() {
			if (Group != null) {
				Drawable drawable = App.Kp2a.GetDb().DrawableFactory.GetIconDrawable(Resources, App.Kp2a.GetDb().KpDatabase, Group.IconId, Group.CustomIconUuid);
				ImageView iv = (ImageView) FindViewById(Resource.Id.icon);
				if (iv != null)
					iv.SetImageDrawable(drawable);
				if (Util.HasActionBar(this))
					ActionBar.SetIcon(drawable);
			}
		}
		
		public override bool OnCreateOptionsMenu(IMenu menu) {
			base.OnCreateOptionsMenu(menu);
			
			MenuInflater inflater = MenuInflater;
			inflater.Inflate(Resource.Menu.group, menu);
			if (Util.HasActionBar(this))
			{
				var searchManager = (SearchManager) GetSystemService(Context.SearchService);
				var searchView = (SearchView) menu.FindItem(Resource.Id.menu_search).ActionView;

				searchView.SetSearchableInfo(searchManager.GetSearchableInfo(ComponentName));
			}
			return true;
		}
		
		private void SetSortMenuText(IMenu menu) {
			bool sortByName = _prefs.GetBoolean(GetString(Resource.String.sort_key), Resources.GetBoolean(Resource.Boolean.sort_default));
			
			int resId;
			if ( sortByName ) {
				resId = Resource.String.sort_db;
			} else {
				resId = Resource.String.sort_name;
			}
			
			menu.FindItem(Resource.Id.menu_sort).SetTitle(resId);
			
		}
		
		public override bool OnPrepareOptionsMenu(IMenu menu) {
			if ( ! base.OnPrepareOptionsMenu(menu) ) {
				return false;
			}
			
			SetSortMenuText(menu);
			
			return true;
		}
		
		public override bool OnOptionsItemSelected(IMenuItem item) {
			switch ( item.ItemId ) {
			case Resource.Id.menu_donate:
				try {
						Util.gotoDonateUrl(this);
				} catch (ActivityNotFoundException) {
					Toast.MakeText(this, Resource.String.error_failed_to_launch_link, ToastLength.Long).Show();
					return false;
				}
				
				return true;
			case Resource.Id.menu_lock:
				App.Kp2a.LockDatabase();
				return true;

			case Resource.Id.menu_search:
			case Resource.Id.menu_search_advanced:
				OnSearchRequested();
				return true;
				
			case Resource.Id.menu_app_settings:
				DatabaseSettingsActivity.Launch(this);
				return true;
				
			case Resource.Id.menu_change_master_key:
				SetPassword();
				return true;
				
			case Resource.Id.menu_sort:
				ToggleSort();
				return true;
			case Resource.Id.menu_rate:
				try {
					Util.gotoMarket(this);
				} catch (ActivityNotFoundException) {
					Toast.MakeText(this, Resource.String.no_url_handler, ToastLength.Long).Show();
				}
				return true;
			case Resource.Id.menu_suggest_improvements:
				try {
					Util.gotoUrl(this, Resource.String.SuggestionsURL);
				} catch (ActivityNotFoundException) {
					Toast.MakeText(this, Resource.String.no_url_handler, ToastLength.Long).Show();
				}
				return true;
			case Resource.Id.menu_translate:
				try {
					Util.gotoUrl(this, Resource.String.TranslationURL);
				} catch (ActivityNotFoundException) {
					Toast.MakeText(this, Resource.String.no_url_handler, ToastLength.Long).Show();
				}
				return true;	
			case Android.Resource.Id.Home:
				//Currently the action bar only displays the home button when we come from a previous activity.
				//So we can simply Finish. See this page for information on how to do this in more general (future?) cases:
				//http://developer.android.com/training/implementing-navigation/ancestral.html
				Finish();
				OverridePendingTransition(Resource.Animation.anim_enter_back, Resource.Animation.anim_leave_back);

				return true;
			}
			
			return base.OnOptionsItemSelected(item);
		}
		
		private void ToggleSort() {
			// Toggle setting
			String sortKey = GetString(Resource.String.sort_key);
			bool sortByName = _prefs.GetBoolean(sortKey, Resources.GetBoolean(Resource.Boolean.sort_default));
			ISharedPreferencesEditor editor = _prefs.Edit();
			editor.PutBoolean(sortKey, ! sortByName);
			EditorCompat.Apply(editor);
			
			// Refresh menu titles
			ActivityCompat.InvalidateOptionsMenu(this);
			
			// Mark all groups as dirty now to refresh them on load
			Database db = App.Kp2a.GetDb();
			db.MarkAllGroupsAsDirty();
			// We'll manually refresh this group so we can remove it
			db.Dirty.Remove(Group);
			
			// Tell the adapter to refresh it's list
			BaseAdapter adapter = (BaseAdapter) ListAdapter;
			adapter.NotifyDataSetChanged();
			
		}
		
		private void SetPassword() {
			SetPasswordDialog dialog = new SetPasswordDialog(this);
			dialog.Show();
		}
		
		 public class RefreshTask : OnFinish {
			 readonly GroupBaseActivity _act;
			public RefreshTask(Handler handler, GroupBaseActivity act):base(handler) {
				_act = act;
			}

			public override void Run() {
				if ( Success) {
					_act.RefreshIfDirty();
				} else {
					DisplayMessage(_act);
				}
			}
		}
		public class AfterDeleteGroup : OnFinish {
			readonly GroupBaseActivity _act;

			public AfterDeleteGroup(Handler handler, GroupBaseActivity act):base(handler) {
				_act = act;
			}
			

			public override void Run() {
				if ( Success) {
					_act.RefreshIfDirty();
				} else {
					Handler.Post( () => {
						Toast.MakeText(_act,  "Unrecoverable error: " + Message, ToastLength.Long).Show();
					});

					App.Kp2a.LockDatabase();
				}
			}
			
		}

	}
}


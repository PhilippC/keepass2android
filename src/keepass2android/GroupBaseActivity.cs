/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 3 of the License, or
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
using KeePassLib.Interfaces;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using keepass2android.Io;
using keepass2android.database.edit;
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

		public virtual void SetupNormalButtons()
		{
			GroupView.SetNormalButtonVisibility(App.Kp2a.GetDb().CanWrite, App.Kp2a.GetDb().CanWrite);
		}
		
		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);

			if (AppTask.TryGetFromActivityResult(data, ref AppTask))
			{
				//make sure the app task is passed to the calling activity
				AppTask.SetActivityResult(this, KeePass.ExitNormal);
			}

			if (resultCode == Result.Ok)
			{
				String groupName = data.Extras.GetString(GroupEditActivity.KeyName);
				int groupIconId = data.Extras.GetInt(GroupEditActivity.KeyIconId);
				PwUuid groupCustomIconId =
					new PwUuid(MemUtil.HexStringToByteArray(data.Extras.GetString(GroupEditActivity.KeyCustomIconId)));
				String strGroupUuid = data.Extras.GetString(GroupEditActivity.KeyGroupUuid);
				GroupBaseActivity act = this;
				Handler handler = new Handler();
				RunnableOnFinish task;
				if (strGroupUuid == null)
				{
					task = AddGroup.GetInstance(this, App.Kp2a, groupName, groupIconId, Group, new RefreshTask(handler, this), false);
				}
				else
				{
					PwUuid groupUuid = new PwUuid(MemUtil.HexStringToByteArray(strGroupUuid));
					task = new EditGroup(this, App.Kp2a, groupName, (PwIcon) groupIconId, groupCustomIconId, App.Kp2a.GetDb().Groups[groupUuid],
					                     new RefreshTask(handler, this));
				}
				ProgressTask pt = new ProgressTask(App.Kp2a, act, task);
				pt.Run();
			}

			if (resultCode == KeePass.ExitCloseAfterTaskComplete)
			{
				AppTask.SetActivityResult(this, KeePass.ExitCloseAfterTaskComplete);
				Finish();
			}

			if (resultCode == KeePass.ExitReloadDb)
			{
				AppTask.SetActivityResult(this, KeePass.ExitReloadDb);
				Finish();
			}

		}
		
		private ISharedPreferences _prefs;
		
		protected PwGroup Group;

		internal AppTask AppTask;
		protected GroupView GroupView;

		protected override void OnResume() {
			base.OnResume();

			AppTask.SetupGroupBaseActivityButtons(this);
			
			RefreshIfDirty();
		}

		public override bool OnSearchRequested()
		{
			Intent i = new Intent(this, typeof(SearchActivity));
			AppTask.ToIntent(i);
			StartActivityForResult(i, 0);
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

			GroupView = new GroupView(this);
			SetContentView(GroupView);

			FindViewById(Resource.Id.cancel_insert_element).Click += (sender, args) => StopMovingElement();
			FindViewById(Resource.Id.insert_element).Click += (sender, args) => InsertElement();

			SetResult(KeePass.ExitNormal);
			
			StyleScrollBars();
			
		}

		protected override void OnStart()
		{
			base.OnStart();
			AppTask.StartInGroupActivity(this);
		}

		private void InsertElement()
		{
			MoveElementTask moveElementTask = (MoveElementTask)AppTask;
			IStructureItem elementToMove = App.Kp2a.GetDb().KpDatabase.RootGroup.FindObject(moveElementTask.Uuid, true, null);


			var moveElement = new MoveElement(elementToMove, Group, this, App.Kp2a, new ActionOnFinish((success, message) => { StopMovingElement(); if (!String.IsNullOrEmpty(message)) Toast.MakeText(this, message, ToastLength.Long).Show();}));
			var progressTask = new ProgressTask(App.Kp2a, this, moveElement);
			progressTask.Run();

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

			ActionBar.Title = titleText;
			if (clickable)
				ActionBar.SetDisplayHomeAsUpEnabled(true);
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

		class SuggestionListener: Java.Lang.Object, SearchView.IOnSuggestionListener
		{
			private readonly CursorAdapter _suggestionsAdapter;
			private readonly GroupBaseActivity _activity;
			private readonly IMenuItem _searchItem;


			public SuggestionListener(CursorAdapter suggestionsAdapter, GroupBaseActivity activity, IMenuItem searchItem)
			{
				_suggestionsAdapter = suggestionsAdapter;
				_activity = activity;
				_searchItem = searchItem;
			}

			public bool OnSuggestionClick(int position)
			{
				var cursor = _suggestionsAdapter.Cursor;
				cursor.MoveToPosition(position);
				string entryIdAsHexString = cursor.GetString(cursor.GetColumnIndexOrThrow(SearchManager.SuggestColumnIntentDataId));
				EntryActivity.Launch(_activity, App.Kp2a.GetDb().Entries[new PwUuid(MemUtil.HexStringToByteArray(entryIdAsHexString))],-1,_activity.AppTask);
				((SearchView) _searchItem.ActionView).Iconified = true;
				return true;
			}

			public bool OnSuggestionSelect(int position)
			{
				return false;
			}
		}

		class OnQueryTextListener: Java.Lang.Object, SearchView.IOnQueryTextListener
		{
			private readonly GroupBaseActivity _activity;

			public OnQueryTextListener(GroupBaseActivity activity)
			{
				_activity = activity;
			}

			public bool OnQueryTextChange(string newText)
			{
				return false;
			}

			public bool OnQueryTextSubmit(string query)
			{
				if (String.IsNullOrEmpty(query))
					return false; //let the default happen

				Intent searchIntent = new Intent(_activity, typeof(search.SearchResults));
				searchIntent.SetAction(Intent.ActionSearch); //currently not necessary to set because SearchResults doesn't care, but let's be as close to the default as possible
				searchIntent.PutExtra(SearchManager.Query, query);
				//forward appTask:
				_activity.AppTask.ToIntent(searchIntent);

				_activity.StartActivityForResult(searchIntent, 0);
				
				return true;
			}
		}
		
		public override bool OnCreateOptionsMenu(IMenu menu) {
			base.OnCreateOptionsMenu(menu);
			
			MenuInflater inflater = MenuInflater;
			inflater.Inflate(Resource.Menu.group, menu);
			if (Util.HasActionBar(this))
			{
				var searchManager = (SearchManager) GetSystemService(SearchService);
				IMenuItem searchItem = menu.FindItem(Resource.Id.menu_search);
				var searchView = (SearchView) searchItem.ActionView;
				
				searchView.SetSearchableInfo(searchManager.GetSearchableInfo(ComponentName));
				searchView.SetOnSuggestionListener(new SuggestionListener(searchView.SuggestionsAdapter, this, searchItem));
				searchView.SetOnQueryTextListener(new OnQueryTextListener(this));
			}
			var item = menu.FindItem(Resource.Id.menu_sync);
			if (item != null)
			{
				if (App.Kp2a.GetDb().Ioc.IsLocalFile())
					item.SetVisible(false);
				else
					item.SetVisible(true);
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

			Util.PrepareDonateOptionMenu(menu, this);
			SetSortMenuText(menu);
			
			return true;
		}
		
		public override bool OnOptionsItemSelected(IMenuItem item) {
			switch ( item.ItemId ) {
			case Resource.Id.menu_donate:
				return Util.GotoDonateUrl(this);
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
				
			case Resource.Id.menu_sync:
				Synchronize();
				return true;
				
			case Resource.Id.menu_sort:
				ToggleSort();
				return true;
			case Android.Resource.Id.Home:
				//Currently the action bar only displays the home button when we come from a previous activity.
				//So we can simply Finish. See this page for information on how to do this in more general (future?) cases:
				//http://developer.android.com/training/implementing-navigation/ancestral.html
				AppTask.SetActivityResult(this, KeePass.ExitNormal);
				Finish();
				OverridePendingTransition(Resource.Animation.anim_enter_back, Resource.Animation.anim_leave_back);

				return true;
			}
			
			return base.OnOptionsItemSelected(item);
		}

		public class SyncOtpAuxFile: RunnableOnFinish
		{
			private readonly IOConnectionInfo _ioc;

			public SyncOtpAuxFile(IOConnectionInfo ioc) : base(null)
			{
				_ioc = ioc;
			}

			public override void Run()
			{
				StatusLogger.UpdateMessage(UiStringKey.SynchronizingOtpAuxFile);
				try
				{
					//simply open the file. The file storage does a complete sync.
					using (App.Kp2a.GetOtpAuxFileStorage(_ioc).OpenFileForRead(_ioc))
					{
					}

					Finish(true);
				}
				catch (Exception e)
				{
				
					Finish(false, e.Message);
				}
				
				
			}

		}

		private void Synchronize()
		{
			var filestorage = App.Kp2a.GetFileStorage(App.Kp2a.GetDb().Ioc);
			RunnableOnFinish task;
			OnFinish onFinish = new ActionOnFinish((success, message) =>
			{
				if (!String.IsNullOrEmpty(message))
					Toast.MakeText(this, message, ToastLength.Long).Show();

				// Tell the adapter to refresh it's list
				BaseAdapter adapter = (BaseAdapter)ListAdapter;
				adapter.NotifyDataSetChanged();

				if (App.Kp2a.GetDb().OtpAuxFileIoc != null)
				{
					var task2 = new SyncOtpAuxFile(App.Kp2a.GetDb().OtpAuxFileIoc);
					new ProgressTask(App.Kp2a, this, task2).Run();
				}
			});
			
			if (filestorage is CachingFileStorage)
			{
				
				task = new SynchronizeCachedDatabase(this, App.Kp2a, onFinish);
			}
			else
			{

				task = new CheckDatabaseForChanges(this, App.Kp2a, onFinish);
			}

			
									

			var progressTask = new ProgressTask(App.Kp2a, this, task);
			progressTask.Run();

		}

		public override void OnBackPressed()
		{
			AppTask.SetActivityResult(this, KeePass.ExitNormal);
			base.OnBackPressed();
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

					App.Kp2a.LockDatabase(false);
				}
			}
			
		}

		public bool IsBeingMoved(PwUuid uuid)
		{
			MoveElementTask moveElementTask = AppTask as MoveElementTask;
			if (moveElementTask != null)
			{
				if (moveElementTask.Uuid.Equals(uuid))
					return true;
			}
			return false;
		}

		public void StartTask(AppTask task)
		{
			AppTask = task;
			task.StartInGroupActivity(this);
		}


		public void StartMovingElement()
		{
			ShowInsertElementButtons();
			GroupView.ListView.InvalidateViews();
			BaseAdapter adapter = (BaseAdapter)ListAdapter;
			adapter.NotifyDataSetChanged();
		}

		public void ShowInsertElementButtons()
		{
			GroupView.ShowInsertButtons();
		}

		public void StopMovingElement()
		{
			try
			{
				MoveElementTask moveElementTask = (MoveElementTask)AppTask;
				IStructureItem elementToMove = App.Kp2a.GetDb().KpDatabase.RootGroup.FindObject(moveElementTask.Uuid, true, null);
				if (elementToMove.ParentGroup != Group)
					App.Kp2a.GetDb().Dirty.Add(elementToMove.ParentGroup);
			}
			catch (Exception e)
			{
				//don't crash if adding to dirty fails but log the exception:
				Kp2aLog.Log(e.ToString());
			}
			
			AppTask = new NullTask();
			AppTask.SetupGroupBaseActivityButtons(this);
			GroupView.ListView.InvalidateViews();
			BaseAdapter adapter = (BaseAdapter)ListAdapter;
			adapter.NotifyDataSetChanged();
		}


		public void EditGroup(PwGroup pwGroup)
		{
			GroupEditActivity.Launch(this, pwGroup.ParentGroup, pwGroup);
		}
	}
}


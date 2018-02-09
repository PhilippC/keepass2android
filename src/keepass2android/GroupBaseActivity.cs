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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
using Android.Provider;
using Android.Support.V4.View;
using Android.Views.Autofill;
using CursorAdapter = Android.Support.V4.Widget.CursorAdapter;
using Object = Java.Lang.Object;

namespace keepass2android
{

	public abstract class GroupBaseActivity : LockCloseActivity
	{
		public const String KeyEntry = "entry";
		public const String KeyMode = "mode";

        static readonly Dictionary<int /*resource id*/, int /*prio*/> bottomBarElementsPriority = new Dictionary<int, int>()
        {
            { Resource.Id.cancel_insert_element, 20 },
            { Resource.Id.insert_element, 20 },
            { Resource.Id.autofill_infotext, 10 },
            { Resource.Id.select_other_entry, 20},
            { Resource.Id.add_url_entry, 20},
        };

	    private readonly HashSet<int /*resource id*/> showableBottomBarElements = new HashSet<int>();

		private ActivityDesign _design;

		public virtual void LaunchActivityForEntry(PwEntry pwEntry, int pos)
		{
			EntryActivity.Launch(this, pwEntry, pos, AppTask);
		}

		protected GroupBaseActivity()
		{
			_design = new ActivityDesign(this);
		}

		protected GroupBaseActivity(IntPtr javaReference, JniHandleOwnership transfer)
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
			SetNormalButtonVisibility(AddGroupEnabled, AddEntryEnabled);
		}


		protected virtual bool AddGroupEnabled
		{
			get { return App.Kp2a.GetDb().CanWrite; }
		}
		protected virtual bool AddEntryEnabled
		{
			get { return App.Kp2a.GetDb().CanWrite; }
		}

		public void SetNormalButtonVisibility(bool showAddGroup, bool showAddEntry)
		{
			if (FindViewById(Resource.Id.fabCancelAddNew) != null)
			{
				FindViewById(Resource.Id.fabCancelAddNew).Visibility = ViewStates.Gone;
				FindViewById(Resource.Id.fabAddNewGroup).Visibility = ViewStates.Gone;
				FindViewById(Resource.Id.fabAddNewEntry).Visibility = ViewStates.Gone;

				FindViewById(Resource.Id.fabAddNew).Visibility = (showAddGroup || showAddEntry) ? ViewStates.Visible : ViewStates.Gone;
			}

		    UpdateBottomBarElementVisibility(Resource.Id.insert_element, false);
		    UpdateBottomBarElementVisibility(Resource.Id.cancel_insert_element, false);

        }

        void UpdateBottomBarVisibility()
	    {
	        var bottomBar = FindViewById<RelativeLayout>(Resource.Id.bottom_bar);
	        //check for null because the "empty" layouts may not have all views
	        int highestPrio = -1;
	        HashSet<int> highestPrioElements = new HashSet<int>();
            if (bottomBar != null)
	        {
	            for (int i = 0; i < bottomBar.ChildCount; i++)
	            {
	                int id = bottomBar.GetChildAt(i).Id;
	                if (!showableBottomBarElements.Contains(id))
	                    continue;
	                int myPrio = bottomBarElementsPriority[id];

                    if (!highestPrioElements.Any() || highestPrio < myPrio)
	                {
	                    highestPrioElements.Clear();
	                    highestPrio = myPrio;
	                }
	                if (highestPrio == myPrio)
	                {
	                    highestPrioElements.Add(id);
                    }
                }

	            bottomBar.Visibility = highestPrioElements.Any() ? ViewStates.Visible : ViewStates.Gone;

	            for (int i = 0; i < bottomBar.ChildCount; i++)
	            {
	                int id = bottomBar.GetChildAt(i).Id;
	                bottomBar.GetChildAt(i).Visibility =
	                    highestPrioElements.Contains(id) ? ViewStates.Visible : ViewStates.Gone;
	            }

	            if (FindViewById(Resource.Id.divider2) != null)
	                FindViewById(Resource.Id.divider2).Visibility = highestPrioElements.Any() ? ViewStates.Visible : ViewStates.Gone;
	        }
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
					task = AddGroup.GetInstance(this, App.Kp2a, groupName, groupIconId, groupCustomIconId, Group, new RefreshTask(handler, this), false);
				}
				else
				{
					PwUuid groupUuid = new PwUuid(MemUtil.HexStringToByteArray(strGroupUuid));
					task = new EditGroup(this, App.Kp2a, groupName, (PwIcon)groupIconId, groupCustomIconId, App.Kp2a.GetDb().Groups[groupUuid],
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

		private String strCachedGroupUuid = null;
		private IMenuItem _offlineItem;
		private IMenuItem _onlineItem;
		private IMenuItem _syncItem;


		public String UuidGroup
		{
			get
			{
				if (strCachedGroupUuid == null)
				{
					strCachedGroupUuid = MemUtil.ByteArrayToHexString(Group.Uuid.UuidBytes);
				}
				return strCachedGroupUuid;
			}
		}


		protected override void OnResume()
		{
			base.OnResume();
			_design.ReapplyTheme();
			AppTask.StartInGroupActivity(this);
			AppTask.SetupGroupBaseActivityButtons(this);

		    UpdateAutofillInfo();

            RefreshIfDirty();
		}

		public override bool OnSearchRequested()
		{
			Intent i = new Intent(this, typeof(SearchActivity));
			AppTask.ToIntent(i);
			StartActivityForResult(i, 0);
			return true;
		}

		public void RefreshIfDirty()
		{
			Database db = App.Kp2a.GetDb();
			if (db.Dirty.Contains(Group))
			{
				db.Dirty.Remove(Group);
				ListAdapter.NotifyDataSetChanged();

			}
		}

		public BaseAdapter ListAdapter
		{
			get { return (BaseAdapter)FragmentManager.FindFragmentById<GroupListFragment>(Resource.Id.list_fragment).ListAdapter; }
		}

		public virtual bool IsSearchResult
		{
			get { return false; }
		}

		protected override void OnCreate(Bundle savedInstanceState)
		{
			_design.ApplyTheme();
			base.OnCreate(savedInstanceState);

			Android.Util.Log.Debug("KP2A", "Creating GBA");

			AppTask = AppTask.GetTaskInOnCreate(savedInstanceState, Intent);

			// Likely the app has been killed exit the activity 
			if (!App.Kp2a.GetDb().Loaded)
			{
				Finish();
				return;
			}

			_prefs = PreferenceManager.GetDefaultSharedPreferences(this);

		    
            SetContentView(ContentResourceId);

		    if (FindViewById(Resource.Id.enable_autofill) != null)
		    {
		        FindViewById(Resource.Id.enable_autofill).Click += (sender, args) =>
		        {
		            var intent = new Intent(Settings.ActionRequestSetAutofillService);
		            intent.SetData(Android.Net.Uri.Parse("package:" + PackageName));
		            try
		            {
		                StartActivity(intent);
		            }
		            catch (ActivityNotFoundException e)
		            {
                        //this exception was reported by many Huawei users
		                Kp2aLog.LogUnexpectedError(e);
		                new AlertDialog.Builder(this)
		                    .SetTitle(Resource.String.autofill_enable)
		                    .SetMessage(Resource.String.autofill_enable_failed)
		                    .SetPositiveButton(Resource.String.ok, (o, eventArgs) => { })
		                    .Show();
		                const string autofillservicewasenabled = "AutofillServiceWasEnabled";
		                _prefs.Edit().PutBoolean(autofillservicewasenabled, true).Commit();
		                UpdateBottomBarElementVisibility(Resource.Id.autofill_infotext, false);
                    }
		        };
		    }

            if (FindViewById(Resource.Id.fabCancelAddNew) != null)
			{
				FindViewById(Resource.Id.fabAddNew).Click += (sender, args) =>
				{
					FindViewById(Resource.Id.fabCancelAddNew).Visibility = ViewStates.Visible;
					FindViewById(Resource.Id.fabAddNewGroup).Visibility = AddGroupEnabled ? ViewStates.Visible : ViewStates.Gone;
					FindViewById(Resource.Id.fabAddNewEntry).Visibility = AddEntryEnabled ? ViewStates.Visible : ViewStates.Gone;
					FindViewById(Resource.Id.fabAddNew).Visibility = ViewStates.Gone;
				};

				FindViewById(Resource.Id.fabCancelAddNew).Click += (sender, args) =>
				{
					FindViewById(Resource.Id.fabCancelAddNew).Visibility = ViewStates.Gone;
					FindViewById(Resource.Id.fabAddNewGroup).Visibility = ViewStates.Gone;
					FindViewById(Resource.Id.fabAddNewEntry).Visibility = ViewStates.Gone;
					FindViewById(Resource.Id.fabAddNew).Visibility = ViewStates.Visible;
				};


			}


			if (FindViewById(Resource.Id.cancel_insert_element) != null)
			{
				FindViewById(Resource.Id.cancel_insert_element).Click += (sender, args) => StopMovingElements();
				FindViewById(Resource.Id.insert_element).Click += (sender, args) => InsertElements();
				Util.MoveBottomBarButtons(Resource.Id.cancel_insert_element, Resource.Id.insert_element, Resource.Id.bottom_bar, this);
			}

		    if (FindViewById(Resource.Id.show_autofill_info) != null)
		    {
		        FindViewById(Resource.Id.show_autofill_info).Click += (sender, args) => Util.GotoUrl(this, "https://philippc.github.io/keepass2android/OreoAutoFill.html");
                Util.MoveBottomBarButtons(Resource.Id.show_autofill_info, Resource.Id.enable_autofill, Resource.Id.autofill_buttons, this);
		    }




		    SetResult(KeePass.ExitNormal);



		}

	    private void UpdateAutofillInfo()
	    {
	        bool canShowAutofillInfo = false;
	        
	        if (!((Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.O) ||
	              !((AutofillManager) GetSystemService(Java.Lang.Class.FromType(typeof(AutofillManager))))
	                  .IsAutofillSupported))
	        {
	            const string autofillservicewasenabled = "AutofillServiceWasEnabled";
	            if (!((AutofillManager) GetSystemService(Java.Lang.Class.FromType(typeof(AutofillManager))))
	                .HasEnabledAutofillServices)
	            {
                    if (!_prefs.GetBoolean(autofillservicewasenabled, false))
	                    canShowAutofillInfo = true;
	            }
	            else
	            {
	                _prefs.Edit().PutBoolean(autofillservicewasenabled, true).Commit();

	            }
	        }
	        UpdateBottomBarElementVisibility(Resource.Id.autofill_infotext, canShowAutofillInfo);
        }

	    protected void UpdateBottomBarElementVisibility(int resourceId, bool canShow)
	    {
            if (canShow)
                showableBottomBarElements.Add(resourceId);
            else
                showableBottomBarElements.Remove(resourceId);
            UpdateBottomBarVisibility();
	    }

	    protected virtual int ContentResourceId
		{
			get { return Resource.Layout.group; }
		}

		private void InsertElements()
		{
			MoveElementsTask moveElementsTask = (MoveElementsTask)AppTask;
			IEnumerable<IStructureItem> elementsToMove =
				moveElementsTask.Uuids.Select(uuid => App.Kp2a.GetDb().KpDatabase.RootGroup.FindObject(uuid, true, null));



			var moveElement = new MoveElements(elementsToMove.ToList(), Group, this, App.Kp2a, new ActionOnFinish((success, message) => { StopMovingElements(); if (!String.IsNullOrEmpty(message)) Toast.MakeText(this, message, ToastLength.Long).Show(); }));
			var progressTask = new ProgressTask(App.Kp2a, this, moveElement);
			progressTask.Run();

		}



		protected void SetGroupTitle()
		{
			String name = Group.Name;
			String titleText;
			bool clickable = (Group != null) && (Group.IsVirtual == false) && (Group.ParentGroup != null);
			if (!String.IsNullOrEmpty(name))
			{
				titleText = name;
			}
			else
			{
				titleText = GetText(Resource.String.root);
			}

			SupportActionBar.Title = titleText;
			if (clickable)
			{
				SupportActionBar.SetHomeButtonEnabled(true);
				SupportActionBar.SetDisplayHomeAsUpEnabled(true);
				SupportActionBar.SetDisplayShowHomeEnabled(true);
			}

		}


		protected void SetGroupIcon()
		{
			if (Group != null)
			{
				Drawable drawable = App.Kp2a.GetDb().DrawableFactory.GetIconDrawable(this, App.Kp2a.GetDb().KpDatabase, Group.IconId, Group.CustomIconUuid, true);
				SupportActionBar.SetDisplayShowHomeEnabled(true);
				//SupportActionBar.SetIcon(drawable);
			}
		}

		class SuggestionListener : Java.Lang.Object, SearchView.IOnSuggestionListener, Android.Support.V7.Widget.SearchView.IOnSuggestionListener
		{
			private readonly CursorAdapter _suggestionsAdapter;
			private readonly GroupBaseActivity _activity;
			private readonly IMenuItem _searchItem;


			public SuggestionListener(Android.Support.V4.Widget.CursorAdapter suggestionsAdapter, GroupBaseActivity activity, IMenuItem searchItem)
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
				EntryActivity.Launch(_activity, App.Kp2a.GetDb().Entries[new PwUuid(MemUtil.HexStringToByteArray(entryIdAsHexString))], -1, _activity.AppTask);
				return true;
			}

			public bool OnSuggestionSelect(int position)
			{
				return false;
			}
		}

		class OnQueryTextListener : Java.Lang.Object, Android.Support.V7.Widget.SearchView.IOnQueryTextListener
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

		public override bool OnCreateOptionsMenu(IMenu menu)
		{

			MenuInflater inflater = MenuInflater;
			inflater.Inflate(Resource.Menu.group, menu);
			var searchManager = (SearchManager)GetSystemService(Context.SearchService);
			IMenuItem searchItem = menu.FindItem(Resource.Id.menu_search);
			var view = MenuItemCompat.GetActionView(searchItem);
			var searchView = view.JavaCast<Android.Support.V7.Widget.SearchView>();

			searchView.SetSearchableInfo(searchManager.GetSearchableInfo(ComponentName));
			searchView.SetOnSuggestionListener(new SuggestionListener(searchView.SuggestionsAdapter, this, searchItem));
			searchView.SetOnQueryTextListener(new OnQueryTextListener(this));

			ActionBar.LayoutParams lparams = new ActionBar.LayoutParams(ActionBar.LayoutParams.MatchParent,
				ActionBar.LayoutParams.MatchParent);
			searchView.LayoutParameters = lparams;
			
			_syncItem = menu.FindItem(Resource.Id.menu_sync);
			

			_offlineItem = menu.FindItem(Resource.Id.menu_work_offline);
			_onlineItem = menu.FindItem(Resource.Id.menu_work_online);
			
			UpdateOfflineModeMenu();


			return base.OnCreateOptionsMenu(menu);

		}

		private void UpdateOfflineModeMenu()
		{
			try
			{
				if (_syncItem != null)
				{
					if (App.Kp2a.GetDb().Ioc.IsLocalFile())
						_syncItem.SetVisible(false);
					else
						_syncItem.SetVisible(!App.Kp2a.OfflineMode);
				}

				if (App.Kp2a.GetFileStorage(App.Kp2a.GetDb().Ioc) is IOfflineSwitchable)
				{
					if (_offlineItem != null)
						_offlineItem.SetVisible(App.Kp2a.OfflineMode == false);
					if (_onlineItem != null)
						_onlineItem.SetVisible(App.Kp2a.OfflineMode);
				}
				else
				{
					if (_offlineItem != null)
						_offlineItem.SetVisible(false);
					if (_onlineItem != null)
						_onlineItem.SetVisible(false);

				}
			}
			catch (Exception e)
			{
				Kp2aLog.LogUnexpectedError(new Exception("Cannot UpdateOfflineModeMenu " + (App.Kp2a == null) + " " + ((App.Kp2a == null) || (App.Kp2a.GetDb() == null)) + " " + (((App.Kp2a == null) || (App.Kp2a.GetDb() == null) || (App.Kp2a.GetDb().Ioc == null)) + " " + (_syncItem != null) + " " + (_offlineItem != null) + " " + (_onlineItem != null))));
			}
			
		}


		public override bool OnPrepareOptionsMenu(IMenu menu)
		{
			if (!base.OnPrepareOptionsMenu(menu))
			{
				return false;
			}

			Util.PrepareDonateOptionMenu(menu, this);


			return true;
		}

		public override bool OnOptionsItemSelected(IMenuItem item)
		{
			switch (item.ItemId)
			{
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

				case Resource.Id.menu_work_offline:
					App.Kp2a.OfflineMode = App.Kp2a.OfflineModePreference = true;
					UpdateOfflineModeMenu();
					return true;

				case Resource.Id.menu_work_online:
					App.Kp2a.OfflineMode = App.Kp2a.OfflineModePreference = false;
					UpdateOfflineModeMenu();
					Synchronize();
					return true;


				case Resource.Id.menu_sort:
					ChangeSort();
					return true;
				case Android.Resource.Id.Home:
					//Currently the action bar only displays the home button when we come from a previous activity.
					//So we can simply Finish. See this page for information on how to do this in more general (future?) cases:
					//http://developer.android.com/training/implementing-navigation/ancestral.html
					AppTask.SetActivityResult(this, KeePass.ExitNormal);
					Finish();
					//OverridePendingTransition(Resource.Animation.anim_enter_back, Resource.Animation.anim_leave_back);

					return true;
			}

			return base.OnOptionsItemSelected(item);
		}

		public class SyncOtpAuxFile : RunnableOnFinish
		{
			private readonly IOConnectionInfo _ioc;

			public SyncOtpAuxFile(IOConnectionInfo ioc)
				: base(null)
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

		private void ChangeSort()
		{
			var sortOrderManager = new GroupViewSortOrderManager(this);
			IEnumerable<string> sortOptions = sortOrderManager.SortOrders.Select(
				o => GetString(o.ResourceId)
				);

			int selectedBefore = sortOrderManager.GetCurrentSortOrderIndex();

			new AlertDialog.Builder(this)
				.SetSingleChoiceItems(sortOptions.ToArray(), selectedBefore, (sender, args) =>
					{
						int selectedAfter = args.Which;

						sortOrderManager.SetNewSortOrder(selectedAfter);
						// Refresh menu titles
						ActivityCompat.InvalidateOptionsMenu(this);

						// Mark all groups as dirty now to refresh them on load
						Database db = App.Kp2a.GetDb();
						db.MarkAllGroupsAsDirty();
						// We'll manually refresh this group so we can remove it
						db.Dirty.Remove(Group);

						// Tell the adapter to refresh it's list

						BaseAdapter adapter = (BaseAdapter)ListAdapter;
						adapter.NotifyDataSetChanged();


					})
					.SetPositiveButton(Android.Resource.String.Ok, (sender, args) => ((Dialog)sender).Dismiss())
					.Show();




		}

		public class RefreshTask : OnFinish
		{
			readonly GroupBaseActivity _act;
			public RefreshTask(Handler handler, GroupBaseActivity act)
				: base(handler)
			{
				_act = act;
			}

			public override void Run()
			{
				if (Success)
				{
					_act.RefreshIfDirty();
				}
				else
				{
					DisplayMessage(_act);
				}
			}
		}
		public class AfterDeleteGroup : OnFinish
		{
			readonly GroupBaseActivity _act;

			public AfterDeleteGroup(Handler handler, GroupBaseActivity act)
				: base(handler)
			{
				_act = act;
			}


			public override void Run()
			{
				if (Success)
				{
					_act.RefreshIfDirty();
				}
				else
				{
					Handler.Post(() =>
					{
						Toast.MakeText(_act, "Unrecoverable error: " + Message, ToastLength.Long).Show();
					});

					App.Kp2a.LockDatabase(false);
				}
			}

		}

		public bool IsBeingMoved(PwUuid uuid)
		{
			MoveElementsTask moveElementsTask = AppTask as MoveElementsTask;
			if (moveElementsTask != null)
			{
				if (moveElementsTask.Uuids.Any(uuidMoved => uuidMoved.Equals(uuid)))
					return true;
			}
			return false;
		}

		public void StartTask(AppTask task)
		{
			AppTask = task;
			task.StartInGroupActivity(this);
		}


		public void StartMovingElements()
		{

			ShowInsertElementsButtons();
			BaseAdapter adapter = (BaseAdapter)ListAdapter;
			adapter.NotifyDataSetChanged();
		}

		public void ShowInsertElementsButtons()
		{
			FindViewById(Resource.Id.fabCancelAddNew).Visibility = ViewStates.Gone;
			FindViewById(Resource.Id.fabAddNewGroup).Visibility = ViewStates.Gone;
			FindViewById(Resource.Id.fabAddNewEntry).Visibility = ViewStates.Gone;
			FindViewById(Resource.Id.fabAddNew).Visibility = ViewStates.Gone;

		    UpdateBottomBarElementVisibility(Resource.Id.insert_element, true);
		    UpdateBottomBarElementVisibility(Resource.Id.cancel_insert_element, true);

		}

		public void StopMovingElements()
		{
			try
			{
				MoveElementsTask moveElementsTask = (MoveElementsTask)AppTask;
				foreach (var uuid in moveElementsTask.Uuids)
				{
					IStructureItem elementToMove = App.Kp2a.GetDb().KpDatabase.RootGroup.FindObject(uuid, true, null);
					if (elementToMove.ParentGroup != Group)
						App.Kp2a.GetDb().Dirty.Add(elementToMove.ParentGroup);
				}
			}
			catch (Exception e)
			{
				//don't crash if adding to dirty fails but log the exception:
				Kp2aLog.LogUnexpectedError(e);
			}

			AppTask = new NullTask();
			AppTask.SetupGroupBaseActivityButtons(this);
			BaseAdapter adapter = (BaseAdapter)ListAdapter;
			adapter.NotifyDataSetChanged();
		}


		public void EditGroup(PwGroup pwGroup)
		{
			GroupEditActivity.Launch(this, pwGroup.ParentGroup, pwGroup);
		}
	}

	public class GroupListFragment : ListFragment, AbsListView.IMultiChoiceModeListener
	{
		private ActionMode _mode;
		private int _statusBarColor;

		public override void OnActivityCreated(Bundle savedInstanceState)
		{
			base.OnActivityCreated(savedInstanceState);
			if (App.Kp2a.GetDb().CanWrite)
			{
				ListView.ChoiceMode = ChoiceMode.MultipleModal;
				ListView.SetMultiChoiceModeListener(this);
				ListView.ItemLongClick += delegate(object sender, AdapterView.ItemLongClickEventArgs args)
				{
					ListView.SetItemChecked(args.Position, true);
				};

			}

			ListView.ItemClick += (sender, args) => ((GroupListItemView)args.View).OnClick();

			StyleListView();

		}

		protected void StyleListView()
		{
			ListView lv = ListView;
			lv.ScrollBarStyle = ScrollbarStyles.InsideInset;
			lv.TextFilterEnabled = true;

			lv.Divider = null;
		}

		public bool OnActionItemClicked(ActionMode mode, IMenuItem item)
		{
			var listView = FragmentManager.FindFragmentById<GroupListFragment>(Resource.Id.list_fragment).ListView;
			var checkedItemPositions = listView.CheckedItemPositions;

			List<IStructureItem> checkedItems = new List<IStructureItem>();
			for (int i = 0; i < checkedItemPositions.Size(); i++)
			{
				if (checkedItemPositions.ValueAt(i))
				{
					checkedItems.Add(((PwGroupListAdapter)ListAdapter).GetItemAtPosition(checkedItemPositions.KeyAt(i)));
				}
			}

			//shouldn't happen, just in case...
			if (!checkedItems.Any())
			{
				return false;
			}
			Handler handler = new Handler();
			switch (item.ItemId)
			{

				case Resource.Id.menu_delete:
					
					DeleteMultipleItems task = new DeleteMultipleItems((GroupBaseActivity)Activity, App.Kp2a.GetDb(), checkedItems,
						new GroupBaseActivity.RefreshTask(handler, ((GroupBaseActivity)Activity)), App.Kp2a);
					task.Start();
					break;
				case Resource.Id.menu_move:
					var navMove = new NavigateToFolderAndLaunchMoveElementTask(checkedItems.First().ParentGroup, checkedItems.Select(i => i.Uuid).ToList(), ((GroupBaseActivity)Activity).IsSearchResult);
					((GroupBaseActivity)Activity).StartTask(navMove);
					break;
				case Resource.Id.menu_copy:

					var copyTask = new CopyEntry((GroupBaseActivity)Activity, App.Kp2a, (PwEntry) checkedItems.First(),
						new GroupBaseActivity.RefreshTask(handler, ((GroupBaseActivity)Activity)));
					
					ProgressTask pt = new ProgressTask(App.Kp2a, Activity, copyTask);
					pt.Run();
					break;
				
				case Resource.Id.menu_navigate:
					NavigateToFolder navNavigate = new NavigateToFolder(checkedItems.First().ParentGroup, true);
					((GroupBaseActivity)Activity).StartTask(navNavigate);
					break;
				case Resource.Id.menu_edit:
					GroupEditActivity.Launch(Activity, checkedItems.First().ParentGroup, (PwGroup)checkedItems.First());
					break;
				default:
					return false;


			}
			listView.ClearChoices();
			((BaseAdapter)ListAdapter).NotifyDataSetChanged();
			if (_mode != null)
				mode.Finish();

			return true;
		}

		public bool OnCreateActionMode(ActionMode mode, IMenu menu)
		{
			MenuInflater inflater = Activity.MenuInflater;
			inflater.Inflate(Resource.Menu.group_entriesselected, menu);
			//mode.Title = "Select Items";
			Android.Util.Log.Debug("KP2A", "Create action mode" + mode);
			((PwGroupListAdapter)ListView.Adapter).InActionMode = true;
			((PwGroupListAdapter)ListView.Adapter).NotifyDataSetChanged();
			_mode = mode;
			if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
			{
				_statusBarColor = Activity.Window.StatusBarColor;
				Activity.Window.SetStatusBarColor(Activity.Resources.GetColor(Resource.Color.appAccentColorDark));
			}
			return true;
		}

		public void OnDestroyActionMode(ActionMode mode)
		{
			Android.Util.Log.Debug("KP2A", "Destroy action mode" + mode);
			((PwGroupListAdapter)ListView.Adapter).InActionMode = false;
			((PwGroupListAdapter)ListView.Adapter).NotifyDataSetChanged();
			_mode = null;
			if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
			{
				Activity.Window.SetStatusBarColor(new Android.Graphics.Color(_statusBarColor));
			}
		}

		public bool OnPrepareActionMode(ActionMode mode, IMenu menu)
		{
			Android.Util.Log.Debug("KP2A", "Prepare action mode" + mode);
			((PwGroupListAdapter)ListView.Adapter).InActionMode = mode != null;
			((PwGroupListAdapter)ListView.Adapter).NotifyDataSetChanged();
			return true;
		}

		public void OnItemCheckedStateChanged(ActionMode mode, int position, long id, bool @checked)
		{
			var menuItem = mode.Menu.FindItem(Resource.Id.menu_edit);
			if (menuItem != null)
			{
				menuItem.SetVisible(IsOnlyOneGroupChecked());
			}

			menuItem = mode.Menu.FindItem(Resource.Id.menu_navigate);
			if (menuItem != null)
			{
				menuItem.SetVisible(((GroupBaseActivity)Activity).IsSearchResult && IsOnlyOneItemChecked());
			}

			menuItem = mode.Menu.FindItem(Resource.Id.menu_copy);
			if (menuItem != null)
			{
				menuItem.SetVisible(IsOnlyOneEntryChecked());
			}
		}

		private bool IsOnlyOneGroupChecked()
		{
			var checkedItems = ListView.CheckedItemPositions;
			bool hadCheckedGroup = false;
			if (checkedItems != null)
			{
				for (int i = 0; i < checkedItems.Size(); i++)
				{
					if (checkedItems.ValueAt(i))
					{
						if (hadCheckedGroup)
						{
							return false;
						}

						if (((PwGroupListAdapter)ListAdapter).IsGroupAtPosition(checkedItems.KeyAt(i)))
						{
							hadCheckedGroup = true;
						}
						else
						{
							return false;
						}
					}
				}
			}
			return hadCheckedGroup;
		}

		private bool IsOnlyOneItemChecked()
		{
			var checkedItems = ListView.CheckedItemPositions;
			bool hadCheckedItem = false;
			if (checkedItems != null)
			{
				for (int i = 0; i < checkedItems.Size(); i++)
				{
					if (checkedItems.ValueAt(i))
					{
						if (hadCheckedItem)
						{
							return false;
						}

						hadCheckedItem = true;
					}
				}
			}
			return hadCheckedItem;
		}

		private bool IsOnlyOneEntryChecked()
		{
			return IsOnlyOneItemChecked() && !IsOnlyOneGroupChecked();
		}
	}
}


using System;
using Android.App;
using Android.Content;
using Android.OS;
using System.Collections.Generic;
using KeePassLib;
using KeePassLib.Security;
using KeePassLib.Utility;

namespace keepass2android
{
	/// <summary>
	/// Interface for data stored in an intent or bundle as extra string
	/// </summary>
	public interface IExtra
	{
		/// <summary>
		/// put data to a bundle by calling one of the PutXX methods
		/// </summary>
		void ToBundle(Bundle b);

		/// <summary>
		/// Put data to an intent by calling PutExtra
		/// </summary>
		void ToIntent(Intent i);
	}

	/// <summary>
	/// represents data stored in an intent or bundle as extra string
	/// </summary>
	public class StringExtra: IExtra
	{
		public string Key { get; set; }
		public string Value{ get; set; }

		#region IExtra implementation

		public void ToBundle(Bundle b)
		{
			b.PutString(Key, Value);
		}

		public void ToIntent(Intent i)
		{
			i.PutExtra(Key, Value);
		}

		#endregion
	}

	/// <summary>
	/// represents data stored in an intent or bundle as extra string array list
	/// </summary>
	public class StringArrayListExtra : IExtra
	{
		public string Key { get; set; }
		public IList<string> Value { get; set; }

		#region IExtra implementation

		public void ToBundle(Bundle b)
		{
			b.PutStringArrayList(Key, Value);
		}

		public void ToIntent(Intent i)
		{
			i.PutStringArrayListExtra(Key, Value);
		}

		#endregion
	}

	/// <summary>
	/// represents data stored in an intent or bundle as extra int
	/// </summary>
	public class IntExtra: IExtra
	{
		public string Key { get; set; }
		public int Value{ get; set; }

		#region IExtra implementation

		public void ToBundle(Bundle b)
		{
			b.PutInt(Key, Value);
		}

		public void ToIntent(Intent i)
		{
			i.PutExtra(Key, Value);
		}

		#endregion
	}


	/// <summary>
	/// represents data stored in an intent or bundle as extra string array
	/// </summary>
	public class StringArrayExtra : IExtra
	{
		public string Key { get; set; }
		public string[] Value { get; set; }

		#region IExtra implementation

		public void ToBundle(Bundle b)
		{
			b.PutStringArray(Key, Value);
		}

		public void ToIntent(Intent i)
		{
			i.PutExtra(Key, Value);
		}

		#endregion
	}

	/// <summary>
	/// base class for "tasks": these are things the user wants to do and which require several activities
	/// </summary>
	/// Therefore AppTasks need to be serializable to bundles and intents to "survive" saving to instance state and changing activities.
	/// An AppTask has a type and may have several parameters ("extras").
	/// Activities call the task at special points so tasks can change the behaviour at these points.
	public abstract class AppTask
	{
		/// <summary>
		/// Loads the parameters of the task from the given bundle
		/// </summary>
		public virtual void Setup(Bundle b)
		{}

		/// <summary>
		/// Returns the parameters of the task for storage in a bundle or intent
		/// </summary>
		/// <value>The extras.</value>
		public virtual IEnumerable<IExtra> Extras { 
			get
			{
				yield break;
			}
		}

		public virtual void AfterUnlockDatabase(PasswordActivity act)
		{
			GroupActivity.Launch(act, this);
		}

		public virtual void AfterAddNewEntry(EntryEditActivity entryEditActivity, PwEntry newEntry)
		{
		}

		
		public virtual void PrepareNewEntry(PwEntry newEntry)
		{
			
		}
		
		public const String AppTaskKey = "KP2A_APPTASK";

		/// <summary>
		/// Should be used in OnCreate to (re)create a task
		/// if savedInstanceState is not null, the task is recreated from there. Otherwise it's taken from the intent.
		/// </summary>
		public static AppTask GetTaskInOnCreate(Bundle savedInstanceState, Intent intent)
		{
			AppTask task;
			if (savedInstanceState != null)
			{
				task = CreateFromBundle(savedInstanceState);
			}
			else
			{
				task = CreateFromIntent(intent);
			}
			Kp2aLog.Log("Loaded task " + task);
			return task;
		}

		public static AppTask CreateFromIntent(Intent i)
		{
			return CreateFromBundle(i.Extras);
		}

		public static AppTask CreateFromBundle(Bundle b)
		{
			return CreateFromBundle(b, new NullTask());
		}

		public static AppTask CreateFromBundle(Bundle b, AppTask failureReturn)
		{
			if (b == null)
				return failureReturn;

			string taskType = b.GetString(AppTaskKey);

			if (string.IsNullOrEmpty(taskType))
				return failureReturn;

			try
			{
			    Type type = Type.GetType("keepass2android." + taskType);
                if (type == null)
                    return failureReturn;
				AppTask task = (AppTask)Activator.CreateInstance(type);
				task.Setup(b);
				return task;
			}
			catch (Exception e)
			{
				Kp2aLog.Log("Cannot convert " + taskType + " in task: " + e);
				return failureReturn;
			}

		}

		/// <summary>
		/// Adds the extras of the task to the intent
		/// </summary>
		public void ToIntent(Intent intent)
		{
			GetTypeExtra(GetType()).ToIntent(intent);

			foreach (IExtra extra in Extras)
			{
				extra.ToIntent(intent);
			}
		}

		/// <summary>
		/// Adds the extras of the task to the bundle
		/// </summary>
		public void ToBundle(Bundle bundle)
		{
			GetTypeExtra(GetType()).ToBundle(bundle);

			foreach (IExtra extra in Extras)
			{
				extra.ToBundle(bundle);
			}

		}

		/// <summary>
		/// Returns an IExtra which must be part of the Extras of a task to describe the type
		/// </summary>
		static IExtra GetTypeExtra(Type type)
		{
			return new StringExtra { Key=AppTaskKey, Value=type.Name};
		}

		public virtual void StartInGroupActivity(GroupBaseActivity groupBaseActivity)
		{
			return;
		}

		public virtual void SetupGroupBaseActivityButtons(GroupBaseActivity groupBaseActivity)
		{
			groupBaseActivity.SetupNormalButtons();
		}

		public void SetActivityResult(Activity activity, Result result)
		{
			Intent data = new Intent();
			ToIntent(data);
			activity.SetResult(result, data);
		}

		/// <summary>
		/// Tries to extract the task from the data given as an Intent object in OnActivityResult. If successful, the task is assigned,
		/// otherwise, false is returned.
		/// </summary>
		public static bool TryGetFromActivityResult(Intent data, ref AppTask task)
		{
			if (data == null)
			{
				Kp2aLog.Log("TryGetFromActivityResult: no data");
				return false;
			}
			AppTask tempTask = CreateFromBundle(data.Extras, null);
			if (tempTask == null)
			{
				Kp2aLog.Log("No AppTask in OnActivityResult");
				return false;
			}
			
			task = tempTask;
			Kp2aLog.Log("AppTask " +task+" in OnActivityResult");
			return true;
		}

		protected void RemoveTaskFromIntent(Activity act)
		{
			if (act.Intent != null)
				act.Intent.RemoveExtra(AppTaskKey);

		}

		public virtual void CompleteOnCreateEntryActivity(EntryActivity activity)
		{
			activity.StartNotificationsService(false);
		}
	}

	/// <summary>
	/// Implementation of AppTask for "no task currently active" (Null pattern)
	/// </summary>
	public class NullTask: AppTask
	{

	}

	/// <summary>
	/// User is about to search an entry for a given URL
	/// </summary>
	/// Derive from SelectEntryTask. This means that as soon as an Entry is opened, we're returning with
	/// ExitAfterTaskComplete. This also allows te specify the flag if we need to display the user notifications.
	public class SearchUrlTask: SelectEntryTask
	{
		public const String UrlToSearchKey = "UrlToSearch";

		public string UrlToSearchFor
		{
			get;
			set;
		}

		public override void Setup(Bundle b)
		{
			base.Setup(b);
			UrlToSearchFor = b.GetString(UrlToSearchKey);
		}
		public override IEnumerable<IExtra> Extras 
		{ 
			get
			{
				foreach (IExtra e in base.Extras)
					yield return e;
				yield return new StringExtra { Key=UrlToSearchKey, Value = UrlToSearchFor };
			}
		}
		public override void AfterUnlockDatabase(PasswordActivity act)
		{
			if (String.IsNullOrEmpty(UrlToSearchFor))
			{
				GroupActivity.Launch(act, new SelectEntryTask() { ShowUserNotifications =  ShowUserNotifications});
			}
			else
			{
				ShareUrlResults.Launch(act, this);
			}
			

			//removed. this causes an issue in the following workflow:
			//When the user wants to find an entry for a URL but has the wrong database open he needs 
			//to switch to another database. But the Task is removed already the first time when going through PasswordActivity 
			// (with the wrong db).
			//Then after switching to the right database, the task is gone.

			//A reason this code existed was the following workflow:
			//Using Chrome browser (with NEW_TASK flag for ActionSend): Share URL -> KP2A.
			//Now the AppTask was in PasswordActivity and didn't get out of it.
			//This is now solved by returning new tasks in ActivityResult.

			//RemoveTaskFromIntent(act);
			//act.AppTask = new NullTask();
		}


	}

	
	/// <summary>
	/// User is about to select an entry for use in another app
	/// </summary>
	public class SelectEntryTask: AppTask
	{
		public SelectEntryTask()
		{
			ShowUserNotifications = true;
			CloseAfterCreate = true;
		}

		public const String ShowUserNotificationsKey = "ShowUserNotifications";

		public bool ShowUserNotifications { get; set; }

		public const String CloseAfterCreateKey = "CloseAfterCreate";

		public bool CloseAfterCreate { get; set; }


		public override void Setup(Bundle b)
		{
			ShowUserNotifications = GetBoolFromBundle(b, ShowUserNotificationsKey, true);
			CloseAfterCreate = GetBoolFromBundle(b, CloseAfterCreateKey, true);
		}

		private static bool GetBoolFromBundle(Bundle b, string key, bool defaultValue)
		{
			bool boolValue;
			if (!Boolean.TryParse(b.GetString(key), out boolValue))	
			{
				boolValue = defaultValue; 
			}
			return boolValue;
		}

		public override IEnumerable<IExtra> Extras
		{
			get
			{
				yield return new StringExtra { Key = ShowUserNotificationsKey, Value = ShowUserNotifications.ToString() };
				yield return new StringExtra { Key = CloseAfterCreateKey, Value = CloseAfterCreate.ToString() };
			}
		}

		public override void CompleteOnCreateEntryActivity(EntryActivity activity)
		{
			if (ShowUserNotifications)
			{
				//show the notifications
				activity.StartNotificationsService(CloseAfterCreate);
			}
			else
			{
				//to avoid getting into inconsistent state (LastOpenedEntry and Notifications): clear notifications:
				CopyToClipboardService.CancelNotifications(activity);
			}
			if (CloseAfterCreate)
			{
				//close
				activity.CloseAfterTaskComplete();	
			}
		}
	}

	/// <summary>
	/// User is about to select an entry. When selected, ask whether the url he was searching for earlier should be stored 
	/// in the selected entry for later use.
	/// </summary>
	public class SelectEntryForUrlTask: SelectEntryTask
	{
		/// <summary>
		/// default constructor for creating from Bundle
		/// </summary>
		public SelectEntryForUrlTask()
		{
			
		}

		public SelectEntryForUrlTask(string url)
		{
			UrlToSearchFor = url;
			ShowUserNotifications = true;
		}

		public const String UrlToSearchKey = "UrlToSearch";
		
		public string UrlToSearchFor
		{
			get;
			set;
		}

		public override void Setup(Bundle b)
		{
			base.Setup(b);
			UrlToSearchFor = b.GetString(UrlToSearchKey);
		}
		public override IEnumerable<IExtra> Extras
		{
			get
			{
				foreach (IExtra e in base.Extras)
					yield return e;
				yield return new StringExtra { Key = UrlToSearchKey, Value = UrlToSearchFor };
			}
		}

		public override void CompleteOnCreateEntryActivity(EntryActivity activity)
		{
			//if the database is readonly (or no URL exists), don't offer to modify the URL
			if ((App.Kp2a.GetDb().CanWrite == false) || (String.IsNullOrEmpty(UrlToSearchFor)))
			{
				base.CompleteOnCreateEntryActivity(activity);
				return;
			}


			AskAddUrlThenCompleteCreate(activity, UrlToSearchFor);

		}

		/// <summary>
		/// brings up a dialog asking the user whether he wants to add the given URL to the entry for automatic finding
		/// </summary>
		public void AskAddUrlThenCompleteCreate(EntryActivity activity, string url)
		{
			AlertDialog.Builder builder = new AlertDialog.Builder(activity);
			builder.SetTitle(activity.GetString(Resource.String.AddUrlToEntryDialog_title));

			builder.SetMessage(activity.GetString(Resource.String.AddUrlToEntryDialog_text, new Java.Lang.Object[] { url }));

			builder.SetPositiveButton(activity.GetString(Resource.String.yes), (dlgSender, dlgEvt) =>
			{
				activity.AddUrlToEntry(url, () => base.CompleteOnCreateEntryActivity(activity));
			});

			builder.SetNegativeButton(activity.GetString(Resource.String.no), (dlgSender, dlgEvt) =>
			{
				base.CompleteOnCreateEntryActivity(activity);
			});

			Dialog dialog = builder.Create();
			dialog.Show();
		}
		
	}


	/// <summary>
	/// User is about to move an entry or group to another group
	/// </summary>
	public class MoveElementTask: AppTask
	{
		public const String UuidKey = "MoveElement_Uuid";

		public PwUuid Uuid
		{
			get;
			set;
		}

		public override void Setup(Bundle b)
		{
			Uuid = new PwUuid(MemUtil.HexStringToByteArray(b.GetString(UuidKey)));
		}
		public override IEnumerable<IExtra> Extras
		{
			get
			{
				yield return new StringExtra { Key = UuidKey, Value = MemUtil.ByteArrayToHexString(Uuid.UuidBytes) };
			}
		}
		public override void StartInGroupActivity(GroupBaseActivity groupBaseActivity)
		{
			base.StartInGroupActivity(groupBaseActivity);
			groupBaseActivity.StartMovingElement();
		}
		public override void SetupGroupBaseActivityButtons(GroupBaseActivity groupBaseActivity)
		{
			groupBaseActivity.ShowInsertElementButtons();
		}
	}

	
	/// <summary>
	/// User is about to create a new entry. The task might already "know" some information about the contents.
	/// </summary>
	public class CreateEntryThenCloseTask: AppTask
	{
		/// <summary>
		/// extra key if only a URL is passed. optional.
		/// </summary>
		public const String UrlKey = "CreateEntry_Url";
		
		/// <summary>
		/// extra key if a json serialized key/value mapping is passed. optional.
		/// </summary>
		/// Uses the PluginSDKs keys because this is mainly used for communicating with plugins.
		/// Of course the data might also contain "non-output-data" (e.g. placeholders), but usually won't.
		public const String AllFieldsKey = Keepass2android.Pluginsdk.Strings.ExtraEntryOutputData;

		/// <summary>
		/// extra key to specify a list of protected field keys in AllFieldsKey. Passed as StringArrayExtra. optional.
		/// </summary>
		public const String ProtectedFieldsListKey = Keepass2android.Pluginsdk.Strings.ExtraProtectedFieldsList;


		/// <summary>
		/// Extra key to specify whether user notifications (e.g. for copy password or keyboard) should be displayed when the entry 
		/// is selected after creating.
		/// </summary>
		public const String ShowUserNotificationsKey = "ShowUserNotifications";


		public string Url { get; set; }

		public string AllFields { get; set; }

		public IList<string> ProtectedFieldsList { get; set; }

		public bool ShowUserNotifications { get; set; }


		public override void Setup(Bundle b)
		{
			bool showUserNotification; 
			if (!Boolean.TryParse(b.GetString(ShowUserNotificationsKey), out showUserNotification))
			{
				showUserNotification = true; //default to true
			}
			ShowUserNotifications = showUserNotification;
			
			Url = b.GetString(UrlKey);
			AllFields = b.GetString(AllFieldsKey);
			ProtectedFieldsList = b.GetStringArrayList(ProtectedFieldsListKey);
		}
		public override IEnumerable<IExtra> Extras 
		{ 
			get
			{
				if (Url != null)
					yield return new StringExtra { Key = UrlKey, Value = Url };
				if (AllFields != null)
					yield return new StringExtra { Key = AllFieldsKey, Value = AllFields };
				if (ProtectedFieldsList != null)
					yield return new StringArrayListExtra { Key = ProtectedFieldsListKey, Value = ProtectedFieldsList };
				
				yield return new StringExtra { Key = ShowUserNotificationsKey, Value = ShowUserNotifications.ToString() };
			}
		}
		
		
		public override void PrepareNewEntry(PwEntry newEntry)
		{
			if (Url != null)
			{
				newEntry.Strings.Set(PwDefs.UrlField, new ProtectedString(false, Url));
			}
			if (AllFields != null)
			{
				
				var allFields = new Org.Json.JSONObject(AllFields);
				for (var iter = allFields.Keys(); iter.HasNext; )
				{
					string key = iter.Next().ToString();
					string value = allFields.Get(key).ToString();
					bool isProtected = ((ProtectedFieldsList != null) && (ProtectedFieldsList.Contains(key))) 
						|| (key == PwDefs.PasswordField);
					newEntry.Strings.Set(key, new ProtectedString(isProtected, value));
				}
				
			}
					
		}

		public override void AfterAddNewEntry(EntryEditActivity entryEditActivity, PwEntry newEntry)
		{
			EntryActivity.Launch(entryEditActivity, newEntry, -1, 
				new SelectEntryTask() { ShowUserNotifications = this.ShowUserNotifications}, 
				ActivityFlags.ForwardResult);
			//no need to call Finish here, that's done in EntryEditActivity ("closeOrShowError")	
		}
		
		public override void CompleteOnCreateEntryActivity(EntryActivity activity)
		{
			//if the user selects an entry before creating the new one, we're not closing the app
			base.CompleteOnCreateEntryActivity(activity);
		}
	}


	/// <summary>
	/// Navigate to a folder and open a Task (appear in SearchResult)
	/// </summary>
	public abstract class NavigateAndLaunchTask: AppTask
	{
		// All group Uuid are stored in guuidKey + indice
		// The last one is the destination group 
		public const String numberOfGroupsKey = "NumberOfGroups";
		public const String gUuidKey = "gUuidKey"; 

		#if INCLUDE_DEBUG_MOVE_GROUPNAME
		public const String gNameKey = "gNameKey";
		private LinkedList<string> groupName;
		#endif

		private LinkedList<string> groupUuid;
		protected AppTask taskToBeLaunchAfterNavigation;

		public NavigateAndLaunchTask() {
			this.taskToBeLaunchAfterNavigation = new NullTask();
		}

		protected NavigateAndLaunchTask(PwGroup groups, AppTask taskToBeLaunchAfterNavigation) {
			this.taskToBeLaunchAfterNavigation = taskToBeLaunchAfterNavigation;
			populateGroupsUuid (groups);
		}

		public void populateGroupsUuid(PwGroup groups) {

			groupUuid = new LinkedList<String>{};

			#if INCLUDE_DEBUG_MOVE_GROUPNAME
			groupName = new LinkedList<String>{};
			#endif

			PwGroup readGroup = groups;
			while (readGroup != null) {

				groupUuid.AddFirst (MemUtil.ByteArrayToHexString (readGroup.Uuid.UuidBytes));

				#if INCLUDE_DEBUG_MOVE_GROUPNAME
				groupName.AddFirst (readGroup.Name);
				#endif

				readGroup = readGroup.ParentGroup;
			}

		}

		/// <summary>
		/// Loads the parameters of the task from the given bundle. Embeded task is not setup from this bundle
		/// </summary>
		/// <param name="b">The bundle component.</param>
		public override void Setup(Bundle b)
		{
			int numberOfGroups = b.GetInt(numberOfGroupsKey);
			groupUuid = new LinkedList<String>{};
#if INCLUDE_DEBUG_MOVE_GROUPNAME
			groupName = new LinkedList<String>{};
#endif

			int i = 0;
			while (i < numberOfGroups) {

				groupUuid.AddLast ( b.GetString (gUuidKey + i) ) ;
#if INCLUDE_DEBUG_MOVE_GROUPNAME
				groupName.AddLast ( b.GetString (gNameKey + i) );
#endif

				i++;
			}
				
		}

		public override IEnumerable<IExtra> Extras
		{
			get
			{
				// Return Navigate group Extras
				IEnumerator<String> eGroupKeys = groupUuid.GetEnumerator ();
#if INCLUDE_DEBUG_MOVE_GROUPNAME
				IEnumerator<String> eGroupName = groupName.GetEnumerator ();
#endif

				int i = 0;
				while (eGroupKeys.MoveNext()) {
					yield return new StringExtra { Key = gUuidKey + i.ToString (), Value = eGroupKeys.Current };
#if INCLUDE_DEBUG_MOVE_GROUPNAME
					eGroupName.MoveNext();
					yield return new StringExtra { Key = gNameKey + i.ToString (), Value = eGroupName.Current };
#endif

					i++;
				}

				yield return new IntExtra{ Key = numberOfGroupsKey, Value = i };

				// Return afterTaskExtras
				IEnumerator<IExtra> afterTaskExtras = taskToBeLaunchAfterNavigation.Extras.GetEnumerator();
				while (afterTaskExtras.MoveNext ()) {
					yield return afterTaskExtras.Current;
				}

			}
		}

		public override void StartInGroupActivity(GroupBaseActivity groupBaseActivity)
		{
			base.StartInGroupActivity(groupBaseActivity);

			if (GroupIsFound(groupBaseActivity) ){ // Group has been found: stop here
				groupBaseActivity.StartTask (taskToBeLaunchAfterNavigation);
				return;

			} else if (groupUuid.Contains(groupBaseActivity.UuidGroup)) { // Need to go up in groups tree

				// Get next Group Uuid
				String nextGroupUuid = groupUuid.Find (groupBaseActivity.UuidGroup).Next.Value;
				PwUuid nextGroupPwUuid = new PwUuid (MemUtil.HexStringToByteArray (nextGroupUuid));

				// Create Group Activity
				PwGroup nextGroup = App.Kp2a.GetDb ().Groups [nextGroupPwUuid];
				GroupActivity.Launch (groupBaseActivity, nextGroup, this);
				return;

			} else { // Need to go down in groups tree
				SetActivityResult(groupBaseActivity, KeePass.ExitMoveEntry);
				groupBaseActivity.Finish();

			} 

		}
		public override void SetupGroupBaseActivityButtons(GroupBaseActivity groupBaseActivity)
		{
			return;
		}

		public bool GroupIsFound(GroupBaseActivity groupBaseActivity)
		{
			return groupUuid.Last.Value.Equals (groupBaseActivity.UuidGroup);
		}
	}

	public class NavigateToFolder: NavigateAndLaunchTask {

		public NavigateToFolder():base() {
		}

		public NavigateToFolder(PwGroup groups): base(groups, new NullTask()) {
			return;
		}

	}

	public class NavigateToFolderAndLaunchMoveElementTask: NavigateAndLaunchTask {
	
		public NavigateToFolderAndLaunchMoveElementTask():base(){

		}


		public NavigateToFolderAndLaunchMoveElementTask(PwGroup groups, PwUuid Uuid):
			base(groups, new MoveElementTask() { Uuid = Uuid }) {
		}

		public override void Setup(Bundle b) {
			base.Setup(b);

			taskToBeLaunchAfterNavigation = new MoveElementTask ();
			taskToBeLaunchAfterNavigation.Setup (b);

		}


	}

}


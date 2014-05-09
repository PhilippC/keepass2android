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

		public virtual bool CloseEntryActivityAfterCreate
		{
			get { return false;}
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
				return false;
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

		public virtual void OnCompleteCreateEntryActivity(EntryActivity entryActivity)
		{
			entryActivity.CompleteOnCreate();
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
	public class SearchUrlTask: AppTask
	{
		public const String UrlToSearchKey = "UrlToSearch";

		public string UrlToSearchFor
		{
			get;
			set;
		}

		public override void Setup(Bundle b)
		{
			UrlToSearchFor = b.GetString(UrlToSearchKey);
		}
		public override IEnumerable<IExtra> Extras 
		{ 
			get
			{
				yield return new StringExtra { Key=UrlToSearchKey, Value = UrlToSearchFor };
			}
		}
		public override void AfterUnlockDatabase(PasswordActivity act)
		{
			ShareUrlResults.Launch(act, this);
			RemoveTaskFromIntent(act);
			act.AppTask = new NullTask();
		}

		public override bool CloseEntryActivityAfterCreate
		{
			get { return true;}
		}
	}

	
	/// <summary>
	/// User is about to select an entry for use in another app
	/// </summary>
	public class SelectEntryTask: AppTask
	{

		public override bool CloseEntryActivityAfterCreate
		{
			//keypoint here: close the app after selecting the entry
			get { return true;}
		}
	}

	/// <summary>
	/// User is about to select an entry. When selected, ask whether the url he was searching for earlier should be stored 
	/// in the selected entry for later use.
	/// </summary>
	public class SelectEntryForUrlTask: AppTask
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
		}

		public const String UrlToSearchKey = "UrlToSearch";

		public string UrlToSearchFor
		{
			get;
			set;
		}

		public override void Setup(Bundle b)
		{
			UrlToSearchFor = b.GetString(UrlToSearchKey);
		}
		public override IEnumerable<IExtra> Extras
		{
			get
			{
				yield return new StringExtra { Key = UrlToSearchKey, Value = UrlToSearchFor };
			}
		}

		public override bool CloseEntryActivityAfterCreate
		{
			get { return true; }
		}

		public override void OnCompleteCreateEntryActivity(EntryActivity entryActivity)
		{
			//if the database is readonly, don't offer to modify the URL
			if (App.Kp2a.GetDb().CanWrite == false)
			{
				base.OnCompleteCreateEntryActivity(entryActivity);
				return;
			}
			entryActivity.AskAddUrlThenCompleteCreate(UrlToSearchFor);
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
		public const String UrlKey = "CreateEntry_Url";

		public string Url
		{
			get;
			set;
		}

		public override void Setup(Bundle b)
		{
			Url = b.GetString(UrlKey);
		}
		public override IEnumerable<IExtra> Extras 
		{ 
			get
			{
				yield return new StringExtra { Key = UrlKey, Value = Url };
			}
		}
		
		
		public override void PrepareNewEntry(PwEntry newEntry)
		{
			newEntry.Strings.Set(PwDefs.UrlField, new ProtectedString(false, Url));
					
		}

		public override void AfterAddNewEntry(EntryEditActivity entryEditActivity, PwEntry newEntry)
		{
			EntryActivity.Launch(entryEditActivity, newEntry, -1, new SelectEntryTask());
			entryEditActivity.SetResult
				(KeePass.ExitCloseAfterTaskComplete);
			//no need to call Finish here, that's done in EntryEditActivity ("closeOrShowError")	
		}
		
		public override bool CloseEntryActivityAfterCreate
		{
			//if the user selects an entry before creating the new one, we're not closing the app
			get { return false;}
		}
	}
}


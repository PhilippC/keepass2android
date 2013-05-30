//  Copyright (c) 2013 Philipp Crocoll
//
//  This program is free software; you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation; either version 2 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program; if not, write to the Free Software
//  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
//
using System;
using Android.App;
using Android.Content;
using Android.OS;
using System.Collections.Generic;

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
	/// interface for "tasks": this are things the user wants to do and which require several activities
	/// </summary>
	public interface IAppTask
	{
		/// <summary>
		/// Loads the parameters of the task from the given bundle
		/// </summary>
		void Setup(Bundle b);

		/// <summary>
		/// Returns the parameters of the task for storage in a bundle or intent
		/// </summary>
		/// <value>The extras.</value>
		IEnumerable<IExtra> Extras { get;}

		void AfterUnlockDatabase(PasswordActivity act);

		bool CloseEntryActivityAfterCreate
		{
			get;
		}
	}

	/// <summary>
	/// Implementation of IAppTask for "no task currently active" (Null pattern)
	/// </summary>
	public class NullTask: IAppTask
	{
		
		public void Setup(Bundle b)
		{
		}

		public IEnumerable<IExtra> Extras 
		{ 
			get
			{
				yield break;
			}
		}

		
		public void AfterUnlockDatabase(PasswordActivity act)
		{
			GroupActivity.Launch(act, this);
		}
		public bool CloseEntryActivityAfterCreate
		{
			get { return false;}
		}
	}

	/// <summary>
	/// User is about to search an entry for a given URL
	/// </summary>
	public class SearchUrlTask: IAppTask
	{
		public const String UrlToSearch_key = "UrlToSearch";

		public string UrlToSearchFor
		{
			get;
			set;
		}

		public void Setup(Bundle b)
		{
			UrlToSearchFor = b.GetString(UrlToSearch_key);
		}
		public IEnumerable<IExtra> Extras 
		{ 
			get
			{
				yield return new StringExtra() { Key=UrlToSearch_key, Value = UrlToSearchFor };
			}
		}
		public void AfterUnlockDatabase(PasswordActivity act)
		{
			ShareUrlResults.Launch(act, this);
		}
		public bool CloseEntryActivityAfterCreate
		{
			get { return true;}
		}
	}

	
	/// <summary>
	/// User is about to select an entry for use in another app
	/// </summary>
	public class SelectEntryTask: IAppTask
	{
		public void Setup(Bundle b)
		{
		}
		public IEnumerable<IExtra> Extras 
		{ 
			get
			{
				yield break;
			}
		}
		public void AfterUnlockDatabase(PasswordActivity act)
		{
			GroupActivity.Launch(act, this);
		}
		public bool CloseEntryActivityAfterCreate
		{
			//keypoint here: close the app after selecting the entry
			get { return true;}
		}
	}

	/// <summary>
	/// 
	/// </summary>
	public static class AppTask
	{
		public const String AppTask_key = "KP2A_APPTASK";

		/// <summary>
		/// Should be used in OnCreate to (re)create a task
		/// if savedInstanceState is not null, the task is recreated from there. Otherwise it's taken from the intent.
		/// </summary>
		public static IAppTask GetTaskInOnCreate(Bundle savedInstanceState, Intent intent)
		{
			if (savedInstanceState != null)
			{
				return AppTask.CreateFromBundle(savedInstanceState);
			}
			else
			{
				return AppTask.CreateFromIntent(intent);
			}
		}

		public static IAppTask CreateFromIntent(Intent i)
		{
			return CreateFromBundle(i.Extras);
		}

		public static IAppTask CreateFromBundle(Bundle b)
		{
			if (b == null)
				return new NullTask();

			string taskType = b.GetString("KP2A_APP_TASK_TYPE");

			if (string.IsNullOrEmpty(taskType))
				return new NullTask();

			Type[] types = {typeof(SearchUrlTask), typeof(NullTask)};

			foreach (Type type in types)
			{
				if (taskType == type.Name)
				{
					IAppTask task = (IAppTask)Activator.CreateInstance(type);
					task.Setup(b);
					return task;
				}
			}

			return new NullTask();
		}

		/// <summary>
		/// Adds the extras of the task to the intent
		/// </summary>
		public static void ToIntent(this IAppTask task, Intent intent)
		{
			AppTask.GetTypeExtra(task.GetType()).ToIntent(intent);

			foreach (IExtra extra in task.Extras)
			{
				extra.ToIntent(intent);
			}
		}

		/// <summary>
		/// Adds the extras of the task to the bundle
		/// </summary>
		public static void ToBundle(this IAppTask task, Bundle bundle)
		{
			AppTask.GetTypeExtra(task.GetType()).ToBundle(bundle);

			foreach (IExtra extra in task.Extras)
			{
				extra.ToBundle(bundle);
			}

		}

		/// <summary>
		/// Returns an IExtra which must be part of the Extras of a task to describe the type
		/// </summary>
		static IExtra GetTypeExtra(Type type)
		{
			return new StringExtra() { Key="KP2A_APP_TASK_TYPE", Value=type.Name};
		}

	}
}


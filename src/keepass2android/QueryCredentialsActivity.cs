using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Keepass2android.Pluginsdk;

namespace keepass2android
{
	[Activity(Label = "@string/app_name",
		ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden,
		Exported = true,
        Theme = "@style/MyTheme_ActionBar")]
	[IntentFilter(new[] { Strings.ActionQueryCredentials},
		Categories = new[] { Intent.CategoryDefault })]
	[IntentFilter(new[] { Strings.ActionQueryCredentialsForOwnPackage },
		Categories = new[] { Intent.CategoryDefault })]
	public class QueryCredentialsActivity : Activity
	{
		private const int RequestCodePluginAccess = 1;
		private const int RequestCodeQuery = 2;
		private const string IsRecreate = "isRecreate";
		private const string StartedQuery = "startedQuery";
		private bool _startedQuery;
		private string _requiredScope;
		private string _requestedUrl;
		private string _pluginPackage;

		public QueryCredentialsActivity (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}

		public QueryCredentialsActivity()
		{
		}

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			//if launched from history, don't re-use the task. Proceed to FileSelect instead.
			if (Intent.Flags.HasFlag(ActivityFlags.LaunchedFromHistory))
			{
				Kp2aLog.Log("Forwarding to SelectCurrentDbActivity. QueryCredentialsActivity started from history.");
				Intent intent = new Intent(this, typeof(SelectCurrentDbActivity));
				intent.AddFlags(ActivityFlags.ForwardResult);
				StartActivity(intent);
				Finish();
				return;
			}

			_pluginPackage = null;
			if (CallingActivity != null)
				_pluginPackage = CallingActivity.PackageName;
			if (_pluginPackage == null)
			{
				Kp2aLog.Log("Couldn't retrieve calling package. Probably activity was started without startActivityForResult()");
				Finish();
				return;
			}
			if (Intent.Action == Strings.ActionQueryCredentialsForOwnPackage)
			{
				_requiredScope = Strings.ScopeQueryCredentialsForOwnPackage;
				_requestedUrl = KeePass.AndroidAppScheme + _pluginPackage;
			}
			else if (Intent.Action == Strings.ActionQueryCredentials)
			{
				_requiredScope = Strings.ScopeQueryCredentials;
				_requestedUrl = Intent.GetStringExtra(Strings.ExtraQueryString);
			}
			else
			{
				Kp2aLog.Log("Invalid action for QueryCredentialsActivity: " + Intent.Action);
				SetResult(Result.FirstUser);
				Finish();
				return;
			}

			//only start the query or request plugin access when creating the first time.
			//if we're restarting (after config change or low memory), we will get onActivityResult() later
			//which will either start the next activity or finish this one.
			if ((savedInstanceState == null) || (savedInstanceState.GetBoolean(IsRecreate, false) == false))
			{
				ShowToast();

				if (new PluginDatabase(this).HasAcceptedScope(_pluginPackage,_requiredScope))
				{
					StartQuery();
				}
				else
				{
					RequestPluginAccess();
				}
			}
		}

		protected override void OnStart()
		{
			base.OnStart();
			Kp2aLog.Log("Starting QueryCredentialsActivity");
		}

		protected override void OnResume()
		{
			base.OnResume();
			Kp2aLog.Log("Resuming QueryCredentialsActivity");
		}

		private void ShowToast()
		{
			string pluginDisplayName = _pluginPackage;
			try
			{
				pluginDisplayName = PackageManager.GetApplicationLabel(PackageManager.GetApplicationInfo(_pluginPackage, 0));
			}
			catch (Exception e)
			{
				Kp2aLog.LogUnexpectedError(e);
			}
			if (String.IsNullOrEmpty(_requestedUrl))
				Toast.MakeText(this, GetString(Resource.String.query_credentials, new Java.Lang.Object[] {pluginDisplayName}), ToastLength.Long).Show();
			else
				Toast.MakeText(this,
				               GetString(Resource.String.query_credentials_for_url,
										 new Java.Lang.Object[] { pluginDisplayName, _requestedUrl }), ToastLength.Long).Show(); ;
		}

		private void StartQuery()
		{
		    //launch SelectCurrentDbActivity (which is root of the stack (exception: we're even below!)) with the appropriate task.
            //will return the results later
            Intent i = new Intent(this, typeof (SelectCurrentDbActivity));
			//don't show user notifications when an entry is opened.
			var task = new SearchUrlTask() {UrlToSearchFor = _requestedUrl, ShowUserNotifications = ShowUserNotificationsMode.WhenTotp};
			task.ToIntent(i);
			StartActivityForResult(i, RequestCodeQuery);
			_startedQuery = true;
		}

		private void RequestPluginAccess()
		{
			Intent i = new Intent(this, typeof(PluginDetailsActivity));
			i.SetAction(Strings.ActionEditPluginSettings);
			i.PutExtra(Strings.ExtraPluginPackage, _pluginPackage);
			StartActivityForResult(i, RequestCodePluginAccess);
		}

		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);

			if (requestCode == RequestCodePluginAccess)
			{
				if (new PluginDatabase(this).HasAcceptedScope(_pluginPackage, _requiredScope))
				{
					//user granted access. Search for the requested credentials:
					StartQuery();
				}
				else
				{
					//user didn't grant access
					SetResult(Result.Canceled);
					Finish();
				}
			}
			if (requestCode == RequestCodeQuery)
			{
				if (resultCode == KeePass.ExitCloseAfterTaskComplete)
				{
					//double check we really have the permission
					if (!new PluginDatabase(this).HasAcceptedScope(_pluginPackage, _requiredScope))
					{
						Kp2aLog.LogUnexpectedError(new Exception("Ohoh! Scope not available, shouldn't get here. Malicious app somewhere?"));
						SetResult(Result.Canceled);
						Finish();
						return;
					}
					//return credentials to caller:
					Intent credentialData = new Intent();
					PluginHost.AddEntryToIntent(credentialData, App.Kp2a.LastOpenedEntry);
					credentialData.PutExtra(Strings.ExtraQueryString,_requestedUrl);
					SetResult(Result.Ok, credentialData);
					Finish();
				}
				else
				{
					SetResult(Result.Canceled);
					Finish();
				}
			}
		}

		protected override void OnSaveInstanceState(Bundle outState)
		{
			base.OnSaveInstanceState(outState);
			outState.PutBoolean(StartedQuery, _startedQuery);
			outState.PutBoolean(IsRecreate, true);
		}
	}
}
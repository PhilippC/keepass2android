using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Util;
using KeePassLib.Utility;
using Keepass2android.Pluginsdk;
using Org.Json;

namespace keepass2android
{
	/// <summary>
	/// Class which manages plugins inside the app
	/// </summary>
	[BroadcastReceiver()]
	[IntentFilter(new[] { Strings.ActionRequestAccess})]
	public class PluginHost: BroadcastReceiver
	{
		
		private const string _tag = "KP2A_PluginHost";
		

		private static readonly string[] _validScopes = { Strings.ScopeDatabaseActions, Strings.ScopeCurrentEntry };

		/// <summary>
		/// Sends a broadcast to all potential plugins prompting them to request access to our app.
		/// </summary>
		public static void TriggerRequests(Context ctx)
		{
			Intent accessIntent = new Intent(Strings.ActionTriggerRequestAccess);
			PackageManager packageManager = ctx.PackageManager;
			IList<ResolveInfo> dictPacks = packageManager.QueryBroadcastReceivers(
				accessIntent, PackageInfoFlags.Receivers);
			PluginDatabase pluginDatabase = new PluginDatabase(ctx);
			foreach (ResolveInfo ri in dictPacks)
			{
				ApplicationInfo appInfo = ri.ActivityInfo.ApplicationInfo;
				String pkgName = appInfo.PackageName;
				
				TriggerRequest(ctx, pkgName, pluginDatabase);
			}

		}

		public static void TriggerRequest(Context ctx, string pkgName, PluginDatabase pluginDatabase)
		{
			try
			{
				Intent triggerIntent = new Intent(Strings.ActionTriggerRequestAccess);
				triggerIntent.SetPackage(pkgName);
				triggerIntent.PutExtra(Strings.ExtraSender, ctx.PackageName);

				triggerIntent.PutExtra(Strings.ExtraRequestToken, pluginDatabase.GetRequestToken(pkgName));
				ctx.SendBroadcast(triggerIntent);
			}
			catch (Exception e)
			{
			}
		}


		public override void OnReceive(Context context, Intent intent)
		{
			PluginDatabase pluginDb = new PluginDatabase(context);
			if (intent.Action == Strings.ActionRequestAccess)
			{
				var senderPackage = intent.GetStringExtra(Strings.ExtraSender);
				var requestToken = intent.GetStringExtra(Strings.ExtraRequestToken);

			 	var requestedScopes = intent.GetStringArrayListExtra(Strings.ExtraScopes);

				if (!AreScopesValid(requestedScopes))
				{
					return;
				}

				if (pluginDb.GetRequestToken(senderPackage) != requestToken)
				{
					Log.Warn(_tag, "Invalid requestToken!");
					return;
				}
				string currentAccessToken = pluginDb.GetAccessToken(senderPackage);
				if ((currentAccessToken != null) 
					&& (AccessManager.IsSubset(requestedScopes,
				                           pluginDb.GetPluginScopes(senderPackage))))
				{
					//permission already there.
					var i = new Intent(Strings.ActionReceiveAccess);
					i.PutExtra(Strings.ExtraSender, context.PackageName);
					i.PutExtra(Strings.ExtraAccessToken, currentAccessToken);
					//TODO: Plugin should verify requestToken to make sure it doesn't receive accessTokens from malicious apps
					i.PutExtra(Strings.ExtraRequestToken, requestToken);
					i.SetPackage(senderPackage);
					context.SendBroadcast(i);

					Log.Debug(_tag, "Plugin " + senderPackage + " enabled.");
				}
				else
				{
					//store that scope was requested but not yet approved (=> accessToken = null)
					pluginDb.StorePlugin(senderPackage, null, requestedScopes);

					Log.Debug(_tag, "Plugin " + senderPackage + " not enabled.");

					//see if the plugin has an access token
					string accessToken = intent.GetStringExtra(Strings.ExtraAccessToken);
					if (accessToken != null)
					{
						//notify plugin that access token is no longer valid or sufficient
						Intent i = new Intent(Strings.ActionRevokeAccess);
						i.PutExtra(Strings.ExtraSender, context.PackageName);
						i.PutExtra(Strings.ExtraAccessToken, accessToken);
						i.SetPackage(senderPackage);
						context.SendBroadcast(i);
						Log.Warn(_tag, "Access token of plugin " + senderPackage + " not (or no more) valid.");
					}
					
				}
				if (OnReceivedRequest != null)
					OnReceivedRequest(this, new PluginHostEventArgs() { Package = senderPackage});

			}
		}

		private bool AreScopesValid(IList<string> requestedScopes)
		{
			foreach (string scope in requestedScopes)
			{
				if (!_validScopes.Contains(scope))
				{
					Log.Warn(_tag, "invalid scope: " + scope);
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// adds the entry output data to the intent to be sent to a plugin
		/// </summary>
		public static void AddEntryToIntent(Intent intent, PwEntryOutput entry)
		{
			/*//add the entry XML
			not yet implemented. What to do with attachments?
			MemoryStream memStream = new MemoryStream();
			KdbxFile.WriteEntries(memStream, new[] {entry});
			string entryData = StrUtil.Utf8.GetString(memStream.ToArray());
			intent.PutExtra(Strings.ExtraEntryData, entryData);
			*/
			//add the output string array (placeholders replaced taking into account the db context)
			Dictionary<string, string> outputFields = entry.OutputStrings.ToDictionary(pair => StrUtil.SafeXmlString(pair.Key), pair => pair.Value.ReadString());

			//add field values as JSON ({ "key":"value", ... } form)
			JSONObject json = new JSONObject(outputFields);
			var jsonStr = json.ToString();
			intent.PutExtra(Strings.ExtraEntryOutputData, jsonStr);

			//add list of which fields are protected (StringArrayExtra)
			string[] protectedFieldsList = entry.OutputStrings.Where(s=>s.Value.IsProtected).Select(s => s.Key).ToArray();
			intent.PutExtra(Strings.ExtraProtectedFieldsList, protectedFieldsList);

			intent.PutExtra(Strings.ExtraEntryId, entry.Uuid.ToHexString());

		}

		public class PluginHostEventArgs
		{
			public string Package { get; set; }
		}
		public static event EventHandler<PluginHostEventArgs> OnReceivedRequest;
	}
}
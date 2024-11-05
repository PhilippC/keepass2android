using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Util;
using Group.Pals.Android.Lib.UI.Filechooser.Utils;
using KeePassLib.Utility;
using Keepass2android.Pluginsdk;
using Org.Json;

namespace keepass2android
{
	/// <summary>
	/// Class which manages plugins inside the app
	/// </summary>
	[BroadcastReceiver(Exported = true)]
	[IntentFilter(new[] { Strings.ActionRequestAccess })]
	public class PluginHost : BroadcastReceiver
	{

		private const string _tag = "KP2A_PluginHost";


		private static readonly string[] _validScopes = { Strings.ScopeDatabaseActions, 
															Strings.ScopeCurrentEntry,
															Strings.ScopeQueryCredentials,
															Strings.ScopeQueryCredentialsForOwnPackage};

		public static IEnumerable<string> GetAllPlugins(Context ctx)
		{
			Intent accessIntent = new Intent(Strings.ActionTriggerRequestAccess);
			PackageManager packageManager = ctx.PackageManager;
			IList<ResolveInfo> dictPacks = packageManager.QueryBroadcastReceivers(
				accessIntent, PackageInfoFlags.Receivers);
			
			return dictPacks.Select(ri => ri.ActivityInfo.ApplicationInfo).Select(appInfo => appInfo.PackageName);
			
		}

		/// <summary>
		/// Sends a broadcast to all potential plugins prompting them to request access to our app.
		/// </summary>
		public static void TriggerRequests(Context ctx)
		{
			PluginDatabase pluginDatabase = new PluginDatabase(ctx);
			foreach (string pkg in GetAllPlugins(ctx))
				TriggerRequest(ctx, pkg, pluginDatabase);

		}

		public static void TriggerRequest(Context ctx, string pkgName, PluginDatabase pluginDatabase)
		{
			try
			{
				Intent triggerIntent = new Intent(Strings.ActionTriggerRequestAccess);
				triggerIntent.SetPackage(pkgName);
				triggerIntent.PutExtra(Strings.ExtraSender, ctx.PackageName);
				string requestToken = pluginDatabase.GetRequestToken(pkgName);
				triggerIntent.PutExtra(Strings.ExtraRequestToken, requestToken);
				Android.Util.Log.Debug(_tag, "Request token: " + requestToken);
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
				string senderPackage = intent.GetStringExtra(Strings.ExtraSender);
				string requestToken = intent.GetStringExtra(Strings.ExtraRequestToken);

				IList<string> requestedScopes = intent.GetStringArrayListExtra(Strings.ExtraScopes);

				if (!AreScopesValid(requestedScopes))
				{
					Log.Debug(_tag, "requested scopes not valid");
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
					OnReceivedRequest(this, new PluginHostEventArgs() { Package = senderPackage });

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

			JSONObject jsonOutput = new JSONObject(outputFields);
			var jsonOutputStr = jsonOutput.ToString();
			intent.PutExtra(Strings.ExtraEntryOutputData, jsonOutputStr);

			JSONArray jsonProtectedFields = new JSONArray(
				(System.Collections.ICollection)entry.OutputStrings
					.Where(pair => pair.Value.IsProtected)
					.Select(pair => pair.Key)
					.ToArray());
			intent.PutExtra(Strings.ExtraProtectedFieldsList, jsonProtectedFields.ToString());

			intent.PutExtra(Strings.ExtraEntryId, entry.Uuid.ToHexString());

		}

		public class PluginHostEventArgs
		{
			public string Package { get; set; }
		}
		public static event EventHandler<PluginHostEventArgs> OnReceivedRequest;
	}
}
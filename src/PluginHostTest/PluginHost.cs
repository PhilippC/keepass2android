using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using KeePassLib;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using Keepass2android;
using Keepass2android.Pluginsdk;
using Org.Json;
using PluginHostTest;

namespace keepass2android
{
	[BroadcastReceiver()]
	[IntentFilter(new[] { Strings.ActionRequestAccess})]
	public class PluginHost: BroadcastReceiver
	{
		
		private const string _tag = "KP2A_PluginHost";
		

		private static readonly string[] _validScopes = { Strings.ScopeDatabaseActions, Strings.ScopeCurrentEntry };

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

		public static void AddEntryToIntent(Intent intent, PwEntry entry)
		{
			/*//add the entry XML
			not yet implemented. What to do with attachments?
			MemoryStream memStream = new MemoryStream();
			KdbxFile.WriteEntries(memStream, new[] {entry});
			string entryData = StrUtil.Utf8.GetString(memStream.ToArray());
			intent.PutExtra(Strings.ExtraEntryData, entryData);
			*/
			//add the compiled string array (placeholders replaced taking into account the db context)
			Dictionary<string, string> compiledFields = new Dictionary<string, string>();
			foreach (var pair in entry.Strings)
			{
				String key = pair.Key;

				String value = entry.Strings.ReadSafe(key);
				value = SprEngine.Compile(value, new SprContext(entry, App.Kp2A.GetDb().KpDatabase, SprCompileFlags.All));

				compiledFields.Add(StrUtil.SafeXmlString(pair.Key), value);
				
			}

			JSONObject json = new JSONObject(compiledFields);
			var jsonStr = json.ToString();
			intent.PutExtra(Strings.ExtraCompiledEntryData, jsonStr);

			intent.PutExtra(Strings.ExtraEntryId, entry.Uuid.ToHexString());

		}
	}
}
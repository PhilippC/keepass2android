using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Android.Content;
using Android.Util;
using Keepass2android.Pluginsdk;

namespace keepass2android
{
	public class PluginDatabase
	{
		private const string _tag = "KP2A_PluginDatabase";
		private readonly Context _ctx;
		private const string _accessToken = "accessToken";
		private const string _scopes = "scopes";
		private const string _requesttoken = "requestToken";
		private const string _pluginlist = "pluginList";



		public PluginDatabase(Context ctx)
		{
			_ctx = ctx;
		}

		private ISharedPreferences GetPreferencesForPlugin(string packageName)
		{
			var prefs = _ctx.GetSharedPreferences("KP2A.Plugin." + packageName, FileCreationMode.Private);
			if (prefs.GetString(_requesttoken, null) == null)
			{
				var editor = prefs.Edit();
				editor.PutString(_requesttoken, Guid.NewGuid().ToString());
				editor.Commit();

				var hostPrefs = GetHostPrefs();
				var plugins = hostPrefs.GetStringSet(_pluginlist, new List<string>());
				if (!plugins.Contains(packageName))
				{
					plugins.Add(packageName);
					hostPrefs.Edit().PutStringSet(_pluginlist, plugins).Commit();
				}

			}
			return prefs;
		}

		/// <summary>
		/// Returns the request token for the plugin. Request token is created of not yet available.
		/// </summary>
		/// <returns>Request token. Never null or empty.</returns>
		public string GetRequestToken(string pkgName)
		{
			return GetPreferencesForPlugin(pkgName).GetString(_requesttoken, null);
		}

		public IList<string> GetPluginScopes(string pluginPackage)
		{
			var prefs = GetPreferencesForPlugin(pluginPackage);
			return AccessManager.StringToStringArray(prefs.GetString(_scopes, ""));
		}

		public IEnumerable<String> GetAllPluginPackages()
		{
			var hostPrefs = GetHostPrefs();
			return hostPrefs.GetStringSet(_pluginlist, new List<string>());
		}

		public bool IsEnabled(string pluginPackage)
		{
			return GetPreferencesForPlugin(pluginPackage).GetString(_accessToken, null) != null;
		}


		public void StorePlugin(string pluginPackage, string accessToken, IList<string> requestedScopes)
		{
			ISharedPreferences hostPrefs = GetHostPrefs();
			ISharedPreferences pluginPrefs = GetPreferencesForPlugin(pluginPackage);
			var stringSet = hostPrefs.GetStringSet(_pluginlist, new Collection<string>());
			if (!stringSet.Contains(pluginPackage))
			{
				stringSet.Add(pluginPackage);
				hostPrefs.Edit()
				         .PutStringSet(_pluginlist, stringSet)
				         .Commit();
			}

			pluginPrefs.Edit()
			           .PutString(_scopes, AccessManager.StringArrayToString(requestedScopes))
			           .PutString(_accessToken, accessToken)
			           .Commit();
		}

		private ISharedPreferences GetHostPrefs()
		{
			return _ctx.GetSharedPreferences("plugins", FileCreationMode.Private);
		}

		public void SetEnabled(string pluginPackage, bool enabled)
		{
			if (enabled)
			{
				string accessToken = Guid.NewGuid().ToString();

				Intent i = new Intent(Strings.ActionReceiveAccess);
				i.SetPackage(pluginPackage);

				i.PutExtra(Strings.ExtraSender, _ctx.PackageName);
				i.PutExtra(Strings.ExtraRequestToken, GetPreferencesForPlugin(pluginPackage).GetString(_requesttoken, null));
				i.PutExtra(Strings.ExtraAccessToken, accessToken);
				_ctx.SendBroadcast(i);

				StorePlugin(pluginPackage, accessToken, GetPluginScopes( pluginPackage));
			}
			else
			{
				Intent i = new Intent(Strings.ActionRevokeAccess);
				i.SetPackage(pluginPackage);
				i.PutExtra(Strings.ExtraSender, _ctx.PackageName);
				i.PutExtra(Strings.ExtraRequestToken, GetPreferencesForPlugin(pluginPackage).GetString(_requesttoken, null));
				_ctx.SendBroadcast(i);
				StorePlugin(pluginPackage, null, GetPluginScopes(pluginPackage));
			}
		}

		public bool IsValidAccessToken(string pluginPackage, string accessToken, string scope)
		{
			if (pluginPackage == null)
			{
				Log.Warn(_tag, "No pluginPackage specified!");
				return false;
			}

			if (accessToken == null)
			{
				Log.Warn(_tag, "No accessToken specified!");
				return false;
			} 

			var prefs = GetPreferencesForPlugin(pluginPackage);
			if (prefs.GetString(_accessToken, null) != accessToken)
			{
				Log.Warn(_tag, "Invalid access token for " + pluginPackage);
				return false;
			}
			if (!AccessManager.StringToStringArray(prefs.GetString(_scopes, "")).Contains(scope))
			{
				Log.Warn(_tag, "Scope " + scope + " not granted for " + pluginPackage);
				return false;
			}
			return true;
		}

		public string GetAccessToken(string pluginPackage)
		{
			return GetPreferencesForPlugin(pluginPackage).GetString(_accessToken, null);
		}

		public void Clear()
		{
			foreach (string plugin in GetAllPluginPackages())
			{
				GetPreferencesForPlugin(plugin).Edit().Clear().Commit();
			}
			GetHostPrefs().Edit().Clear().Commit();
		}

		
		public IEnumerable<string> GetPluginsWithAcceptedScope(string scope)
		{
			return GetAllPluginPackages().Where(plugin =>
				{
					var prefs = GetPreferencesForPlugin(plugin);
					return (prefs.GetString(_accessToken, null) != null)
						 && AccessManager.StringToStringArray(prefs.GetString(_scopes, "")).Contains(scope);

				});
		}

		public void ClearPlugin(string plugin)
		{
			var prefs = _ctx.GetSharedPreferences("KP2A.Plugin." + plugin, FileCreationMode.Private);
			prefs.Edit().Clear().Commit();
		}
	}
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Android.Content;
using Android.Content.PM;
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
			return PluginHost.GetAllPlugins(_ctx);
		}

		public bool IsPackageInstalled(string targetPackage)
		{
			try
			{
				PackageInfo info = _ctx.PackageManager.GetPackageInfo(targetPackage, PackageInfoFlags.MetaData);
			}
			catch (PackageManager.NameNotFoundException e)
			{
				return false;
			}
			return true;
		}

		public bool IsEnabled(string pluginPackage)
		{
			return GetPreferencesForPlugin(pluginPackage).GetString(_accessToken, null) != null;
		}


		public void StorePlugin(string pluginPackage, string accessToken, IList<string> requestedScopes)
		{
			ISharedPreferences pluginPrefs = GetPreferencesForPlugin(pluginPackage);

			pluginPrefs.Edit()
					   .PutString(_scopes, AccessManager.StringArrayToString(requestedScopes))
					   .PutString(_accessToken, accessToken)
					   .Commit();
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

				StorePlugin(pluginPackage, accessToken, GetPluginScopes(pluginPackage));
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

		/// <summary>
		/// Checks if the given pluginPackage has been granted the requiredScope
		/// </summary>
		public bool HasAcceptedScope(string pluginPackage, string requiredScope)
		{
			if (pluginPackage == null)
			{
				Log.Warn(_tag, "No pluginPackage specified!");
				return false;
			}

			var prefs = GetPreferencesForPlugin(pluginPackage);
			if (prefs.GetString(_accessToken, null) == null)
			{
				Log.Info(_tag, "No access token for " + pluginPackage);
				return false;
			}
			if (!AccessManager.StringToStringArray(prefs.GetString(_scopes, "")).Contains(requiredScope))
			{
				Log.Info(_tag, "Scope " + requiredScope + " not granted for " + pluginPackage);
				return false;
			}
			return true;
		}
	}
}
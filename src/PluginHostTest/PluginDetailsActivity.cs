using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.Util;
using Keepass2android.Pluginsdk;
using keepass2android;
using keepass2android.views;

namespace PluginHostTest
{
	[Activity(Label = "TODO Details")]
	public class PluginDetailsActivity : Activity
	{
		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

			string pluginPackage = Intent.GetStringExtra("PluginPackage");

			Intent mainIntent = new Intent(Intent.ActionMain, null);
			mainIntent.AddCategory(Intent.CategoryLauncher);

			IList<ResolveInfo> appList = PackageManager.QueryIntentActivities(mainIntent, 0);
			//Collections.Sort(appList, new ResolveInfo.DisplayNameComparator(PackageManager));

			foreach (ResolveInfo temp in appList) 
			{

				Log.Verbose("my logs", "package and activity name = "
						+ temp.ActivityInfo.PackageName + "    "
						+ temp.ActivityInfo.Name + " " + temp.ActivityInfo.IconResource);

			 }
			var pluginRes = PackageManager.GetResourcesForApplication(pluginPackage);
			var title = GetStringFromPlugin(pluginRes, pluginPackage, "kp2aplugin_title");
			var author = GetStringFromPlugin(pluginRes, pluginPackage, "kp2aplugin_author");
			var shortDesc = GetStringFromPlugin(pluginRes, pluginPackage, "kp2aplugin_shortdesc");

			var version = PackageManager.GetPackageInfo(pluginPackage, 0).VersionName;
			
			SetContentView(Resource.Layout.plugin_details);
			if (title != null)
				FindViewById<TextView>(Resource.Id.txtLabel).Text = title;
			FindViewById<TextView>(Resource.Id.txtVersion).Text = version;
			SetTextOrHide(Resource.Id.txtAuthor, author);
			SetTextOrHide(Resource.Id.txtShortDesc, shortDesc);

			var checkbox = FindViewById<CheckBox>(Resource.Id.cb_enabled);
			PluginDatabase pluginDb = new PluginDatabase(this);
			checkbox.Checked = pluginDb.IsEnabled(pluginPackage);
			checkbox.CheckedChange += delegate(object sender, CompoundButton.CheckedChangeEventArgs args)
				{
					pluginDb.SetEnabled(pluginPackage, checkbox.Checked);
				};
			
			Drawable d = PackageManager.GetApplicationIcon(pluginPackage);
			FindViewById<ImageView>(Resource.Id.imgIcon).SetImageDrawable(d);
			
			FindViewById<TextView>(Resource.Id.txtVersion).Text = version;

			var scopesContainer = FindViewById<LinearLayout>(Resource.Id.scopes_list);

			foreach (string scope in pluginDb.GetPluginScopes(pluginPackage))
			{
				string scopeId = scope.Substring("keepass2android.".Length);

				TextWithHelp help = new TextWithHelp(this, GetString(Resources.GetIdentifier(scopeId + "_title", "string", PackageName)), GetString(Resources.GetIdentifier(scopeId + "_explanation", "string", PackageName)));
				LinearLayout.LayoutParams layoutParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.FillParent, ViewGroup.LayoutParams.WrapContent);
				help.LayoutParameters = layoutParams;
				scopesContainer.AddView(help);	
			}
			
			
		}

		private void SetTextOrHide(int resourceId, string text)
		{
			if (text != null)
			{
				FindViewById<TextView>(resourceId).Text = text;
				FindViewById<TextView>(resourceId).Visibility = ViewStates.Visible;
			}
			else
				FindViewById<TextView>(resourceId).Visibility = ViewStates.Gone;
		}

		public static string GetStringFromPlugin(Resources pluginRes, string pluginPackage, string stringId)
		{
			int titleId = pluginRes.GetIdentifier(pluginPackage + ":string/"+stringId, null, null);
			string title = null;
			if (titleId != 0)
				title = pluginRes.GetString(titleId);
			return title;
		}
	}
}
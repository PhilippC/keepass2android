using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Keepass2android.Pluginsdk;

namespace keepass2android
{
	[Activity(Label = "@string/plugins", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden, Theme="@style/android:Theme.Material.Light")]
	[IntentFilter(new[] { "kp2a.action.PluginListActivity" }, Categories = new[] { Intent.CategoryDefault })]
	public class PluginListActivity : ListActivity
	{
		private PluginArrayAdapter _pluginArrayAdapter;
		private List<PluginItem> _items;

		protected override void OnCreate(Bundle bundle)
		{
			new ActivityDesign(this).ApplyTheme();
			base.OnCreate(bundle);

			

			SetContentView(Resource.Layout.plugin_list);

			PluginHost.TriggerRequests(this);

			ListView listView = FindViewById<ListView>(Android.Resource.Id.List);
			listView.ItemClick +=
				(sender, args) =>
				{
					Intent i = new Intent(this, typeof(PluginDetailsActivity));
					i.PutExtra(Strings.ExtraPluginPackage, _items[args.Position].Package);
					StartActivity(i);
				};

			FindViewById<Button>(Resource.Id.btnPluginsOnline).Click += delegate
				{
					Util.GotoUrl(this, "https://keepass2android.codeplex.com/wikipage?title=Available%20Plug-ins");
				};

		}
		protected override void OnResume()
		{
			base.OnResume();
			PluginDatabase pluginDb = new PluginDatabase(this);

			_items = (from pluginPackage in pluginDb.GetAllPluginPackages()
					  let version = PackageManager.GetPackageInfo(pluginPackage, 0).VersionName
					  let enabledStatus = pluginDb.IsEnabled(pluginPackage) ? GetString(Resource.String.plugin_enabled) : GetString(Resource.String.plugin_disabled)
					  select new PluginItem(pluginPackage, enabledStatus, this)).ToList();
			/*
				{
					new PluginItem("PluginA", Resource.Drawable.Icon, "keepass2android.plugina", "connected"),
					new PluginItem("KeepassNFC", Resource.Drawable.Icon, "com.bla.blubb.plugina", "disconnected")
				};
			 * */
			_pluginArrayAdapter = new PluginArrayAdapter(this, Resource.Layout.ListViewPluginRow, _items);
			ListAdapter = _pluginArrayAdapter;
		}
	}
}
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
using keepass2android.views;

namespace keepass2android
{
	[Activity(Label = AppNames.AppName, Theme = "@style/android:Theme.Material.Light")]
	[IntentFilter(new[] { Strings.ActionEditPluginSettings },
		Label = AppNames.AppName,
		Categories = new[] { Intent.CategoryDefault })]
	public class PluginDetailsActivity : Activity
	{
		private CheckBox _checkbox;
		private string _pluginPackageName;

		protected override void OnCreate(Bundle bundle)
		{
			new ActivityDesign(this).ApplyTheme();
			base.OnCreate(bundle);
			

			_pluginPackageName = Intent.GetStringExtra(Strings.ExtraPluginPackage);

			var pluginRes = PackageManager.GetResourcesForApplication(_pluginPackageName);
			var title = GetStringFromPlugin(pluginRes, _pluginPackageName, "kp2aplugin_title");
			var author = GetStringFromPlugin(pluginRes, _pluginPackageName, "kp2aplugin_author");
			var shortDesc = GetStringFromPlugin(pluginRes, _pluginPackageName, "kp2aplugin_shortdesc");
			var version = PackageManager.GetPackageInfo(_pluginPackageName, 0).VersionName;

			SetContentView(Resource.Layout.plugin_details);
			if (title != null)
				FindViewById<TextView>(Resource.Id.txtLabel).Text = title;
			FindViewById<TextView>(Resource.Id.txtVersion).Text = version;
			SetTextOrHide(Resource.Id.txtAuthor, author);
			SetTextOrHide(Resource.Id.txtShortDesc, shortDesc);

			_checkbox = FindViewById<CheckBox>(Resource.Id.cb_enabled);
			_checkbox.CheckedChange += delegate
			{
				new PluginDatabase(this).SetEnabled(_pluginPackageName, _checkbox.Checked);
			};

			Drawable d = PackageManager.GetApplicationIcon(_pluginPackageName);
			FindViewById<ImageView>(Resource.Id.imgIcon).SetImageDrawable(d);

			FindViewById<TextView>(Resource.Id.txtVersion).Text = version;

			//cannot be wrong to update the view when we received an update
			PluginHost.OnReceivedRequest += OnPluginHostOnOnReceivedRequest;

			if (Intent.Action == Strings.ActionEditPluginSettings)
			{
				//this action can be triggered by external apps so we don't know if anybody has ever triggered
				//the plugin to request access. Do this now, don't set the view right now
				PluginHost.TriggerRequest(this, _pluginPackageName, new PluginDatabase(this));
				//show the buttons instead of the checkbox
				_checkbox.Visibility = ViewStates.Invisible;
				FindViewById(Resource.Id.accept_button).Visibility = ViewStates.Invisible; //show them only after access is requested
				FindViewById(Resource.Id.deny_button).Visibility = ViewStates.Visible;

				FindViewById(Resource.Id.accept_button).Click += delegate(object sender, EventArgs args)
				{
					new PluginDatabase(this).SetEnabled(_pluginPackageName, true);
					SetResult(Result.Ok);
					Finish();
				};

				FindViewById(Resource.Id.deny_button).Click += delegate(object sender, EventArgs args)
				{
					new PluginDatabase(this).SetEnabled(_pluginPackageName, false);
					SetResult(Result.Canceled);
					Finish();
				};

				//in case the plugin requested scopes previously, make sure we display them
				UpdateView();
			}
			else
			{
				UpdateView();
				_checkbox.Visibility = ViewStates.Visible;
				FindViewById(Resource.Id.accept_button).Visibility = ViewStates.Gone;
				FindViewById(Resource.Id.deny_button).Visibility = ViewStates.Gone;
			}
		}

		private void OnPluginHostOnOnReceivedRequest(object sender, PluginHost.PluginHostEventArgs args)
		{
			if (args.Package == _pluginPackageName)
			{
				FindViewById(Resource.Id.accept_button).Visibility = ViewStates.Visible;
				UpdateView();
			}
		}

		protected override void OnDestroy()
		{
			PluginHost.OnReceivedRequest -= OnPluginHostOnOnReceivedRequest;
			base.OnDestroy();
		}

		private void UpdateView()
		{
			var scopesContainer = FindViewById<LinearLayout>(Resource.Id.scopes_list);
			scopesContainer.RemoveAllViews();

			var pluginDb = new PluginDatabase(this);
			_checkbox.Checked = pluginDb.IsEnabled(_pluginPackageName);
			foreach (string scope in pluginDb.GetPluginScopes(_pluginPackageName))
			{
				string scopeId = scope.Substring("keepass2android.".Length);

				TextWithHelp help = new TextWithHelp(this,
													 GetString(Resources.GetIdentifier(scopeId + "_title", "string", PackageName)),
													 GetString(Resources.GetIdentifier(scopeId + "_explanation", "string", PackageName)));
				LinearLayout.LayoutParams layoutParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.FillParent,
																					   ViewGroup.LayoutParams.WrapContent);
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
			int titleId = pluginRes.GetIdentifier(pluginPackage + ":string/" + stringId, null, null);
			string title = null;
			if (titleId != 0)
				title = pluginRes.GetString(titleId);
			return title;
		}
	}
}
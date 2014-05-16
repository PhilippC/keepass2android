using System;
using System.Diagnostics;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Keepass2android.Pluginsdk;
using keepass2android;

namespace PluginHostTest
{
	[Activity(Label = "PluginHostTest", MainLauncher = true, Icon = "@drawable/icon")]
	public class Activity1 : Activity
	{
		int count = 1;

		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

			// Set our view from the "main" layout resource
			SetContentView(Resource.Layout.Main);

			// Get our button from the layout resource,
			// and attach an event to it
			Button button = FindViewById<Button>(Resource.Id.MyButton);

			button.Click += delegate { PluginHost.TriggerRequests(this);  };

			FindViewById<Button>(Resource.Id.managePluginsButton).Click += delegate(object sender, EventArgs args)
				{
					StartActivity(new Intent(this, typeof(PluginListActivity)));
				};
			FindViewById<Button>(Resource.Id.entryviewButton).Click += delegate
			{
					StartActivity(new Intent(this, typeof(EntryActivity)));
			};

			FindViewById<Button>(Resource.Id.testDbButton).Click += delegate
				{
					string message = "ok. ";
					try
					{
						Stopwatch sw = new Stopwatch();
						sw.Start();
						PluginDatabase db = new PluginDatabase(this);
						db.Clear();
						

						if (db.GetAllPluginPackages().Count() != 0)
							throw new Exception("db not empty!");

						const string testPackageA = "test.package.a";
						const string testPackageB = "test.package.b";
						db.ClearPlugin(testPackageA);
						db.ClearPlugin(testPackageB);
						EnsurePackageDataIsEmpty(db, testPackageA);
						EnsurePackageDataIsEmpty(db, testPackageB);

						string[] requestedScopes = {
							Strings.ScopeDatabaseActions
						};
						db.StorePlugin(testPackageA, null, requestedScopes);
						EnsurePackageDataIsEmpty(db, testPackageB);
						EnsurePackageDataIsEmpty(new PluginDatabase(this), testPackageB);
						db.StorePlugin(testPackageB, null, requestedScopes);
						EnsurePackageHasUnacceptedScope(db, testPackageA, Strings.ScopeDatabaseActions);
						EnsurePackageHasUnacceptedScope(db, testPackageB, Strings.ScopeDatabaseActions);
						EnsurePackageHasUnacceptedScope(new PluginDatabase(this), testPackageA, Strings.ScopeDatabaseActions);
						
						if (db.GetAllPluginPackages().Count() != 2)
							throw new Exception("wrong count of plugins");
						if (db.GetPluginsWithAcceptedScope(Strings.ScopeDatabaseActions).Any())
						{
							throw new Exception("wrong count of accepted plugins");
						}
						if (new PluginDatabase(this).GetPluginsWithAcceptedScope(Strings.ScopeDatabaseActions).Any())
						{
							throw new Exception("wrong count of accepted plugins");
						}

						db.SetEnabled(testPackageA, true);
						if (db.GetPluginsWithAcceptedScope(Strings.ScopeDatabaseActions).Single() != testPackageA)
						{
							throw new Exception("wrong plugin");
						}
						if (new PluginDatabase(this).GetPluginsWithAcceptedScope(Strings.ScopeDatabaseActions).Single() != testPackageA)
						{
							throw new Exception("wrong plugin");
						}
						if (db.GetPluginsWithAcceptedScope("somescope").Any())
						{
							throw new Exception("wrong count of accepted plugins");
						}
						var accessTokenA = db.GetAccessToken(testPackageA);
						if (String.IsNullOrEmpty(accessTokenA))
							throw new Exception("expected access token!");
						if (!db.IsEnabled(testPackageA))
							throw new Exception("plugin not enabled!");
						if (db.IsEnabled(testPackageB))
							throw new Exception("plugin enabled!");
						if (!db.IsValidAccessToken(testPackageA, accessTokenA, Strings.ScopeDatabaseActions))
							throw new Exception("invalid token!");
						db.SetEnabled(testPackageA, false);
						if (db.IsValidAccessToken(testPackageA, accessTokenA, Strings.ScopeDatabaseActions))
							throw new Exception("valid token?!");
						if (db.GetPluginsWithAcceptedScope(Strings.ScopeDatabaseActions).Any())
							throw new Exception("unexpected!");


						new PluginDatabase(this).SetEnabled(testPackageB, true);
						if (!db.IsEnabled(testPackageB))
							throw new Exception("plugin not enabled!");

						db.SetEnabled(testPackageA, true);
						accessTokenA = db.GetAccessToken(testPackageA);
						
						message += sw.ElapsedMilliseconds + "ms";

						Stopwatch swQuery = new Stopwatch();
						swQuery.Start();
						int n = 3;
						for (int i = 0; i < n; i++)
						{
							if (db.GetPluginsWithAcceptedScope(Strings.ScopeDatabaseActions).Count() != 2)
							{
								throw new Exception("wrong plugin");
							}
							if (!db.IsValidAccessToken(testPackageA, accessTokenA, Strings.ScopeDatabaseActions))
								throw new Exception("invalid token");
						}
						message += "/ " + swQuery.ElapsedMilliseconds/(double)n/2.0 + "ms for query";


					}
					catch (Exception exception)
					{
						message = exception.ToString();
					}
					Toast.MakeText(this, message, ToastLength.Long).Show();

				};

			
		}

		private void EnsurePackageHasUnacceptedScope(PluginDatabase db, string plugin, string scope)
		{
			if (String.IsNullOrEmpty(db.GetRequestToken(plugin)))
				throw new Exception("invalid request token");
			if (db.GetAccessToken(plugin) != null)
				throw new Exception("invalid access token!");
			if (db.GetPluginScopes(plugin).Count != 1)
				throw new Exception("Unexpected scopes!");
			if (db.GetPluginScopes(plugin).First() != scope)
				throw new Exception("Unexpected scope in db!");
		}

		private static void EnsurePackageDataIsEmpty(PluginDatabase db, string testPackageA)
		{
			if (String.IsNullOrEmpty(db.GetRequestToken(testPackageA)))
				throw new Exception("invalid request token");
			if (db.GetAccessToken(testPackageA) != null)
				throw new Exception("invalid access token!");
			if (db.GetPluginScopes(testPackageA).Count > 0)
				throw new Exception("Unexpected scopes!");
		}
	}
}


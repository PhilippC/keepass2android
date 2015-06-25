using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using Android.OS;

using Toolbar = Android.Support.V7.Widget.Toolbar;
namespace MaterialTest2
{
	[Activity(Theme="@style/MyTheme", Label = "MaterialTest", MainLauncher = true, Icon = "@drawable/icon")]
	public class MainActivity : ActionBarActivity
	{
		int count = 1;

		private DrawerLayout mDrawerLayout;
		//private RecyclerView mDrawerList;
		private ActionBarDrawerToggle mDrawerToggle;

		private string mDrawerTitle;

		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

			// Set our view from the "main" layout resource
			SetContentView(Resource.Layout.Main);

			mDrawerTitle = this.Title;
			//mPlanetTitles = this.Resources.GetStringArray (Resource.Array.planets_array);
			mDrawerLayout = FindViewById<DrawerLayout>(Resource.Id.drawerLayout);
			//mDrawerList = FindViewById<RecyclerView> (Resource.Id.left_drawer);

			
			//mDrawerLayout.SetDrawerShadow (Resource.Drawable.drawer_shadow, GravityCompat.Start);
			// improve performance by indicating the list if fixed size.
			//mDrawerList.HasFixedSize = true;
			//mDrawerList.SetLayoutManager (new LinearLayoutManager (this));

			// set up the drawer's list view with items and click listener
			//mDrawerList.SetAdapter (new PlanetAdapter (mPlanetTitles, this));
			// enable ActionBar app icon to behave as action to toggle nav drawer
		
			// ActionBarDrawerToggle ties together the the proper interactions
			// between the sliding drawer and the action bar app icon

			mDrawerToggle = new MyActionBarDrawerToggle (this, mDrawerLayout,
				Resource.Drawable.abc_ic_menu_copy_mtrl_am_alpha, 
				Resource.String.drawer_open, 
				Resource.String.drawer_close);

			mDrawerLayout.SetDrawerListener (mDrawerToggle);

			// Get our button from the layout resource,
			// and attach an event to it
			Button button = FindViewById<Button>(Resource.Id.MyButton);

			button.Click += delegate { button.Text = string.Format("{0} clicks!", count++); };

			var toolbar = FindViewById<Toolbar> (Resource.Id.toolbar2);
			//Toolbar will now take on default Action Bar characteristics
			SetSupportActionBar (toolbar);
			//You can now use and reference the ActionBar
			SupportActionBar.Title = "Hello from Toolbar";

			SupportActionBar.SetDisplayHomeAsUpEnabled (true);
			SupportActionBar.SetHomeButtonEnabled(true);

			//var toggle = new ActionBarDrawerToggle(
		}

		internal class MyActionBarDrawerToggle : ActionBarDrawerToggle
		{
			MainActivity owner;

			public MyActionBarDrawerToggle(MainActivity activity, DrawerLayout layout, int imgRes, int openRes, int closeRes)
				: base(activity, layout, openRes, closeRes)
			{
				owner = activity;
			}

			public override void OnDrawerClosed(View drawerView)
			{
				owner.SupportActionBar.Title = owner.Title;
				owner.InvalidateOptionsMenu();
			}

			public override void OnDrawerOpened(View drawerView)
			{
				owner.SupportActionBar.Title = owner.mDrawerTitle;
				owner.InvalidateOptionsMenu();
			}
		}
	}
}


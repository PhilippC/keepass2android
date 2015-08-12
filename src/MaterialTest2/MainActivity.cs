using System;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Runtime;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;

using Android.Support.Design.Widget;

using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Util;
using Toolbar = Android.Support.V7.Widget.Toolbar;
namespace MaterialTest2
{
    public class MyDrawerLayout : Android.Support.V4.Widget.DrawerLayout
    {
        private bool _fitsSystemWindows;

        protected MyDrawerLayout(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public MyDrawerLayout(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
        {
        }

        public MyDrawerLayout(Context context, IAttributeSet attrs) : base(context, attrs)
        {
        }

        public MyDrawerLayout(Context context) : base(context)
        {
        }

        private int[] mInsets = new int[4];

        protected override bool FitSystemWindows(Rect insets)
        {
            if (Build.VERSION.SdkInt >= Build.VERSION_CODES.Kitkat)
            {
                // Intentionally do not modify the bottom inset. For some reason, 
                // if the bottom inset is modified, window resizing stops working.
                // TODO: Figure out why.

                mInsets[0] = insets.Left;
                mInsets[1] = insets.Top;
                mInsets[2] = insets.Right;

                insets.Left = 0;
                insets.Top = 0;
                insets.Right = 0;
            }

            return base.FitSystemWindows(insets);
            
        }
        public int[] GetInsets()
        {
            return mInsets;
        }
    }


	[Activity(Theme="@style/MyTheme", Label = "MaterialTest", MainLauncher = false, Icon = "@drawable/icon", WindowSoftInputMode = SoftInput.AdjustResize)]
	public class MainActivity : AppCompatActivity
	{
		int count = 1;

		private DrawerLayout mDrawerLayout;
		//private RecyclerView mDrawerList;
		private ActionBarDrawerToggle mDrawerToggle;

		private string mDrawerTitle;

		public override bool OnCreateOptionsMenu(IMenu menu)
		{
			MenuInflater.Inflate(Resource.Menu.menu_password, menu);
			return true;
		}

		public override bool OnOptionsItemSelected(IMenuItem item)
		{
			switch (item.ItemId)
			{
				case Android.Resource.Id.Home:
					mDrawerLayout.OpenDrawer(Android.Support.V4.View.GravityCompat.Start);
					return true;
			}
			return base.OnOptionsItemSelected(item);
		}

		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

			// Set our view from the "main" layout resource
			SetContentView(Resource.Layout.Main);

			mDrawerTitle = this.Title;
			//mPlanetTitles = this.Resources.GetStringArray (Resource.Array.planets_array);
			mDrawerLayout = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
			//mDrawerList = FindViewById<RecyclerView> (Resource.Id.left_drawer);

			
			//mDrawerLayout.SetDrawerShadow (Resource.Drawable.drawer_shadow, GravityCompat.Start);
			// improve performance by indicating the list if fixed size.
			//mDrawerList.HasFixedSize = true;
			//mDrawerList.SetLayoutManager (new LinearLayoutManager (this));
			NavigationView nv;
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

			button.Click += delegate { button.Text = string.Format("{0} clicks!", count++);
				                         FindViewById(Resource.Id.MyButton).SetMinimumHeight(30*count);
			};

			
			FindViewById<ImageButton>(Resource.Id.eyebutton).Click += delegate(object sender, EventArgs args)
				{
					Snackbar.Make(FindViewById<ImageButton>(Resource.Id.eyebutton), "Here's a snackbar!", Snackbar.LengthLong).SetAction("Action",
					new ClickListener(v =>
					{
						Console.WriteLine("Action handler");
					})).Show();
				};

			var toolbar = FindViewById<Toolbar> (Resource.Id.toolbar);
			//SupportActionBar.SetBackgroundDrawable(GetDrawable(Resource.Drawable.ic_keepass2android));
				//Toolbar will now take on default Action Bar characteristics
			SetSupportActionBar (toolbar);
			//You can now use and reference the ActionBar
			//SupportActionBar.Title = "Hello from Toolbar";

			var collapsingToolbar = FindViewById<CollapsingToolbarLayout> (Resource.Id.collapsing_toolbar);
			collapsingToolbar.SetTitle ("Unlock Database");
			
			//SupportActionBar.SetHomeAsUpIndicator (Resource.Drawable.ic_menu);
			//SupportActionBar.SetDisplayHomeAsUpEnabled (true);

            SupportActionBar.SetDisplayHomeAsUpEnabled(true);
            SupportActionBar.SetHomeButtonEnabled(true);
            mDrawerToggle.SyncState();

			var fab = FindViewById<FloatingActionButton> (Resource.Id.fab);
			fab.Click += (sender, e) => {
				Snackbar.Make (fab, "Here's a snackbar!", Snackbar.LengthLong).SetAction ("Action",
					new ClickListener (v => {
						Console.WriteLine ("Action handler");
					})).Show ();
			};

		}

		public class ClickListener : Java.Lang.Object, View.IOnClickListener
		{
			public ClickListener (Action<View> handler)
			{
				Handler = handler;
			}

			public Action<View> Handler { get; set; }

			public void OnClick (View v)
			{
				var h = Handler;
				if (h != null)
					h (v);
			}
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


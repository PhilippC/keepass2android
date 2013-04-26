using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;

namespace KeyValueTest
{
	[Activity (Label = "KeyValueTest", MainLauncher = true)]
	public class Activity1 : Activity
	{
		string className = null;
		string ClassName
		{
			get {
				if (className == null)
					className = this.GetType().Name;
				return className;
			}
		}
		
		protected override void OnResume()
		{
			base.OnResume();
			Android.Util.Log.Debug("DEBUG",ClassName+".OnResume");
		}
		
		protected override void OnStart()
		{
			base.OnStart();
			Android.Util.Log.Debug("DEBUG",ClassName+".OnStart");
		}

		
		protected override void OnDestroy()
		{
			base.OnDestroy();
			Android.Util.Log.Debug("DEBUG",ClassName+".OnDestroy"+IsFinishing.ToString());
		}
		
		protected override void OnPause()
		{
			base.OnPause();
			Android.Util.Log.Debug("DEBUG",ClassName+".OnPause");
		}
		
		protected override void OnStop()
		{
			base.OnStop();
			Android.Util.Log.Debug("DEBUG",ClassName+".OnStop");
		}

		View CreateView(string key, string value)
		{
			LinearLayout layout = new LinearLayout(this, null);
			layout.Orientation = Orientation.Vertical;
			layout.LayoutParameters = new ViewGroup.LayoutParams(LinearLayout.LayoutParams.FillParent, LinearLayout.LayoutParams.WrapContent);
			TextView keyView = new TextView(this);
			if (key != null)
				keyView.Text = key;
			layout.AddView(keyView);
			TextView valueView = new TextView(this);
			if (value != null)
				valueView.Text = value;
			valueView.SetTextIsSelectable(true);
			layout.AddView(valueView);
			return layout;
		}

		protected override void OnCreate(Bundle bundle)
		{
			Android.Util.Log.Debug("DEBUG", ClassName + ".OnCreate");
			base.OnCreate(bundle);

			// Set our view from the "main" layout resource
			SetContentView(Resource.Layout.Main);
   



			FindViewById<LinearLayout>(Resource.Id.extra_strings).AddView(CreateView("key1","value1"));

			FindViewById<LinearLayout>(Resource.Id.extra_strings).AddView(CreateView("key2","value2"));
			FindViewById<LinearLayout>(Resource.Id.extra_strings).AddView(CreateView("key3","value3"));



		}
	}
}



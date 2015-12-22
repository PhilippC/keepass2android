using System;
using System.Linq;
using Android.App;
using Android.Preferences;

namespace keepass2android
{
	class ActivityDesign
	{
		private readonly Activity _activity;

		private int? _currentThemeId;

		private string _currentIconSet;

		private readonly string _attributeTheme;

		private bool _secureWindow;

		public ActivityDesign(Activity activity)
		{
			_activity = activity;
			try
			{
				var activityAttr = activity.GetType().GetCustomAttributes(false).Where(
				x => x is Android.App.ActivityAttribute
				).Cast<ActivityAttribute>().First();
				_attributeTheme = activityAttr.Theme;
				
			}
			catch (Exception e)
			{
				Kp2aLog.Log(e.ToString());
			}
			
		}

		private bool SecureWindowPref()
		{
			return (PreferenceManager.GetDefaultSharedPreferences(_activity).GetBoolean(
				_activity.GetString(Resource.String.ViewDatabaseSecure_key), true));
		}

		public void ApplyTheme()
		{
			if (HasThemes())
			{
				var dark = UseDarkTheme;
				int newTheme = dark ? DarkTheme : LightTheme;
				_activity.SetTheme(newTheme);
				_currentThemeId = newTheme;
				_secureWindow = SecureWindowPref();
			}
			_currentIconSet = PreferenceManager.GetDefaultSharedPreferences(_activity)
				.GetString("IconSetKey", _activity.PackageName);
		}

		public int DarkTheme
		{
			get
			{
				if (string.IsNullOrEmpty(_attributeTheme))
					return Resource.Style.MyTheme_Dark;
				if (_attributeTheme.Contains("MyTheme_Blue"))
					return Resource.Style.MyTheme_Blue_Dark;
				if (_attributeTheme.Contains("MyTheme_ActionBar"))
					return Resource.Style.MyTheme_ActionBar_Dark;
				return Resource.Style.MyTheme_Dark;
			}
			
		}

		public int LightTheme
		{
			get
			{
				if (string.IsNullOrEmpty(_attributeTheme))
					return Resource.Style.MyTheme;
				if (_attributeTheme.Contains("MyTheme_Blue"))
					return Resource.Style.MyTheme_Blue;
				if (_attributeTheme.Contains("MyTheme_ActionBar"))
					return Resource.Style.MyTheme_ActionBar;
				return Resource.Style.MyTheme;
			}

		}

		public void ReapplyTheme()
		{
			if (HasThemes())
			{
				int newTheme = UseDarkTheme ? DarkTheme : LightTheme;
				if (newTheme != _currentThemeId)
				{
					Kp2aLog.Log("recreating due to theme change.");
					_activity.Recreate();
					return;
				}	
			}
			
			if (PreferenceManager.GetDefaultSharedPreferences(_activity)
				.GetString("IconSetKey", _activity.PackageName) != _currentIconSet)
			{
				Kp2aLog.Log("recreating due to icon set change.");
				_activity.Recreate();
				return;

			}

			if (SecureWindowPref() != _secureWindow)
			{
				Kp2aLog.Log("recreating due to secure window change.");
				_activity.Recreate();
				return;
			}
		}

		public bool UseDarkTheme
		{
			get
			{
				var prefs = PreferenceManager.GetDefaultSharedPreferences(_activity);
				string design = prefs.GetString(_activity.GetString(Resource.String.design_key), _activity.GetString(Resource.String.design_default));
				bool dark = (design == "Dark");
				return dark;
			}
		}

		public void ApplyDialogTheme()
		{
			if (HasThemes())
			{
				bool dark = UseDarkTheme;
				_activity.SetTheme(dark ? Resource.Style.Base_Dialog : Resource.Style.Base_Dialog_Dark);
			}

		}

		public bool HasThemes()
		{
			return ((int)Android.OS.Build.VERSION.SdkInt >= 14);
		}
		
	}
}
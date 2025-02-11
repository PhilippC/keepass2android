using System;
using System.Linq;
using Android.App;
using Android.Content.Res;
using Android.OS;
using Android.Preferences;
using AndroidX.AppCompat.App;
using keepass2android;

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
				Kp2aLog.LogUnexpectedError(e); 
			}
			
		}

		private bool SecureWindowPref()
		{
			return (PreferenceManager.GetDefaultSharedPreferences(_activity).GetBoolean(
				_activity.GetString(Resource.String.ViewDatabaseSecure_key), true));
		}

		public void ApplyTheme()
		{
			_currentThemeId = NightModePreference;
            AppCompatDelegate.DefaultNightMode = _currentThemeId.Value;
            _secureWindow = SecureWindowPref();
		
			_currentIconSet = PreferenceManager.GetDefaultSharedPreferences(_activity)
				.GetString("IconSetKey", _activity.PackageName);
		}


		public void ReapplyTheme()
		{
			
			int newTheme = NightModePreference;
			if (newTheme != _currentThemeId)
			{
				Kp2aLog.Log("recreating due to theme change.");
                _activity.Recreate();
                _activity.Finish(); //withouth this, we'll have two GroupActivities on the backstack afterwards
				return;
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

		public int NightModePreference
		{
			get
			{
				var prefs = PreferenceManager.GetDefaultSharedPreferences(_activity);
				string design = prefs.GetString(_activity.GetString(Resource.String.design_key), _activity.GetString(Resource.String.design_default));
                return design switch
                {
                    "System" => AppCompatDelegate.ModeNightFollowSystem,
                    "Light" => AppCompatDelegate.ModeNightNo,
                    "Dark" => AppCompatDelegate.ModeNightYes,
                    _ => AppCompatDelegate.ModeNightFollowSystem,
                };
			}
		}

		public void ApplyDialogTheme()
		{
			
			_activity.SetTheme(Resource.Style.Kp2aTheme_Dialog);
			

		}

		
	}
}
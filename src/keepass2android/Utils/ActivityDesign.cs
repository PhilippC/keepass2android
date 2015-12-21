using Android.App;
using Android.Preferences;

namespace keepass2android
{
	class ActivityDesign
	{
		private readonly Activity _activity;

		private int? _currentThemeId;

		private string _currentIconSet;

		public ActivityDesign(Activity activity)
		{
			_activity = activity;
		}

		public void ApplyTheme()
		{
			/*if (HasThemes())
			{
				var dark = UseDarkTheme;
				//int newTheme = dark ? Resource.Style.ThemeDark : Resource.Style.ThemeLight;
				int newTheme = Resource.Style.ThemeMaterial;
				_activity.SetTheme(newTheme);
				_currentThemeId = newTheme;
			}*/
			_currentIconSet = PreferenceManager.GetDefaultSharedPreferences(_activity)
				.GetString("IconSetKey", _activity.PackageName);
		}

		public void ReapplyTheme()
		{
			/*if (HasThemes())
			{
				//int newTheme = UseDarkTheme ? Resource.Style.ThemeDark : Resource.Style.ThemeLight;
				int newTheme = Resource.Style.ThemeMaterial;
				if (newTheme != _currentThemeId)
				{
					Kp2aLog.Log("recreating due to theme change.");
					_activity.Recreate();
					return;
				}	
			}
			*/
			if (PreferenceManager.GetDefaultSharedPreferences(_activity)
				.GetString("IconSetKey", _activity.PackageName) != _currentIconSet)
			{
				Kp2aLog.Log("recreating due to icon set change.");
				_activity.Recreate();
				
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
				//_activity.SetTheme(dark ? Resource.Style.DialogDark : Resource.Style.DialogLight);
			}

		}

		public bool HasThemes()
		{
			return ((int)Android.OS.Build.VERSION.SdkInt >= 14);
		}
		
	}
}
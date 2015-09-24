using Android.App;
using Android.Preferences;

namespace keepass2android
{
	class ActivityDesign
	{
		private readonly Activity _activity;

		private int? _currentThemeId;

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
				}	
			}
			*/
			
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
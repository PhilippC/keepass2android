using Android.Graphics.Drawables;

namespace keepass2android
{
	/// <summary>
	/// Reperesents the popup menu item in EntryActivity to toggle visibility of all protected strings (e.g. Password)
	/// </summary>
	class ToggleVisibilityPopupMenuItem : IPopupMenuItem
	{
		private readonly EntryActivity _activity;
		

		public ToggleVisibilityPopupMenuItem(EntryActivity activity)
		{
			_activity = activity;
			
		}

		public Drawable Icon 
		{ 
			get
			{
				//return new TextDrawable("\uF06E", _activity);
				return _activity.Resources.GetDrawable(Resource.Drawable.ic_menu_view);
				
			}
		}
		public string Text
		{
			get
			{
				return _activity.Resources.GetString(
					_activity._showPassword ? 
						Resource.String.menu_hide_password 
						: Resource.String.show_password);
			}
		}

		
		public void HandleClick()
		{
			_activity.ToggleVisibility();
		}
	}
}
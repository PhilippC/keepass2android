using Android.Graphics.Drawables;
using Android.Widget;
using keepass2android;

namespace keepass2android
{
	/// <summary>
	/// Reperesents the popup menu item in EntryActivity to toggle visibility of all protected strings (e.g. Password)
	/// </summary>
	class ToggleVisibilityPopupMenuItem : IPopupMenuItem
	{
		private readonly EntryActivity _activity;
        private readonly TextView _valueView;


        public ToggleVisibilityPopupMenuItem(EntryActivity activity, TextView valueView)
        {
            _activity = activity;
            _valueView = valueView;
        }

		public Drawable Icon 
		{ 
			get
			{
				//return new TextDrawable("\uF06E", _activity);
				return _activity.Resources.GetDrawable(Resource.Drawable.baseline_visibility_24);
				
			}
		}
		public string Text
		{
			get
			{
				return _activity.Resources.GetString(
					_activity.GetVisibilityForProtectedView(_valueView) ? 
						Resource.String.menu_hide_password 
						: Resource.String.show_password);
			}
		}

		
		public void HandleClick()
		{
			_activity.ToggleVisibility(_valueView);
		}
	}
}
// This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
//
//   Keepass2Android is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   Keepass2Android is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.

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
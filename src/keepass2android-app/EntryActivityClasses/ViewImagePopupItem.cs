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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using keepass2android;

namespace keepass2android.EntryActivityClasses
{
    internal class ViewImagePopupItem : IPopupMenuItem
    {
        private readonly string _key;
        private readonly EntryActivity _entryActivity;

        public ViewImagePopupItem(string key, EntryActivity entryActivity)
        {
            _key = key;
            _entryActivity = entryActivity;
        }
        public Drawable Icon
        {
            get
            {
                return _entryActivity.Resources.GetDrawable(Resource.Drawable.baseline_image_24);
            }
        }

        public string Text
        {
            get
            {
                return _entryActivity.Resources.GetString(Resource.String.ShowAttachedImage);
            }
        }

        public void HandleClick()
        {
            _entryActivity.ShowAttachedImage(_key);

        }
    }
}
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
using keepass2android;

namespace keepass2android
{
  /// <summary>
  /// Represents the popup menu item in EntryActivity to store the binary attachment on SD card
  /// </summary>
  internal class WriteBinaryToFilePopupItem : IPopupMenuItem
  {
    private readonly string _key;
    private readonly EntryActivity _activity;

    public WriteBinaryToFilePopupItem(string key, EntryActivity activity)
    {
      _key = key;
      _activity = activity;
    }

    public Drawable Icon
    {
      get { return _activity.Resources.GetDrawable(Resource.Drawable.baseline_save_24); }
    }

    public string Text
    {
      get { return _activity.Resources.GetString(Resource.String.SaveAttachmentDialog_save); }
    }

    public void HandleClick()
    {
      _activity.WriteBinaryToFile(_key, false);
    }
  }
}
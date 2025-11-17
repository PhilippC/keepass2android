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

using Android.Content;
using Android.Graphics.Drawables;
using keepass2android;

namespace keepass2android
{
    /// <summary>
    /// Reperesents the popup menu item in EntryActivity to copy a string to clipboard
    /// </summary>
    class CopyToClipboardPopupMenuIcon : IPopupMenuItem
    {
        private readonly Context _context;
        private readonly IStringView _stringView;
        private readonly bool _isProtected;

        public CopyToClipboardPopupMenuIcon(Context context, IStringView stringView, bool isProtected)
        {
            _context = context;
            _stringView = stringView;
            _isProtected = isProtected;
        }

        public Drawable Icon
        {
            get
            {
                return _context.Resources.GetDrawable(Resource.Drawable.baseline_content_copy_24);
            }
        }
        public string Text
        {
            get { return _context.Resources.GetString(Resource.String.copy_to_clipboard); }
        }


        public void HandleClick()
        {
            CopyToClipboardService.CopyValueToClipboardWithTimeout(_context, _stringView.Text, _isProtected);
        }
    }
}
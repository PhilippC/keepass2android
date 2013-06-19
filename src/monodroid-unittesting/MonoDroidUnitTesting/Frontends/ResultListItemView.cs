//
// Copyright (C) 2012 Maya Studios (http://mayastudios.com)
//
// This file is part of MonoDroidUnitTesting.
//
// MonoDroidUnitTesting is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// MonoDroidUnitTesting is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License
// along with MonoDroidUnitTesting. If not, see <http://www.gnu.org/licenses/>.
//
using Android.Content;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Widget;


namespace MonoDroidUnitTesting {
  internal class ResultListItemView : LinearLayout {
    private readonly ImageView m_iconView;
    private readonly TextView m_textView;

    public ResultListItemView(Context ctx) : base(ctx) {
      this.Orientation = Orientation.Horizontal;

      int preferredListItemHeight = AbstractResultActivity.GetPreferredListItemHeight(ctx);

      this.m_iconView = new ImageView(this.Context);
      this.m_iconView.SetAdjustViewBounds(true);
      this.m_iconView.LayoutParameters = MonoDroidUnitTesting.Utils.LayoutParams.ForLL(
        width: LayoutParams.WrapContent, margin: 7, gravity: GravityFlags.Center);
      this.m_iconView.SetMinimumHeight(preferredListItemHeight / 2);
      this.m_iconView.SetMaxHeight(preferredListItemHeight / 2);
      AddView(this.m_iconView);

      this.m_textView = new TextView(this.Context);
      this.m_textView.LayoutParameters = MonoDroidUnitTesting.Utils.LayoutParams.ForLL();
      this.m_textView.SetTextSize(ComplexUnitType.Px, preferredListItemHeight / 4);
      AddView(this.m_textView);
    }

    public void SetIcon(Drawable icon) {
      this.m_iconView.SetImageDrawable(icon);
    }

    public void SetHtml(string htmlCode) {
      this.m_textView.TextFormatted = Html.FromHtml(htmlCode);
    }

    public void SetText(string text) {
      this.m_textView.Text = text;
    }
  }
}
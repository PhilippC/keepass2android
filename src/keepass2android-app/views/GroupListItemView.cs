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
using Android.Content;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using keepass2android;

namespace keepass2android.view
{
  public abstract class GroupListItemView : LinearLayout
  {
    protected readonly GroupBaseActivity _groupBaseActivity;

    protected GroupListItemView(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    public GroupListItemView(GroupBaseActivity context)
        : base(context)
    {
      _groupBaseActivity = context;

    }

    public GroupListItemView(Context context, IAttributeSet attrs)
        : base(context, attrs)
    {
    }

    public GroupListItemView(Context context, IAttributeSet attrs, int defStyleAttr)
        : base(context, attrs, defStyleAttr)
    {
    }

    public GroupListItemView(Context context, IAttributeSet attrs, int defStyleAttr, int defStyleRes)
        : base(context, attrs, defStyleAttr, defStyleRes)
    {
    }


    public override bool Activated
    {
      get { return base.Activated; }
      set
      {
        if (value)
        {
          FindViewById(Resource.Id.icon).Visibility = ViewStates.Invisible;
          FindViewById(Resource.Id.check_mark).Visibility = ViewStates.Visible;
        }
        else
        {
          FindViewById(Resource.Id.icon).Visibility = ViewStates.Visible;
          FindViewById(Resource.Id.check_mark).Visibility = ViewStates.Invisible;
        }

        base.Activated = value;
      }
    }

    public void SetRightArrowVisibility(bool visible)
    {
      FindViewById(Resource.Id.right_arrow).Visibility = visible ? ViewStates.Visible : ViewStates.Invisible;
    }

    public abstract void OnClick();

  }
}
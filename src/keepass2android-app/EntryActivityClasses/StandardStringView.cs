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
using Android.App;
using Android.Views;
using Android.Widget;

namespace keepass2android
{
    internal class StandardStringView : IStringView
    {
        private readonly List<int> _viewIds;
        private readonly int _containerViewId;
        private readonly Activity _activity;

        public StandardStringView(List<int> viewIds, int containerViewId, Activity activity)
        {
            _viewIds = viewIds;
            _containerViewId = containerViewId;
            _activity = activity;
        }

        public string Text
        {
            set
            {
                View container = _activity.FindViewById(_containerViewId);
                foreach (int viewId in _viewIds)
                {
                    TextView tv = (TextView)_activity.FindViewById(viewId);
                    if (String.IsNullOrEmpty(value))
                    {
                        container.Visibility = tv.Visibility = ViewStates.Gone;
                    }
                    else
                    {
                        container.Visibility = tv.Visibility = ViewStates.Visible;
                        tv.Text = value;
                    }
                }
            }
            get
            {
                TextView tv = (TextView)_activity.FindViewById(_viewIds.First());
                return tv.Text;
            }
        }
    }
}
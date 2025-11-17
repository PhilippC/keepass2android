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
using Android.Views;
using Android.Widget;

namespace keepass2android
{
  internal class ExtraStringView : IStringView
  {
    private readonly View _container;
    private readonly TextView _valueView;
    private readonly TextView _visibleValueView;
    private readonly TextView _keyView;

    public ExtraStringView(LinearLayout container, TextView valueView, TextView visibleValueView, TextView keyView)
    {
      _container = container;
      _valueView = valueView;
      _visibleValueView = visibleValueView;
      _keyView = keyView;
    }

    public View View
    {
      get { return _container; }
    }

    public string Text
    {
      get { return _valueView.Text; }
      set
      {
        if (String.IsNullOrEmpty(value))
        {
          _container.Visibility = ViewStates.Gone;
        }
        else
        {
          _container.Visibility = ViewStates.Visible;
          _valueView.Text = value;
          if (_visibleValueView != null)
            _visibleValueView.Text = value;

        }
      }
    }
  }
}
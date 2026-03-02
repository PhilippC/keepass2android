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
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using keepass2android;

namespace keepass2android.views
{
  public class TextWithHelp : RelativeLayout
  {
    private Kp2aShortHelpView _kp2AShortHelpView;

    public TextWithHelp(Context context, IAttributeSet attrs) :
        base(context, attrs)
    {
      Initialize(attrs);
    }

    public TextWithHelp(Context context, string text, string helpText) :
        base(context, null)
    {
      Initialize(text, helpText);
    }


    public TextWithHelp(Context context, IAttributeSet attrs, int defStyle) :
        base(context, attrs, defStyle)
    {
      Initialize(attrs);
    }

    private void Initialize(IAttributeSet attrs)
    {

      TypedArray a = Context.ObtainStyledAttributes(attrs, Resource.Styleable.TextWithHelp);

      string helpText = a.GetString(Resource.Styleable.TextWithHelp_help_text);

      const string xmlns = "http://schemas.android.com/apk/res/android";
      string text = Context.GetString(attrs.GetAttributeResourceValue(xmlns, "text", Resource.String.ellipsis));
      Initialize(text, helpText);
    }

    private void Initialize(string text, string helpText)
    {
      LayoutInflater inflater = (LayoutInflater)Context.GetSystemService(Context.LayoutInflaterService);
      inflater.Inflate(Resource.Layout.text_with_help, this);

      _kp2AShortHelpView = ((Kp2aShortHelpView)FindViewById(Resource.Id.help));
      _kp2AShortHelpView.HelpText = helpText;

      ((TextView)FindViewById(Resource.Id.text)).Text = text;
    }

    public string HelpText
    {
      get { return _kp2AShortHelpView.HelpText; }
      set { _kp2AShortHelpView.HelpText = value; }
    }
  }
}
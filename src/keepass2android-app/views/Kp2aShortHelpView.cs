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
using Android.Graphics;
using Android.Runtime;
using Android.Text;
using Android.Text.Method;
using Android.Text.Style;
using Android.Text.Util;
using Android.Util;
using Android.Views;
using Android.Widget;
using Google.Android.Material.Dialog;
using keepass2android;
using KeePassLib.Interfaces;

namespace keepass2android.views
{
  public class Kp2aShortHelpView : TextView
  {
    private string _helpText;
    private static Typeface _iconFont;

    protected Kp2aShortHelpView(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }

    public Kp2aShortHelpView(Context context) : base(context)
    {
    }

    public Kp2aShortHelpView(Context context, IAttributeSet attrs) : base(context, attrs)
    {
      Initialize(attrs);
    }

    public Kp2aShortHelpView(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
    {
      Initialize(attrs);
    }

    public string HelpText
    {
      get { return _helpText; }
      set
      {
        _helpText = value;
        UpdateView();
      }
    }

    public string TitleText { get; set; }

    private void UpdateView()
    {
      if (!String.IsNullOrEmpty(_helpText))
      {
        Text = Context.GetString(Resource.String.icon_info);
        Clickable = true;


        if (_iconFont == null)
          _iconFont = Typeface.CreateFromAsset(Context.Assets, "fontawesome-webfont.ttf");

        System.Diagnostics.Debug.Assert(_iconFont != null, "_iconFont != null");

        SetTypeface(_iconFont, TypefaceStyle.Normal);
        //TextFormatted = Html.FromHtml("<a>" + Text + "</a>");
        //var spannableString = new SpannableString(Text);
        //spannableString.SetSpan(new UnderlineSpan(), 0, Text.Length, SpanTypes.ExclusiveExclusive);
        //TextFormatted = spannableString;

        MovementMethod = LinkMovementMethod.Instance;
        Click += (sender, args) =>
        {
          string title = Context.GetString(AppNames.AppNameResource);
          if (!string.IsNullOrEmpty(TitleText))
            title = TitleText;
          new MaterialAlertDialogBuilder(Context)
                      .SetTitle(title)
                      .SetMessage(_helpText)
                      .SetPositiveButton(Android.Resource.String.Ok, (o, eventArgs) => { })
                  .Show();
        };
        Visibility = ViewStates.Visible;
      }
      else
      {
        Visibility = ViewStates.Gone;
      }
    }

    void Initialize(IAttributeSet attrs)
    {
      TypedArray a = Context.ObtainStyledAttributes(
   attrs,
   Resource.Styleable.Kp2aShortHelpView);
      TitleText = a.GetString(Resource.Styleable.Kp2aShortHelpView_title_text);
      HelpText = a.GetString(Resource.Styleable.Kp2aShortHelpView_help_text);

    }
  }
}
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
using Android.Content.PM;

namespace keepass2android
{
  [Activity(Label = "@string/app_name",
      ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden,
      Theme = "@style/Kp2aTheme_ActionBar",
      Exported = true)]
  [IntentFilter(new[] { "kp2a.action.AboutActivity" }, Categories = new[] { Intent.CategoryDefault })]
  public class AboutActivity : Activity, IDialogInterfaceOnDismissListener
  {
    private AboutDialog _dialog;

    protected override void OnResume()
    {
      if ((_dialog == null) || (_dialog.IsShowing == false))
      {
        _dialog = new AboutDialog(this);
        _dialog.SetOnDismissListener(this);
        _dialog.Show();
      }
      base.OnResume();
    }

    public void OnDismiss(IDialogInterface dialog)
    {
      Finish();
    }
  }
}

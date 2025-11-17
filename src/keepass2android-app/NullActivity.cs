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

using Android.Content.PM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace keepass2android
{
    [Activity(Label = AppNames.AppName,
        MainLauncher = false,
        Theme = "@style/Kp2aTheme_BlueNoActionBar",
        Exported = true)]
    ///For autofill, we sometimes need to pass an intent to an inline presentation which never gets fired. We use this as a dummy activity.
    public class NullActivity : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            Kp2aLog.Log("NullActivity.OnCreate - this is unexpected.");
            base.OnCreate(savedInstanceState);
            Finish();
        }
    }
}

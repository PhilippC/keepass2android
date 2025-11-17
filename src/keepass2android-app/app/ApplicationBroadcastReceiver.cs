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
using System.Linq;
using Android.Content;
using Android.App;
using Android.Preferences;


namespace keepass2android
{
  [BroadcastReceiver]
  public class ApplicationBroadcastReceiver : BroadcastReceiver
  {
    public override void OnReceive(Context context, Intent intent)
    {
      Kp2aLog.Log("Received broadcast intent: " + intent.Action);

      switch (intent.Action)
      {
        case Intents.LockDatabaseByTimeout:
          App.Kp2a.Lock(true, true);
          break;
        case Intents.LockDatabase:
          App.Kp2a.Lock();
          break;
        case Intents.CloseDatabase:
          App.Kp2a.Lock(false /*no quick unlock*/);
          break;
      }
    }
  }
}
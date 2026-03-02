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
using Android.App;
using Android.Content;
using Android.OS;
using KeePassLib.Serialization;

namespace keepass2android.Io
{
  public interface IFileStorageSetupInitiatorActivity
  {
    void StartSelectFileProcess(IOConnectionInfo ioc, bool isForSave, int requestCode);
    void StartFileUsageProcess(IOConnectionInfo ioc, int requestCode, bool alwaysReturnSuccess);
    void OnImmediateResult(int requestCode, int result, Intent intent);

    Activity Activity { get; }

    void IocToIntent(Intent intent, IOConnectionInfo ioc);
    void PerformManualFileSelect(bool isForSave, int requestCode, string protocolId);
  }
}
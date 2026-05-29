// Derived from KeePassDX (https://github.com/Kunzisoft/KeePassDX)
// Original work Copyright 2025 Jeremy Jamet / Kunzisoft.
// Licensed under the GNU General Public License v3 or later.
//
// Modifications Copyright 2026 Philipp Crocoll.
// This file is part of Keepass2Android.
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
using Android.OS;
using Java.Interop;

namespace Kp2aPasskey.Core
{
  /// <summary>
  /// Data class for FIDO2/WebAuthn passkey credentials
  /// </summary>
  [Android.Runtime.Preserve(AllMembers = true)]
  public class PasskeyData : Java.Lang.Object, IParcelable
  {
    public string Username { get; set; } = string.Empty;
    public string PrivateKeyPem { get; set; } = string.Empty;
    public string CredentialId { get; set; } = string.Empty;
    public string UserHandle { get; set; } = string.Empty;
    public string RelyingParty { get; set; } = string.Empty;
    public bool? BackupEligibility { get; set; }
    public bool? BackupState { get; set; }

    public PasskeyData()
    {
    }

    public PasskeyData(Parcel parcel)
    {
      Username = parcel.ReadString() ?? string.Empty;
      PrivateKeyPem = parcel.ReadString() ?? string.Empty;
      CredentialId = parcel.ReadString() ?? string.Empty;
      UserHandle = parcel.ReadString() ?? string.Empty;
      RelyingParty = parcel.ReadString() ?? string.Empty;

      var hasBackupEligibility = parcel.ReadInt() == 1;
      BackupEligibility = hasBackupEligibility ? parcel.ReadInt() == 1 : null;

      var hasBackupState = parcel.ReadInt() == 1;
      BackupState = hasBackupState ? parcel.ReadInt() == 1 : null;
    }

    public void WriteToParcel(Parcel dest, ParcelableWriteFlags flags)
    {
      dest.WriteString(Username);
      dest.WriteString(PrivateKeyPem);
      dest.WriteString(CredentialId);
      dest.WriteString(UserHandle);
      dest.WriteString(RelyingParty);

      dest.WriteInt(BackupEligibility.HasValue ? 1 : 0);
      if (BackupEligibility.HasValue)
        dest.WriteInt(BackupEligibility.Value ? 1 : 0);

      dest.WriteInt(BackupState.HasValue ? 1 : 0);
      if (BackupState.HasValue)
        dest.WriteInt(BackupState.Value ? 1 : 0);
    }

    public int DescribeContents() => 0;

    [ExportField("CREATOR")]
    public static ParcelableCreator InitializeCreator()
    {
      return new ParcelableCreator();
    }

    public class ParcelableCreator : Java.Lang.Object, IParcelableCreator
    {
      public Java.Lang.Object CreateFromParcel(Parcel source)
      {
        return new PasskeyData(source);
      }

      public Java.Lang.Object[]? NewArray(int size)
      {
        return new PasskeyData[size];
      }
    }
  }
}

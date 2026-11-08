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

namespace Kp2aPasskey.Core
{
  /// <summary>
  /// Data class for FIDO2/WebAuthn passkey credentials.
  /// Contains the private key, so it is deliberately not Parcelable: it must not be put into Intents or Bundles.
  /// </summary>
  public class PasskeyData
  {
    public string Username { get; set; } = string.Empty;
    public string PrivateKeyPem { get; set; } = string.Empty;
    public string CredentialId { get; set; } = string.Empty;
    public string UserHandle { get; set; } = string.Empty;
    public string RelyingParty { get; set; } = string.Empty;
    public bool? BackupEligibility { get; set; }
    public bool? BackupState { get; set; }
  }
}

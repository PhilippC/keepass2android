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
using System.Linq;
using KeePassLib;
using KeePassLib.Security;
using Org.Json;

namespace Kp2aPasskey.Core
{
  /// <summary>
  /// Helper class for storing and retrieving passkey data in KeePass entries.
  /// Passkeys are stored in ExtraFields (Strings) using the KPEX interop format,
  /// compatible with KeePassXC and other apps supporting KPEX passkey fields.
  /// </summary>
  public static class PasskeyStorage
  {
    // ExtraField keys for passkey storage — KPEX interop format
    // KPEX prefix (KeePass EXtension) is the established interop standard for passkey fields
    public const string FIELD_USERNAME = "KPEX_PASSKEY_USERNAME";
    public const string FIELD_PRIVATE_KEY = "KPEX_PASSKEY_PRIVATE_KEY_PEM";
    public const string FIELD_CREDENTIAL_ID = "KPEX_PASSKEY_CREDENTIAL_ID";
    public const string FIELD_USER_HANDLE = "KPEX_PASSKEY_USER_HANDLE";
    public const string FIELD_RELYING_PARTY = "KPEX_PASSKEY_RELYING_PARTY";
    public const string FIELD_FLAG_BE = "KPEX_PASSKEY_FLAG_BE";
    public const string FIELD_FLAG_BS = "KPEX_PASSKEY_FLAG_BS";
    public const string PASSKEY_TAG = "Passkey";

    // Fields that must be stored encrypted (ProtectInMemory) in the KeePass entry
    public static readonly IReadOnlyList<string> ProtectedFields =
      [FIELD_PRIVATE_KEY, FIELD_CREDENTIAL_ID, FIELD_USER_HANDLE];


    public static JSONObject CreatePasskeyFieldsJson(string relyingParty, string username, PasskeyData passkey)
    {
      // Build AllFields JSON with passkey data in extra fields (compatible with KeePassDX/KeePassXC)
      // Start with standard entry fields
      var passkeyFieldsJson = new JSONObject();
      passkeyFieldsJson.Put(PwDefs.TitleField, $"Passkey for {relyingParty}");
      passkeyFieldsJson.Put(PwDefs.UserNameField, username);
      passkeyFieldsJson.Put(PwDefs.UrlField, relyingParty);

      // Add passkey extra fields
      passkeyFieldsJson.Put(PasskeyStorage.FIELD_USERNAME, passkey.Username);
      passkeyFieldsJson.Put(PasskeyStorage.FIELD_PRIVATE_KEY, passkey.PrivateKeyPem);
      passkeyFieldsJson.Put(PasskeyStorage.FIELD_CREDENTIAL_ID, passkey.CredentialId);
      passkeyFieldsJson.Put(PasskeyStorage.FIELD_USER_HANDLE, passkey.UserHandle);
      passkeyFieldsJson.Put(PasskeyStorage.FIELD_RELYING_PARTY, passkey.RelyingParty);
      if (passkey.BackupEligibility.HasValue)
        passkeyFieldsJson.Put(PasskeyStorage.FIELD_FLAG_BE, passkey.BackupEligibility.Value ? "1" : "0");
      if (passkey.BackupState.HasValue)
        passkeyFieldsJson.Put(PasskeyStorage.FIELD_FLAG_BS, passkey.BackupState.Value ? "1" : "0");
      return passkeyFieldsJson;
    }
    /// <summary>
    /// Retrieve passkey data from a KeePass entry
    /// </summary>
    public static PasskeyData? RetrievePasskey(PwEntry entry)
    {
      if (entry == null)
        return null;

      // Check if entry contains passkey data (must have at minimum private key and credential ID)
      if (!entry.Strings.Exists(FIELD_PRIVATE_KEY) ||
          !entry.Strings.Exists(FIELD_CREDENTIAL_ID))
      {
        return null;
      }

      var username = entry.Strings.ReadSafe(FIELD_USERNAME);
      var privateKeyPem = entry.Strings.ReadSafe(FIELD_PRIVATE_KEY);
      var credentialId = entry.Strings.ReadSafe(FIELD_CREDENTIAL_ID);
      var userHandle = entry.Strings.ReadSafe(FIELD_USER_HANDLE);
      var relyingParty = entry.Strings.ReadSafe(FIELD_RELYING_PARTY);

      // All required fields must be present
      if (string.IsNullOrEmpty(username) ||
          string.IsNullOrEmpty(privateKeyPem) ||
          string.IsNullOrEmpty(credentialId) ||
          string.IsNullOrEmpty(userHandle) ||
          string.IsNullOrEmpty(relyingParty))
      {
        return null;
      }

      var passkey = new PasskeyData
      {
        Username = username,
        PrivateKeyPem = privateKeyPem,
        CredentialId = credentialId,
        UserHandle = userHandle,
        RelyingParty = relyingParty
      };

      // Parse optional boolean fields
      var backupEligibleStr = entry.Strings.ReadSafe(FIELD_FLAG_BE);
      if (!string.IsNullOrEmpty(backupEligibleStr))
        passkey.BackupEligibility = ParseBool(backupEligibleStr);

      var backupStateStr = entry.Strings.ReadSafe(FIELD_FLAG_BS);
      if (!string.IsNullOrEmpty(backupStateStr))
        passkey.BackupState = ParseBool(backupStateStr);

      return passkey;
    }

    private static bool ParseBool(string value) =>
      value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Check if an entry contains passkey data
    /// </summary>
    public static bool HasPasskey(PwEntry entry)
    {
      return entry != null &&
             (entry.Tags.Contains(PASSKEY_TAG) ||
              entry.Strings.Exists(FIELD_USERNAME) ||
              entry.Strings.Exists(FIELD_PRIVATE_KEY) ||
              entry.Strings.Exists(FIELD_CREDENTIAL_ID) ||
              entry.Strings.Exists(FIELD_USER_HANDLE) ||
              entry.Strings.Exists(FIELD_RELYING_PARTY));
    }

    /// <summary>
    /// Remove passkey data from an entry
    /// </summary>
    public static void RemovePasskey(PwEntry entry)
    {
      if (entry == null)
        return;

      // Remove tag
      if (entry.Tags.Contains(PASSKEY_TAG))
      {
        var tags = new List<string>(entry.Tags);
        tags.Remove(PASSKEY_TAG);
        entry.Tags = tags;
      }

      // Remove all passkey fields
      entry.Strings.Remove(FIELD_USERNAME);
      entry.Strings.Remove(FIELD_PRIVATE_KEY);
      entry.Strings.Remove(FIELD_CREDENTIAL_ID);
      entry.Strings.Remove(FIELD_USER_HANDLE);
      entry.Strings.Remove(FIELD_RELYING_PARTY);
      entry.Strings.Remove(FIELD_FLAG_BE);
      entry.Strings.Remove(FIELD_FLAG_BS);
    }
  }
}

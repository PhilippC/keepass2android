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

    /// <summary>
    /// Store passkey data in a KeePass entry using ExtraFields (Strings)
    /// </summary>
    public static void StorePasskey(PwEntry entry, PasskeyData passkey)
    {
      if (entry == null || passkey == null)
        throw new ArgumentNullException();

      // Add Passkey tag
      if (!entry.Tags.Contains(PASSKEY_TAG))
      {
        var tags = new List<string>(entry.Tags);
        tags.Add(PASSKEY_TAG);
        entry.Tags = tags;
      }

      // Store passkey data in ExtraFields (Strings)
      entry.Strings.Set(FIELD_USERNAME, new ProtectedString(false, passkey.Username));
      entry.Strings.Set(FIELD_PRIVATE_KEY, new ProtectedString(true, passkey.PrivateKeyPem)); // Protected
      entry.Strings.Set(FIELD_CREDENTIAL_ID, new ProtectedString(true, passkey.CredentialId)); // Protected
      entry.Strings.Set(FIELD_USER_HANDLE, new ProtectedString(true, passkey.UserHandle)); // Protected
      entry.Strings.Set(FIELD_RELYING_PARTY, new ProtectedString(false, passkey.RelyingParty));

      if (passkey.BackupEligibility.HasValue)
        entry.Strings.Set(FIELD_FLAG_BE, new ProtectedString(false, passkey.BackupEligibility.Value.ToString().ToLower()));

      if (passkey.BackupState.HasValue)
        entry.Strings.Set(FIELD_FLAG_BS, new ProtectedString(false, passkey.BackupState.Value.ToString().ToLower()));

      // Also store the username in the standard username field if not already set
      if (string.IsNullOrEmpty(entry.Strings.ReadSafe(PwDefs.UserNameField)))
      {
        entry.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, passkey.Username));
      }

      // Add passkey: URL for searchability
      var url = entry.Strings.ReadSafe(PwDefs.UrlField);
      var passkeyUrl = $"passkey:{passkey.RelyingParty}";
      if (!url.Contains(passkeyUrl))
      {
        var newUrl = string.IsNullOrEmpty(url) ? passkeyUrl : $"{url}\n{passkeyUrl}";
        entry.Strings.Set(PwDefs.UrlField, new ProtectedString(false, newUrl));
      }
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
      if (!string.IsNullOrEmpty(backupEligibleStr) && bool.TryParse(backupEligibleStr, out var backupEligible))
      {
        passkey.BackupEligibility = backupEligible;
      }

      var backupStateStr = entry.Strings.ReadSafe(FIELD_FLAG_BS);
      if (!string.IsNullOrEmpty(backupStateStr) && bool.TryParse(backupStateStr, out var backupState))
      {
        passkey.BackupState = backupState;
      }

      return passkey;
    }

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

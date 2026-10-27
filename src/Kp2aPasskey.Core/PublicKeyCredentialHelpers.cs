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
using System.Collections.Generic;
using Org.Json;

namespace Kp2aPasskey.Core
{
  /// <summary>
  /// WebAuthn user verification requirement.
  /// https://www.w3.org/TR/webauthn-3/#enumdef-userverificationrequirement
  /// </summary>
  public enum UserVerificationRequirement
  {
    Required,
    Preferred,
    Discouraged
  }

  public static class UserVerificationRequirementExtensions
  {
    public static string ToWebAuthnString(this UserVerificationRequirement value)
    {
      return value switch
      {
        UserVerificationRequirement.Required => "required",
        UserVerificationRequirement.Preferred => "preferred",
        UserVerificationRequirement.Discouraged => "discouraged",
        _ => "preferred"
      };
    }

    public static UserVerificationRequirement FromString(string? value)
    {
      if (string.IsNullOrEmpty(value)) return UserVerificationRequirement.Preferred;
      var v = value.Trim().ToLowerInvariant();
      return v switch
      {
        "required" => UserVerificationRequirement.Required,
        "preferred" => UserVerificationRequirement.Preferred,
        "discouraged" => UserVerificationRequirement.Discouraged,
        _ => UserVerificationRequirement.Preferred
      };
    }
  }

  /// <summary>
  /// Helper class to parse PublicKeyCredentialRequestOptions from JSON.
  /// https://www.w3.org/TR/webauthn-3/#dictdef-publickeycredentialrequestoptions
  /// </summary>
  public class PublicKeyCredentialRequestOptions
  {
    public JSONObject Json { get; }
    public byte[] Challenge { get; }
    public long Timeout { get; }
    public string RpId { get; }
    public string UserVerification { get; }
    public List<PublicKeyCredentialDescriptor> AllowCredentials { get; }

    /// <summary>
    /// Parsed user verification requirement; defaults to Preferred if unknown.
    /// </summary>
    public UserVerificationRequirement UserVerificationRequirement =>
      UserVerificationRequirementExtensions.FromString(UserVerification);

    public PublicKeyCredentialRequestOptions(string requestJson)
    {
      Json = new JSONObject(requestJson);
      Challenge = Android.Util.Base64.Decode(
        Json.GetString("challenge"),
        Android.Util.Base64Flags.UrlSafe | Android.Util.Base64Flags.NoPadding
      );
      Timeout = Json.OptLong("timeout", 0);
      RpId = Json.OptString("rpId", "");
      UserVerification = Json.OptString("userVerification", "preferred");
      AllowCredentials = ParseAllowCredentials(Json);
    }

    private static List<PublicKeyCredentialDescriptor> ParseAllowCredentials(JSONObject json)
    {
      var allowCredentials = new List<PublicKeyCredentialDescriptor>();
      try
      {
        var allowCredentialsArray = json.OptJSONArray("allowCredentials");
        if (allowCredentialsArray != null)
        {
          for (int i = 0; i < allowCredentialsArray.Length(); i++)
          {
            var credentialJson = allowCredentialsArray.GetJSONObject(i);
            var type = credentialJson?.OptString("type", "public-key") ?? "public-key";
            var idBase64 = credentialJson?.GetString("id");
            if (idBase64 == null) continue;

            var id = Android.Util.Base64.Decode(
              idBase64,
              Android.Util.Base64Flags.UrlSafe | Android.Util.Base64Flags.NoPadding
            );
            if (id == null) continue;

            // Parse optional transports array
            var transports = new List<string>();
            var transportsArray = credentialJson.OptJSONArray("transports");
            if (transportsArray != null)
            {
              for (int j = 0; j < transportsArray.Length(); j++)
              {
                var transport = transportsArray.GetString(j);
                if (transport != null)
                {
                  transports.Add(transport);
                }
              }
            }

            allowCredentials.Add(new PublicKeyCredentialDescriptor(type, id, transports));
          }
        }
      }
      catch (Exception)
      {
        // If parsing fails, return empty list (no filtering)
      }
      return allowCredentials;
    }
  }

  /// <summary>
  /// Helper class to parse PublicKeyCredentialCreationOptions from JSON.
  /// https://www.w3.org/TR/webauthn-3/#dictdef-publickeycredentialcreationoptions
  /// </summary>
  public class PublicKeyCredentialCreationOptions
  {
    public JSONObject Json { get; }
    public PublicKeyCredentialRpEntity RelyingPartyEntity { get; }
    public PublicKeyCredentialUserEntity UserEntity { get; }
    public byte[] Challenge { get; }
    public List<PublicKeyCredentialParameters> PubKeyCredParams { get; }
    public long Timeout { get; }
    public List<PublicKeyCredentialDescriptor> ExcludeCredentials { get; }
    public AuthenticatorSelectionCriteria AuthenticatorSelection { get; }
    public string Attestation { get; }
    public byte[]? ClientDataHash { get; }

    /// <summary>
    /// User verification requirement from authenticatorSelection; defaults to Preferred if unknown.
    /// </summary>
    public UserVerificationRequirement UserVerificationRequirement =>
      UserVerificationRequirementExtensions.FromString(AuthenticatorSelection?.UserVerification);

    public PublicKeyCredentialCreationOptions(string requestJson, byte[]? clientDataHash)
    {
      Json = new JSONObject(requestJson);
      ClientDataHash = clientDataHash;

      // Parse relying party
      var rpJson = Json.GetJSONObject("rp");
      RelyingPartyEntity = new PublicKeyCredentialRpEntity(
        rpJson.GetString("name"),
        rpJson.GetString("id")
      );

      // Parse user
      var userJson = Json.GetJSONObject("user");
      var userId = Android.Util.Base64.Decode(
        userJson.GetString("id"),
        Android.Util.Base64Flags.UrlSafe | Android.Util.Base64Flags.NoPadding
      );
      UserEntity = new PublicKeyCredentialUserEntity(
        userJson.GetString("name"),
        userId,
        userJson.GetString("displayName")
      );

      // Parse challenge
      Challenge = Android.Util.Base64.Decode(
        Json.GetString("challenge"),
        Android.Util.Base64Flags.UrlSafe | Android.Util.Base64Flags.NoPadding
      );

      // Parse pubKeyCredParams
      var pubKeyCredParamsJson = Json.GetJSONArray("pubKeyCredParams");
      var pubKeyCredParamsList = new List<PublicKeyCredentialParameters>();
      for (int i = 0; i < pubKeyCredParamsJson.Length(); i++)
      {
        var e = pubKeyCredParamsJson.GetJSONObject(i);
        pubKeyCredParamsList.Add(
          new PublicKeyCredentialParameters(
            e.GetString("type"),
            e.GetLong("alg")
          )
        );
      }
      PubKeyCredParams = pubKeyCredParamsList;

      // Parse optional fields
      Timeout = Json.OptLong("timeout", 0);
      ExcludeCredentials = new List<PublicKeyCredentialDescriptor>();
      AuthenticatorSelection = ParseAuthenticatorSelection(Json);
      Attestation = Json.OptString("attestation", "none");
    }

    private static AuthenticatorSelectionCriteria ParseAuthenticatorSelection(JSONObject json)
    {
      try
      {
        var sel = json.OptJSONObject("authenticatorSelection");
        if (sel == null)
          return new AuthenticatorSelectionCriteria("platform", "required", false, "preferred");
        var attachment = sel.OptString("authenticatorAttachment", "platform");
        var residentKey = sel.OptString("residentKey", "required");
        var requireResidentKey = sel.OptBoolean("requireResidentKey", false);
        var userVerification = sel.OptString("userVerification", "preferred");
        return new AuthenticatorSelectionCriteria(attachment, residentKey, requireResidentKey, userVerification);
      }
      catch
      {
        return new AuthenticatorSelectionCriteria("platform", "required", false, "preferred");
      }
    }
  }

  public class PublicKeyCredentialRpEntity
  {
    public string Name { get; }
    public string Id { get; }

    public PublicKeyCredentialRpEntity(string name, string id)
    {
      Name = name;
      Id = id;
    }
  }

  public class PublicKeyCredentialUserEntity
  {
    public string Name { get; }
    public byte[] Id { get; }
    public string DisplayName { get; }

    public PublicKeyCredentialUserEntity(string name, byte[] id, string displayName)
    {
      Name = name;
      Id = id;
      DisplayName = displayName;
    }
  }

  public class PublicKeyCredentialParameters
  {
    public string Type { get; }
    public long Alg { get; }

    public PublicKeyCredentialParameters(string type, long alg)
    {
      Type = type;
      Alg = alg;
    }
  }

  public class PublicKeyCredentialDescriptor
  {
    public string Type { get; }
    public byte[] Id { get; }
    public List<string> Transports { get; }

    public PublicKeyCredentialDescriptor(string type, byte[] id, List<string> transports)
    {
      Type = type;
      Id = id;
      Transports = transports;
    }
  }

  public class AuthenticatorSelectionCriteria
  {
    public string AuthenticatorAttachment { get; }
    public string ResidentKey { get; }
    public bool RequireResidentKey { get; }
    public string UserVerification { get; }

    public AuthenticatorSelectionCriteria(
      string authenticatorAttachment,
      string residentKey,
      bool requireResidentKey = false,
      string userVerification = "preferred"
    )
    {
      AuthenticatorAttachment = authenticatorAttachment;
      ResidentKey = residentKey;
      RequireResidentKey = requireResidentKey;
      UserVerification = userVerification;
    }
  }
}

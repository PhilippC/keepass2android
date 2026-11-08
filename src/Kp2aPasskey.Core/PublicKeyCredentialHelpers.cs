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
  /// Thrown when a caller-supplied WebAuthn request is invalid (missing/invalid relying party id,
  /// oversized fields, undecodable values, ...). Callers handle it like any other parsing error
  /// and return a failure / no-credential response.
  /// </summary>
  public class PasskeyRequestValidationException : Exception
  {
    public PasskeyRequestValidationException(string message) : base(message)
    {
    }
  }

  internal static class WebAuthnRequestParsing
  {
    /// <summary>
    /// Returns the string value of <paramref name="key"/>, or null if the key is missing or JSON null.
    /// (Org.Json's OptString returns "null" for JSON null values.)
    /// </summary>
    public static string? OptStringOrNull(JSONObject json, string key)
    {
      if (!json.Has(key) || json.IsNull(key))
        return null;
      return json.OptString(key, null!);
    }

    /// <summary>
    /// Determines the relying party id per WebAuthn: the explicitly given rpId if present, otherwise the
    /// effective domain of the caller origin. Throws <see cref="PasskeyRequestValidationException"/> if
    /// neither is available. <paramref name="getCallerOrigin"/> is only called if the request has no rpId.
    /// </summary>
    public static string ResolveRpId(string? requestedRpId, Func<string?>? getCallerOrigin)
    {
      if (!string.IsNullOrWhiteSpace(requestedRpId))
        return requestedRpId;

      var effectiveDomain = TryGetEffectiveDomain(getCallerOrigin?.Invoke());
      if (effectiveDomain != null)
        return effectiveDomain;

      throw new PasskeyRequestValidationException(
        "Passkey request does not specify a relying party id and it cannot be derived from the caller origin.");
    }

    /// <summary>
    /// Returns the effective domain (host) of a web origin like https://example.com, or null if the
    /// origin is not a secure web origin with a DNS host name (e.g. android:apk-key-hash:... or an IP address).
    /// </summary>
    public static string? TryGetEffectiveDomain(string? origin)
    {
      if (string.IsNullOrWhiteSpace(origin))
        return null;
      if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        return null;
      if (uri.HostNameType != UriHostNameType.Dns)
        return null;
      bool secure = uri.Scheme == Uri.UriSchemeHttps
                    || (uri.Scheme == Uri.UriSchemeHttp && string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
      if (!secure)
        return null;
      var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
      return string.IsNullOrEmpty(host) ? null : host;
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

    /// <param name="requestJson">The caller-supplied request JSON.</param>
    /// <param name="getCallerOrigin">Returns the verified web origin of the caller (privileged browser), or null.
    /// Only called if the request has no rpId; the rpId is then derived from the origin. If no rpId can be
    /// determined, <see cref="PasskeyRequestValidationException"/> is thrown.</param>
    public PublicKeyCredentialRequestOptions(string requestJson, Func<string?>? getCallerOrigin = null)
    {
      Json = new JSONObject(requestJson);
      Challenge = Android.Util.Base64.Decode(
        Json.GetString("challenge"),
        Android.Util.Base64Flags.UrlSafe | Android.Util.Base64Flags.NoPadding
      );
      Timeout = Json.OptLong("timeout", 0);
      RpId = WebAuthnRequestParsing.ResolveRpId(
        WebAuthnRequestParsing.OptStringOrNull(Json, "rpId"), getCallerOrigin);
      UserVerification = Json.OptString("userVerification", "preferred");
      AllowCredentials = ParseAllowCredentials(Json);
      AllowCredentialsSpecified = IsAllowCredentialsSpecified(Json);
    }

    /// <summary>
    /// True if the request restricts the credentials, i.e. allowCredentials is a non-empty list (or present but
    /// malformed). Then only credentials in <see cref="AllowCredentials"/> may be used, even if that list ended up
    /// empty because no descriptor could be parsed (fail closed).
    /// </summary>
    public bool AllowCredentialsSpecified { get; }

    /// <summary>
    /// Returns true if the credential with the given stored ID (base64url) may be used for this request.
    /// </summary>
    public bool IsCredentialAllowed(string? credentialIdBase64Url)
    {
      if (!AllowCredentialsSpecified)
        return true;
      if (string.IsNullOrEmpty(credentialIdBase64Url))
        return false;

      byte[]? credentialId;
      try
      {
        credentialId = Android.Util.Base64.Decode(credentialIdBase64Url, Android.Util.Base64Flags.UrlSafe);
      }
      catch (Exception)
      {
        return false;
      }
      if (credentialId == null || credentialId.Length == 0)
        return false;

      return AllowCredentials.Any(d => d.Id != null && d.Id.AsSpan().SequenceEqual(credentialId));
    }

    private static bool IsAllowCredentialsSpecified(JSONObject json)
    {
      if (!json.Has("allowCredentials") || json.IsNull("allowCredentials"))
        return false;
      var array = json.OptJSONArray("allowCredentials");
      // Not an array: malformed, treat as restricting.
      return array == null || array.Length() > 0;
    }

    private static List<PublicKeyCredentialDescriptor> ParseAllowCredentials(JSONObject json)
    {
      var allowCredentials = new List<PublicKeyCredentialDescriptor>();
      var allowCredentialsArray = json.OptJSONArray("allowCredentials");
      if (allowCredentialsArray == null)
        return allowCredentials;

      for (int i = 0; i < allowCredentialsArray.Length(); i++)
      {
        // A malformed descriptor is skipped on its own; filtering stays active (see AllowCredentialsSpecified).
        try
        {
          var credentialJson = allowCredentialsArray.GetJSONObject(i);
          var type = credentialJson.OptString("type", "public-key");
          if (type != "public-key") continue;

          var id = Android.Util.Base64.Decode(credentialJson.GetString("id"), Android.Util.Base64Flags.UrlSafe);
          if (id == null || id.Length == 0) continue;

          // Parse optional transports array
          var transports = new List<string>();
          var transportsArray = credentialJson.OptJSONArray("transports");
          if (transportsArray != null)
          {
            for (int j = 0; j < transportsArray.Length(); j++)
            {
              var transport = transportsArray.OptString(j, null);
              if (transport != null)
              {
                transports.Add(transport);
              }
            }
          }

          allowCredentials.Add(new PublicKeyCredentialDescriptor(type, id, transports));
        }
        catch (Exception)
        {
          // skip this descriptor
        }
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

    /// <param name="requestJson">The caller-supplied request JSON.</param>
    /// <param name="clientDataHash">The caller-supplied clientDataHash, if any.</param>
    /// <param name="getCallerOrigin">Returns the verified web origin of the caller (privileged browser), or null.
    /// Only called if the request has no rp.id; rp.id is then derived from the origin. If no rp.id can be
    /// determined, <see cref="PasskeyRequestValidationException"/> is thrown.</param>
    public PublicKeyCredentialCreationOptions(string requestJson, byte[]? clientDataHash, Func<string?>? getCallerOrigin = null)
    {
      Json = new JSONObject(requestJson);
      ClientDataHash = clientDataHash;

      // Parse relying party
      var rpJson = Json.GetJSONObject("rp");
      RelyingPartyEntity = new PublicKeyCredentialRpEntity(
        rpJson.GetString("name"),
        WebAuthnRequestParsing.ResolveRpId(WebAuthnRequestParsing.OptStringOrNull(rpJson, "id"), getCallerOrigin)
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

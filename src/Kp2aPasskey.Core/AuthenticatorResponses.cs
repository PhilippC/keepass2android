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

using System.Security.Cryptography;
using System.Text;
using Android.Util;
using Kp2aPasskey.Core;
using Org.Json;
using PeterO.Cbor;

namespace keepass2android.services.Kp2aCredentialProvider.Passkey
{
  /// <summary>
  /// Builds authenticator data structure for FIDO2/WebAuthn protocol.
  /// 
  /// This is a core component of the WebAuthn specification that creates cryptographic evidence
  /// during both passkey registration (attestation) and authentication (assertion).
  /// 
  /// The authenticator data proves:
  /// - The request came from the correct relying party (via RP ID hash)
  /// - User presence and verification status
  /// - Whether the credential is backed up and eligible for sync
  /// - For registration: includes attested credential data (AuthenticatorAttestationGuid + credential ID + public key)
  /// 
  /// Structure per https://www.w3.org/TR/webauthn-3/#table-authData:
  /// - rpIdHash: 32 bytes (SHA-256 of relying party ID)
  /// - flags: 1 byte (bit flags for UP/UV/BE/BS/AT/ED)
  /// - signCount: 4 bytes (signature counter, always 0 for keepass2android)
  /// - attestedCredentialData: variable length (only present when AT flag is set)
  /// - extensions: variable length (only present when ED flag is set)
  /// 
  /// Used by:
  /// - AuthenticatorAttestationResponse: During passkey creation
  /// - AuthenticatorAssertionResponse: During passkey authentication
  /// </summary>
  public static class AuthenticatorDataBuilder
  {
    /// <summary>
    /// Builds the authenticator data byte array per WebAuthn specification.
    /// </summary>
    /// <param name="relyingPartyId">The relying party identifier (typically domain name) as UTF-8 bytes</param>
    /// <param name="userPresent">True if user presence was verified (e.g., user interacted with device)</param>
    /// <param name="userVerified">True if user was cryptographically verified (biometric/PIN)</param>
    /// <param name="backupEligibility">True if credential can be backed up to other devices (BE flag)</param>
    /// <param name="backupState">True if credential is currently backed up (BS flag)</param>
    /// <param name="attestedCredentialData">True to set AT flag (indicates attested credential data follows)</param>
    /// <returns>Authenticator data byte array (37+ bytes)</returns>
    public static byte[] BuildAuthenticatorData(
      byte[] relyingPartyId,
      bool userPresent,
      bool userVerified,
      bool backupEligibility,
      bool backupState,
      bool attestedCredentialData = false
    )
    {
      // Build flags byte per WebAuthn spec
      // https://www.w3.org/TR/webauthn-3/#table-authData
      byte flags = 0;

      // Bit 0: User Present (UP) - User interacted with authenticator
      if (userPresent)
        flags |= 0x01;

      // Bit 2: User Verified (UV) - User verified via biometric/PIN
      if (userVerified)
        flags |= 0x04;

      // Bit 3: Backup Eligibility (BE) - Credential can be backed up
      // Indicates if this credential can be synced to other devices
      if (backupEligibility)
        flags |= 0x08;

      // Bit 4: Backup State (BS) - Credential is currently backed up
      // Indicates if this credential is currently synced/backed up
      if (backupState)
        flags |= 0x10;

      // Bit 6: Attested Credential Data (AT) - Credential data present
      // Set during registration to indicate AuthenticatorAttestationGuid + credentialId + publicKey follow
      if (attestedCredentialData)
        flags |= 0x40;

      // Note: Bit 1 (reserved), Bit 5 (reserved), Bit 7 (ED - Extensions Data) not used

      // Construct authenticator data: rpIdHash (32 bytes) + flags (1 byte) + signCount (4 bytes)
      // signCount is always 0 for keepass2android (we don't track signature counts)
      return PasskeyCryptoHelper.HashSha256(relyingPartyId)
        .Concat([flags])
        .Concat("\0\0\0\0"u8.ToArray()) // signCount = 0 (big-endian). This is explicitly allowed per spec.
        .ToArray();
    }
  }

  /// <summary>
  /// Client data response interface
  /// </summary>
  public interface IClientDataResponse
  {
    byte[] HashData();
    string BuildResponse();
  }

  /// <summary>
  /// Client data response when hash is pre-calculated by the system.
  /// Used for privileged apps where Android provides the pre-calculated hash.
  /// Always returns a placeholder for clientDataJSON as the system already has the actual JSON.
  /// </summary>
  public class ClientDataDefinedResponse(byte[] clientDataHash) : IClientDataResponse
  {
    private const string ClientDataJsonPrivileged = "<placeholder>";

    public byte[] HashData() => clientDataHash;

    public string BuildResponse()
    {
      // Always return placeholder for privileged contexts
      // The system already has the actual clientDataJSON
      return ClientDataJsonPrivileged;
    }
  }

  /// <summary>
  /// Client data response built from challenge and origin.
  /// </summary>
  public class ClientDataBuildResponse : IClientDataResponse
  {
    public enum RequestType
    {
      Create,
      Get
    }

    private readonly JSONObject _clientDataJson;

    public ClientDataBuildResponse(RequestType type, byte[] challenge, string origin)
    {

      // Build the client data JSON object
      _clientDataJson = new JSONObject();
      _clientDataJson.Put("type", type == RequestType.Create ? "webauthn.create" : "webauthn.get");
      _clientDataJson.Put("challenge", Base64EncodeUrlSafe(challenge));
      _clientDataJson.Put("origin", origin);
      _clientDataJson.Put("crossOrigin", false);
    }

    public byte[] HashData()
    {
      // Hash the RAW JSON string (not base64-encoded)
      using var sha256 = SHA256.Create();
      return sha256.ComputeHash(Encoding.UTF8.GetBytes(_clientDataJson.ToString()));
    }

    public string BuildResponse()
    {
      // Return base64-encoded JSON (matching KeePassDX)
      return Base64EncodeUrlSafe(Encoding.UTF8.GetBytes(_clientDataJson.ToString()));
    }

    private static string Base64EncodeUrlSafe(byte[] data)
    {
      return Base64.EncodeToString(data, Base64Flags.UrlSafe | Base64Flags.NoPadding | Base64Flags.NoWrap);
    }
  }

  /// <summary>
  /// Authenticator Attestation Response for passkey creation (registration).
  /// 
  /// This class constructs the response returned to the relying party when a new passkey
  /// is created. It implements the WebAuthn AuthenticatorAttestationResponse structure.
  /// 
  /// The response contains:
  /// - clientDataJSON: Base64-encoded JSON containing challenge, origin, and type
  /// - authenticatorData: Binary data including RP ID hash, flags, counter, and credential info
  /// - attestationObject: CBOR-encoded object containing format, statement, and auth data
  /// - transports: Supported transport methods (internal, hybrid)
  /// - publicKey: The credential's public key in CBOR format
  /// - publicKeyAlgorithm: COSE algorithm identifier (ES256=-7, RS256=-257, EdDSA=-8)
  /// 
  /// The authenticatorData includes:
  /// - AuthenticatorAttestationGuid: Authenticator Attestation GUID identifying keepass2android
  /// - Credential ID: Unique identifier for this credential
  /// - Credential Public Key: COSE-encoded public key
  /// - Flags: AT (attested credential data present), BE/BS (backup eligibility/state), UP/UV
  /// 
  /// Attestation format is "none" as we don't provide attestation signatures.
  /// 
  /// See: https://www.w3.org/TR/webauthn-3/#authenticatorattestationresponse
  /// </summary>
  public class AuthenticatorAttestationResponse(
    PublicKeyCredentialCreationOptions requestOptions,
    byte[] credentialId,
    byte[] credentialPublicKey,
    bool userPresent,
    bool userVerified,
    bool backupEligibility,
    bool backupState,
    long publicKeyTypeId,
    byte[] publicKeySpki,
    IClientDataResponse clientDataResponse
    )

  {
    // AuthenticatorAttestationGuid in RFC 4122 (big-endian) format, not Microsoft's mixed-endian format

    public static byte[] AuthenticatorAttestationGuid { get; } =
    [
      0xea, 0xec, 0xde, 0xf2, 0x1c, 0x31, 0x56, 0x34,
      0x86, 0x39, 0xf1, 0xcb, 0xd9, 0xc0, 0x0a, 0x08
    ];

    private byte[] BuildAuthData()
    {
      var authData = AuthenticatorDataBuilder.BuildAuthenticatorData(
        relyingPartyId: Encoding.UTF8.GetBytes(requestOptions.RelyingPartyEntity.Id),
        userPresent: userPresent,
        userVerified: userVerified,
        backupEligibility: backupEligibility,
        backupState: backupState,
        attestedCredentialData: true
      );

      // Append AuthenticatorAttestationGuid + credIdLen + credentialId + credentialPublicKey
      var credIdLen = new[]
      {
        (byte)(credentialId.Length >> 8),
        (byte)credentialId.Length
      };

      var result = authData
        .Concat(AuthenticatorAttestationGuid)
        .Concat(credIdLen)
        .Concat(credentialId)
        .Concat(credentialPublicKey)
        .ToArray();

      return result;
    }

    private byte[] BuildAttestationObject(byte[] authData)
    {
      // https://www.w3.org/TR/webauthn-3/#attestation-object
      var cbor = CBORObject.NewMap()
        .Add("fmt", "none")
        .Add("attStmt", CBORObject.NewMap())
        .Add("authData", authData);

      return cbor.EncodeToBytes();
    }

    public JSONObject ToJson()
    {
      var authData = BuildAuthData();
      var attestationObject = BuildAttestationObject(authData);

      var json = new JSONObject();
      json.Put("clientDataJSON", clientDataResponse.BuildResponse());
      json.Put("authenticatorData", Base64EncodeUrlSafe(authData));
      var transports = new JSONArray();
      transports.Put("internal");
      transports.Put("hybrid");
      json.Put("transports", transports);
      json.Put("publicKey", Base64EncodeUrlSafe(publicKeySpki));
      json.Put("publicKeyAlgorithm", publicKeyTypeId);
      json.Put("attestationObject", Base64EncodeUrlSafe(attestationObject));
      return json;
    }

    private static string Base64EncodeUrlSafe(byte[] data)
    {
      return Base64.EncodeToString(data, Base64Flags.UrlSafe | Base64Flags.NoPadding | Base64Flags.NoWrap);
    }
  }

  /// <summary>
  /// Authenticator Assertion Response for passkey authentication (login).
  /// 
  /// This class constructs the response returned to the relying party when authenticating
  /// with an existing passkey. It implements the WebAuthn AuthenticatorAssertionResponse.
  /// 
  /// The response contains:
  /// - clientDataJSON: Base64-encoded JSON containing challenge, origin, and type (webauthn.get)
  /// - authenticatorData: Binary data with RP ID hash, flags, and signature counter
  /// - signature: Cryptographic signature over authenticatorData || hash(clientDataJSON)
  /// - userHandle: Base64-encoded user ID to identify which user is authenticating
  /// 
  /// The signature proves:
  /// 1. Possession of the private key (only the key holder can create valid signatures)
  /// 2. Freshness via the challenge (prevents replay attacks)
  /// 3. Binding to the relying party via RP ID hash in authenticatorData
  /// 
  /// The authenticatorData includes flags indicating:
  /// - User Present (UP): User interacted with the authenticator
  /// - User Verified (UV): User was verified (biometric/PIN)
  /// - Backup Eligibility (BE) and State (BS): Credential sync status
  /// 
  /// The signature is computed using the credential's private key (ECDSA P-256, RSA, or EdDSA)
  /// over the concatenation of authenticatorData and the SHA-256 hash of clientDataJSON.
  /// 
  /// See: https://www.w3.org/TR/webauthn-3/#authenticatorassertionresponse
  /// </summary>
  public class AuthenticatorAssertionResponse
  {
    private readonly string _userHandle;
    private readonly byte[] _authenticatorData;
    private readonly byte[] _signature;

    public AuthenticatorAssertionResponse(
      PublicKeyCredentialRequestOptions requestOptions,
      bool userPresent,
      bool userVerified,
      bool backupEligibility,
      bool backupState,
      string userHandle,
      string privateKeyPem,
      IClientDataResponse clientDataResponse
    )
    {
      _userHandle = userHandle;

      _authenticatorData = AuthenticatorDataBuilder.BuildAuthenticatorData(
        relyingPartyId: Encoding.UTF8.GetBytes(requestOptions.RpId),
        userPresent: userPresent,
        userVerified: userVerified,
        backupEligibility: backupEligibility,
        backupState: backupState
      );

      var clientDataHash = clientDataResponse.HashData();

      // Sign: authenticatorData || clientDataHash
      var dataToSign = _authenticatorData.Concat(clientDataHash).ToArray();
      _signature = PasskeyCryptoHelper.Sign(privateKeyPem, dataToSign);
    }

    public JSONObject ToJson(IClientDataResponse clientDataResponse)
    {
      var json = new JSONObject();
      var clientDataJson = clientDataResponse.BuildResponse();
      var authenticatorDataB64 = Base64EncodeUrlSafe(_authenticatorData);
      var signatureB64 = Base64EncodeUrlSafe(_signature);

      json.Put("clientDataJSON", clientDataJson);
      json.Put("authenticatorData", authenticatorDataB64);
      json.Put("signature", signatureB64);
      json.Put("userHandle", _userHandle);

      return json;
    }

    private static string Base64EncodeUrlSafe(byte[] data)
    {
      return Base64.EncodeToString(data, Base64Flags.UrlSafe | Base64Flags.NoPadding | Base64Flags.NoWrap);
    }
  }

  /// <summary>
  /// FIDO Public Key Credential wrapper.
  /// </summary>
  public class FidoPublicKeyCredential(string id, JSONObject response, string authenticatorAttachment = "platform")
  {

    public string ToJson()
    {
      var json = new JSONObject();
      json.Put("id", id);
      json.Put("rawId", id);
      json.Put("type", "public-key");
      json.Put("authenticatorAttachment", authenticatorAttachment);
      json.Put("response", response);
      json.Put("clientExtensionResults", new JSONObject());

      return json.ToString();
    }
  }
}

// This file is part of Keepass2Android, Copyright 2026 Philipp Crocoll.
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

using Android.Util;
using Java.Security;
using Java.Security.Spec;
using Org.Json;

namespace Kp2aPasskey.Core
{
  /// <summary>
  /// Builds the WebAuthn responses (registration and assertion) returned to the caller.
  /// </summary>
  public static class PasskeyResponseBuilder
  {
    private const string PendingKeyCredentialId = "credentialId";
    private const string PendingKeyKeyTypeId = "keyTypeId";
    private const string PendingKeyPublicKey = "publicKeyBase64";

    /// <summary>
    /// Data needed to build the registration response once the entry has been created. It is kept in the instance
    /// state across the unlock and entry creation, so it must not contain the private key.
    /// </summary>
    public static JSONObject CreatePendingRegistrationData(IPublicKey publicKey, long keyTypeId, string credentialId)
    {
      var data = new JSONObject();
      data.Put(PendingKeyPublicKey, Base64.EncodeToString(publicKey.GetEncoded(), Base64Flags.Default));
      data.Put(PendingKeyKeyTypeId, keyTypeId);
      data.Put(PendingKeyCredentialId, credentialId);
      return data;
    }

    /// <summary>
    /// Returns BE and BS for a stored passkey. Entries without stored flags use the given defaults; BS is never set
    /// without BE.
    /// </summary>
    public static (bool backupEligibility, bool backupState) ResolveBackupFlags(PasskeyData passkey,
      bool defaultBackupEligibility, bool defaultBackupState)
    {
      var be = passkey.BackupEligibility ?? defaultBackupEligibility;
      var bs = be && (passkey.BackupState ?? defaultBackupState);
      return (be, bs);
    }

    /// <summary>
    /// Builds the registration response JSON (PublicKeyCredential with AuthenticatorAttestationResponse) for the
    /// passkey stored in the new entry. Throws if the entry does not contain the passkey of <paramref name="pendingData"/>.
    /// </summary>
    public static string BuildRegistrationResponseJson(PasskeyData passkey, JSONObject pendingData,
      PublicKeyCredentialCreationOptions creationOptions, IClientDataResponse clientDataResponse, bool userVerified,
      bool backupEligibility, bool backupState)
    {
      if (passkey.CredentialId != pendingData.OptString(PendingKeyCredentialId, ""))
        throw new InvalidOperationException("Created entry does not contain the passkey generated for this request");

      // Both fields are always written by CreatePendingRegistrationData; GetLong/GetString throw if they are missing.
      var keyTypeId = pendingData.GetLong(PendingKeyKeyTypeId);
      var publicKey = DecodePublicKey(pendingData.GetString(PendingKeyPublicKey), keyTypeId);
      var credentialPublicKeyCbor = PasskeyCryptoHelper.ConvertPublicKeyToMap(publicKey, keyTypeId)?.EncodeToBytes()
                                    ?? throw new InvalidOperationException("Failed to convert public key");
      var publicKeySpki = publicKey.GetEncoded()
                          ?? throw new InvalidOperationException("Failed to convert public key to X.509 format");

      var attestationResponse = new AuthenticatorAttestationResponse(
        requestOptions: creationOptions,
        credentialId: Base64Url.Decode(passkey.CredentialId),
        credentialPublicKey: credentialPublicKeyCbor,
        userPresent: true,
        userVerified: userVerified,
        backupEligibility: backupEligibility,
        backupState: backupState,
        publicKeyTypeId: keyTypeId,
        publicKeySpki: publicKeySpki,
        clientDataResponse: clientDataResponse
      );

      JSONObject? extensionResults = null;
      if (creationOptions.CredPropsRequested)
      {
        // All Kp2a passkeys are discoverable (resident) credentials.
        extensionResults = new JSONObject();
        extensionResults.Put("credProps", new JSONObject().Put("rk", true));
      }

      return new FidoPublicKeyCredential(
        id: passkey.CredentialId,
        response: attestationResponse.ToJson(),
        clientExtensionResults: extensionResults
      ).ToJson();
    }

    /// <summary>
    /// Builds the assertion response JSON (PublicKeyCredential with AuthenticatorAssertionResponse), signed with the
    /// passkey's private key.
    /// </summary>
    public static string BuildAssertionResponseJson(PasskeyData passkey, PublicKeyCredentialRequestOptions requestOptions,
      IClientDataResponse clientDataResponse, bool userVerified, bool backupEligibility, bool backupState)
    {
      var assertionResponse = new AuthenticatorAssertionResponse(
        requestOptions: requestOptions,
        userPresent: true,
        userVerified: userVerified,
        backupEligibility: backupEligibility,
        backupState: backupState,
        userHandle: passkey.UserHandle,
        privateKeyPem: passkey.PrivateKeyPem,
        clientDataResponse: clientDataResponse
      );

      return new FidoPublicKeyCredential(
        id: passkey.CredentialId,
        response: assertionResponse.ToJson(clientDataResponse)
      ).ToJson();
    }

    /// <summary>
    /// Decodes an X.509 (SPKI) public key, standard base64, of the given COSE key type.
    /// </summary>
    private static IPublicKey DecodePublicKey(string publicKeyBase64, long keyTypeId)
    {
      if (string.IsNullOrEmpty(publicKeyBase64))
        throw new InvalidOperationException("Public key not found in passkey data");

      var publicKeyBytes = Base64.Decode(publicKeyBase64, Base64Flags.Default)!;
      return keyTypeId switch
      {
        PasskeyCryptoHelper.CoseAlgEs256 => GeneratePublicKey("EC", publicKeyBytes),
        PasskeyCryptoHelper.CoseAlgRs256 => GeneratePublicKey("RSA", publicKeyBytes),
        // Java's Ed25519 KeyFactory requires the Keystore on Android, so use the wrapper.
        PasskeyCryptoHelper.CoseAlgEd25519 => new Ed25519PublicKeyWrapper(publicKeyBytes),
        _ => throw new NotSupportedException($"Unsupported key type {keyTypeId}")
      };
    }

    private static IPublicKey GeneratePublicKey(string algorithm, byte[] x509EncodedKey) =>
      KeyFactory.GetInstance(algorithm)?.GeneratePublic(new X509EncodedKeySpec(x509EncodedKey))
      ?? throw new InvalidOperationException($"Failed to load {algorithm} public key");
  }
}

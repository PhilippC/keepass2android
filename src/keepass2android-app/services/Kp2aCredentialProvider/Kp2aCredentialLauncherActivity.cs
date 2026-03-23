// Parts of the file are derived from KeePassDX (https://github.com/Kunzisoft/KeePassDX)
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

using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using AndroidX.Core.App;
using AndroidX.Credentials;
using AndroidX.Credentials.Exceptions;
using AndroidX.Credentials.Provider;
using Java.Security;
using Java.Security.Spec;
using Java.Time;
using Keepass2android.Pluginsdk;
using keepass2android.services.AutofillBase;
using keepass2android.services.Kp2aCredentialProvider.Passkey;
using KeePassLib;
using Kp2aPasskey.Core;
using KeePassLib.Utility;
using Org.Json;

namespace keepass2android.services.Kp2aCredentialProvider
{
  [Activity(
    Label = AppNames.AppName,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden,
    Theme = "@style/Kp2aTheme_ActionBar",
    WindowSoftInputMode = SoftInput.AdjustResize,
    Permission = "keepass2android." + AppNames.PackagePart + ".permission.Kp2aChooseAutofill"
  )]
  public class Kp2aCredentialLauncherActivity : AndroidX.AppCompat.App.AppCompatActivity
  {
    public const string CredentialRequestTypeKey = "credential_request_type";
    public const int CredentialRequestTypeCreatePassword = 1;
    public const int CredentialRequestTypeUnlockForGetCredentials = 2;
    public const int CredentialRequestTypeGetPasswordForEntry = 3;
    public const int CredentialRequestTypeCreatePasskey = 4;
    public const int CredentialRequestTypeGetPasskeyForEntry = 6;

    // Intent extra keys
    public const string ExtraRelyingPartyId = "extra_relying_party_id";
    public const string ExtraRequestJson = "extra_request_json";

    private const int CreateEntryRequestCode = 100;
    private const int UnlockRequestCode = 300;

    // Bundle keys for state persistence
    private const string BundleKeyPasskeyRequestJson = "passkey_request_json";
    private const string BundleKeyPasskeyCallingPackage = "passkey_calling_package";
    private const string BundleKeyPasskeyOrigin = "passkey_origin";
    private const string BundleKeyPasskeyResponseData = "passkey_response_data";
    private const string BundleKeyPasskeyClientDataHash = "passkey_client_data_hash";
    private const string BundleKeyPendingOperation = "pending_operation";
    private const string BundleKeyUserVerifiedForCreate = "user_verified_for_create";

    // ViewModel-style state variables for passkey operations
    private PublicKeyCredentialCreationOptions? _passkeyCreationOptions;
    private string? _passkeyRequestJson;
    private string? _passkeyCallingPackage;
    private string? _passkeyOrigin;
    private JSONObject? _passkeyResponseData;
    private byte[]? _passkeyClientDataHash;
    private enum PendingOperation
    {
      None,
      UnlockForGetCredentials,       // Unlock so the Begin-Get picker can be (re-)populated with all credential types
      CreatePasskey,                 // DB was open when creation started → awaiting CreateEntryRequestCode
      GetPasskeyForEntryAfterUnlock, // Same as above, for a pre-selected entry
    }

    private PendingOperation _pendingOperation;

    /// <summary>True when user completed device credential/biometric before creating a passkey.</summary>
    /// <remarks>This field is only needed for passkey creation because that involves an activity lifecycle break after UV.</remarks>
    private bool _userVerifiedForCreate;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
      base.OnCreate(savedInstanceState);

      // Restore state from saved instance if activity was recreated
      if (savedInstanceState != null)
      {
        RestoreInstanceState(savedInstanceState);
      }

      var intent = Intent;
      if (intent == null)
      {
        // should never happen
        SetResult(Result.Canceled);
        Finish();
      }
      else
      {
        var requestType = intent.GetIntExtra(CredentialRequestTypeKey, 0);
        switch (requestType)
        {

          case CredentialRequestTypeCreatePassword:
            // create request for user/password credentials (non-passkey)
            HandleCreatePasswordRequest(intent);
            break;
          case CredentialRequestTypeCreatePasskey:
            // create request for passkey
            HandleCreatePasskeyRequest(intent);
            break;
          case CredentialRequestTypeUnlockForGetCredentials:
            // The database was (and probably still is) locked when the onBeginGetCredentialRequest came in. We're supposed to unlock the database and then return matching entries.
            //We can use the same workflow for get-password as well as get-passkey requests.
            HandleUnlockForGetCredentialsRequest(intent);
            break;
          case CredentialRequestTypeGetPasswordForEntry:
            // After we (or the service) have returned entries for Android's credential UI (see GetCredentialHelper), the user has clicked one of these.
            // The previously returned entries only contained username/display name/UUID but not the actual FIDO2 assertion response.
            HandleGetPasswordForEntryRequest(intent);
            break;

          case CredentialRequestTypeGetPasskeyForEntry:
            // After we (or the service) have returned entries for Android's Passkey UI (see GetCredentialHelper), the user has clicked one of these.
            // The previously returned entries only contained username/display name/UUID but not the actual FIDO2 assertion response.
            HandleGetPasskeyForEntryRequest(intent);
            break;

          default:
            // unexpected intent
            SetResult(Result.Canceled);
            Finish();
            break;
        }
      }
    }

    private void HandleCreatePasswordRequest(Intent requestIntent)
    {
      var createRequest = PendingIntentHandler.RetrieveProviderCreateCredentialRequest(
        requestIntent
      );
      if (createRequest is { CallingRequest: CreatePasswordRequest })
      {
        if (createRequest.CallingRequest is not CreatePasswordRequest request)
        {
          SetUpFailureResponseForCreateAndFinish("Unable to extract request from intent");
        }
        else
        {
          var callingPackage = createRequest.CallingAppInfo.PackageName;

          var forwardIntent = new Intent(this, typeof(SelectCurrentDbActivity));

          Dictionary<string, string> outputFields = [];
          if (callingPackage != null)
          {
            outputFields.TryAdd(PwDefs.UrlField, $"{KeePass.AndroidAppScheme}{callingPackage}");
          }

          outputFields.TryAdd(PwDefs.UserNameField, request.Id);
          outputFields.TryAdd(PwDefs.PasswordField, request.Password);

          JSONObject jsonOutput = new(outputFields);
          var jsonOutputStr = jsonOutput.ToString();
          forwardIntent.PutExtra(Strings.ExtraEntryOutputData, jsonOutputStr);

          JSONArray jsonProtectedFields = new(
            (System.Collections.ICollection)Array.Empty<string>()
          );
          forwardIntent.PutExtra(Strings.ExtraProtectedFieldsList, jsonProtectedFields.ToString());

          forwardIntent.PutExtra(AppTask.AppTaskKey, "CreateEntryThenCloseTask");
          forwardIntent.PutExtra(CreateEntryThenCloseTask.ShowUserNotificationsKey, "false");
          StartActivityForResult(forwardIntent, CreateEntryRequestCode);
        }
      }
      else
      {
        SetUpFailureResponseForCreateAndFinish("Unable to extract request from intent");
      }
    }

    private void HandleUnlockForGetCredentialsRequest(Intent requestIntent)
    {
      _pendingOperation = PendingOperation.UnlockForGetCredentials;
      LaunchUnlockDatabase();
    }

    /// <summary>
    /// Launches <see cref="SelectCurrentDbActivity"/> to unlock the database and return here via
    /// <see cref="OnActivityResult"/> with <see cref="UnlockRequestCode"/>.
    /// </summary>
    private void LaunchUnlockDatabase()
    {
      var forwardIntent = new Intent(this, typeof(SelectCurrentDbActivity));
      new UnlockThenCloseWithResultTask(true) { ResultCode = (int)Result.Ok }.ToIntent(forwardIntent);
      StartActivityForResult(forwardIntent, UnlockRequestCode);
    }

    private void HandleGetPasswordForEntryRequest(Intent requestIntent)
    {
      var getRequest = PendingIntentHandler.RetrieveProviderGetCredentialRequest(requestIntent);
      var options = getRequest?.CredentialOptions;
      if (options == null || options.Count == 0)
      {
        SetUpFailureResponseForGetAndFinish();
        return;
      }

      var option = options.FirstOrDefault(o => o is GetPasswordOption);
      if (option is GetPasswordOption)
      {
        var entryId = requestIntent.GetStringExtra(Strings.ExtraEntryId);
        if (string.IsNullOrEmpty(entryId))
        {
          SetUpFailureResponseForGetAndFinish();
          return;
        }
        var entryUuid = new PwUuid(MemUtil.HexStringToByteArray(entryId));

        var lastOpenedEntry = App.Kp2a.LastOpenedEntry;
        if (lastOpenedEntry != null && entryUuid.Equals(lastOpenedEntry.Uuid))
        {
          SetupGetCredentialResponseForEntryAndFinish(lastOpenedEntry.Entry);
        }
        else
        {
          foreach (Database db in App.Kp2a.OpenDatabases)
          {
            if (db.EntriesById.TryGetValue(entryUuid, out var resultEntry))
            {
              SetupGetCredentialResponseForEntryAndFinish(resultEntry);
              return;
            }
          }
          //nothing found in open databases
          SetUpNoCredentialResponseForGetAndFinish();
        }
      }
      else
      {
        SetUpFailureResponseForGetAndFinish();
      }
    }

    protected override void OnActivityResult(
      int requestCode,
      [GeneratedEnum] Result resultCode,
      Intent? data
    )
    {
      base.OnActivityResult(requestCode, resultCode, data);
      switch (requestCode)
      {
        case CreateEntryRequestCode:
          SetupCreateCredentialResponseAndFinish(resultCode);
          break;

        case UnlockRequestCode:
          SetupUnlockResponseAndFinish(resultCode);
          break;
      }
    }

    /// <summary>
    /// Saves the password/passkey and sets the response back to the calling app.
    /// </summary>
    /// <param name="resultCode">The result code of forward intent.</param>
    private void SetupCreateCredentialResponseAndFinish([GeneratedEnum] Result resultCode)
    {
      var isPasskeyRequest = _pendingOperation is PendingOperation.CreatePasskey;
      Intent result = new Intent();

      if (resultCode == KeePass.ExitCloseAfterTaskComplete)
      {
        if (isPasskeyRequest)
        {
          // Handle passkey creation
          try
          {
            // Retrieve stored passkey data from instance variables
            var callingPackage = _passkeyCallingPackage ?? "";
            var origin = _passkeyOrigin;
            var passkeyData = _passkeyResponseData;
            var clientDataHash = _passkeyClientDataHash;
            var creationOptions = _passkeyCreationOptions;

            var credentialResponse = CreatePasskeyResponse(passkeyData, creationOptions, clientDataHash, callingPackage, origin);
            PendingIntentHandler.SetCreateCredentialResponse(result, credentialResponse);

            SetResult(Result.Ok, result);
          }
          catch (Exception ex)
          {
            Kp2aLog.Log($"Kp2aCredentialLauncherActivity: Error creating passkey response: {ex.Message}");
            PendingIntentHandler.SetCreateCredentialException(
              result,
              new CreateCredentialUnknownException(ex.Message)
            );
            SetResult(Result.Canceled, result);
          }
          finally
          {
            // Clear the state after processing
            ClearPasskeyCreationState();
          }
        }
        else
        {
          // Handle password creation
          PendingIntentHandler.SetCreateCredentialResponse(result, new CreatePasswordResponse());
          SetResult(Result.Ok, result);
        }
      }
      else
      {
        PendingIntentHandler.SetCreateCredentialException(
          result,
          new CreateCredentialCancellationException()
        );
        SetResult(Result.Canceled, result);
        // Clear the state on cancellation
        if (isPasskeyRequest)
        {
          ClearPasskeyCreationState();
        }
      }
      if (!IsFinishing)
      {
        Finish();
      }
    }

    private CreatePublicKeyCredentialResponse CreatePasskeyResponse(JSONObject? passkeyData,
      PublicKeyCredentialCreationOptions? creationOptions, byte[]? clientDataHash, string callingPackage, string? origin)
    {
      if (passkeyData == null || creationOptions == null)
      {
        throw new InvalidOperationException("Passkey data not found in state");
      }

      var relyingPartyId = creationOptions.RelyingPartyEntity.Id;

      // Get the created entry (passkey data should already be in CustomData)
      var entry = App.Kp2a.LastOpenedEntry?.Entry;
      if (entry == null)
      {
        throw new InvalidOperationException("Entry was not created");
      }

      // Retrieve passkey data from the entry's CustomData
      var passkey = PasskeyStorage.RetrievePasskey(entry);
      if (passkey == null)
      {
        throw new InvalidOperationException("Passkey data not found in entry");
      }
      var keyTypeId = passkeyData.OptLong("keyTypeId", -7);

      // Convert credential ID from base64
      var credentialId = Base64.Decode(passkey.CredentialId, Base64Flags.UrlSafe | Base64Flags.NoPadding);

      // Load public key from stored data
      var publicKey = LoadPublicKeyFromPasskeyData(passkeyData);

      // Build client data response
      var clientDataResponse = BuildClientDataResponse(clientDataHash, callingPackage, origin, relyingPartyId, creationOptions);

      // Build public key encodings (CBOR for credential, X.509 SPKI for Android)
      var (credentialPublicKeyCbor, publicKeySpki) = BuildPublicKeyEncodings(publicKey, keyTypeId);

      // Build attestation response
      var attestationResponse = new AuthenticatorAttestationResponse(
        requestOptions: creationOptions,
        credentialId: credentialId,
        credentialPublicKey: credentialPublicKeyCbor,
        userPresent: true,
        userVerified: _userVerifiedForCreate,
        backupEligibility: passkey.BackupEligibility ?? PasskeyPreferences.GetBackupEligibility(this),
        backupState: passkey.BackupState ?? PasskeyPreferences.GetBackupState(this),
        publicKeyTypeId: keyTypeId,
        publicKeySpki: publicKeySpki,
        clientDataResponse: clientDataResponse
      );

      // Create FIDO credential and set response
      var credentialResponse = CreateFidoAttestationResponse(passkey.CredentialId, attestationResponse);
      _userVerifiedForCreate = false; // clear after use
      return credentialResponse;

    }

    private void ClearPasskeyCreationState()
    {
      _passkeyCreationOptions = null;
      _passkeyRequestJson = null;
      _passkeyCallingPackage = null;
      _passkeyOrigin = null;
      _passkeyResponseData = null;
      _passkeyClientDataHash = null;
      _pendingOperation = PendingOperation.None;
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
      base.OnSaveInstanceState(outState);

      // Save passkey creation state to survive activity recreation
      if (_pendingOperation != PendingOperation.None)
      {
        outState.PutString(BundleKeyPasskeyRequestJson, _passkeyRequestJson);
        outState.PutString(BundleKeyPasskeyCallingPackage, _passkeyCallingPackage);
        outState.PutString(BundleKeyPasskeyOrigin, _passkeyOrigin);
        outState.PutString(BundleKeyPasskeyResponseData, _passkeyResponseData?.ToString());

        if (_passkeyClientDataHash != null)
        {
          outState.PutByteArray(BundleKeyPasskeyClientDataHash, _passkeyClientDataHash);
        }

        outState.PutInt(BundleKeyPendingOperation, (int)_pendingOperation);
        outState.PutBoolean(BundleKeyUserVerifiedForCreate, _userVerifiedForCreate);
      }
    }

    private void RestoreInstanceState(Bundle savedInstanceState)
    {
      _passkeyRequestJson = savedInstanceState.GetString(BundleKeyPasskeyRequestJson);
      _passkeyCallingPackage = savedInstanceState.GetString(BundleKeyPasskeyCallingPackage);
      _passkeyOrigin = savedInstanceState.GetString(BundleKeyPasskeyOrigin);

      var passkeyResponseDataStr = savedInstanceState.GetString(BundleKeyPasskeyResponseData);
      if (!string.IsNullOrEmpty(passkeyResponseDataStr))
      {
        try
        {
          _passkeyResponseData = new JSONObject(passkeyResponseDataStr);
        }
        catch (Exception ex)
        {
          Kp2aLog.Log($"Kp2aCredentialLauncherActivity: Failed to restore passkey response data: {ex.Message}");
        }
      }

      _passkeyClientDataHash = savedInstanceState.GetByteArray(BundleKeyPasskeyClientDataHash);
      _pendingOperation = (PendingOperation)savedInstanceState.GetInt(BundleKeyPendingOperation);
      _userVerifiedForCreate = savedInstanceState.GetBoolean(BundleKeyUserVerifiedForCreate);

      // Recreate PublicKeyCredentialCreationOptions from stored JSON if needed
      if (_pendingOperation is PendingOperation.CreatePasskey
          && !string.IsNullOrEmpty(_passkeyRequestJson))
      {
        try
        {
          _passkeyCreationOptions = new PublicKeyCredentialCreationOptions(
            _passkeyRequestJson,
            _passkeyClientDataHash
          );
        }
        catch (Exception ex)
        {
          Kp2aLog.Log($"Kp2aCredentialLauncherActivity: Failed to restore creation options: {ex.Message}");
          // Clear the state if we can't restore it properly
          ClearPasskeyCreationState();
        }
      }
    }

    private IPublicKey LoadPublicKeyFromPasskeyData(JSONObject passkeyData)
    {
      var publicKeyBase64 = passkeyData.GetString("publicKeyBase64");
      if (string.IsNullOrEmpty(publicKeyBase64))
      {
        throw new InvalidOperationException("Public key not found in passkey data");
      }

      var publicKeyBytes = Base64.Decode(publicKeyBase64, Base64Flags.Default);
      var x509KeySpec = new X509EncodedKeySpec(publicKeyBytes);

      // Try different key types: EC, RSA, Ed25519
      // For Ed25519: use wrapper instead of KeyFactory (which requires Keystore on Android)
      var keyTypes = new[] { "EC", "RSA" };
      foreach (var keyType in keyTypes)
      {
        try
        {
          var keyFactory = KeyFactory.GetInstance(keyType);
          var publicKey = keyFactory.GeneratePublic(x509KeySpec);
          if (publicKey != null)
          {
            return publicKey;
          }
        }
        catch
        {
          // Try next key type
        }
      }

      // Try Ed25519 using wrapper (Java's Ed25519 KeyFactory requires Keystore)
      try
      {
        return new Ed25519PublicKeyWrapper(publicKeyBytes);
      }
      catch (Exception ex)
      {
        throw new InvalidOperationException($"Failed to load public key (tried EC, RSA, Ed25519): {ex.Message}");
      }
    }

    private IClientDataResponse BuildClientDataResponse(
      byte[]? clientDataHash,
      string callingPackage,
      string? origin,
      string relyingParty,
      PublicKeyCredentialCreationOptions creationOptions
    )
    {
      // Use stored origin or calculate fallback
      if (string.IsNullOrEmpty(origin))
      {
        // Fallback: calculate from package name
        origin = Kp2aDigitalAssetLinksDataSource.IsTrustedBrowser(callingPackage)
          ? $"https://{relyingParty}"
          : $"android:apk-key-hash:"; // Placeholder if origin not stored
      }

      // If hash is pre-calculated by system (privileged app), use ClientDataDefinedResponse with placeholder
      if (clientDataHash != null)
      {
        return new ClientDataDefinedResponse(clientDataHash);
      }

      return new ClientDataBuildResponse(
        ClientDataBuildResponse.RequestType.Create,
        creationOptions.Challenge,
        origin
      );
    }

    private (byte[] credentialPublicKeyCbor, byte[] publicKeySpki) BuildPublicKeyEncodings(IPublicKey publicKey, long keyTypeId)
    {
      var coseKeyObject = PasskeyCryptoHelper.ConvertPublicKeyToMap(publicKey, keyTypeId);
      if (coseKeyObject == null)
      {
        throw new InvalidOperationException("Failed to convert public key");
      }

      var credentialPublicKeyCbor = coseKeyObject.EncodeToBytes();

      var publicKeySpki = PasskeyCryptoHelper.ConvertPublicKey(publicKey, keyTypeId);
      if (publicKeySpki == null)
      {
        throw new InvalidOperationException("Failed to convert public key to X.509 format");
      }

      return (credentialPublicKeyCbor, publicKeySpki);
    }

    private CreatePublicKeyCredentialResponse CreateFidoAttestationResponse(
      string credentialIdBase64,
      AuthenticatorAttestationResponse attestationResponse
    )
    {
      var fidoCredential = new FidoPublicKeyCredential(
        id: credentialIdBase64,
        response: attestationResponse.ToJson()
      );

      var responseJson = fidoCredential.ToJson();
      return new CreatePublicKeyCredentialResponse(responseJson);
    }

    private GetCredentialResponse CreateFidoAssertionResponse(
      string credentialId,
      AuthenticatorAssertionResponse assertionResponse,
      IClientDataResponse clientDataResponse
    )
    {
      var fidoCredential = new FidoPublicKeyCredential(
        id: credentialId,
        response: assertionResponse.ToJson(clientDataResponse)
      );

      var responseJson = fidoCredential.ToJson();
      return new GetCredentialResponse(new PublicKeyCredential(responseJson));
    }

    /// <summary>
    /// Sets and returns the credential list from the unlocked database
    /// </summary>
    /// <param name="resultCode">The result code of forward intent.</param>
    private void SetupUnlockResponseAndFinish([GeneratedEnum] Result resultCode)
    {
      var thisIntent = Intent;
      if (thisIntent != null)
      {

        if (_pendingOperation == PendingOperation.GetPasskeyForEntryAfterUnlock)
        {
          if (resultCode == Result.Ok && App.Kp2a.DatabaseIsUnlocked)
            HandleGetPasskeyForEntryRequest(thisIntent); // re-enter to run UV if required
          else
            Finish(); // unlock was cancelled or failed
          return;
        }

        // UnlockForGetCredentials path: unlock completed, now populate the Begin-Get picker
        // with all available credential types so the user can make their selection.
        if (_pendingOperation == PendingOperation.UnlockForGetCredentials)
        {
          var getRequest = PendingIntentHandler.RetrieveBeginGetCredentialRequest(thisIntent);
          if (getRequest != null && App.Kp2a.DatabaseIsUnlocked)
          {
            var response = new BeginGetCredentialResponse.Builder();
            var callingPackage = getRequest.CallingAppInfo?.PackageName;
            foreach (var option in getRequest.BeginGetCredentialOptions)
            {
              if (option is BeginGetPasswordOption passwordOption && callingPackage != null)
                GetCredentialHelper.AddMatchingPasswordEntries(this, callingPackage, passwordOption, response);
              else if (option is BeginGetPublicKeyCredentialOption passkeyOption)
                GetCredentialHelper.AddMatchingPasskeyEntries(this, passkeyOption, response);
            }
            var result = new Intent();
            PendingIntentHandler.SetBeginGetCredentialResponse(result, response.Build());
            SetResult(Result.Ok, result);
          }
        }
      }

      if (!IsFinishing)
      {
        Finish();
      }
    }

    private void SetupGetCredentialResponseForEntryAndFinish(PwEntry entry)
    {
      var username = entry.Strings.ReadSafe(PwDefs.UserNameField);
      var password = entry.Strings.ReadSafe(PwDefs.PasswordField);
      if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
      {
        SetUpNoCredentialResponseForGetAndFinish();
        return;
      }

      var result = new Intent();
      PendingIntentHandler.SetGetCredentialResponse(
        result,
        new GetCredentialResponse(new PasswordCredential(username, password))
      );
      SetResult(Result.Ok, result);

      if (!IsFinishing)
      {
        Finish();
      }
    }

    /// <summary>
    /// If the request is null, send an unknown exception to client and finish the flow.
    /// </summary>
    /// <param name="message">The error message to send to the client.</param>
    private void SetUpFailureResponseForCreateAndFinish(string? message = null)
    {
      var result = new Intent();
      PendingIntentHandler.SetCreateCredentialException(
        result,
        new CreateCredentialUnknownException(message)
      );
      SetResult(Result.Ok, result);

      if (!IsFinishing)
      {
        Finish();
      }
    }

    /// <summary>
    /// Sets up a failure response for get request and finishes the activity.
    /// </summary>
    /// <param name="message">The error message to include in the response.</param>
    private void SetUpFailureResponseForGetAndFinish(string? message = null)
    {
      var result = new Intent();
      PendingIntentHandler.SetGetCredentialException(
        result,
        new GetCredentialUnknownException(message)
      );
      SetResult(Result.Ok, result);

      if (!IsFinishing)
      {
        Finish();
      }
    }

    /// <summary>
    /// Sets up a no credential response for get request and finishes the activity.
    /// </summary>
    private void SetUpNoCredentialResponseForGetAndFinish()
    {
      Intent result = new Intent();
      PendingIntentHandler.SetGetCredentialException(result, new NoCredentialException());
      SetResult(Result.Ok, result);

      if (!IsFinishing)
      {
        Finish();
      }
    }

    #region helper methods

    /// <summary>
    /// Validates and parses the passkey creation request, generates the key pair, populates all
    /// instance state, and launches <see cref="SelectCurrentDbActivity"/> to create the entry.
    /// Throws on any error so the caller can map exceptions to a failure response uniformly.
    /// </summary>
    private void PreparePasskeyCreationAndLaunchEntryCreation(Intent requestIntent)
    {
      var createRequest = PendingIntentHandler.RetrieveProviderCreateCredentialRequest(requestIntent);

      if (createRequest == null || createRequest.CallingRequest is not CreatePublicKeyCredentialRequest passkeyRequest)
        throw new InvalidOperationException("Unable to extract passkey create request from intent");

      var requestJson = passkeyRequest.RequestJson;
      if (string.IsNullOrEmpty(requestJson))
        throw new InvalidOperationException("Missing request JSON");

      var clientDataHash = passkeyRequest.GetClientDataHash();

      var creationOptions = _passkeyCreationOptions
        ?? new PublicKeyCredentialCreationOptions(requestJson, clientDataHash);

      var requestedAlgorithms = creationOptions.PubKeyCredParams.Select(p => p.Alg).ToList();

      // Generate credential ID (16 random bytes)
      var credentialId = new byte[16];
      new System.Security.Cryptography.RNGCryptoServiceProvider().GetBytes(credentialId);

      // Generate key pair
      var keyPairResult = PasskeyCryptoHelper.GenerateKeyPair(requestedAlgorithms);
      if (!keyPairResult.HasValue)
        throw new InvalidOperationException(
          $"No supported public key algorithm found. Requested: [{string.Join(", ", requestedAlgorithms)}]"
        );

      var (keyPair, keyTypeId) = keyPairResult.Value;
      var privateKeyPem = PasskeyCryptoHelper.ConvertPrivateKeyToPem(keyPair.Private);
      PasskeyData passkey = new PasskeyData
      {
        Username = creationOptions.UserEntity.Name,
        PrivateKeyPem = privateKeyPem,
        CredentialId = Base64.EncodeToString(credentialId, Base64Flags.UrlSafe | Base64Flags.NoPadding | Base64Flags.NoWrap),
        UserHandle = Base64.EncodeToString(creationOptions.UserEntity.Id, Base64Flags.UrlSafe | Base64Flags.NoPadding | Base64Flags.NoWrap),
        RelyingParty = creationOptions.RelyingPartyEntity.Id,
        BackupEligibility = PasskeyPreferences.GetBackupEligibility(this),
        BackupState = PasskeyPreferences.GetBackupState(this)
      };

      var callingPackage = createRequest.CallingAppInfo?.PackageName ?? "";
      var origin = Kp2aDigitalAssetLinksDataSource.IsTrustedBrowser(callingPackage)
        ? $"https://{creationOptions.RelyingPartyEntity.Id}"
        : PasskeyOptionParsingHelper.GetOriginForCallingApp(createRequest.CallingAppInfo);

      var passkeyFieldsJson = CreatePasskeyFieldsJson(creationOptions.RelyingPartyEntity.Id, creationOptions.UserEntity.Name, passkey);

      var forwardIntent = BuildLaunchIntentToCreatePasskey(passkeyFieldsJson);

      // Build the response JSON we'll need once the entry has been created.
      var passkeyResponseJson = CreatePasskeyResponseJson(keyPair, keyTypeId, passkeyFieldsJson);

      // Persist state so OnActivityResult / ContinuePasskeyCreationAfterUnlock can reconstruct the response.
      _passkeyCreationOptions = creationOptions;
      _passkeyRequestJson = requestJson;
      _passkeyCallingPackage = callingPackage;
      _passkeyOrigin = origin;
      _passkeyResponseData = passkeyResponseJson;
      _passkeyClientDataHash = clientDataHash;
      _pendingOperation = PendingOperation.CreatePasskey;

      StartActivityForResult(forwardIntent, CreateEntryRequestCode);
    }

    private Intent BuildLaunchIntentToCreatePasskey(JSONObject passkeyFieldsJson)
    {
      var jsonProtectedFields = new JSONArray(new List<string>
      {
        PasskeyStorage.FIELD_PRIVATE_KEY,
        PasskeyStorage.FIELD_CREDENTIAL_ID,
        PasskeyStorage.FIELD_USER_HANDLE
      });
      var jsonTags = new JSONArray(new List<string> { PasskeyStorage.PASSKEY_TAG });

      var forwardIntent = new Intent(this, typeof(SelectCurrentDbActivity));
      forwardIntent.PutExtra(Strings.ExtraEntryOutputData, passkeyFieldsJson.ToString());
      forwardIntent.PutExtra(Strings.ExtraProtectedFieldsList, jsonProtectedFields.ToString());
      forwardIntent.PutExtra(CreateEntryThenCloseTask.TagsKey, jsonTags.ToString());
      forwardIntent.PutExtra(AppTask.AppTaskKey, "CreateEntryThenCloseTask");
      forwardIntent.PutExtra(CreateEntryThenCloseTask.ShowUserNotificationsKey, "false");
      return forwardIntent;
    }

    private JSONObject CreatePasskeyResponseJson(KeyPair keyPair, long keyTypeId, JSONObject passkeyFieldsJson)
    {
      var publicKeyBytes = keyPair.Public.GetEncoded();
      var passkeyResponseJson = new JSONObject();
      passkeyResponseJson.Put("publicKeyBase64", Base64.EncodeToString(publicKeyBytes, Base64Flags.Default));
      passkeyResponseJson.Put("keyTypeId", keyTypeId);
      passkeyResponseJson.Put("passkeyFieldsJson", passkeyFieldsJson.ToString());
      return passkeyResponseJson;
    }

    private static JSONObject CreatePasskeyFieldsJson(string relyingParty, string username, PasskeyData passkey)
    {
      // Build AllFields JSON with passkey data in extra fields (compatible with KeePassDX/KeePassXC)
      // Start with standard entry fields
      var passkeyFieldsJson = new JSONObject();
      passkeyFieldsJson.Put(PwDefs.TitleField, $"Passkey for {relyingParty}");
      passkeyFieldsJson.Put(PwDefs.UserNameField, username);
      passkeyFieldsJson.Put(PwDefs.UrlField, $"passkey:{relyingParty}");

      // Add passkey extra fields
      passkeyFieldsJson.Put(PasskeyStorage.FIELD_USERNAME, passkey.Username);
      passkeyFieldsJson.Put(PasskeyStorage.FIELD_PRIVATE_KEY, passkey.PrivateKeyPem);
      passkeyFieldsJson.Put(PasskeyStorage.FIELD_CREDENTIAL_ID, passkey.CredentialId);
      passkeyFieldsJson.Put(PasskeyStorage.FIELD_USER_HANDLE, passkey.UserHandle);
      passkeyFieldsJson.Put(PasskeyStorage.FIELD_RELYING_PARTY, passkey.RelyingParty);
      if (passkey.BackupEligibility.HasValue)
        passkeyFieldsJson.Put(PasskeyStorage.FIELD_FLAG_BE, passkey.BackupEligibility.Value.ToString().ToLower());
      if (passkey.BackupState.HasValue)
        passkeyFieldsJson.Put(PasskeyStorage.FIELD_FLAG_BS, passkey.BackupState.Value.ToString().ToLower());
      return passkeyFieldsJson;
    }

    #endregion


    #region Passkey Handler Methods

    /// <summary>
    /// Handles the request to create a new passkey.
    /// If user verification is required, shows device credential/biometric prompt first.
    /// </summary>
    private void HandleCreatePasskeyRequest(Intent requestIntent)
    {
      try
      {
        var createRequest = PendingIntentHandler.RetrieveProviderCreateCredentialRequest(requestIntent);
        if (createRequest?.CallingRequest is not CreatePublicKeyCredentialRequest passkeyRequest)
        {
          throw new InvalidOperationException("Unable to extract passkey create request from intent");
        }

        var requestJson = passkeyRequest.RequestJson;
        if (string.IsNullOrEmpty(requestJson))
        {
          throw new InvalidOperationException("Missing request JSON");
        }

        var clientDataHash = passkeyRequest.GetClientDataHash();
        _passkeyCreationOptions = new PublicKeyCredentialCreationOptions(requestJson, clientDataHash);
        var uvRequired = _passkeyCreationOptions.UserVerificationRequirement == Kp2aPasskey.Core.UserVerificationRequirement.Required
          || (PasskeyPreferences.GetForceUserVerificationWhenPreferred(this)
              && _passkeyCreationOptions.UserVerificationRequirement == Kp2aPasskey.Core.UserVerificationRequirement.Preferred);

        if (!uvRequired)
        {
          _userVerifiedForCreate = false;
          PreparePasskeyCreationAndLaunchEntryCreation(requestIntent);
          return;
        }

        if (!UserVerificationHelper.CanAuthenticate(this))
        {
          SetUpFailureResponseForCreateAndFinish("User verification required but not available on this device.");
          return;
        }

        var title = GetString(Resource.String.passkey_user_verification_required_title);
        var subtitle = GetString(Resource.String.passkey_user_verification_required_description);
        UserVerificationHelper.ShowUserVerification(
          this,
          title,
          subtitle,
          onSuccess: () =>
          {
            _userVerifiedForCreate = true;
            try
            {
              PreparePasskeyCreationAndLaunchEntryCreation(requestIntent);
            }
            catch (Exception e)
            {
              Kp2aLog.Log($"Kp2aCredentialLauncherActivity: Error creating passkey: {e.Message}");
              SetUpFailureResponseForCreateAndFinish(e.Message);
            }
          },
          onCancelOrError: () =>
          {
            var result = new Intent();
            PendingIntentHandler.SetCreateCredentialException(result, new CreateCredentialCancellationException());
            SetResult(Result.Ok, result);
            if (!IsFinishing) Finish();
          });
      }
      catch (Exception e)
      {
        Kp2aLog.Log($"Kp2aCredentialLauncherActivity: Error creating passkey: {e.Message}");
        SetUpFailureResponseForCreateAndFinish(e.Message);
      }
    }



    /// <summary>
    /// Builds a FIDO2 assertion response for the given passkey and returns it to the credential manager.
    /// </summary>
    /// <param name="userVerified">True if user completed device credential/biometric verification.</param>
    private void BuildAndReturnAssertionFromPasskeyAndFinish(
      PasskeyData passkey,
      CallingAppInfo? callingAppInfo,
      PublicKeyCredentialRequestOptions requestOptions,
      GetPublicKeyCredentialOption passkeyOption,
      bool userVerified)
    {
      // Build client data response with the correct origin
      string origin;
      var callingPackage = callingAppInfo?.PackageName ?? "";

      if (Kp2aDigitalAssetLinksDataSource.IsTrustedBrowser(callingPackage))
      {
        // For browsers, use the RP ID as the web origin
        origin = $"https://{requestOptions.RpId}";
      }
      else
      {
        // Calculate Android APK origin for native apps
        origin = PasskeyOptionParsingHelper.GetOriginForCallingApp(callingAppInfo);
      }
      // Try to extract clientDataHash from GetPublicKeyCredentialOption
      // According to Android docs: if clientDataHash is provided, use it and set placeholder for clientDataJSON
      var clientDataHash = PasskeyOptionParsingHelper.ExtractClientDataHashFromOption(passkeyOption);

      IClientDataResponse clientDataResponse;
      if (clientDataHash != null)
      {
        // Privileged app: use provided hash and placeholder for clientDataJSON
        clientDataResponse = new ClientDataDefinedResponse(clientDataHash);
      }
      else
      {
        // Non-privileged app: build actual clientDataJSON
        clientDataResponse = new ClientDataBuildResponse(
          ClientDataBuildResponse.RequestType.Get,
          requestOptions.Challenge,
          origin
        );
      }

      // Build assertion response
      var assertionResponse = new AuthenticatorAssertionResponse(
        requestOptions: requestOptions,
        userPresent: true,
        userVerified: userVerified,
        backupEligibility: passkey.BackupEligibility ?? PasskeyPreferences.GetBackupEligibility(this),
        backupState: passkey.BackupState ?? PasskeyPreferences.GetBackupState(this),
        userHandle: passkey.UserHandle,
        privateKeyPem: passkey.PrivateKeyPem,
        clientDataResponse: clientDataResponse
      );

      var getCredentialResponse = CreateFidoAssertionResponse(
        passkey.CredentialId,
        assertionResponse,
        clientDataResponse
      );

      var result = new Intent();
      PendingIntentHandler.SetGetCredentialResponse(result, getCredentialResponse);
      SetResult(Result.Ok, result);

      if (!IsFinishing)
      {
        Finish();
      }
    }

    /// <summary>
    /// Handles the request to get a specific passkey for an entry.
    /// Unlocks the database first if needed. If user verification is required, shows device credential/biometric prompt.
    /// </summary>
    private void HandleGetPasskeyForEntryRequest(Intent requestIntent)
    {
      // Check if database is locked and launch unlock flow if needed
      if (!App.Kp2a.DatabaseIsUnlocked)
      {
        _pendingOperation = PendingOperation.GetPasskeyForEntryAfterUnlock;
        LaunchUnlockDatabase();
        return;
      }

      var requestJson = requestIntent.GetStringExtra(ExtraRequestJson);
      if (string.IsNullOrEmpty(requestJson))
      {
        ReturnPasskeyForEntryAndFinish(requestIntent, userVerified: false);
        return;
      }

      var requestOptions = new PublicKeyCredentialRequestOptions(requestJson);
      var uvRequired = requestOptions.UserVerificationRequirement == Kp2aPasskey.Core.UserVerificationRequirement.Required
        || (PasskeyPreferences.GetForceUserVerificationWhenPreferred(this)
            && requestOptions.UserVerificationRequirement == Kp2aPasskey.Core.UserVerificationRequirement.Preferred);

      if (!uvRequired)
      {
        ReturnPasskeyForEntryAndFinish(requestIntent, userVerified: false);
        return;
      }

      if (!UserVerificationHelper.CanAuthenticate(this))
      {
        SetUpFailureResponseForGetAndFinish("User verification required but not available on this device.");
        return;
      }

      var title = GetString(Resource.String.passkey_user_verification_required_title);
      var subtitle = GetString(Resource.String.passkey_user_verification_required_description);
      UserVerificationHelper.ShowUserVerification(
        this,
        title,
        subtitle,
        onSuccess: () =>
        {
          ReturnPasskeyForEntryAndFinish(requestIntent, userVerified: true);
        },
        onCancelOrError: () =>
        {
          var result = new Intent();
          PendingIntentHandler.SetGetCredentialException(result, new GetCredentialCancellationException());
          SetResult(Result.Ok, result);
          if (!IsFinishing) Finish();
        });
    }

    /// <summary>
    /// Builds and returns the passkey assertion response for a specific entry.
    /// Database must already be unlocked before calling this.
    /// <param name="userVerified">True if the user completed device credential/biometric verification.</param>
    /// </summary>
    private void ReturnPasskeyForEntryAndFinish(Intent requestIntent, bool userVerified)
    {
      try
      {
        var getRequest = PendingIntentHandler.RetrieveProviderGetCredentialRequest(requestIntent);
        var options = getRequest?.CredentialOptions;

        if (options == null || options.Count == 0)
        {
          SetUpFailureResponseForGetAndFinish("No credential options found");
          return;
        }

        var option = options.FirstOrDefault(o => o is GetPublicKeyCredentialOption);
        if (!(option is GetPublicKeyCredentialOption passkeyOption))
        {
          SetUpFailureResponseForGetAndFinish("Invalid credential option type");
          return;
        }

        var entryId = requestIntent.GetStringExtra(Strings.ExtraEntryId);
        var requestJson = passkeyOption.RequestJson;

        if (string.IsNullOrEmpty(entryId) || string.IsNullOrEmpty(requestJson))
        {
          SetUpFailureResponseForGetAndFinish("Missing entry ID or request JSON");
          return;
        }

        var entryUuid = new PwUuid(MemUtil.HexStringToByteArray(entryId));

        // Find the entry in open databases
        PwEntry? foundEntry = null;
        var lastOpenedEntry = App.Kp2a.LastOpenedEntry;
        if (lastOpenedEntry != null && entryUuid.Equals(lastOpenedEntry.Uuid))
        {
          foundEntry = lastOpenedEntry.Entry;
        }
        else
        {
          foreach (Database db in App.Kp2a.OpenDatabases)
          {
            if (db.EntriesById.TryGetValue(entryUuid, out var resultEntry))
            {
              foundEntry = resultEntry;
              break;
            }
          }
        }

        if (foundEntry == null)
        {
          SetUpNoCredentialResponseForGetAndFinish();
          return;
        }

        // Retrieve passkey data from entry
        var passkey = PasskeyStorage.RetrievePasskey(foundEntry);
        if (passkey == null)
        {
          SetUpFailureResponseForGetAndFinish("Entry does not contain passkey data");
          return;
        }

        // Parse request options and delegate to shared assertion builder
        var requestOptions = new PublicKeyCredentialRequestOptions(requestJson);

        BuildAndReturnAssertionFromPasskeyAndFinish(passkey, getRequest?.CallingAppInfo, requestOptions, passkeyOption, userVerified);
      }
      catch (Exception e)
      {
        Kp2aLog.Log($"Kp2aCredentialLauncherActivity: Error retrieving passkey. {e.Message}");
        SetUpFailureResponseForGetAndFinish($"Passkey retrieval failed: {e.Message}");
      }
    }


    #endregion
  }
}

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
using Google.Android.Material.Dialog;
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
    // Handle as many configuration changes as possible ourselves: recreating the activity interrupts dialogs and
    // user verification prompts (see OnCreate).
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden
      | ConfigChanges.ScreenSize | ConfigChanges.SmallestScreenSize | ConfigChanges.ScreenLayout
      | ConfigChanges.UiMode,
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
    private const string BundleKeyPasskeyOrigin = "passkey_origin";
    private const string BundleKeyPasskeyResponseData = "passkey_response_data";
    private const string BundleKeyPasskeyClientDataHash = "passkey_client_data_hash";
    private const string BundleKeyPendingOperation = "pending_operation";
    private const string BundleKeyUserVerifiedForCreate = "user_verified_for_create";
    private const string BundleKeyWaitingForActivityResult = "waiting_for_activity_result";

    // Key inside _passkeyResponseData
    private const string PasskeyDataKeyCredentialId = "credentialId";

    // Error messages returned to Credential Manager. Details are only written to the log.
    private const string InvalidRequestMessage = "Invalid passkey request";

    // ViewModel-style state variables for passkey operations
    private PublicKeyCredentialCreationOptions? _passkeyCreationOptions;
    private string? _passkeyRequestJson;
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

    /// <summary>True while a child activity (unlock, create entry) is running and its result will arrive in OnActivityResult.</summary>
    private bool _waitingForActivityResult;

    /// <summary>True if the calling app was verified via assetlinks.json and should be pinned to a new passkey entry.</summary>
    private bool _pinCallerAppToNewEntry;

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

        if (savedInstanceState != null)
        {
          // The activity was recreated, i.e. a previous instance has already handled the request. Handling it again
          // would e.g. generate a second key pair or show the unlock screen twice.
          if (_waitingForActivityResult)
          {
            // the result of the child activity is delivered to this instance via OnActivityResult
            Kp2aLog.Log("Kp2aCredentialLauncherActivity: recreated while waiting for activity result");
            return;
          }

          // A dialog or user verification prompt was shown. Don't try to restore it, just let the user try again.
          Kp2aLog.Log($"Kp2aCredentialLauncherActivity: recreated during request type {requestType}, cancelling");
          Toast.MakeText(this, Resource.String.passkey_request_interrupted, ToastLength.Long)?.Show();
          CancelRequestAndFinish(requestType);
          return;
        }

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
          StartChildActivityForResult(forwardIntent, CreateEntryRequestCode);
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
      StartChildActivityForResult(forwardIntent, UnlockRequestCode);
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
      _waitingForActivityResult = false;
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
            var origin = _passkeyOrigin;
            var passkeyData = _passkeyResponseData;
            var clientDataHash = _passkeyClientDataHash;
            var creationOptions = _passkeyCreationOptions;

            var credentialResponse = CreatePasskeyResponse(passkeyData, creationOptions, clientDataHash, origin);
            PendingIntentHandler.SetCreateCredentialResponse(result, credentialResponse);

            SetResult(Result.Ok, result);
          }
          catch (Exception ex)
          {
            Kp2aLog.Log($"Kp2aCredentialLauncherActivity: Error creating passkey response: {ex.Message}");
            // The entry has already been saved at this point, so tell the user it won't work.
            Toast.MakeText(this, GetString(Resource.String.passkey_create_failed, ex.Message), ToastLength.Long)?.Show();
            PendingIntentHandler.SetCreateCredentialException(
              result,
              new CreateCredentialUnknownException(ex.Message)
            );
            // Result.Ok is required, otherwise Credential Manager drops the exception and reports a cancellation.
            SetResult(Result.Ok, result);
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
      PublicKeyCredentialCreationOptions? creationOptions, byte[]? clientDataHash, string? origin)
    {
      if (passkeyData == null || creationOptions == null)
      {
        throw new InvalidOperationException("Passkey data not found in state");
      }

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

      if (passkey.CredentialId != passkeyData.OptString(PasskeyDataKeyCredentialId, ""))
      {
        throw new InvalidOperationException("Created entry does not contain the passkey generated for this request");
      }
      var keyTypeId = passkeyData.OptLong("keyTypeId", -7);

      // Convert credential ID from base64
      var credentialId = Base64.Decode(passkey.CredentialId, Base64Flags.UrlSafe | Base64Flags.NoPadding);

      // Load public key from stored data
      var publicKey = LoadPublicKeyFromPasskeyData(passkeyData);

      // Build client data response. origin and clientDataHash were determined by PasskeyOriginHelper.ResolveCallerOrigin
      // when the request came in, so clientDataHash is only set for verified privileged callers.
      if (string.IsNullOrEmpty(origin))
      {
        throw new InvalidOperationException("Passkey origin not found in state");
      }
      var clientDataResponse = PasskeyOriginHelper.CreateClientDataResponse(
        new PasskeyCallerOrigin(origin, clientDataHash),
        ClientDataBuildResponse.RequestType.Create,
        creationOptions.Challenge);

      // Build public key encodings (CBOR for credential, X.509 SPKI for Android)
      var (credentialPublicKeyCbor, publicKeySpki) = BuildPublicKeyEncodings(publicKey, keyTypeId);

      var beFlag = passkey.BackupEligibility ?? PasskeyPreferences.BackupEligibilityForEntriesWithoutFlags;
      var bsFlag = beFlag && (passkey.BackupState ?? PasskeyPreferences.BackupStateForEntriesWithoutFlags);

      // Build attestation response
      var attestationResponse = new AuthenticatorAttestationResponse(
        requestOptions: creationOptions,
        credentialId: credentialId,
        credentialPublicKey: credentialPublicKeyCbor,
        userPresent: true,
        userVerified: _userVerifiedForCreate,
        backupEligibility: beFlag,
        backupState: bsFlag,
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
      _passkeyOrigin = null;
      _passkeyResponseData = null;
      _passkeyClientDataHash = null;
      _pendingOperation = PendingOperation.None;
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
      base.OnSaveInstanceState(outState);

      // Saved for all request types, see OnCreate
      outState.PutBoolean(BundleKeyWaitingForActivityResult, _waitingForActivityResult);

      // Save passkey creation state to survive activity recreation
      if (_pendingOperation != PendingOperation.None)
      {
        outState.PutString(BundleKeyPasskeyRequestJson, _passkeyRequestJson);
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
      _waitingForActivityResult = savedInstanceState.GetBoolean(BundleKeyWaitingForActivityResult);

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

    private (byte[] credentialPublicKeyCbor, byte[] publicKeySpki) BuildPublicKeyEncodings(IPublicKey publicKey, long keyTypeId)
    {
      var coseKeyObject = PasskeyCryptoHelper.ConvertPublicKeyToMap(publicKey, keyTypeId);
      if (coseKeyObject == null)
      {
        throw new InvalidOperationException("Failed to convert public key");
      }

      var credentialPublicKeyCbor = coseKeyObject.EncodeToBytes();

      var publicKeySpki = publicKey?.GetEncoded();
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

      // Verify the caller: only privileged browsers (verified by signing certificate) may provide the origin
      // and a pre-computed clientDataHash. For all other callers, the clientDataHash is ignored.
      var callerOrigin = PasskeyOriginHelper.ResolveCallerOrigin(this, createRequest.CallingAppInfo, passkeyRequest.GetClientDataHash());
      var clientDataHash = callerOrigin.ClientDataHash;

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

      var origin = callerOrigin.Origin;

      var passkeyFieldsJson = PasskeyStorage.CreatePasskeyFieldsJson(creationOptions.RelyingPartyEntity.Id, creationOptions.UserEntity.Name, passkey);

      // If the calling app was verified via assetlinks.json in HandleCreatePasskeyRequest, pin it to the new entry, so
      // later requests from this app are accepted without checking again. Not if the user only confirmed an unverified app.
      var callerApp = GetNativeCallerApp(createRequest.CallingAppInfo);
      if (callerApp != null && _pinCallerAppToNewEntry)
      {
        PasskeyAppPins.AddPinFields(passkeyFieldsJson, callerApp);
      }

      var forwardIntent = BuildLaunchIntentToCreatePasskey(passkeyFieldsJson);

      // Build the response JSON we'll need once the entry has been created.
      var passkeyResponseJson = CreatePasskeyResponseJson(keyPair, keyTypeId, passkey.CredentialId!);

      // Persist state so OnActivityResult / ContinuePasskeyCreationAfterUnlock can reconstruct the response.
      _passkeyCreationOptions = creationOptions;
      _passkeyRequestJson = requestJson;
      _passkeyOrigin = origin;
      _passkeyResponseData = passkeyResponseJson;
      _passkeyClientDataHash = clientDataHash;
      _pendingOperation = PendingOperation.CreatePasskey;

      StartChildActivityForResult(forwardIntent, CreateEntryRequestCode);
    }

    private Intent BuildLaunchIntentToCreatePasskey(JSONObject passkeyFieldsJson)
    {
      var forwardIntent = new Intent(this, typeof(SelectCurrentDbActivity));
      forwardIntent.PutExtra(Strings.ExtraEntryOutputData, passkeyFieldsJson.ToString());
      // Must use PutStringArrayListExtra — CreateEntryThenCloseTask.Setup reads these with GetStringArrayList
      forwardIntent.PutStringArrayListExtra(Strings.ExtraProtectedFieldsList, PasskeyStorage.ProtectedFields.ToList());
      forwardIntent.PutStringArrayListExtra(CreateEntryThenCloseTask.TagsKey, new List<string> { PasskeyStorage.PASSKEY_TAG });
      forwardIntent.PutExtra(AppTask.AppTaskKey, "CreateEntryThenCloseTask");
      forwardIntent.PutExtra(CreateEntryThenCloseTask.ShowUserNotificationsKey, "false");
      return forwardIntent;
    }

    /// <summary>
    /// Data needed to build the response once the entry has been created. Saved in the instance state,
    /// so it must not contain the private key.
    /// </summary>
    private JSONObject CreatePasskeyResponseJson(KeyPair keyPair, long keyTypeId, string credentialId)
    {
      var publicKeyBytes = keyPair.Public.GetEncoded();
      var passkeyResponseJson = new JSONObject();
      passkeyResponseJson.Put("publicKeyBase64", Base64.EncodeToString(publicKeyBytes, Base64Flags.Default));
      passkeyResponseJson.Put("keyTypeId", keyTypeId);
      passkeyResponseJson.Put(PasskeyDataKeyCredentialId, credentialId);
      return passkeyResponseJson;
    }

    /// <summary>
    /// Starts a child activity whose result is handled in <see cref="OnActivityResult"/>.
    /// </summary>
    private void StartChildActivityForResult(Intent intent, int requestCode)
    {
      _waitingForActivityResult = true;
      StartActivityForResult(intent, requestCode);
    }


    /// <summary>
    /// If the caller is a browser acting on behalf of a website, but is not contained in any privileged apps allowlist,
    /// asks the user whether to add it to the custom allowlist. Without that, the browser can't provide the website's origin
    /// and passkeys can't be used on websites in that browser.
    /// Returns false if no dialog is needed. Otherwise the dialog is shown and either <paramref name="continueRequest"/>
    /// (after the browser was added) or <paramref name="cancelRequest"/> is called later.
    /// </summary>
    private bool AskToAddUnknownBrowser(CallingAppInfo? callingAppInfo, System.Action continueRequest, System.Action cancelRequest)
    {
      var browser = PrivilegedAppsAllowlist.GetUnknownBrowserCaller(this, callingAppInfo);
      if (browser == null)
        return false;

      Kp2aLog.Log($"Kp2aCredentialLauncherActivity: browser {browser.PackageName} is not in the privileged apps allowlists");
      var message = GetString(Resource.String.passkey_unknown_browser_message,
        PrivilegedAppsAllowlist.GetAppLabel(this, browser.PackageName),
        browser.PackageName,
        string.Join("\n", browser.Fingerprints));

      var builder = new MaterialAlertDialogBuilder(this);
      builder.SetTitle(GetString(Resource.String.passkey_unknown_browser_title));
      builder.SetMessage(message);
      builder.SetPositiveButton(GetString(Resource.String.passkey_unknown_browser_add), (_, _) =>
      {
        if (PrivilegedAppsAllowlist.AddCustomApp(this, browser))
          continueRequest();
        else
          cancelRequest();
      });
      builder.SetNegativeButton(GetString(Android.Resource.String.Cancel), (_, _) => cancelRequest());
      var dialog = builder.Create();
      dialog.CancelEvent += (_, _) => cancelRequest();
      dialog.Show();
      return true;
    }

    /// <summary>
    /// Returns the calling app (package name and signing certificates) if it is a regular app, i.e. not a privileged
    /// browser acting on behalf of a website. Returns null for privileged browsers and if the caller is unknown.
    /// </summary>
    private PrivilegedApp? GetNativeCallerApp(CallingAppInfo? callingAppInfo)
    {
      if (callingAppInfo == null || string.IsNullOrEmpty(callingAppInfo.PackageName))
        return null;
      if (PasskeyOriginHelper.GetOriginFromPrivilegedCaller(this, callingAppInfo) != null)
        return null;
      var fingerprints = PrivilegedAppsAllowlist.GetFingerprints(callingAppInfo.SigningInfo);
      return fingerprints.Count > 0 ? new PrivilegedApp(callingAppInfo.PackageName, fingerprints) : null;
    }

    /// <summary>
    /// For regular apps (not privileged browsers): checks that the app may use passkeys for the relying party.
    /// Apps already pinned to <paramref name="entry"/> are accepted without further checks. Otherwise the relying party's
    /// assetlinks.json must list the app. If it can't be retrieved, the user is asked whether to continue anyway.
    /// Only after a successful check (not after the user's confirmation) the app is pinned, and only to new passkeys
    /// (entry null), in <see cref="PreparePasskeyCreationAndLaunchEntryCreation"/>. Existing entries are not modified.
    /// Returns false if no (asynchronous) check is needed. Otherwise the check is started and later either
    /// <paramref name="continueRequest"/>, <paramref name="rejectRequest"/> or <paramref name="cancelRequest"/> is called.
    /// </summary>
    private bool VerifyCallingApp(CallingAppInfo? callingAppInfo, string relyingPartyId, PwEntry? entry,
      System.Action continueRequest, System.Action<string> rejectRequest, System.Action cancelRequest)
    {
      var app = GetNativeCallerApp(callingAppInfo);
      if (app == null)
        return false;
      if (entry != null && PasskeyAppPins.IsPinned(entry, app))
        return false;

      _ = VerifyCallingAppAsync(app, relyingPartyId, entry, continueRequest, rejectRequest, cancelRequest);
      return true;
    }

    private async Task VerifyCallingAppAsync(PrivilegedApp app, string relyingPartyId, PwEntry? entry,
      System.Action continueRequest, System.Action<string> rejectRequest, System.Action cancelRequest)
    {
      AppLinkResult result;
      try
      {
        result = await AppLinkVerifier.VerifyAsync(relyingPartyId, app);
      }
      catch (Exception e)
      {
        Kp2aLog.Log($"Kp2aCredentialLauncherActivity: app link verification failed: {e.Message}");
        result = AppLinkResult.Unreachable;
      }

      if (IsFinishing || IsDestroyed)
        return;

      var appLabel = PrivilegedAppsAllowlist.GetAppLabel(this, app.PackageName);

      switch (result)
      {
        case AppLinkResult.Linked:
          // Existing entries are not changed: saving the database can take long and cause sync conflicts, while
          // checking again next time is fast.
          if (entry == null)
            _pinCallerAppToNewEntry = true;
          continueRequest();
          break;
        case AppLinkResult.NotLinked:
          var message = GetString(Resource.String.passkey_app_not_linked, appLabel, relyingPartyId);
          Toast.MakeText(this, message, ToastLength.Long)?.Show();
          rejectRequest(message);
          break;
        default:
          var builder = new MaterialAlertDialogBuilder(this);
          builder.SetTitle(GetString(Resource.String.passkey_app_unverified_title));
          builder.SetMessage(GetString(Resource.String.passkey_app_unverified_message, appLabel, app.PackageName, relyingPartyId));
          // Not pinned: the app will be checked again next time
          builder.SetPositiveButton(GetString(Resource.String.passkey_app_unverified_continue), (_, _) => continueRequest());
          builder.SetNegativeButton(GetString(Android.Resource.String.Cancel), (_, _) => cancelRequest());
          var dialog = builder.Create();
          dialog.CancelEvent += (_, _) => cancelRequest();
          dialog.Show();
          break;
      }
    }

    private static PwEntry? FindEntry(string? entryId)
    {
      if (string.IsNullOrEmpty(entryId))
        return null;
      var entryUuid = new PwUuid(MemUtil.HexStringToByteArray(entryId));
      var lastOpenedEntry = App.Kp2a.LastOpenedEntry;
      if (lastOpenedEntry != null && entryUuid.Equals(lastOpenedEntry.Uuid))
        return lastOpenedEntry.Entry;
      foreach (Database db in App.Kp2a.OpenDatabases)
      {
        if (db.EntriesById.TryGetValue(entryUuid, out var entry))
          return entry;
      }
      return null;
    }

    /// <summary>
    /// Parses caller-supplied creation options. Returns null (and logs the details) if the JSON is malformed.
    /// </summary>
    private static PublicKeyCredentialCreationOptions? TryParseCreationOptions(string requestJson, byte[]? clientDataHash)
    {
      try
      {
        var options = new PublicKeyCredentialCreationOptions(requestJson, clientDataHash);
        if (string.IsNullOrEmpty(options.RelyingPartyEntity?.Id))
          throw new InvalidOperationException("Missing rp.id");
        return options;
      }
      catch (Exception e)
      {
        Kp2aLog.Log($"Kp2aCredentialLauncherActivity: invalid passkey creation request: {e.GetType().Name}: {e.Message}");
        return null;
      }
    }

    /// <summary>
    /// Parses caller-supplied request options. Returns null (and logs the details) if the JSON is malformed.
    /// </summary>
    private static PublicKeyCredentialRequestOptions? TryParseRequestOptions(string requestJson)
    {
      try
      {
        return new PublicKeyCredentialRequestOptions(requestJson);
      }
      catch (Exception e)
      {
        Kp2aLog.Log($"Kp2aCredentialLauncherActivity: invalid passkey get request: {e.GetType().Name}: {e.Message}");
        return null;
      }
    }

    #endregion


    #region Passkey Handler Methods

    /// <summary>
    /// Handles the request to create a new passkey.
    /// If user verification is required, shows device credential/biometric prompt first.
    /// </summary>
    /// <param name="browserCheckDone">True if the user was already asked about adding an unknown browser.</param>
    /// <param name="appCheckDone">True if the calling app has already been verified (see <see cref="VerifyCallingApp"/>).</param>
    private void HandleCreatePasskeyRequest(Intent requestIntent, bool browserCheckDone = false, bool appCheckDone = false)
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

        // Parse the caller-supplied JSON before showing any dialog. Malformed input must result in an error
        // response to Credential Manager, not in a crash.
        var creationOptions = TryParseCreationOptions(requestJson, passkeyRequest.GetClientDataHash());
        if (creationOptions == null)
        {
          SetUpFailureResponseForCreateAndFinish(InvalidRequestMessage);
          return;
        }
        _passkeyCreationOptions = creationOptions;

        if (!browserCheckDone && AskToAddUnknownBrowser(createRequest.CallingAppInfo,
              continueRequest: () => HandleCreatePasskeyRequest(requestIntent, browserCheckDone: true),
              cancelRequest: SetUpCancellationResponseForCreateAndFinish))
        {
          return;
        }

        if (!appCheckDone && VerifyCallingApp(createRequest.CallingAppInfo, _passkeyCreationOptions.RelyingPartyEntity.Id, entry: null,
              continueRequest: () => HandleCreatePasskeyRequest(requestIntent, browserCheckDone: true, appCheckDone: true),
              rejectRequest: SetUpFailureResponseForCreateAndFinish,
              cancelRequest: SetUpCancellationResponseForCreateAndFinish))
        {
          return;
        }

        var uvDecision = DecideUserVerification(_passkeyCreationOptions.UserVerificationRequirement);

        if (uvDecision == UserVerificationDecision.Skip || uvDecision == UserVerificationDecision.AlreadyVerified)
        {
          _userVerifiedForCreate = uvDecision == UserVerificationDecision.AlreadyVerified;
          PreparePasskeyCreationAndLaunchEntryCreation(requestIntent);
          return;
        }

        if (uvDecision == UserVerificationDecision.Unavailable)
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
          onCancelOrError: SetUpCancellationResponseForCreateAndFinish);
      }
      catch (Exception e)
      {
        Kp2aLog.Log($"Kp2aCredentialLauncherActivity: Error creating passkey: {e.Message}");
        SetUpFailureResponseForCreateAndFinish(e.Message);
      }
    }

    private enum UserVerificationDecision
    {
      Skip,
      Prompt,
      AlreadyVerified,
      Unavailable
    }

    /// <summary>
    /// A user verification (biometric database unlock or UV prompt) within this time is reused, so the user is not asked twice.
    /// </summary>
    private static readonly TimeSpan RecentUserVerificationMaxAge = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Decides whether to show the user verification prompt. "required" always prompts. "preferred" prompts if the
    /// force-UV setting is on (default). A user verification within the last 60 s (biometric database unlock or
    /// UV prompt) replaces the prompt.
    /// Without a screen lock, "preferred" falls back to no verification (UV=0) instead of failing; only "required"
    /// is refused then.
    /// </summary>
    private UserVerificationDecision DecideUserVerification(Kp2aPasskey.Core.UserVerificationRequirement requirement)
    {
      var required = requirement == Kp2aPasskey.Core.UserVerificationRequirement.Required;
      var wanted = required
        || (requirement == Kp2aPasskey.Core.UserVerificationRequirement.Preferred
            && PasskeyPreferences.GetForceUserVerificationWhenPreferred(this));
      if (!wanted)
        return UserVerificationDecision.Skip;
      if (App.Kp2a.WasUserVerifiedWithin(RecentUserVerificationMaxAge))
        return UserVerificationDecision.AlreadyVerified;
      if (UserVerificationHelper.CanAuthenticate(this))
        return UserVerificationDecision.Prompt;
      return required ? UserVerificationDecision.Unavailable : UserVerificationDecision.Skip;
    }

    private void SetUpCancellationResponseForCreateAndFinish()
    {
      var result = new Intent();
      PendingIntentHandler.SetCreateCredentialException(result, new CreateCredentialCancellationException());
      SetResult(Result.Ok, result);
      if (!IsFinishing) Finish();
    }

    private void SetUpCancellationResponseForGetAndFinish()
    {
      var result = new Intent();
      PendingIntentHandler.SetGetCredentialException(result, new GetCredentialCancellationException());
      SetResult(Result.Ok, result);
      if (!IsFinishing) Finish();
    }

    private void CancelRequestAndFinish(int requestType)
    {
      switch (requestType)
      {
        case CredentialRequestTypeCreatePassword:
        case CredentialRequestTypeCreatePasskey:
          SetUpCancellationResponseForCreateAndFinish();
          break;
        case CredentialRequestTypeGetPasswordForEntry:
        case CredentialRequestTypeGetPasskeyForEntry:
          SetUpCancellationResponseForGetAndFinish();
          break;
        default:
          SetResult(Result.Canceled);
          Finish();
          break;
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
      // Build client data response with the verified origin. A caller-supplied clientDataHash
      // is only used if the caller is a verified privileged app (browser), otherwise it is ignored.
      var callerOrigin = PasskeyOriginHelper.ResolveCallerOrigin(
        this,
        callingAppInfo,
        PasskeyOptionParsingHelper.ExtractClientDataHashFromOption(passkeyOption));
      var clientDataResponse = PasskeyOriginHelper.CreateClientDataResponse(
        callerOrigin,
        ClientDataBuildResponse.RequestType.Get,
        requestOptions.Challenge);

      var assertionBe = passkey.BackupEligibility ?? PasskeyPreferences.BackupEligibilityForEntriesWithoutFlags;
      var assertionBs = assertionBe && (passkey.BackupState ?? PasskeyPreferences.BackupStateForEntriesWithoutFlags);

      // Build assertion response
      var assertionResponse = new AuthenticatorAssertionResponse(
        requestOptions: requestOptions,
        userPresent: true,
        userVerified: userVerified,
        backupEligibility: assertionBe,
        backupState: assertionBs,
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
    /// <param name="browserCheckDone">True if the user was already asked about adding an unknown browser.</param>
    /// <param name="appCheckDone">True if the calling app has already been verified (see <see cref="VerifyCallingApp"/>).</param>
    private void HandleGetPasskeyForEntryRequest(Intent requestIntent, bool browserCheckDone = false, bool appCheckDone = false)
    {
      // Check if database is locked and launch unlock flow if needed
      if (!App.Kp2a.DatabaseIsUnlocked)
      {
        _pendingOperation = PendingOperation.GetPasskeyForEntryAfterUnlock;
        LaunchUnlockDatabase();
        return;
      }

      // All checks and the user verification decision are based on the final request, i.e. the JSON that will be signed
      // (not on the request JSON from the Begin-Get phase which was stored in the PendingIntent).
      var request = ValidateFinalGetRequestOrFinish(requestIntent);
      if (request == null)
        return;

      if (!browserCheckDone && AskToAddUnknownBrowser(
            request.CallingAppInfo,
            continueRequest: () => HandleGetPasskeyForEntryRequest(requestIntent, browserCheckDone: true),
            cancelRequest: SetUpCancellationResponseForGetAndFinish))
      {
        return;
      }

      if (!appCheckDone
          && VerifyCallingApp(request.CallingAppInfo,
            request.Passkey.RelyingParty, request.Entry,
            continueRequest: () => HandleGetPasskeyForEntryRequest(requestIntent, browserCheckDone: true, appCheckDone: true),
            rejectRequest: SetUpFailureResponseForGetAndFinish,
            cancelRequest: SetUpCancellationResponseForGetAndFinish))
      {
        return;
      }

      var requestOptions = request.RequestOptions;
      var uvDecision = DecideUserVerification(requestOptions.UserVerificationRequirement);

      if (uvDecision == UserVerificationDecision.Skip || uvDecision == UserVerificationDecision.AlreadyVerified)
      {
        ReturnPasskeyAssertionAndFinish(request, userVerified: uvDecision == UserVerificationDecision.AlreadyVerified);
        return;
      }

      if (uvDecision == UserVerificationDecision.Unavailable)
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
          ReturnPasskeyAssertionAndFinish(request, userVerified: true);
        },
        onCancelOrError: SetUpCancellationResponseForGetAndFinish);
    }

    /// <summary>
    /// The final get request for a passkey entry, validated against the entry's passkey.
    /// </summary>
    private sealed record ValidatedPasskeyGetRequest(
      CallingAppInfo? CallingAppInfo,
      GetPublicKeyCredentialOption PasskeyOption,
      PublicKeyCredentialRequestOptions RequestOptions,
      PwEntry Entry,
      PasskeyData Passkey);

    /// <summary>
    /// Retrieves the final get request from <paramref name="requestIntent"/>, parses it and checks it against the passkey
    /// of the selected entry: the relying party must match the request's rpId and, if the request contains a non-empty
    /// allowCredentials list, the credential ID must be contained in it.
    /// Database must already be unlocked before calling this.
    /// On failure, sets an error response, finishes the activity and returns null.
    /// </summary>
    private ValidatedPasskeyGetRequest? ValidateFinalGetRequestOrFinish(Intent requestIntent)
    {
      try
      {
        var getRequest = PendingIntentHandler.RetrieveProviderGetCredentialRequest(requestIntent);
        var options = getRequest?.CredentialOptions;
        if (getRequest == null || options == null || options.Count == 0)
        {
          SetUpFailureResponseForGetAndFinish("No credential options found");
          return null;
        }

        if (options.FirstOrDefault(o => o is GetPublicKeyCredentialOption) is not GetPublicKeyCredentialOption passkeyOption)
        {
          SetUpFailureResponseForGetAndFinish("Invalid credential option type");
          return null;
        }

        var requestJson = passkeyOption.RequestJson;
        if (string.IsNullOrEmpty(requestJson))
        {
          Kp2aLog.Log("Kp2aCredentialLauncherActivity: final passkey get request has no request JSON");
          SetUpFailureResponseForGetAndFinish(InvalidRequestMessage);
          return null;
        }

        var requestOptions = TryParseRequestOptions(requestJson);
        if (requestOptions == null)
        {
          SetUpFailureResponseForGetAndFinish(InvalidRequestMessage);
          return null;
        }

        var entry = FindEntry(requestIntent.GetStringExtra(Strings.ExtraEntryId));
        if (entry == null)
        {
          SetUpNoCredentialResponseForGetAndFinish();
          return null;
        }

        var passkey = PasskeyStorage.RetrievePasskey(entry);
        if (passkey == null)
        {
          SetUpFailureResponseForGetAndFinish("Entry does not contain passkey data");
          return null;
        }

        // The assertion is signed for requestOptions.RpId. It must be the relying party the passkey was created for.
        if (string.IsNullOrEmpty(passkey.RelyingParty)
            || !string.Equals(passkey.RelyingParty, requestOptions.RpId, StringComparison.Ordinal))
        {
          Kp2aLog.Log($"Kp2aCredentialLauncherActivity: refusing passkey get request: rpId '{requestOptions.RpId}' "
                      + $"does not match the passkey's relying party '{passkey.RelyingParty}'");
          SetUpFailureResponseForGetAndFinish("Relying party does not match the selected passkey");
          return null;
        }

        if (!requestOptions.IsCredentialAllowed(passkey.CredentialId))
        {
          Kp2aLog.Log("Kp2aCredentialLauncherActivity: refusing passkey get request: credential ID not in allowCredentials");
          SetUpFailureResponseForGetAndFinish("Selected passkey is not allowed by the request");
          return null;
        }

        return new ValidatedPasskeyGetRequest(getRequest.CallingAppInfo, passkeyOption, requestOptions, entry, passkey);
      }
      catch (Exception e)
      {
        Kp2aLog.Log($"Kp2aCredentialLauncherActivity: Error processing passkey get request: {e.GetType().Name}: {e.Message}");
        SetUpFailureResponseForGetAndFinish(InvalidRequestMessage);
        return null;
      }
    }

    /// <summary>
    /// Builds and returns the passkey assertion response for a validated request.
    /// <param name="userVerified">True if the user was verified in this flow.</param>
    /// </summary>
    private void ReturnPasskeyAssertionAndFinish(ValidatedPasskeyGetRequest request, bool userVerified)
    {
      try
      {
        BuildAndReturnAssertionFromPasskeyAndFinish(request.Passkey, request.CallingAppInfo, request.RequestOptions,
          request.PasskeyOption, userVerified);
      }
      catch (Exception e)
      {
        Kp2aLog.Log($"Kp2aCredentialLauncherActivity: Error retrieving passkey. {e.GetType().Name}: {e.Message}");
        SetUpFailureResponseForGetAndFinish("Passkey retrieval failed");
      }
    }


    #endregion
  }
}

// This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
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

using System.Diagnostics;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Views;
using AndroidX.Core.App;
using AndroidX.Credentials;
using AndroidX.Credentials.Exceptions;
using AndroidX.Credentials.Provider;
using Java.Time;
using Keepass2android.Pluginsdk;
using KeePassLib;
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
    public const int CredentialRequestTypeUnlock = 2;
    public const int CredentialRequestTypeGetPasswordForEntry = 3;
    private const int CreateEntryRequestCode = 100;
    private const int UnlockRequestCode = 300;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
      base.OnCreate(savedInstanceState);

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
            HandleCreatePasswordRequest(intent);
            break;
          case CredentialRequestTypeUnlock:
            HandleUnlockRequest(intent);
            break;
          case CredentialRequestTypeGetPasswordForEntry:
            HandleGetPasswordForEntryRequest(intent);
            break;

          default:
            // should never happen
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
      if (createRequest != null && createRequest?.CallingRequest is CreatePasswordRequest)
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

    private void HandleUnlockRequest(Intent requestIntent)
    {
      var getRequest = PendingIntentHandler.RetrieveBeginGetCredentialRequest(requestIntent);
      var callingPackage = getRequest?.CallingAppInfo?.PackageName;

      if (getRequest == null || callingPackage == null)
      {
        //should never happen
        return;
      }

      var query = $"{KeePass.AndroidAppScheme}{callingPackage}";
      //launch SelectCurrentDbActivity (which is root of the stack (exception: we're even below!)) with the appropriate task.
      //will return the results later
      Intent forwardIntent = new(this, typeof(SelectCurrentDbActivity));
      //don't show user notifications when an entry is opened.
      var task = new SearchUrlTask() { UrlToSearchFor = query };
      task.ToIntent(forwardIntent);
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

      var option = options.First();
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
    /// Saves the password and sets the response back to the calling app.
    /// </summary>
    /// <param name="resultCode">The result code of forward intent.</param>
    private void SetupCreateCredentialResponseAndFinish([GeneratedEnum] Result resultCode)
    {
      var result = new Intent();
      if (resultCode == KeePass.ExitCloseAfterTaskComplete)
      {
        PendingIntentHandler.SetCreateCredentialResponse(result, new CreatePasswordResponse());
        SetResult(Result.Ok, result);
      }
      else
      {
        PendingIntentHandler.SetCreateCredentialException(
          result,
          new CreateCredentialCancellationException()
        );
        SetResult(Result.Canceled, result);
      }
      if (!IsFinishing)
      {
        Finish();
      }
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
        var getRequest = PendingIntentHandler.RetrieveBeginGetCredentialRequest(thisIntent);
        var options = getRequest?.BeginGetCredentialOptions;
        if (options != null && options.Count >= 0)
        {
          var option = options.First();
          if (option != null && option is BeginGetPasswordOption)
          {
            var result = new Intent();
            if (resultCode == KeePass.ExitCloseAfterTaskComplete)
            {
              var response = new BeginGetCredentialResponse.Builder();
              var entry = App.Kp2a.LastOpenedEntry;
              var username = entry?.Entry.Strings.ReadSafe(PwDefs.UserNameField);
              if (entry != null && !string.IsNullOrEmpty(username))
              {
                var lastAccessTime =
                  Build.VERSION.SdkInt >= BuildVersionCodes.O
                    ? Instant.OfEpochMilli(
                      new DateTimeOffset(entry.Entry.LastAccessTime).ToUnixTimeMilliseconds()
                    )
                    : null;

                response.AddCredentialEntry(
                  new PasswordCredentialEntry.Builder(
                    this,
                    username,
                    PendingIntentCompat.GetActivity(
                      this,
                      App.Kp2a.RequestCodeForCredentialProvider,
                      new Intent(this, typeof(Kp2aCredentialLauncherActivity))
                        .PutExtra(
                          CredentialRequestTypeKey,
                          CredentialRequestTypeGetPasswordForEntry
                        )
                        .PutExtra(Strings.ExtraEntryId, entry.Entry.Uuid.ToHexString()),
                      (int)PendingIntentFlags.UpdateCurrent,
                      true
                    )!,
                    option.JavaCast<BeginGetPasswordOption>()
                  )
                    .SetDisplayName(entry.Entry.Strings.ReadSafe(PwDefs.TitleField))
                    .SetAffiliatedDomain(entry.Entry.ParentGroup?.Name)
                    .SetLastUsedTime(lastAccessTime)
                    //TODO SetIcon() of the entry
                    .Build()
                );
              }
              PendingIntentHandler.SetBeginGetCredentialResponse(result, response.Build());
              SetResult(Result.Ok, result);
            }
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
      if (username == null || string.IsNullOrEmpty(password))
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
      var result = new Intent();
      PendingIntentHandler.SetGetCredentialException(result, new NoCredentialException());
      SetResult(Result.Ok, result);

      if (!IsFinishing)
      {
        Finish();
      }
    }
  }
}

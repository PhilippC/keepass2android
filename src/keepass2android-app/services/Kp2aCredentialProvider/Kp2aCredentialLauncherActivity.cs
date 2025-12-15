using Android.Content;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using AndroidX.Credentials;
using AndroidX.Credentials.Exceptions;
using AndroidX.Credentials.Provider;
using Keepass2android.Pluginsdk;
using KeePassLib;
using Org.Json;

namespace keepass2android.services.Kp2aCredentialProvider
{
  [Activity(Label = AppNames.AppName,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden,
    Theme = "@style/Kp2aTheme_ActionBar",
    WindowSoftInputMode = SoftInput.AdjustResize,
    Permission = "keepass2android." + AppNames.PackagePart + ".permission.Kp2aChooseAutofill")]
  public class Kp2aCredentialLauncherActivity : AndroidX.AppCompat.App.AppCompatActivity
  {
    public const string CredentialRequestTypeKey = "credential_request_type";
    public const int CredentialRequestTypeCreatePassword = 1;
    private const int CreateEntryRequestCode = 100;

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

          default:
            // should never happen
            SetResult(Result.Canceled);
            Finish();
            break;
        }
      }
    }

    private void HandleCreatePasswordRequest(Intent intent)
    {
      var createRequest = PendingIntentHandler.RetrieveProviderCreateCredentialRequest(intent);
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
              (System.Collections.ICollection)Array.Empty<string>());
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
    protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent? data)
    {
      base.OnActivityResult(requestCode, resultCode, data);
      if (requestCode == CreateEntryRequestCode)
      {
        var result = new Intent();
        if (resultCode == KeePass.ExitCloseAfterTaskComplete)
        {
          PendingIntentHandler.SetCreateCredentialResponse(
            result,
            new CreatePasswordResponse()
          );
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
    }

    /// <summary>
    /// If the request is null, send an unknown exception to client and finish the flow.
    /// </summary>
    /// <param name="message">The error message to send to the client.</param>
    private void SetUpFailureResponseForCreateAndFinish(string message)
    {
      var result = new Intent();
      PendingIntentHandler.SetCreateCredentialException(
          result,
          new CreateCredentialUnknownException(message)
      );
      SetResult(Result.Ok, result);
      Finish();
    }
  }
}
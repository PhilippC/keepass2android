using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using keepass2android.Io;
using keepass2android.Utils;

namespace keepass2android
{
	[Activity(Label = "")]
	public class SelectStorageLocationActivity : SelectStorageLocationActivityBase, IDialogInterfaceOnDismissListener
	{
		private ActivityDesign _design;
		
		private const string BundleKeySelectedIoc = "BundleKeySelectedIoc";
		
		
		public const string ExtraKeyWritableRequirements = "EXTRA_KEY_WRITABLE_REQUIREMENTS";

		public SelectStorageLocationActivity() : base(App.Kp2a)
		{
			_design = new ActivityDesign(this);
		}

		protected override void OnCreate(Bundle bundle)
		{
			_design.ApplyTheme();
			base.OnCreate(bundle);
			
			


			Kp2aLog.Log("SelectStorageLocationActivity.OnCreate");

			
			if (IsStorageSelectionForSave)
			{
				throw new Exception("save is not yet implemented. In StartSelectFile, no handler for onCreate is passed.");
			}

			bool allowThirdPartyGet = Intent.GetBooleanExtra(FileStorageSelectionActivity.AllowThirdPartyAppGet, false);
			bool allowThirdPartySend = Intent.GetBooleanExtra(FileStorageSelectionActivity.AllowThirdPartyAppSend, false);

			bool isRecreated = false;
			if (bundle == null)
				State = new Bundle();
			else
			{
				State = (Bundle)bundle.Clone();
				var selectedIocString = bundle.GetString(BundleKeySelectedIoc, null);
				if (selectedIocString != null)
					_selectedIoc = IOConnectionInfo.UnserializeFromString(selectedIocString);
				isRecreated = true;
			}

			//todo: handle orientation change while dialog is shown

			if (!isRecreated)
			{
				StartFileStorageSelection(RequestCodeFileStorageSelectionForPrimarySelect, allowThirdPartyGet, allowThirdPartySend);
			}
				

		}

		protected Bundle State { get; set; }

		protected override void ShowToast(string text)
		{
			Toast.MakeText(this, text, ToastLength.Long).Show();
		}

		protected override void ShowInvalidSchemeMessage(string dataString)
		{
			Toast.MakeText(this, Resources.GetString(Resource.String.unknown_uri_scheme, new Java.Lang.Object[] { dataString }),
										   ToastLength.Long).Show();
		}

		protected override string IntentToFilename(Intent data)
		{
			return Util.IntentToFilename(data, this);
		}

		protected override void SetIoConnectionFromIntent(IOConnectionInfo ioc, Intent data)
		{
			PasswordActivity.SetIoConnectionFromIntent(ioc, data);
		}

		protected override Result ExitFileStorageSelectionOk
		{
			get { return KeePass.ExitFileStorageSelectionOk; }
		}

		protected override void StartSelectFile( bool isForSave, int browseRequestCode, string protocolId)
		{
			FileSelectHelper fileSelectHelper = new FileSelectHelper(this, isForSave, browseRequestCode);
			fileSelectHelper.OnOpen += (sender, ioc) =>
			{
				IocSelected(ioc,browseRequestCode);
			};

			App.Kp2a.GetFileStorage(protocolId).StartSelectFile(new FileStorageSetupInitiatorActivity(this,
																												  OnActivityResult,
																												  s => fileSelectHelper.PerformManualFileSelect(s)
																				), isForSave, browseRequestCode, protocolId);
		}

		protected override void ShowAndroidBrowseDialog(int requestCode, bool isForSave, bool tryGetPermanentAccess)
		{
			Util.ShowBrowseDialog(this, requestCode, isForSave, tryGetPermanentAccess);
		}

		protected override bool IsStorageSelectionForSave 
		{ 
			get { return Intent.GetBooleanExtra(FileStorageSetupDefs.ExtraIsForSave, false); }
		}


		protected override void OnSaveInstanceState(Bundle outState)
		{
			base.OnSaveInstanceState(outState);

			outState.PutAll(State);
			if (_selectedIoc != null)
				outState.PutString(BundleKeySelectedIoc, IOConnectionInfo.SerializeToString(_selectedIoc));
		}

		protected override void OnResume()
		{
			base.OnResume();
			_design.ReapplyTheme();
		}


		protected override void ReturnCancel()
		{
			SetResult(Result.Canceled);
			Finish();
		}

		protected override void ReturnOk(IOConnectionInfo ioc)
		{
			Intent intent = new Intent();
			PasswordActivity.PutIoConnectionToIntent(ioc, intent);
			SetResult(Result.Ok, intent);
			Finish();
		}

		protected override void ShowAlertDialog(string message, EventHandler<DialogClickEventArgs> onOk, EventHandler<DialogClickEventArgs> onCancel)
		{
			new AlertDialog.Builder(this)
					.SetPositiveButton(Android.Resource.String.Ok, onOk)
					.SetMessage(message)
					.SetCancelable(false)
					.SetNegativeButton(Android.Resource.String.Cancel, onCancel)
					.Create()
					.Show();
				
		}

		protected override WritableRequirements RequestedWritableRequirements
		{
			get { return (WritableRequirements) Intent.GetIntExtra(ExtraKeyWritableRequirements, (int)WritableRequirements.ReadOnly); }
		}

		

		protected override void PerformCopy(Func<Action> copyAndReturnPostExecute)
		{

			new SimpleLoadingDialog(this, GetString(Resource.String.CopyingFile), false,
			                      copyAndReturnPostExecute  
				).Execute();
		}

		protected override void StartFileStorageSelection(int requestCode, bool allowThirdPartyGet,
		                                                  bool allowThirdPartySend)
		{
			#if !EXCLUDE_FILECHOOSER

			Intent intent = new Intent(this, typeof(FileStorageSelectionActivity));
			intent.PutExtra(FileStorageSelectionActivity.AllowThirdPartyAppGet, allowThirdPartyGet);
			intent.PutExtra(FileStorageSelectionActivity.AllowThirdPartyAppSend, allowThirdPartySend);

			StartActivityForResult(intent, requestCode);
			#else
			Toast.MakeText(this, "File chooser is excluded!", ToastLength.Long).Show();
			#endif
		}

		protected override void StartFileChooser(string defaultPath, int requestCode, bool forSave)
		{
			new FileSelectHelper(this, forSave, requestCode).StartFileChooser(defaultPath);
		}



		public void OnDismiss(IDialogInterface dialog)
		{
//			ReturnCancel();
		}

		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			Kp2aLog.Log("onAR");
			base.OnActivityResult(requestCode, resultCode, data);
		}

	}


}
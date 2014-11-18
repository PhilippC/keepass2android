using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Group.Pals.Android.Lib.UI.Filechooser.Utils.UI;
using KeePassLib.Serialization;
using keepass2android.Io;
using Environment = Android.OS.Environment;

namespace keepass2android
{
	[Activity(Label = "")]
	public class SelectStorageLocationActivity : Activity, IDialogInterfaceOnDismissListener
	{
		private ActivityDesign _design;
		private bool _isRecreated;
		private IOConnectionInfo _selectedIoc;
		private const string BundleKeySelectedIoc = "BundleKeySelectedIoc";
		private const int RequestCodeFileStorageSelectionForPrimarySelect = 983713;
		private const int RequestCodeFileStorageSelectionForCopyToWritableLocation = 983714;
		private const int RequestCodeFileFileBrowseForWritableLocation = 983715;

		public enum WritableRequirements
		{
			ReadOnly = 0,
			WriteDesired = 1,
			WriteDemanded = 2
		}

		public const string ExtraKeyWritableRequirements = "EXTRA_KEY_WRITABLE_REQUIREMENTS";

		public SelectStorageLocationActivity()
		{
			_design = new ActivityDesign(this);
		}

		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);
			
			_design.ApplyTheme();


			Kp2aLog.Log("SelectStorageLocationActivity.OnCreate");

			
			if (IsStorageSelectionForSave)
			{
				throw new Exception("save is not yet implemented. In StartSelectFile, no handler for onCreate is passed.");
			}

			bool allowThirdPartyGet = Intent.GetBooleanExtra(FileStorageSelectionActivity.AllowThirdPartyAppGet, false);
			bool allowThirdPartySend = Intent.GetBooleanExtra(FileStorageSelectionActivity.AllowThirdPartyAppSend, false);
			if (bundle == null)
				State = new Bundle();
			else
			{
				State = (Bundle)bundle.Clone();
				var selectedIocString = bundle.GetString(BundleKeySelectedIoc, null);
				if (selectedIocString != null)
					_selectedIoc = IOConnectionInfo.UnserializeFromString(selectedIocString);
				_isRecreated = true;
			}

			if (!_isRecreated)
			{
				Intent intent = new Intent(this, typeof(FileStorageSelectionActivity));
				intent.PutExtra(FileStorageSelectionActivity.AllowThirdPartyAppGet, allowThirdPartyGet);
				intent.PutExtra(FileStorageSelectionActivity.AllowThirdPartyAppSend, allowThirdPartySend);
				StartActivityForResult(intent, RequestCodeFileStorageSelectionForPrimarySelect);	
			}
				

		}

		protected Bundle State { get; set; }

		protected bool IsStorageSelectionForSave 
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

		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);
			if ((requestCode == RequestCodeFileStorageSelectionForPrimarySelect) || ((requestCode == RequestCodeFileStorageSelectionForCopyToWritableLocation)))
			{
				int browseRequestCode = Intents.RequestCodeFileBrowseForOpen;
				if (requestCode == RequestCodeFileStorageSelectionForCopyToWritableLocation)
				{
					browseRequestCode = RequestCodeFileFileBrowseForWritableLocation;
				}

				if (resultCode == KeePass.ExitFileStorageSelectionOk)
				{

					string protocolId = data.GetStringExtra("protocolId");

					if (protocolId == "androidget")
					{
						Util.ShowBrowseDialog(this, Intents.RequestCodeFileBrowseForOpen, false);
					}
					else
					{
						bool isForSave = (requestCode == RequestCodeFileStorageSelectionForPrimarySelect) ?
							IsStorageSelectionForSave : true;
						
						App.Kp2a.GetFileStorage(protocolId).StartSelectFile(new FileStorageSetupInitiatorActivity(this,
							OnActivityResult,
							defaultPath =>
								{
								if (defaultPath.StartsWith("sftp://"))
									Util.ShowSftpDialog(this, filename => OnReceivedSftpData(filename, browseRequestCode, isForSave), ReturnCancel);
								else
									//todo oncreate nur wenn for save?
									Util.ShowFilenameDialog(this, filename => OnOpenButton(filename, browseRequestCode),
												filename => OnOpenButton(filename, browseRequestCode), 
												ReturnCancel, false, defaultPath, GetString(Resource.String.enter_filename_details_url),
													browseRequestCode);
							}
							), isForSave, browseRequestCode, protocolId);
					}


				}
				else
				{
					ReturnCancel();
				}
	
			}

			if ((requestCode == Intents.RequestCodeFileBrowseForOpen) || (requestCode == RequestCodeFileFileBrowseForWritableLocation))
			{
				if (resultCode == (Result)FileStorageResults.FileChooserPrepared)
				{
					IOConnectionInfo ioc = new IOConnectionInfo();
					PasswordActivity.SetIoConnectionFromIntent(ioc, data);
#if !EXCLUDE_FILECHOOSER
					bool isForSave = (requestCode == RequestCodeFileFileBrowseForWritableLocation) ?
						true : IsStorageSelectionForSave ;
						
					StartFileChooser(ioc.Path, requestCode, isForSave);
#else
						IocSelected(new IOConnectionInfo { Path = "/mnt/sdcard/keepass/yubi.kdbx" }, requestCode);
#endif
					return;
				}
				if ((resultCode == Result.Canceled) && (data != null) && (data.HasExtra("EXTRA_ERROR_MESSAGE")))
				{
					Toast.MakeText(this, data.GetStringExtra("EXTRA_ERROR_MESSAGE"), ToastLength.Long).Show();
				}

				if (resultCode == Result.Ok)
				{
					string filename = Util.IntentToFilename(data, this);
					if (filename != null)
					{
						if (filename.StartsWith("file://"))
						{
							filename = filename.Substring(7);
							filename = Java.Net.URLDecoder.Decode(filename);
						}

						IOConnectionInfo ioc = new IOConnectionInfo
							{
								Path = filename
							};

						IocSelected(ioc, requestCode);
					}
					else
					{
						if (data.Data.Scheme == "content")
						{
							IocSelected(IOConnectionInfo.FromPath(data.DataString), requestCode);
	
						}
						else
						{
							Toast.MakeText(this, Resources.GetString(Resource.String.unknown_uri_scheme, new Java.Lang.Object[] {data.DataString}),
							               ToastLength.Long).Show();
							ReturnCancel();
						}
						
					}
				}
				else
				{
					ReturnCancel();	
				}
				
				
			}
				


			
		}

		private void ReturnCancel()
		{
			SetResult(Result.Canceled);
			Finish();
		}

		private void IocSelected(IOConnectionInfo ioc, int requestCode)
		{
			if (requestCode == RequestCodeFileFileBrowseForWritableLocation)
			{
				IocForCopySelected(ioc);
			}
			else if (requestCode == Intents.RequestCodeFileBrowseForOpen)
			{
				PrimaryIocSelected(ioc);
			}
			else
			{
#if DEBUG
				throw new Exception("invalid request code!");
#endif
			}

			

		}

		private void IocForCopySelected(IOConnectionInfo targetIoc)
		{
			new keepass2android.Utils.SimpleLoadingDialog(this, GetString(Resource.String.CopyingFile), false,
			() =>
				{
					IOConnectionInfo sourceIoc = _selectedIoc;

					try
					{
						CopyFile(targetIoc, sourceIoc);
					}
					catch (Exception e)
					{
						return () =>
							{
								Toast.MakeText(this, App.Kp2a.GetResourceString(UiStringKey.ErrorOcurred) + " " + e.Message, ToastLength.Long).Show();
								ReturnCancel();
							};
					}
					

					return () => {ReturnOk(targetIoc); };
				}
			).Execute(new Object[] {});
		}

		private static void CopyFile(IOConnectionInfo targetIoc, IOConnectionInfo sourceIoc)
		{
			IFileStorage sourceStorage = App.Kp2a.GetFileStorage(sourceIoc);
			IFileStorage targetStorage = App.Kp2a.GetFileStorage(targetIoc);

			using (
				var writeTransaction = targetStorage.OpenWriteTransaction(targetIoc,
				                                                          App.Kp2a.GetBooleanPreference(
					                                                          PreferenceKey.UseFileTransactions)))
			{
				using (var writeStream = writeTransaction.OpenFile())
				{
					sourceStorage.OpenFileForRead(sourceIoc).CopyTo(writeStream);
				}
				writeTransaction.CommitWrite();
			}
		}

		private void PrimaryIocSelected(IOConnectionInfo ioc)
		{
			if (!App.Kp2a.GetFileStorage(ioc).IsPermanentLocation(ioc))
			{
				new AlertDialog.Builder(this)
					.SetPositiveButton(Android.Resource.String.Ok, (sender, args) => { MoveToWritableLocation(ioc); })
					.SetMessage(Resources.GetString(Resource.String.FileIsTemporarilyAvailable) + " "
							+ Resources.GetString(Resource.String.CopyFileRequired) + " "
							+ Resources.GetString(Resource.String.ClickOkToSelectLocation))
					.SetCancelable(false)
					.SetNegativeButton(Android.Resource.String.Cancel, (sender, args) => { ReturnCancel(); })
					//.SetOnDismissListener(this)
					.Create()
					.Show();
				return;
			}
			var filestorage = App.Kp2a.GetFileStorage(ioc);

			if ((RequestedWritableRequirements != WritableRequirements.ReadOnly) && (filestorage.IsReadOnly(ioc)))
			{
				string readOnlyExplanation = Resources.GetString(Resource.String.FileIsReadOnly);
				BuiltInFileStorage builtInFileStorage = filestorage as BuiltInFileStorage;
				if (builtInFileStorage != null)
				{
					if (builtInFileStorage.IsReadOnlyBecauseKitkatRestrictions(ioc))
						readOnlyExplanation = Resources.GetString(Resource.String.FileIsReadOnlyOnKitkat);
				}
				new AlertDialog.Builder(this)
						.SetPositiveButton(Android.Resource.String.Ok, (sender, args) => { MoveToWritableLocation(ioc); })
						.SetCancelable(false)
						.SetNegativeButton(Android.Resource.String.Cancel, (sender, args) => { ReturnCancel(); })
					//.SetOnDismissListener(this)
						.SetMessage(readOnlyExplanation + " "
							+ (RequestedWritableRequirements == WritableRequirements.WriteDemanded ?
								Resources.GetString(Resource.String.CopyFileRequired)
								: Resources.GetString(Resource.String.CopyFileRequiredForEditing))
							+ " "
							+ Resources.GetString(Resource.String.ClickOkToSelectLocation))
						.Create()
						.Show();
				return;
			}
			ReturnOk(ioc);
		}

		private void ReturnOk(IOConnectionInfo ioc)
		{
			Intent intent = new Intent();
			PasswordActivity.PutIoConnectionToIntent(ioc, intent);
			SetResult(Result.Ok, intent);
			Finish();
		}

		private WritableRequirements RequestedWritableRequirements
		{
			get { return (WritableRequirements) Intent.GetIntExtra(ExtraKeyWritableRequirements, (int)WritableRequirements.ReadOnly); }
		}

		private void MoveToWritableLocation(IOConnectionInfo ioc)
		{
			_selectedIoc = ioc;

			Intent intent = new Intent(this, typeof(FileStorageSelectionActivity));
			intent.PutExtra(FileStorageSelectionActivity.AllowThirdPartyAppGet, false);
			intent.PutExtra(FileStorageSelectionActivity.AllowThirdPartyAppSend, false);
			
			StartActivityForResult(intent, RequestCodeFileStorageSelectionForCopyToWritableLocation);	

		}

		private bool OnReceivedSftpData(string filename, int requestCode, bool isForSave)
		{
			IOConnectionInfo ioc = new IOConnectionInfo { Path = filename };
#if !EXCLUDE_FILECHOOSER
			StartFileChooser(ioc.Path, requestCode, isForSave);
#else
			IocSelected(ioc, requestCode);
#endif
			return true;
		}

#if !EXCLUDE_FILECHOOSER
		private void StartFileChooser(string defaultPath, int requestCode, bool forSave)
		{
			Kp2aLog.Log("FSA: defaultPath="+defaultPath);
			string fileProviderAuthority = FileChooserFileProvider.TheAuthority;
			if (defaultPath.StartsWith("file://"))
			{
				fileProviderAuthority = PackageName+".android-filechooser.localfile";
			}
			Intent i = Keepass2android.Kp2afilechooser.Kp2aFileChooserBridge.GetLaunchFileChooserIntent(this, fileProviderAuthority,
			                                                                                            defaultPath);


			if (forSave)
			{
				i.PutExtra("group.pals.android.lib.ui.filechooser.FileChooserActivity.save_dialog", true);
				i.PutExtra("group.pals.android.lib.ui.filechooser.FileChooserActivity.default_file_ext", "kdbx");
			}
			StartActivityForResult(i, requestCode);
		}

#endif
		private bool OnOpenButton(String fileName, int requestCode)
		{
			

			IOConnectionInfo ioc = new IOConnectionInfo
			{
				Path = fileName
			};

			IocSelected(ioc, requestCode);

			return true;

		}

		public void OnDismiss(IDialogInterface dialog)
		{
//			ReturnCancel();
		}
	}


}
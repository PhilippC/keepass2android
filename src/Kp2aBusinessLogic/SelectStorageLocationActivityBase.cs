using System;
using Android.App;
using Android.Content;
using Android.Widget;
using Java.Net;
using KeePassLib.Serialization;
using keepass2android.Io;

namespace keepass2android
{
	/// <summary>
	/// base class for SelectStorageLocationActivity containing testable (non-UI) code
	/// </summary>
	public abstract class SelectStorageLocationActivityBase: Activity
	{
		public enum WritableRequirements
		{
			ReadOnly = 0,
			WriteDesired = 1,
			WriteDemanded = 2
		}

		protected const int RequestCodeFileStorageSelectionForPrimarySelect = 983713;
		private const int RequestCodeFileStorageSelectionForCopyToWritableLocation = 983714;
		private const int RequestCodeFileFileBrowseForWritableLocation = 983715;
		private const int RequestCodeFileBrowseForOpen = 983716;
		

		protected IOConnectionInfo _selectedIoc;
		private IKp2aApp _app;

		public SelectStorageLocationActivityBase(IKp2aApp app)
		{
			_app = app;
		}

		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);
			if ((requestCode == RequestCodeFileStorageSelectionForPrimarySelect) || ((requestCode == RequestCodeFileStorageSelectionForCopyToWritableLocation)))
			{
				int browseRequestCode = RequestCodeFileBrowseForOpen;
				if (requestCode == RequestCodeFileStorageSelectionForCopyToWritableLocation)
				{
					browseRequestCode = RequestCodeFileFileBrowseForWritableLocation;
				}

				if (resultCode == ExitFileStorageSelectionOk)
				{

					string protocolId = data.GetStringExtra("protocolId");

					if (protocolId == "androidget")
					{
						ShowAndroidBrowseDialog(RequestCodeFileBrowseForOpen, false);
					}
					else
					{
						bool isForSave = (requestCode == RequestCodeFileStorageSelectionForPrimarySelect) ?
							                 IsStorageSelectionForSave : true;

						
						StartSelectFile(isForSave, browseRequestCode, protocolId);
						
					}


				}
				else
				{
					ReturnCancel();
				}

			}

			if ((requestCode == RequestCodeFileBrowseForOpen) || (requestCode == RequestCodeFileFileBrowseForWritableLocation))
			{
				if (resultCode == (Result)FileStorageResults.FileChooserPrepared)
				{
					IOConnectionInfo ioc = new IOConnectionInfo();
					SetIoConnectionFromIntent(ioc, data);
					bool isForSave = (requestCode == RequestCodeFileFileBrowseForWritableLocation) ?
						                 true : IsStorageSelectionForSave;

					StartFileChooser(ioc.Path, requestCode, isForSave);

					return;
				}
				if ((resultCode == Result.Canceled) && (data != null) && (data.HasExtra("EXTRA_ERROR_MESSAGE")))
				{
					ShowToast(data.GetStringExtra("EXTRA_ERROR_MESSAGE"));
				}

				if (resultCode == Result.Ok)
				{
					string filename = IntentToFilename(data);
					if (filename != null)
					{
						if (filename.StartsWith("file://"))
						{
							filename = filename.Substring(7);
							filename = URLDecoder.Decode(filename);
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
							ShowInvalidSchemeMessage(data.DataString);
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

		protected abstract void ShowToast(string text);

		protected abstract void ShowInvalidSchemeMessage(string dataString);

		protected abstract string IntentToFilename(Intent data);

		protected abstract void SetIoConnectionFromIntent(IOConnectionInfo ioc, Intent data);

		protected abstract Result ExitFileStorageSelectionOk { get; }

		/// <summary>
		/// Starts the appropriate file selection process (either manual file select or prepare filechooser + filechooser)
		/// </summary>
		/// <param name="isForSave"></param>
		/// <param name="browseRequestCode"></param>
		/// <param name="protocolId"></param>
		protected abstract void StartSelectFile(bool isForSave, int browseRequestCode, string protocolId);

		protected abstract void ShowAndroidBrowseDialog(int requestCode, bool isForSave);

		protected abstract bool IsStorageSelectionForSave { get; }


		private void IocSelected(IOConnectionInfo ioc, int requestCode)
		{
			if (requestCode == RequestCodeFileFileBrowseForWritableLocation)
			{
				IocForCopySelected(ioc);
			}
			else if (requestCode == RequestCodeFileBrowseForOpen)
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
			PerformCopy(() =>
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
								ShowToast(_app.GetResourceString(UiStringKey.ErrorOcurred) + " " + e.Message);
								ReturnCancel();
							};
					}


					return () => { ReturnOk(targetIoc); };
				});
		}

		protected abstract void PerformCopy(Func<Action> copyAndReturnPostExecute);

		private void MoveToWritableLocation(IOConnectionInfo ioc)
		{
			_selectedIoc = ioc;

			StartFileStorageSelection(RequestCodeFileStorageSelectionForCopyToWritableLocation, false, false);

		}

		protected abstract void StartFileStorageSelection(int requestCode,
		                                                  bool allowThirdPartyGet, bool allowThirdPartySend);

		protected bool OnReceivedSftpData(string filename, int requestCode, bool isForSave)
		{
			IOConnectionInfo ioc = new IOConnectionInfo { Path = filename };
#if !EXCLUDE_FILECHOOSER
			StartFileChooser(ioc.Path, requestCode, isForSave);
#else
			IocSelected(ioc, requestCode);
#endif
			return true;
		}

		protected abstract void StartFileChooser(string path, int requestCode, bool isForSave);

		protected bool OnOpenButton(String fileName, int requestCode)
		{


			IOConnectionInfo ioc = new IOConnectionInfo
				{
					Path = fileName
				};

			IocSelected(ioc, requestCode);

			return true;

		}


		protected virtual void CopyFile(IOConnectionInfo targetIoc, IOConnectionInfo sourceIoc)
		{
			IFileStorage sourceStorage = _app.GetFileStorage(sourceIoc, false); //don't cache source. file won't be used ever again
			IFileStorage targetStorage = _app.GetFileStorage(targetIoc);

			using (
				var writeTransaction = targetStorage.OpenWriteTransaction(targetIoc,
				                                                          _app.GetBooleanPreference(
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
			var filestorage = _app.GetFileStorage(ioc, false);
			if (!filestorage.IsPermanentLocation(ioc))
			{
				
				string message = _app.GetResourceString(UiStringKey.FileIsTemporarilyAvailable) + " " + _app.GetResourceString(UiStringKey.CopyFileRequired) + " " + _app.GetResourceString(UiStringKey.ClickOkToSelectLocation);
				EventHandler<DialogClickEventArgs> onOk = (sender, args) => { MoveToWritableLocation(ioc); };
				EventHandler<DialogClickEventArgs> onCancel = (sender, args) => { ReturnCancel(); };
				ShowAlertDialog(message, onOk, onCancel);
				return;
			}
			

			if ((RequestedWritableRequirements != WritableRequirements.ReadOnly) && (filestorage.IsReadOnly(ioc)))
			{
				string readOnlyExplanation = _app.GetResourceString(UiStringKey.FileIsReadOnly);
				BuiltInFileStorage builtInFileStorage = filestorage as BuiltInFileStorage;
				if (builtInFileStorage != null)
				{
					if (builtInFileStorage.IsReadOnlyBecauseKitkatRestrictions(ioc))
						readOnlyExplanation = _app.GetResourceString(UiStringKey.FileIsReadOnlyOnKitkat);
				}
				EventHandler<DialogClickEventArgs> onOk = (sender, args) => { MoveToWritableLocation(ioc); };
				EventHandler<DialogClickEventArgs> onCancel = (sender, args) =>
					{
						if (RequestedWritableRequirements == WritableRequirements.WriteDemanded)
							ReturnCancel();
						else
							ReturnOk(ioc);
					};
				ShowAlertDialog(readOnlyExplanation + " "
								+ (RequestedWritableRequirements == WritableRequirements.WriteDemanded ?
									   _app.GetResourceString(UiStringKey.CopyFileRequired)
									   : _app.GetResourceString(UiStringKey.CopyFileRequiredForEditing))
								+ " "
								+ _app.GetResourceString(UiStringKey.ClickOkToSelectLocation), onOk, onCancel);
				return;
			}
			ReturnOk(ioc);
		}

		protected abstract void ShowAlertDialog(string message, EventHandler<DialogClickEventArgs> onOk, EventHandler<DialogClickEventArgs> onCancel);

		protected abstract WritableRequirements RequestedWritableRequirements { get; }

		protected abstract void ReturnOk(IOConnectionInfo ioc);

		protected abstract void ReturnCancel();
	}
}
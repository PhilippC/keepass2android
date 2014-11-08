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
using KeePassLib.Serialization;
using keepass2android.Io;
using Environment = Android.OS.Environment;

namespace keepass2android
{
	[Activity(Label = "")]
	public class SelectStorageLocationActivity : Activity
	{
		private ActivityDesign _design;
		private bool _isRecreated;
		private const int RequestCodeFileStorageSelection = 983713;

		public SelectStorageLocationActivity()
		{
			_design = new ActivityDesign(this);
		}

		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);
			
			_design.ApplyTheme();


			Kp2aLog.Log("SelectStorageLocationActivity.OnCreate");

			IsForSave = Intent.GetBooleanExtra(FileStorageSetupDefs.ExtraIsForSave, false);
			if (IsForSave)
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
				_isRecreated = true;
			}

			if (!_isRecreated)
			{
				Intent intent = new Intent(this, typeof(FileStorageSelectionActivity));
				intent.PutExtra(FileStorageSelectionActivity.AllowThirdPartyAppGet, allowThirdPartyGet);
				intent.PutExtra(FileStorageSelectionActivity.AllowThirdPartyAppSend, allowThirdPartySend);
				StartActivityForResult(intent, RequestCodeFileStorageSelection);	
			}
				

		}

		protected Bundle State { get; set; }

		protected bool IsForSave { get; set; }


		protected override void OnSaveInstanceState(Bundle outState)
		{
			base.OnSaveInstanceState(outState);

			outState.PutAll(State);
		}

		protected override void OnResume()
		{
			base.OnResume();
			_design.ReapplyTheme();
		}

		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);
			if (requestCode == RequestCodeFileStorageSelection)
			{
				if (resultCode == KeePass.ExitFileStorageSelectionOk)
				{

					string protocolId = data.GetStringExtra("protocolId");

					if (protocolId == "androidget")
					{
						Util.ShowBrowseDialog(this, Intents.RequestCodeFileBrowseForOpen, false);
					}
					else
					{
						App.Kp2a.GetFileStorage(protocolId).StartSelectFile(new FileStorageSetupInitiatorActivity(this,
							OnActivityResult,
							defaultPath =>
							{
								if (defaultPath.StartsWith("sftp://"))
									Util.ShowSftpDialog(this, OnReceivedSftpData, ReturnCancel);
								else
									Util.ShowFilenameDialog(this, OnOpenButton, null, ReturnCancel, false, defaultPath, GetString(Resource.String.enter_filename_details_url),
													Intents.RequestCodeFileBrowseForOpen);
							}
							), false, 0, protocolId);
					}


				}
				else
				{
					if (resultCode == (Result)FileStorageResults.FileChooserPrepared)
					{
						IOConnectionInfo ioc = new IOConnectionInfo();
						PasswordActivity.SetIoConnectionFromIntent(ioc, data);
#if !EXCLUDE_FILECHOOSER
				StartFileChooser(ioc.Path);
#else
						ReturnIoc(new IOConnectionInfo { Path = "/mnt/sdcard/keepass/yubi.kdbx" });
#endif
						return;
					}
					if ((resultCode == Result.Canceled) && (data != null) && (data.HasExtra("EXTRA_ERROR_MESSAGE")))
					{
						Toast.MakeText(this, data.GetStringExtra("EXTRA_ERROR_MESSAGE"), ToastLength.Long).Show();
					}
					ReturnCancel();
				}
	
			}
			
			if (requestCode == Intents.RequestCodeFileBrowseForOpen)
			{
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

						ReturnIoc(ioc);
					}
					else
					{
						if (data.Data.Scheme == "content")
						{
							ReturnIoc(IOConnectionInfo.FromPath(data.DataString));
	
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

		private void ReturnIoc(IOConnectionInfo ioc)
		{
			Intent intent = new Intent();
			PasswordActivity.PutIoConnectionToIntent(ioc, intent);
			SetResult(Result.Ok, intent);
			Finish();

		}

		private bool OnReceivedSftpData(string filename)
		{
			IOConnectionInfo ioc = new IOConnectionInfo { Path = filename };
#if !EXCLUDE_FILECHOOSER
			StartFileChooser(ioc.Path);
#else
			ReturnIoc(ioc);
#endif
			return true;
		}

#if !EXCLUDE_FILECHOOSER
		private void StartFileChooser(string defaultPath)
		{
			Kp2aLog.Log("FSA: defaultPath="+defaultPath);
			string fileProviderAuthority = FileChooserFileProvider.TheAuthority;
			if (defaultPath.StartsWith("file://"))
			{
				fileProviderAuthority = PackageName+".android-filechooser.localfile";
			}
			Intent i = Keepass2android.Kp2afilechooser.Kp2aFileChooserBridge.GetLaunchFileChooserIntent(this, fileProviderAuthority,
			                                                                                            defaultPath);

			StartActivityForResult(i, Intents.RequestCodeFileBrowseForOpen);
		}

#endif
		private bool OnOpenButton(String fileName)
		{

			IOConnectionInfo ioc = new IOConnectionInfo
			{
				Path = fileName
			};

			ReturnIoc(ioc);

			return true;

		}

	}


}
using System;
using System.IO;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Util;
using Android.Widget;
using KeePass.DataExchange;
using KeePass.DataExchange.Formats;
using KeePassLib.Interfaces;
using KeePassLib.Serialization;
using keepass2android.Io;

namespace keepass2android
{

	[Activity(Label = "@string/app_name",
		ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden,
        Theme = "@style/MyTheme_ActionBar")]
	[IntentFilter(new[] {"keepass2android.ExportDatabaseActivity"}, Categories = new[] {Intent.CategoryDefault})]
	public class ExportDatabaseActivity : LockCloseActivity
	{
		FileFormatProvider[] _ffp = new FileFormatProvider[]
			{
				new KeePassKdb2x(),
				new KeePassXml2x(), 
				new KeePassCsv1x()
			};

		private int _fileFormatIndex;

		protected override void OnCreate(Android.OS.Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			AlertDialog.Builder builder = new AlertDialog.Builder(this);
			builder.SetTitle(Resource.String.export_fileformats_title);
			builder.SetSingleChoiceItems(Resource.Array.export_fileformat_options, _fileFormatIndex,
				delegate(object sender, DialogClickEventArgs args) { _fileFormatIndex = args.Which; });
			builder.SetPositiveButton(Android.Resource.String.Ok, delegate
				{
					Intent intent = new Intent(this, typeof(FileStorageSelectionActivity));
					//intent.PutExtra(FileStorageSelectionActivity.AllowThirdPartyAppSend, true);
					
					StartActivityForResult(intent, 0);
				});
			builder.SetNegativeButton(Resource.String.cancel, delegate {
					Finish();
				});
			builder.Show();
		}

		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);

			if (resultCode == KeePass.ExitFileStorageSelectionOk)
			{
				string protocolId = data.GetStringExtra("protocolId");
				App.Kp2a.GetFileStorage(protocolId).StartSelectFile(new FileStorageSetupInitiatorActivity(this,
						OnActivityResult,
						defaultPath =>
						{
							if (defaultPath.StartsWith("sftp://"))
								Util.ShowSftpDialog(this, OnReceiveSftpData, () => { });
							else
								Util.ShowFilenameDialog(this, OnCreateButton, null, null, false, defaultPath, GetString(Resource.String.enter_filename_details_url),
												Intents.RequestCodeFileBrowseForOpen);
						}
						), true, RequestCodeDbFilename, protocolId);
				return;
			}

			if (resultCode == Result.Ok)
			{
				if (requestCode == RequestCodeDbFilename)
				{
					string filename = Util.IntentToFilename(data, this);

					bool fileExists = data.GetBooleanExtra("group.pals.android.lib.ui.filechooser.FileChooserActivity.result_file_exists", true);

					if (fileExists)
					{
						ExportTo(new IOConnectionInfo { Path = ConvertFilenameToIocPath(filename) });
						
					}
					else
					{
						var task = new CreateNewFilename(new ActionOnFinish((success, messageOrFilename) =>
						{
							if (!success)
							{
								Toast.MakeText(this, messageOrFilename, ToastLength.Long).Show();
								return;
							}
							ExportTo(new IOConnectionInfo { Path = ConvertFilenameToIocPath(messageOrFilename) });
							

						}), filename);

						new ProgressTask(App.Kp2a, this, task).Run();
					}

					return;


				}

			}
			if (resultCode == (Result)FileStorageResults.FileUsagePrepared)
			{
				var ioc = new IOConnectionInfo();
				PasswordActivity.SetIoConnectionFromIntent(ioc, data);
				ExportTo(ioc);
				return;
			}
			if (resultCode == (Result)FileStorageResults.FileChooserPrepared)
			{
				IOConnectionInfo ioc = new IOConnectionInfo();
				PasswordActivity.SetIoConnectionFromIntent(ioc, data);
				StartFileChooser(ioc.Path, RequestCodeDbFilename, true);
				return;
			}
			Finish();

		}

		private void ExportTo(IOConnectionInfo ioc)
		{
			var exportDb = new ExportDb(App.Kp2a, new ActionOnFinish(delegate(bool success, string message)
				{
					if (!success)
						Toast.MakeText(this, message, ToastLength.Long).Show();
					else
						Toast.MakeText(this, GetString(Resource.String.export_database_successful), ToastLength.Long).Show();
					Finish();
				}
				), _ffp[_fileFormatIndex], ioc);
			ProgressTask pt = new ProgressTask(App.Kp2a, this, exportDb);
			pt.Run();
		}

		protected int RequestCodeDbFilename
		{
			get { return 0; }
		}

		private bool OnCreateButton(string filename, Dialog dialog)
		{
			if (filename.Length == 0)
			{
				Toast.MakeText(this,
								Resource.String.error_filename_required,
								ToastLength.Long).Show();
				return false;
			}

			IOConnectionInfo ioc = new IOConnectionInfo { Path = filename };
			try
			{
				App.Kp2a.GetFileStorage(ioc);
			}
			catch (NoFileStorageFoundException)
			{
				Toast.MakeText(this,
								"Unexpected scheme in " + filename,
								ToastLength.Long).Show();
				return false;
			}

			ExportTo(new IOConnectionInfo() { Path = filename });
			return true;
		}

		private bool OnReceiveSftpData(string filename)
		{
			StartFileChooser(filename, RequestCodeDbFilename, true);
			return true;
		}

		private static string ConvertFilenameToIocPath(string filename)
		{
			if ((filename != null) && (filename.StartsWith("file://")))
			{
				filename = filename.Substring(7);
				filename = Java.Net.URLDecoder.Decode(filename);
			}
			return filename;
		}

		private void StartFileChooser(string defaultPath, int requestCode, bool forSave)
		{
#if !EXCLUDE_FILECHOOSER
			Kp2aLog.Log("FSA: defaultPath=" + defaultPath);
			string fileProviderAuthority = FileChooserFileProvider.TheAuthority;
			if (defaultPath.StartsWith("file://"))
			{
				fileProviderAuthority = "keepass2android."+AppNames.PackagePart+".android-filechooser.localfile";
			}
			Intent i = Keepass2android.Kp2afilechooser.Kp2aFileChooserBridge.GetLaunchFileChooserIntent(this, fileProviderAuthority,
																										defaultPath);

			if (forSave)
			{
				i.PutExtra("group.pals.android.lib.ui.filechooser.FileChooserActivity.save_dialog", true);
				i.PutExtra("group.pals.android.lib.ui.filechooser.FileChooserActivity.default_file_ext", _ffp[_fileFormatIndex].DefaultExtension);
			}

			StartActivityForResult(i, requestCode);
#endif

		}

		public class ExportDb : RunnableOnFinish
		{
			private readonly IKp2aApp _app;
			private readonly FileFormatProvider _fileFormat;
			private IOConnectionInfo _targetIoc;

			public ExportDb(IKp2aApp app, OnFinish onFinish, FileFormatProvider fileFormat, IOConnectionInfo targetIoc) : base(onFinish)
			{
				_app = app;
				this._fileFormat = fileFormat;
				_targetIoc = targetIoc;
			}

			public override void Run()
			{
				StatusLogger.UpdateMessage(UiStringKey.exporting_database);
				var pd = _app.GetDb().KpDatabase;
				PwExportInfo pwInfo = new PwExportInfo(pd.RootGroup, pd, true);
				
				try
				{
					using (var writeTransaction =_app.GetFileStorage(_targetIoc).OpenWriteTransaction(_targetIoc, _app.GetDb().KpDatabase.UseFileTransactions))
					{
						Stream sOut = writeTransaction.OpenFile();
						_fileFormat.Export(pwInfo, sOut, new NullStatusLogger());
						
						if (sOut != null) sOut.Close();

						writeTransaction.CommitWrite();
						
					}
					Finish(true);

					
				}
				catch (Exception ex)
				{
					Finish(false, ex.Message);
				}


			}
		}

	}
}
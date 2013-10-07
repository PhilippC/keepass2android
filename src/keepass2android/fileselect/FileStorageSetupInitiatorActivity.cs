using System;
using Android.App;
using Android.Content;
using KeePassLib.Serialization;
using Keepass2android.Javafilestorage;
using keepass2android.Io;
using keepass2android.fileselect;

namespace keepass2android
{
	public class FileStorageSetupInitiatorActivity: 
#if !EXCLUDE_JAVAFILESTORAGE
		Java.Lang.Object
		,IJavaFileStorageFileStorageSetupInitiatorActivity
#endif
		, IFileStorageSetupInitiatorActivity
	{
		private readonly Activity _activity;
		private readonly Action<int, Result, Intent> _onActivityResult;

		public FileStorageSetupInitiatorActivity(Activity activity, Action<int,Result,Intent> onActivityResult)
		{
			_activity = activity;
			_onActivityResult = onActivityResult;
		}

		public void StartSelectFileProcess(IOConnectionInfo ioc, bool isForSave, int requestCode)
		{
			Kp2aLog.Log("FSSIA: StartSelectFileProcess "+ioc.Path);
			Intent fileStorageSetupIntent = new Intent(_activity, typeof(FileStorageSetupActivity));
			fileStorageSetupIntent.PutExtra(FileStorageSetupDefs.ExtraProcessName, FileStorageSetupDefs.ProcessNameSelectfile);
			fileStorageSetupIntent.PutExtra(FileStorageSetupDefs.ExtraIsForSave, isForSave);
			PasswordActivity.PutIoConnectionToIntent(ioc, fileStorageSetupIntent);

			_activity.StartActivityForResult(fileStorageSetupIntent, requestCode);
		}

		public void StartFileUsageProcess(IOConnectionInfo ioc, int requestCode)
		{
			Intent fileStorageSetupIntent = new Intent(_activity, typeof(FileStorageSetupActivity));
			fileStorageSetupIntent.PutExtra(FileStorageSetupDefs.ExtraProcessName, FileStorageSetupDefs.ProcessNameFileUsageSetup);
			PasswordActivity.PutIoConnectionToIntent(ioc, fileStorageSetupIntent);

			_activity.StartActivityForResult(fileStorageSetupIntent, requestCode);
		}

		public void OnImmediateResult(int requestCode, int result, Intent intent)
		{
			_onActivityResult(requestCode, (Result)result, intent);
		}

		public Activity Activity {
			get { return _activity; }
		}

		public void IocToIntent(Intent intent, IOConnectionInfo ioc)
		{
			PasswordActivity.PutIoConnectionToIntent(ioc, intent);
		}

		public void PerformManualFileSelect(bool isForSave, int requestCode, string protocolId)
		{
			throw new NotImplementedException();
		}

		public void StartFileUsageProcess(string p0, int p1)
		{
			StartFileUsageProcess(new IOConnectionInfo() { Path = p0 }, p1);
		}

		public void StartSelectFileProcess(string p0, bool p1, int p2)
		{
			StartSelectFileProcess(new IOConnectionInfo() { Path = p0 }, p1, p2);
		}		
	}
}
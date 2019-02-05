using System;
using Android.App;
using Android.Content;
using KeePassLib.Serialization;
using keepass2android.Io;
using keepass2android.fileselect;

namespace keepass2android
{
	public class FileStorageSetupInitiatorActivity: 
#if !EXCLUDE_JAVAFILESTORAGE
		Java.Lang.Object
#if !NoNet
		,Keepass2android.Javafilestorage.IJavaFileStorageFileStorageSetupInitiatorActivity
#endif
		,
#endif
 IFileStorageSetupInitiatorActivity
	{
		private readonly Activity _activity;
		private readonly Action<int, Result, Intent> _onActivityResult;
		private readonly Action<string> _startManualFileSelect;

		public FileStorageSetupInitiatorActivity(Activity activity, 
			Action<int,Result,Intent> onActivityResult,
			Action<String> startManualFileSelect)
		{
			_activity = activity;
			_onActivityResult = onActivityResult;
			_startManualFileSelect = startManualFileSelect;
		}

		public void StartSelectFileProcess(IOConnectionInfo ioc, bool isForSave, int requestCode)
		{
			Kp2aLog.Log("FSSIA: StartSelectFileProcess ");
			Intent fileStorageSetupIntent = new Intent(_activity, typeof(FileStorageSetupActivity));
			fileStorageSetupIntent.PutExtra(FileStorageSetupDefs.ExtraProcessName, FileStorageSetupDefs.ProcessNameSelectfile);
			fileStorageSetupIntent.PutExtra(FileStorageSetupDefs.ExtraIsForSave, isForSave);
			Util.PutIoConnectionToIntent(ioc, fileStorageSetupIntent);

			_activity.StartActivityForResult(fileStorageSetupIntent, requestCode);
		}

		public void StartFileUsageProcess(IOConnectionInfo ioc, int requestCode, bool alwaysReturnSuccess)
		{
			Intent fileStorageSetupIntent = new Intent(_activity, typeof(FileStorageSetupActivity));
			fileStorageSetupIntent.PutExtra(FileStorageSetupDefs.ExtraProcessName, FileStorageSetupDefs.ProcessNameFileUsageSetup);
			fileStorageSetupIntent.PutExtra(FileStorageSetupDefs.ExtraAlwaysReturnSuccess, alwaysReturnSuccess);
			Util.PutIoConnectionToIntent(ioc, fileStorageSetupIntent);

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
			Util.PutIoConnectionToIntent(ioc, intent);
		}

		public void PerformManualFileSelect(bool isForSave, int requestCode, string protocolId)
		{
			_startManualFileSelect(protocolId + "://");
		}

		public void StartFileUsageProcess(string path, int requestCode, bool alwaysReturnSuccess)
		{
			StartFileUsageProcess(new IOConnectionInfo() { Path = path }, requestCode, alwaysReturnSuccess);
		}

		public void StartSelectFileProcess(string p0, bool p1, int p2)
		{
			StartSelectFileProcess(new IOConnectionInfo() { Path = p0 }, p1, p2);
		}		
	}
}
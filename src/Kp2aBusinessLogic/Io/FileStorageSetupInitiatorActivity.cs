using System;
using Android.App;
using Android.Content;
using Android.OS;
using KeePassLib.Serialization;

namespace keepass2android.Io
{
	public interface IFileStorageSetupInitiatorActivity
	{
		void StartSelectFileProcess(IOConnectionInfo ioc, bool isForSave, int requestCode);
		void StartFileUsageProcess(IOConnectionInfo ioc, int requestCode);
		void OnImmediateResult(int requestCode, int result, Intent intent);

		Activity Activity { get;  }

		void IocToIntent(Intent intent, IOConnectionInfo ioc);
		void PerformManualFileSelect(bool isForSave, int requestCode, string protocolId);
	}
}
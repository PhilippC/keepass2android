using System;
using Android.App;
using KeePassLib.Serialization;

namespace keepass2android
{
	class CreateNewFilename : RunnableOnFinish
	{
		private readonly string _filename;

		public CreateNewFilename(Activity activity, OnOperationFinishedHandler operationFinishedHandler, string filename)
			: base(activity,operationFinishedHandler)
		{
			_filename = filename;
		}

		public override void Run()
		{
			try
			{
				int lastIndexOfSlash = _filename.LastIndexOf("/", StringComparison.Ordinal);
				string parent = _filename.Substring(0, lastIndexOfSlash);
				string newFilename = _filename.Substring(lastIndexOfSlash + 1);

				string resultingFilename = App.Kp2a.GetFileStorage(new IOConnectionInfo { Path = parent }).CreateFilePath(parent, newFilename);

				Finish(true, resultingFilename);
			}
			catch (Exception e)
			{
				Finish(false, Util.GetErrorMessage(e));
			}

		}
	}
}
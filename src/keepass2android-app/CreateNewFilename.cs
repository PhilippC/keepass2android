using System;
using Android.App;
using KeePassLib.Serialization;

namespace keepass2android
{
    class CreateNewFilename : OperationWithFinishHandler
    {
        private readonly string _filename;

        public CreateNewFilename(IKp2aApp app, OnOperationFinishedHandler operationFinishedHandler, string filename)
            : base(app, operationFinishedHandler)
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
using Android.Content;
using Java.Nio.FileNio;
#if !EXCLUDE_JAVAFILESTORAGE

namespace keepass2android.Io
{
	public class SftpFileStorage: JavaFileStorage
	{
		public SftpFileStorage(Context ctx, IKp2aApp app, bool debugEnabled) :
			base(new Keepass2android.Javafilestorage.SftpStorage(ctx.ApplicationContext), app)
		{
            var storage = BaseStorage;
            if (debugEnabled)
            {
                string? logFilename = null;
                if (Kp2aLog.LogToFile)
                {
                    logFilename = Kp2aLog.LogFilename;
                }
                storage.SetJschLogging(true, logFilename);
            }
            else
            {
                storage.SetJschLogging(false, null);
            }
        }

        private Keepass2android.Javafilestorage.SftpStorage BaseStorage
        {
            get
            {
                return _jfs as Keepass2android.Javafilestorage.SftpStorage;
            }
        }

        public override bool UserShouldBackup
	    {
	        get { return true; }
	    }
	}

	
}
#endif
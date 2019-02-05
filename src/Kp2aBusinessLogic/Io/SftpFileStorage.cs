using Android.Content;
#if !EXCLUDE_JAVAFILESTORAGE

namespace keepass2android.Io
{
	public class SftpFileStorage: JavaFileStorage
	{
		public SftpFileStorage(Context ctx, IKp2aApp app) :
			base(new Keepass2android.Javafilestorage.SftpStorage(ctx.ApplicationContext), app)
		{
		}


	    public override bool UserShouldBackup
	    {
	        get { return true; }
	    }
	}

	
}
#endif
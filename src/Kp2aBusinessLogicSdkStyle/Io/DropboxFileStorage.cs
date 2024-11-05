using Android.Content;
#if !EXCLUDE_JAVAFILESTORAGE

namespace keepass2android.Io
{
	public partial class DropboxFileStorage: JavaFileStorage
	{
		public DropboxFileStorage(Context ctx, IKp2aApp app) :
			base(new Keepass2android.Javafilestorage.DropboxV2Storage(ctx, AppKey, AppSecret), app)
		{
		}


	    public override bool UserShouldBackup
	    {
	        get { return false; }
	    }
	}

	public partial class DropboxAppFolderFileStorage: JavaFileStorage
	{
		public DropboxAppFolderFileStorage(Context ctx, IKp2aApp app) :
			base(new Keepass2android.Javafilestorage.DropboxV2AppFolderStorage(ctx, AppKey, AppSecret), app)
		{
		}

	    public override bool UserShouldBackup
	    {
	        get { return false; }
	    }

    }
	
}
#endif
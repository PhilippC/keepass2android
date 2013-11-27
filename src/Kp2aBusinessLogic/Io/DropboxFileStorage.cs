using Android.Content;
#if !EXCLUDE_JAVAFILESTORAGE

namespace keepass2android.Io
{
	public partial class DropboxFileStorage: JavaFileStorage
	{
		public DropboxFileStorage(Context ctx, IKp2aApp app) :
			base(new Keepass2android.Javafilestorage.DropboxFileStorage(ctx, AppKey, AppSecret), app)
		{
		}

		
	}

	public partial class DropboxAppFolderFileStorage: JavaFileStorage
	{
		public DropboxAppFolderFileStorage(Context ctx, IKp2aApp app) :
			base(new Keepass2android.Javafilestorage.DropboxAppFolderFileStorage(ctx, AppKey, AppSecret), app)
		{
		}

		
	}
	
}
#endif
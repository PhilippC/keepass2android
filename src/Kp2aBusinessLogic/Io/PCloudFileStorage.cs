using Android.Content;
#if !EXCLUDE_JAVAFILESTORAGE

namespace keepass2android.Io
{
	public partial class PCloudFileStorage: JavaFileStorage
	{
		private const string ClientId = "CkRWTQXY6Lm";

		public PCloudFileStorage(Context ctx, IKp2aApp app) :
			base(new Keepass2android.Javafilestorage.PCloudFileStorage(ctx, ClientId), app)
		{
		}


	    public override bool UserShouldBackup
	    {
	        get { return false; }
	    }
	}

}
#endif
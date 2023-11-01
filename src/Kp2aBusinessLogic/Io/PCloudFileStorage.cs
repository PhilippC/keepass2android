using Android.Content;
#if !EXCLUDE_JAVAFILESTORAGE

namespace keepass2android.Io
{
	public class PCloudFileStorage: JavaFileStorage
	{
		private const string ClientId = "CkRWTQXY6Lm";

		public PCloudFileStorage(Context ctx, IKp2aApp app) :
			base(new Keepass2android.Javafilestorage.PCloudFileStorage(ctx, ClientId, "pcloud", ""), app)
		{

        }


	    public override bool UserShouldBackup
	    {
	        get { return false; }
	    }
	}
    public class PCloudFileStorageAll : JavaFileStorage
    {
        private const string ClientId = "FLm22de7bdS";

        public PCloudFileStorageAll(Context ctx, IKp2aApp app) :
            base(new Keepass2android.Javafilestorage.PCloudFileStorage(ctx, ClientId, "pcloudall", "PCLOUDALL_"), app)
        {
            

        }


        public override bool UserShouldBackup
        {
            get { return false; }
        }
    }

}
#endif
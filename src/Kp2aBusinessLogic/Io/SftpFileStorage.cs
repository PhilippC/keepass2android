using Android.Content;
#if !EXCLUDE_JAVAFILESTORAGE

namespace keepass2android.Io
{
	public class SftpFileStorage: JavaFileStorage
	{
		public SftpFileStorage(IKp2aApp app) :
			base(new Keepass2android.Javafilestorage.SftpStorage(), app)
		{
		}

		
	}

	
}
#endif
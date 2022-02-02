using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using KeePassLib.Serialization;
#if !EXCLUDE_JAVAFILESTORAGE
namespace keepass2android.Io
{
	public class GoogleDriveFileStorage : JavaFileStorage
	{
		public GoogleDriveFileStorage(Context ctx, IKp2aApp app) :
			base(new Keepass2android.Javafilestorage.GoogleDriveFullFileStorage(), app)
		{
		}


	    public override bool UserShouldBackup
	    {
	        get { return false; }
	    }
	}

    public class GoogleDriveAppDataFileStorage : JavaFileStorage
    {
        public GoogleDriveAppDataFileStorage(Context ctx, IKp2aApp app) :
            base(new Keepass2android.Javafilestorage.GoogleDriveAppDataFileStorage(), app)
        {
        }


        public override bool UserShouldBackup
        {
            get { return false; }
        }
    }
}
#endif
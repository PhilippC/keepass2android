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
			base(new Keepass2android.Javafilestorage.GoogleDriveFileStorage(), app)
		{
		}


	}
}
#endif
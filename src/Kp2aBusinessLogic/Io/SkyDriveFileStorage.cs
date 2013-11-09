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
using Keepass2android.Javafilestorage;

namespace keepass2android.Io
{
	public class SkyDriveFileStorage: JavaFileStorage
	{
		private const string ClientId = "000000004010C234";

		public SkyDriveFileStorage(Context ctx, IKp2aApp app) :
			base(new Keepass2android.Javafilestorage.SkyDriveFileStorage(ClientId, ctx), app)
		{
		}

		
	}
}
#endif
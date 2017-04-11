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
	public class OneDriveFileStorage: JavaFileStorage
	{
		private const string ClientId = "000000004010C234";

		public OneDriveFileStorage(Context ctx, IKp2aApp app) :
			base(new Keepass2android.Javafilestorage.OneDriveStorage(ctx, ClientId), app)
		{
		}

		public override IEnumerable<string> SupportedProtocols
		{
			get
			{
				yield return "skydrive";
				yield return "onedrive";
			}
		}
	}
}
#endif
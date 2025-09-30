using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Preferences;
using Android.Runtime;
using Android.Views;
using Android.Widget;
#if !NoNet && !EXCLUDE_JAVAFILESTORAGE

using Keepass2android.Javafilestorage;
#endif
using KeePassLib.Serialization;

namespace keepass2android.Io
{
#if !NoNet && !EXCLUDE_JAVAFILESTORAGE
	public class WebDavFileStorage: JavaFileStorage
	{
        private readonly IKp2aApp _app;
        private readonly WebDavStorage baseWebdavStorage;

        public WebDavFileStorage(IKp2aApp app, int chunkSize, Context appContext) : base(new Keepass2android.Javafilestorage.WebDavStorage(app.CertificateErrorHandler, chunkSize, appContext), app)
        {
            _app = app;
            baseWebdavStorage = (WebDavStorage)Jfs;

        }

		public override IEnumerable<string> SupportedProtocols
		{
			get
			{
				yield return "http";
				yield return "https";
				yield return "owncloud";
			    yield return "nextcloud";
            }
		}

	    public override bool UserShouldBackup
	    {
	        get { return true; }
	    }

	    public static string owncloudPrefix = "owncloud://";
	    public static string nextcloudPrefix = "nextcloud://";

        public static string Owncloud2Webdav(string owncloudUrl, string prefix)
		{
			
			if (owncloudUrl.StartsWith(prefix))
			{
				owncloudUrl = owncloudUrl.Substring(prefix.Length);
			}
			if (!owncloudUrl.Contains("://"))
				owncloudUrl = "https://" + owncloudUrl;
			if (!owncloudUrl.EndsWith("/"))
				owncloudUrl += "/";
			owncloudUrl += "remote.php/webdav/";
			return owncloudUrl;
		}

		public override void StartSelectFile(IFileStorageSetupInitiatorActivity activity, bool isForSave, int requestCode,
			string protocolId)
		{
			//need to override so we can loop the protocolId through
			activity.PerformManualFileSelect(isForSave, requestCode, protocolId);
		}

		public override string IocToPath(IOConnectionInfo ioc)
		{
			if (ioc.Path.StartsWith("owncloud"))
				throw new Exception("owncloud-URIs must be converted to https:// after credential input!");
			if (!String.IsNullOrEmpty(ioc.UserName))
			{
				//legacy support. Some users may have stored IOCs with UserName inside.
				return ((WebDavStorage)Jfs).BuildFullPath(ioc.Path, ioc.UserName, ioc.Password);
			}
			return base.IocToPath(ioc);
		}


        public override IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
        {
            baseWebdavStorage.SetUploadChunkSize(_app.WebDavChunkedUploadSize);
            return base.OpenWriteTransaction(ioc, useFileTransaction);
        }
    }


#endif
}
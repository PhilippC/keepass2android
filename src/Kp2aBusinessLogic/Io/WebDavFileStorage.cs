// This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
//
//   Keepass2Android is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   Keepass2Android is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.

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
    public class WebDavFileStorage : JavaFileStorage
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
            string path = base.IocToPath(ioc);
            //make sure the path is normalized, e.g. spaces and umlauts are percent encoded.
            string normalized = new Uri(path).AbsoluteUri;
            return normalized;

        }


        public override IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
        {
            baseWebdavStorage.SetUploadChunkSize(_app.WebDavChunkedUploadSize);
            return base.OpenWriteTransaction(ioc, useFileTransaction);
        }
    }


#endif
}
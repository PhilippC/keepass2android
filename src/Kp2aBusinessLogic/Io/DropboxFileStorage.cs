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

using Android.Content;
#if !EXCLUDE_JAVAFILESTORAGE

namespace keepass2android.Io
{
    public partial class DropboxFileStorage : JavaFileStorage
    {
        public DropboxFileStorage(Context ctx, IKp2aApp app) :
            base(new Keepass2android.Javafilestorage.DropboxV2Storage(ctx, AppKey, AppSecret), app)
        {
        }


        public override bool UserShouldBackup
        {
            get { return false; }
        }

        static public bool IsConfigured => !string.IsNullOrEmpty(AppKey) && !string.IsNullOrEmpty(AppSecret);
    }

    public partial class DropboxAppFolderFileStorage : JavaFileStorage
    {
        public DropboxAppFolderFileStorage(Context ctx, IKp2aApp app) :
            base(new Keepass2android.Javafilestorage.DropboxV2AppFolderStorage(ctx, AppKey, AppSecret), app)
        {
        }

        public override bool UserShouldBackup
        {
            get { return false; }
        }

        static public bool IsConfigured => !string.IsNullOrEmpty(AppKey) && !string.IsNullOrEmpty(AppSecret);
    }

}
#endif
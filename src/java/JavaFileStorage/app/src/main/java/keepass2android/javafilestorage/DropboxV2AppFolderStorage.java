/*
 * This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
 *
 *   Keepass2Android is free software: you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation, either version 3 of the License, or
 *   (at your option) any later version.
 *
 *   Keepass2Android is distributed in the hope that it will be useful,
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *   GNU General Public License for more details.
 *
 *   You should have received a copy of the GNU General Public License
 *   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
 */

package keepass2android.javafilestorage;

import android.content.Context;

/**
 * Created by Philipp on 22.11.2016.
 */
public class DropboxV2AppFolderStorage extends DropboxV2Storage{

        public DropboxV2AppFolderStorage(Context ctx, String _appKey,
                                           String _appSecret) {
            super(ctx, _appKey, _appSecret, false, AccessType.AppFolder);


        }

        public DropboxV2AppFolderStorage(Context ctx, String _appKey, String _appSecret, boolean clearKeysOnStart)
        {
            super(ctx, _appKey, _appSecret, clearKeysOnStart, AccessType.AppFolder);

        }


        @Override
        public String getProtocolId() {
            return "dropboxKP2A";
        }

    }


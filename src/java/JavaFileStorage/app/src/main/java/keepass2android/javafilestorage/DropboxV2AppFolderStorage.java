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


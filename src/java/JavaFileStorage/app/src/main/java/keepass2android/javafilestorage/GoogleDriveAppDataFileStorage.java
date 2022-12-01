package keepass2android.javafilestorage;

import com.google.android.gms.common.Scopes;

public class GoogleDriveAppDataFileStorage extends GoogleDriveBaseFileStorage
{
    private static final String GDRIVE_PROTOCOL_ID = "gdriveKP2A";

    @Override
    protected String getScopeString() {
        return Scopes.DRIVE_FILE;
    }

    @Override
    public String getProtocolId() {
        return GDRIVE_PROTOCOL_ID;
    }
}

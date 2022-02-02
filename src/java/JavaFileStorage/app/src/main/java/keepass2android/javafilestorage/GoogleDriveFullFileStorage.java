package keepass2android.javafilestorage;

import com.google.android.gms.common.Scopes;
import com.google.android.gms.common.api.Scope;

public class GoogleDriveFullFileStorage extends GoogleDriveBaseFileStorage
{
    private static final String GDRIVE_PROTOCOL_ID = "gdrive";

    @Override
    protected String getScopeString() {
        return Scopes.DRIVE_FULL;
    }

    @Override
    public String getProtocolId() {
        return GDRIVE_PROTOCOL_ID;
    }
}


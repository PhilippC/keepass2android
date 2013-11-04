package keepass2android.javafilestorage.skydrive;

import org.json.JSONObject;

public class SkyDriveFolder extends SkyDriveObject {
    public static final String TYPE = "folder";

    public SkyDriveFolder(JSONObject object) {
        super(object);
    }

    public int getCount() {
        return mObject.optInt("count");
    }
}

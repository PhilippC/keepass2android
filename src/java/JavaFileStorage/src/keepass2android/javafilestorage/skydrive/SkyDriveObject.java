package keepass2android.javafilestorage.skydrive;



import org.json.JSONObject;

public abstract class SkyDriveObject {

    public static class From {
        private final JSONObject mFrom;

        public From(JSONObject from) {
            assert from != null;
            mFrom = from;
        }

        public String getName() {
            return mFrom.optString("name");
        }

        public String getId() {
            return mFrom.optString("id");
        }

        public JSONObject toJson() {
            return mFrom;
        }
    }

    public static class SharedWith {
        private final JSONObject mSharedWidth;

        public SharedWith(JSONObject sharedWith) {
            assert sharedWith != null;
            mSharedWidth = sharedWith;
        }

        public String getAccess() {
            return mSharedWidth.optString("access");
        }

        public JSONObject toJson() {
            return mSharedWidth;
        }
    }


    public static SkyDriveObject create(JSONObject skyDriveObject) {
        String type = skyDriveObject.optString("type");

        if (type.equals(SkyDriveFolder.TYPENAME)) {
            return new SkyDriveFolder(skyDriveObject);
        } else if (type.equals(SkyDriveFile.TYPENAME)) {
            return new SkyDriveFile(skyDriveObject);
        } else return null;
    }

    protected final JSONObject mObject;

    public SkyDriveObject(JSONObject object) {
        assert object != null;
        mObject = object;
    }

    public String getId() {
        return mObject.optString("id");
    }

    public From getFrom() {
        return new From(mObject.optJSONObject("from"));
    }

    public String getName() {
        return mObject.optString("name");
    }

    public String getParentId() {
        return mObject.optString("parent_id");
    }

    public String getDescription() {
        return mObject.isNull("description") ? null : mObject.optString("description");
    }

    public String getType() {
        return mObject.optString("type");
    }

    public String getLink() {
        return mObject.optString("link");
    }

    public String getCreatedTime() {
        return mObject.optString("created_time");
    }

    public String getUpdatedTime() {
        return mObject.optString("updated_time");
    }

    public String getUploadLocation() {
        return mObject.optString("upload_location");
    }

    public SharedWith getSharedWith() {
        return new SharedWith(mObject.optJSONObject("shared_with"));
    }

    public JSONObject toJson() {
        return mObject;
    }


}
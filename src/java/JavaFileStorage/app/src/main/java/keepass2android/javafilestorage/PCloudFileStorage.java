package keepass2android.javafilestorage;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.net.Uri;
import android.os.Bundle;

import java.io.FileNotFoundException;
import java.io.InputStream;
import java.util.Arrays;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.List;
import java.util.NoSuchElementException;
import java.util.UUID;
import java.util.regex.Pattern;

import com.pcloud.sdk.ApiClient;
import com.pcloud.sdk.ApiError;
import com.pcloud.sdk.Authenticators;
import com.pcloud.sdk.AuthorizationActivity;
import com.pcloud.sdk.AuthorizationData;
import com.pcloud.sdk.AuthorizationRequest;
import com.pcloud.sdk.AuthorizationResult;
import com.pcloud.sdk.Call;
import com.pcloud.sdk.DataSource;
import com.pcloud.sdk.PCloudSdk;
import com.pcloud.sdk.RemoteEntry;
import com.pcloud.sdk.RemoteFile;
import com.pcloud.sdk.RemoteFolder;
import com.pcloud.sdk.UploadOptions;

/**
 * FileStorage implementation for PCloud provider.
 * https://www.pcloud.com/
 */
public class PCloudFileStorage extends JavaFileStorageBase
{
    final static private int PCLOUD_AUTHORIZATION_REQUEST_CODE = 1001845497;

    final static private String SHARED_PREF_NAME = "PCLOUD";
    final static private String SHARED_PREF_AUTH_TOKEN = "AUTH_TOKEN";
    final static private String SHARED_PREF_API_HOST = "API_HOST";

    private final Context ctx;

    private ApiClient apiClient;
    private String clientId;
    private String protocolId;

    ///prefix for SHARED_PREF keys so we can distinguish between different instances
    private String sharedPrefPrefix;

    public PCloudFileStorage(Context ctx, String clientId, String protocolId, String sharedPrefPrefix) {
        this.ctx = ctx;
        this.clientId = clientId;
        this.protocolId = protocolId;
        this.sharedPrefPrefix = sharedPrefPrefix;

        this.apiClient = createApiClientFromSharedPrefs();
        android.util.Log.d("KP2A", "Init pcloud with protocol " + protocolId + ", prefix=" + sharedPrefPrefix + ", clientId=" + clientId);
    }

    @Override
    public boolean requiresSetup(String path) {
        return !this.isConnected();
    }

    @Override
    public void startSelectFile(FileStorageSetupInitiatorActivity activity, boolean isForSave, int requestCode) {
        String path = getProtocolId() + "://";
        activity.startSelectFileProcess(path, isForSave, requestCode);
    }

    @Override
    public void prepareFileUsage(Context appContext, String path) throws Throwable {
        if (!isConnected()) {
            throw new UserInteractionRequiredException();
        }
    }

    @Override
    public void prepareFileUsage(FileStorageSetupInitiatorActivity activity, String path, int requestCode,
                                 boolean alwaysReturnSuccess) {
        if (this.isConnected()) {
            Intent intent = new Intent();
            intent.putExtra(EXTRA_PATH, path);
            activity.onImmediateResult(requestCode, RESULT_FILEUSAGE_PREPARED, intent);
        } else {
            activity.startFileUsageProcess(path, requestCode, alwaysReturnSuccess);
        }
    }

    @Override
    public String getProtocolId() {

        return protocolId;
    }

    @Override
    public String getDisplayName(String path) {
        return path;
    }

    @Override
    public String getFilename(String path) {
        return path.substring(path.lastIndexOf("/") + 1);
    }

    @Override
    public boolean checkForFileChangeFast(String path, String previousFileVersion) throws Exception {
        if (previousFileVersion == null || "".equals(previousFileVersion)) {
            return false;
        }

        path = this.cleanPath(path);

        RemoteFile remoteFile = this.getRemoteFileByPath(path);

        return !remoteFile.hash().equals(previousFileVersion);
    }

    @Override
    public String getCurrentFileVersionFast(String path) throws Exception {
        path = this.cleanPath(path);

        RemoteFile remoteFile = this.getRemoteFileByPath(path);

        return remoteFile.hash();
    }

    @Override
    public InputStream openFileForRead(String path) throws Exception {
        path = this.cleanPath(path);

        RemoteFile remoteFile = this.getRemoteFileByPath(path);

        return remoteFile.byteStream();
    }

    @Override
    public void uploadFile(String path, byte[] data, boolean writeTransactional) throws Exception {
        path = this.cleanPath(path);

        DataSource dataSource = DataSource.create(data);
        String filename = path.substring(path.lastIndexOf("/") + 1);
        String filePath = path.substring(0, path.lastIndexOf("/"));
        RemoteFolder remoteFolder = this.getRemoteFolderByPath(filePath);

        try {
            RemoteFile remoteFile = this.apiClient.createFile(
                remoteFolder, filename, dataSource, null, null, UploadOptions.OVERRIDE_FILE
            ).execute();
        } catch (ApiError e) {
            throw convertApiError(e);
        }
    }

    @Override
    public String createFolder(String parentPath, String newDirName) throws Exception {
        String parentPathWithoutProtocol = this.cleanPath(parentPath);

        RemoteFolder remoteFolder = this.getRemoteFolderByPath(parentPathWithoutProtocol);

        try {
            this.apiClient.createFolder(remoteFolder, newDirName).execute();
        } catch (ApiError e) {
            throw convertApiError(e);
        }

        return this.createFilePath(parentPath, newDirName);
    }

    @Override
    public String createFilePath(String parentPath, String newFileName) throws Exception {
        String cleanpath = this.cleanPath(parentPath);
        String filepath = this.getProtocolId() + "://";

        return (
                filepath
                +cleanpath
                +("".equals(newFileName) || "/".equals(cleanpath) ? "" : "/") +newFileName

        );
    }

    @Override
    public List<FileEntry> listFiles(String parentPath) throws Exception {
        parentPath = this.cleanPath(parentPath);

        ArrayList<FileEntry> fileEntries = new ArrayList<>();

        RemoteFolder remoteFolder = this.getRemoteFolderByPath(parentPath);

        for (RemoteEntry remoteEntry : remoteFolder.children()) {
            fileEntries.add(this.convertRemoteEntryToFileEntry(remoteEntry, parentPath));
        }

        return fileEntries;
    }

    @Override
    public FileEntry getFileEntry(String path) throws Exception {
        path = this.cleanPath(path);
        //do not call getRemoteFileByPath because path could represent a file or folder, we don't know here
        RemoteEntry remoteEntry = this.getRemoteEntryByPath(path);

        return this.convertRemoteEntryToFileEntry(
            remoteEntry,
            path.substring(0, path.lastIndexOf("/"))
        );
    }

    @Override
    public void delete(String path) throws Exception {
        path = this.cleanPath(path);

        RemoteEntry remoteEntry = this.getRemoteEntryByPath(path);

        try {
            if (remoteEntry.isFolder())
                this.apiClient.deleteFolder(remoteEntry.asFolder(), true).execute();
            else
                this.apiClient.delete(remoteEntry).execute();
        } catch (ApiError e) {
            throw convertApiError(e);
        }
    }

    @Override
    public void onCreate(FileStorageSetupActivity activity, Bundle savedInstanceState) {

    }

    @Override
    public void onResume(FileStorageSetupActivity activity) {
        if (activity.getProcessName().equals(PROCESS_NAME_SELECTFILE)) {
            activity.getState().putString(EXTRA_PATH, activity.getPath());
        }

        if (this.isConnected()) {
            finishActivityWithSuccess(activity);
        } else if (!activity.getState().getBoolean("hasStartedAuth", false)) {
            Activity castedActivity = (Activity)activity;
            AuthorizationRequest req = AuthorizationRequest.create()
                    .setClientId(this.clientId)
                    .setType(AuthorizationRequest.Type.TOKEN)
                    .setForceAccessApproval(true)
                    .build();
            Intent authIntent = AuthorizationActivity.createIntent(castedActivity, req);
            castedActivity.startActivityForResult(authIntent, PCLOUD_AUTHORIZATION_REQUEST_CODE);
            activity.getState().putBoolean("hasStartedAuth", true);
        }


    }

    @Override
    public void onActivityResult(FileStorageSetupActivity activity, int requestCode, int resultCode, Intent data) {
        if (requestCode == PCLOUD_AUTHORIZATION_REQUEST_CODE && data != null) {
            activity.getState().putBoolean("hasStartedAuth", false);
            AuthorizationData authData = AuthorizationActivity.getResult(data);


            this.handleAuthResult(activity, authData);
        }
    }

    private void handleAuthResult(FileStorageSetupActivity activity, AuthorizationData authorizationData) {

        if (authorizationData.result == AuthorizationResult.ACCESS_GRANTED) {
            String authToken = authorizationData.token;
            String apiHost = authorizationData.apiHost;
            setAuthToken(authToken, apiHost);
            finishActivityWithSuccess(activity);
        } else {
            android.util.Log.d("KP2A", "Auth failed with " + authorizationData.result.toString() + ", code=" + authorizationData.authCode + ", error=" + authorizationData.errorMessage);
            Activity castedActivity = (Activity)activity;
            Intent resultData = new Intent();
            resultData.putExtra(EXTRA_ERROR_MESSAGE, "Authentication failed!");

            //reset any stored token in case we have an invalid one
            clearAuthToken();

            castedActivity.setResult(Activity.RESULT_CANCELED, resultData);
            castedActivity.finish();
        }
    }

    @Override
    public void onStart(FileStorageSetupActivity activity) {

    }

    private ApiClient createApiClientFromSharedPrefs() {
        SharedPreferences prefs = getPrefs();
        String authToken = prefs.getString(SHARED_PREF_AUTH_TOKEN, null);
        String apiHost = prefs.getString(SHARED_PREF_API_HOST, null);
        return this.createApiClient(authToken, apiHost);
    }

    private ApiClient createApiClient(String authToken, String apiHost) {
        if (authToken == null || apiHost == null) {
            return null;
        }
        ApiClient.Builder builder = PCloudSdk.newClientBuilder();
        builder = builder.apiHost(apiHost);

        return builder
                .authenticator(Authenticators.newOAuthAuthenticator(authToken))
            .create();
    }

    private boolean isConnected() {
        return (this.apiClient != null);
    }

    private void clearAuthToken() {
        this.apiClient = null;
        SharedPreferences prefs = getPrefs();
        SharedPreferences.Editor edit = prefs.edit();
        edit.clear();
        edit.apply();
    }

    private SharedPreferences getPrefs()
    {
        return this.ctx.getSharedPreferences(sharedPrefPrefix + SHARED_PREF_NAME, Context.MODE_PRIVATE);
    }

    private void setAuthToken(String authToken, String apiHost) {
        this.apiClient = this.createApiClient(authToken, apiHost);
        SharedPreferences prefs = getPrefs();
        SharedPreferences.Editor edit = prefs.edit();
        edit.putString(SHARED_PREF_AUTH_TOKEN, authToken);
        edit.putString(SHARED_PREF_API_HOST, apiHost);
        edit.apply();
    }

    private String cleanPath(String path) {
        return (
            "/" + path.replaceAll("^(" + Pattern.quote(this.getProtocolId()) + "://)?/*", "")
        );
    }

    private RemoteFile getRemoteFileByPath(String path) throws Exception {
        Call<RemoteFile> call = this.apiClient.loadFile(path);

        try {
            return call.execute();
        } catch (ApiError apiError) {
            throw convertApiError(apiError);
        }
    }

    private RemoteFolder getRemoteFolderByPath(String path) throws Exception {
        Call<RemoteFolder> call;
        if ("".equals(path))
             call = this.apiClient.listFolder(RemoteFolder.ROOT_FOLDER_ID, false);
        else
             call = this.apiClient.listFolder(path, false);

        try {
            return call.execute();
        } catch (ApiError apiError) {
            throw convertApiError(apiError);
        }

    }

    private RemoteEntry getRemoteEntryByPath(String path) throws Exception {
        if ("/".equals(path)) {
            try {
                return this.apiClient.listFolder(RemoteFolder.ROOT_FOLDER_ID, false).execute();
            } catch (ApiError apiError) {
                throw convertApiError(apiError);
            }
        }

        String filename = path.substring(path.lastIndexOf("/") + 1);
        String parentPath = path.substring(0, path.lastIndexOf("/"));

        Call<RemoteFolder> call;
        if ("".equals(parentPath))
            call = this.apiClient.listFolder(RemoteFolder.ROOT_FOLDER_ID, false);
        else
            call = this.apiClient.listFolder(parentPath, false);

        RemoteFolder folder;
        try {
            folder = call.execute();
        } catch (ApiError apiError) {
            throw convertApiError(apiError);
        }

        for (RemoteEntry remoteEntry : folder.children()) {
            if (remoteEntry.name() != null && remoteEntry.name().equals(filename))
                return remoteEntry;
        }
        throw new FileNotFoundException("did not find " + path);

    }

    private Exception convertApiError(ApiError e) {
        String strErrorCode = String.valueOf(e.errorCode());
        if (strErrorCode.startsWith("1") || "2000".equals(strErrorCode) || "2095".equals(strErrorCode)) {
            this.clearAuthToken();
            return new UserInteractionRequiredException("Unlinked from PCloud! User must re-link.", e);
        } else if (strErrorCode.startsWith("2")) {
            return new FileNotFoundException(e.toString());
        }

        return e;
    }

    private FileEntry convertRemoteEntryToFileEntry(RemoteEntry remoteEntry, String parentPath) {
        FileEntry fileEntry = new FileEntry();
        fileEntry.canRead = true;
        fileEntry.canWrite = true;
        fileEntry.path = (
            this.getProtocolId() + "://" +
            ("/".equals(parentPath) ? "" : parentPath) +
            "/" + remoteEntry.name()
        );
        fileEntry.displayName = remoteEntry.name();
        fileEntry.isDirectory = !remoteEntry.isFile();
        fileEntry.lastModifiedTime = remoteEntry.lastModified().getTime();

        if (remoteEntry.isFile()) {
            fileEntry.sizeInBytes = remoteEntry.asFile().size();
        }

        return fileEntry;
    }
}

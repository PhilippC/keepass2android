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
import com.pcloud.sdk.AuthorizationResult;
import com.pcloud.sdk.Call;
import com.pcloud.sdk.DataSource;
import com.pcloud.sdk.PCloudSdk;
import com.pcloud.sdk.RemoteEntry;
import com.pcloud.sdk.RemoteFile;
import com.pcloud.sdk.RemoteFolder;

/**
 * FileStorage implementation for PCloud provider.
 * https://www.pcloud.com/
 */
public class PCloudFileStorage extends JavaFileStorageBase
{
    final static private int PCLOUD_AUTHORIZATION_REQUEST_CODE = 1001845497;

    final static private String SHARED_PREF_NAME = "PCLOUD";
    final static private String SHARED_PREF_AUTH_TOKEN = "AUTH_TOKEN";

    private final Context ctx;

    private ApiClient apiClient;
    private String clientId;

    public PCloudFileStorage(Context ctx, String clientId) {
        this.ctx = ctx;
        this.clientId = clientId;
        this.apiClient = createApiClientFromSharedPrefs();
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
        return "pcloud";
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
        String filePath = path.substring(0, path.lastIndexOf("/") + 1);
        RemoteFolder remoteFolder = this.getRemoteFolderByPath(filePath);

        String tempName = "." + UUID.randomUUID().toString();
        try {
            RemoteFile remoteFile = this.apiClient.createFile(remoteFolder, tempName, dataSource).execute();
            this.apiClient.rename(remoteFile, filename).execute();
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
        return (
            this.getProtocolId() + "://" +
            this.cleanPath(parentPath) +
            ("".equals(newFileName) ? "" : "/") +
            newFileName
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

        RemoteEntry remoteEntry = this.getRemoteEntryByPath(path);

        return this.convertRemoteEntryToFileEntry(
            remoteEntry,
            path.substring(0, path.lastIndexOf("/"))
        );
    }

    @Override
    public void delete(String path) throws Exception {
        path = this.cleanPath(path);

        RemoteEntry remoteEntry = this.getRemoteFileByPath(path);

        try {
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
            Intent authIntent = AuthorizationActivity.createIntent(castedActivity, this.clientId);
            castedActivity.startActivityForResult(authIntent, PCLOUD_AUTHORIZATION_REQUEST_CODE);
            activity.getState().putBoolean("hasStartedAuth", true);
        }

    }

    @Override
    public void onActivityResult(FileStorageSetupActivity activity, int requestCode, int resultCode, Intent data) {
        if (requestCode == PCLOUD_AUTHORIZATION_REQUEST_CODE && data != null) {
            activity.getState().putBoolean("hasStartedAuth", false);
            AuthorizationResult result = (AuthorizationResult)(
                data.getSerializableExtra(AuthorizationActivity.KEY_AUTHORIZATION_RESULT)
            );
            this.handleAuthResult(activity, result, data);
        }
    }

    private void handleAuthResult(FileStorageSetupActivity activity, AuthorizationResult authorizationResult,
                                  Intent data) {
        if (authorizationResult == AuthorizationResult.ACCESS_GRANTED) {
            String authToken = data.getStringExtra(AuthorizationActivity.KEY_ACCESS_TOKEN);
            setAuthToken(authToken);
            finishActivityWithSuccess(activity);
        } else {
            Activity castedActivity = (Activity)activity;
            Intent resultData = new Intent();
            resultData.putExtra(EXTRA_ERROR_MESSAGE, "Authentication failed.");
            castedActivity.setResult(Activity.RESULT_CANCELED, resultData);
            castedActivity.finish();
        }
    }

    @Override
    public void onStart(FileStorageSetupActivity activity) {

    }

    private ApiClient createApiClientFromSharedPrefs() {
        SharedPreferences prefs = this.ctx.getSharedPreferences(SHARED_PREF_NAME, Context.MODE_PRIVATE);
        String authToken = prefs.getString(SHARED_PREF_AUTH_TOKEN, null);
        return this.createApiClient(authToken);
    }

    private ApiClient createApiClient(String authToken) {
        if (authToken == null) {
            return null;
        }

        return (
            PCloudSdk.newClientBuilder()
            .authenticator(Authenticators.newOAuthAuthenticator(authToken))
            .create()
        );
    }

    private boolean isConnected() {
        return (this.apiClient != null);
    }

    private void clearAuthToken() {
        this.apiClient = null;
        SharedPreferences prefs = this.ctx.getSharedPreferences(SHARED_PREF_NAME, Context.MODE_PRIVATE);
        SharedPreferences.Editor edit = prefs.edit();
        edit.clear();
        edit.apply();
    }

    private void setAuthToken(String authToken) {
        this.apiClient = this.createApiClient(authToken);
        SharedPreferences prefs = this.ctx.getSharedPreferences(SHARED_PREF_NAME, Context.MODE_PRIVATE);
        SharedPreferences.Editor edit = prefs.edit();
        edit.putString(SHARED_PREF_AUTH_TOKEN, authToken);
        edit.apply();
    }

    private String cleanPath(String path) {
        return (
            "/" + path.replaceAll("^(" + Pattern.quote(this.getProtocolId()) + "://)?/*", "")
        );
    }

    private RemoteFile getRemoteFileByPath(String path) throws Exception {
        RemoteEntry remoteEntry = this.getRemoteEntryByPath(path);

        try {
            return remoteEntry.asFile();
        } catch (IllegalStateException e) {
            throw new FileNotFoundException(e.toString());
        }
    }

    private RemoteFolder getRemoteFolderByPath(String path) throws Exception {
        RemoteEntry remoteEntry = this.getRemoteEntryByPath(path);

        try {
            return remoteEntry.asFolder();
        } catch (IllegalStateException e) {
            throw new FileNotFoundException(e.toString());
        }
    }

    private RemoteEntry getRemoteEntryByPath(String path) throws Exception {
        Call<RemoteFolder> call = this.apiClient.listFolder(RemoteFolder.ROOT_FOLDER_ID, true);

        RemoteFolder folder;
        try {
            folder = call.execute();
        } catch (ApiError apiError) {
            throw convertApiError(apiError);
        }

        if ("/".equals(path)) {
            return folder;
        }

        String[] fileNames = path.substring(1).split("/");
        RemoteFolder currentFolder = folder;
        Iterator<String> fileNamesIterator = Arrays.asList(fileNames).iterator();
        while (true) {
            String fileName = fileNamesIterator.next();

            Iterator<RemoteEntry> entryIterator = currentFolder.children().iterator();
            while (true) {
                RemoteEntry remoteEntry;
                try {
                    remoteEntry = entryIterator.next();
                } catch (NoSuchElementException e) {
                    throw new FileNotFoundException(e.toString());
                }

                if (currentFolder.folderId() == remoteEntry.parentFolderId() && fileName.equals(remoteEntry.name())) {
                    if (!fileNamesIterator.hasNext()) {
                        return remoteEntry;
                    }

                    try {
                        currentFolder = remoteEntry.asFolder();
                    } catch (IllegalStateException e) {
                        throw new FileNotFoundException(e.toString());
                    }

                    break;
                }
            }
        }
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

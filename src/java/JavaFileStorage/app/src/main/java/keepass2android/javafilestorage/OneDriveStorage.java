package keepass2android.javafilestorage;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.os.AsyncTask;
import android.os.Bundle;
import android.support.annotation.NonNull;
import android.util.Log;
import android.widget.Toast;

import com.onedrive.sdk.concurrency.ICallback;
import com.onedrive.sdk.core.ClientException;
import com.onedrive.sdk.core.DefaultClientConfig;
import com.onedrive.sdk.core.IClientConfig;
import com.onedrive.sdk.core.OneDriveErrorCodes;
import com.onedrive.sdk.extensions.IItemCollectionPage;
import com.onedrive.sdk.extensions.IItemCollectionRequestBuilder;
import com.onedrive.sdk.extensions.IOneDriveClient;
import com.onedrive.sdk.extensions.Item;
import com.onedrive.sdk.extensions.OneDriveClient;
import com.onedrive.sdk.http.OneDriveServiceException;

import java.io.FileNotFoundException;
import java.io.InputStream;
import java.io.UnsupportedEncodingException;
import java.util.ArrayList;
import java.util.List;

/**
 * Created by Philipp on 20.11.2016.
 */
public class OneDriveStorage extends JavaFileStorageBase
{
    final IClientConfig oneDriveConfig;
    final keepass2android.javafilestorage.onedrive.MyMSAAuthenticator msaAuthenticator;

    IOneDriveClient oneDriveClient;

    public OneDriveStorage(final Context context, final String clientId) {
        msaAuthenticator = new keepass2android.javafilestorage.onedrive.MyMSAAuthenticator(context) {
            @Override
            public String getClientId() {
                return clientId;
            }

            @Override
            public String[] getScopes() {
                return new String[] { "offline_access", "onedrive.readwrite" };
            }
        };
        oneDriveConfig = DefaultClientConfig.createWithAuthenticator(msaAuthenticator);
        initAuthenticator(null);


    }


    @Override
    public boolean requiresSetup(String path) {
        return !isConnected(null);
    }

    @Override
    public void startSelectFile(FileStorageSetupInitiatorActivity activity, boolean isForSave, int requestCode) {

        initAuthenticator((Activity)activity.getActivity());

        String path = getProtocolId()+":///";
        Log.d("KP2AJ", "startSelectFile "+path+", connected: "+path);
		if (isConnected(null))
		{
			Intent intent = new Intent();
			intent.putExtra(EXTRA_IS_FOR_SAVE, isForSave);
			intent.putExtra(EXTRA_PATH, path);
			activity.onImmediateResult(requestCode, RESULT_FILECHOOSER_PREPARED, intent);
		}
		else
        {
            activity.startSelectFileProcess(path, isForSave, requestCode);
        }
    }

    private boolean isConnected(Activity activity) {
        if (oneDriveClient == null)
        {
            try
            {
                Log.d("KP2AJ", "trying silent login");
                if (msaAuthenticator.loginSilent() != null)
                {
                    Log.d("KP2AJ", "ok: silent login");

                    oneDriveClient = buildClient(activity);


                }
                else Log.d("KP2AJ", "trying silent login failed.");
            }
            catch (Exception e)
            {
                e.printStackTrace();
            }
        }
        return oneDriveClient != null;
    }

    private void initAuthenticator(Activity activity) {
        msaAuthenticator.init(
                oneDriveConfig.getExecutors(),
                oneDriveConfig.getHttpProvider(),
                activity,
                oneDriveConfig.getLogger());
    }


    @Override
    public void prepareFileUsage(FileStorageSetupInitiatorActivity activity, String path, int requestCode, boolean alwaysReturnSuccess) {
        initAuthenticator((Activity)activity.getActivity());
        if (isConnected((Activity)activity.getActivity()))
        {
            Intent intent = new Intent();
            intent.putExtra(EXTRA_PATH, path);
            activity.onImmediateResult(requestCode, RESULT_FILEUSAGE_PREPARED, intent);
        }
        else
        {
            activity.startFileUsageProcess(path, requestCode, alwaysReturnSuccess);
        }

    }

    @Override
    public String getProtocolId() {
        return "onedrive";
    }

    @Override
    public void prepareFileUsage(Context appContext, String path) throws UserInteractionRequiredException {
        if (!isConnected(null))
        {
            throw new UserInteractionRequiredException();
        }

    }

    @Override
    public void onCreate(FileStorageSetupActivity activity, Bundle savedInstanceState) {

        Log.d("KP2AJ", "OnCreate");

    }

    @Override
    public void onResume(final FileStorageSetupActivity activity) {




    }

    private IOneDriveClient buildClient(Activity activity) {

        return new OneDriveClient.Builder()
                .fromConfig(oneDriveConfig)
                .loginAndBuildClient(activity);

    }

    String getPathFromSkydrivePath(String skydrivePath)
    {
        String path = "";
        if (skydrivePath.equals(""))
            return "";

        String[] parts = skydrivePath.split("/");

        for (int i = 0; i < parts.length; i++) {
            String part = parts[i];
            logDebug("parsing part " + part);
            int indexOfSeparator = part.lastIndexOf(NAME_ID_SEP);
            if (indexOfSeparator < 0) {
                // seems invalid, but we're very generous here
                path += "/" + part;
                continue;
            }
            String name = part.substring(0, indexOfSeparator);
            try {
                name = decode(name);
            } catch (UnsupportedEncodingException e) {
                // ignore
            }
            path += "/" + name;
        }
        logDebug("return " +path + ". original was " + skydrivePath);
        return path;

    }

    String removeProtocol(String path) throws Exception {
        if (path == null)
            return null;
        if (path.startsWith("skydrive"))
            return getPathFromSkydrivePath(path.substring("skydrive://".length()));
        return path.substring(getProtocolId().length()+3);
    }

    @Override
    public String getDisplayName(String path) {

        if (path == null)
            return null;
        if (path.startsWith("skydrive"))
            return getProtocolId()+"://"+getPathFromSkydrivePath(path.substring("skydrive://".length()));

        return path;
    }

    @Override
    public String getFilename(String path) throws Exception {
        return path.substring(path.lastIndexOf("/")+1);
    }

    @Override
    public boolean checkForFileChangeFast(String path, String previousFileVersion) throws Exception {
        return false;
    }

    @Override
    public String getCurrentFileVersionFast(String path) {
        return null;
    }

    @Override
    public InputStream openFileForRead(String path) throws Exception {
        try {
            path = removeProtocol(path);
            logDebug("openFileForRead. Path="+path);
            InputStream result = oneDriveClient.getDrive()
                    .getRoot()
                    .getItemWithPath(path)
                    .getContent()
                    .buildRequest()
                    .get();
            logDebug("ok");
            return result;

        }
        catch (OneDriveServiceException e)
        {
            throw convertException(e);
        }
    }

    private Exception convertException(OneDriveServiceException e) {
        if (e.isError(OneDriveErrorCodes.ItemNotFound))
            return new FileNotFoundException(e.getMessage());
        return e;
    }

    @Override
    public void uploadFile(String path, byte[] data, boolean writeTransactional) throws Exception {
        try {
            path = removeProtocol(path);
            oneDriveClient.getDrive()
                    .getRoot()
                    .getItemWithPath(path)
                    .getContent()
                    .buildRequest()
                    .put(data);
        } catch (OneDriveServiceException e) {
            throw convertException(e);
        }
    }

    @Override
    public String createFolder(String parentPath, String newDirName) throws Exception {
        throw new Exception("not implemented.");
    }

    @Override
    public String createFilePath(String parentPath, String newFileName) throws Exception {
        String path = parentPath;
        if (!path.endsWith("/"))
            path = path + "/";
        path = path + newFileName;

        return path;
    }

    @Override
    public List<FileEntry> listFiles(String parentPath) throws Exception {
        try {
            ArrayList<FileEntry> result = new ArrayList<FileEntry>();
            parentPath = removeProtocol(parentPath);
            IItemCollectionPage itemsPage = oneDriveClient.getDrive()
                    .getRoot()
                    .getItemWithPath(parentPath)
                    .getChildren()
                    .buildRequest()
                    .get();
            if (parentPath.endsWith("/"))
                parentPath = parentPath.substring(0,parentPath.length()-1);
            while (true)
            {
                List<Item> items = itemsPage.getCurrentPage();
                if (items.isEmpty())
                    return result;

                for (Item i: items)
                {
                    FileEntry e = getFileEntry(parentPath + "/" + i.name, i);
                    Log.d("KP2AJ", e.path);
                    result.add(e);
                }
                IItemCollectionRequestBuilder nextPageReqBuilder = itemsPage.getNextPage();
                if (nextPageReqBuilder == null)
                    return result;
                itemsPage = nextPageReqBuilder.buildRequest().get();

            }
        } catch (OneDriveServiceException e) {
            throw convertException(e);
        }
    }

    @NonNull
    private FileEntry getFileEntry(String path, Item i) {
        FileEntry e = new FileEntry();
        if (i.size != null)
            e.sizeInBytes = i.size;
        else if ((i.remoteItem != null) && (i.remoteItem.size != null))
            e.sizeInBytes = i.remoteItem.size;

        e.displayName = i.name;
        e.canRead = e.canWrite = true;
        e.path = getProtocolId() +"://"+path;
        if (i.lastModifiedDateTime != null)
            e.lastModifiedTime = i.lastModifiedDateTime.getTimeInMillis();
        else if ((i.remoteItem != null)&&(i.remoteItem.lastModifiedDateTime != null))
            e.lastModifiedTime = i.remoteItem.lastModifiedDateTime.getTimeInMillis();
        e.isDirectory = (i.folder != null) || ((i.remoteItem != null) && (i.remoteItem.folder != null));
        return e;
    }

    @Override
    public FileEntry getFileEntry(String filename) throws Exception {
        try {
            filename = removeProtocol(filename);
            Item item = oneDriveClient.getDrive()
                    .getRoot()
                    .getItemWithPath(filename)
                    .buildRequest()
                    .get();
            return getFileEntry(filename, item);
        } catch (OneDriveServiceException e) {
            throw convertException(e);
        }
    }

    @Override
    public void delete(String path) throws Exception {
        try {
            path = removeProtocol(path);
            oneDriveClient.getDrive()
                    .getRoot()
                    .getItemWithPath(path)
                    .buildRequest()
                    .delete();
        } catch (OneDriveServiceException e) {
            throw convertException(e);
        }
    }

    @Override
    public void onStart(final FileStorageSetupActivity activity) {
        Log.d("KP2AJ", "onStart");
        if (activity.getProcessName().equals(PROCESS_NAME_SELECTFILE))
            activity.getState().putString(EXTRA_PATH, activity.getPath());

        JavaFileStorage.FileStorageSetupActivity storageSetupAct = activity;

        if (oneDriveClient != null) {
            Log.d("KP2AJ", "auth successful");
            try {

                finishActivityWithSuccess(activity);
                return;

            } catch (Exception e) {
                Log.d("KP2AJ", "finish with error: " + e.toString());
                finishWithError(activity, e);
                return;
            }
        }


        {
            Log.d("KP2AJ", "Starting auth");
            new AsyncTask<Object, Object, Object>() {

                @Override
                protected Object doInBackground(Object... params) {
                    try {
                        return buildClient((Activity) activity);
                    } catch (Exception e) {
                        return null;
                    }
                }

                @Override
                protected void onPostExecute(Object o) {
                    if (o == null)
                    {
                        Log.i(TAG, "authenticating not successful");
                        Intent data = new Intent();
                        data.putExtra(EXTRA_ERROR_MESSAGE, "authenticating not succesful");
                        ((Activity)activity).setResult(Activity.RESULT_CANCELED, data);
                        ((Activity)activity).finish();

                    }
                    else
                    {
                        Log.i(TAG, "authenticating successful");

                        oneDriveClient = (IOneDriveClient) o;
                        finishActivityWithSuccess(activity);
                    }
                }
            }.execute();

        }
    }

    @Override
    public void onActivityResult(FileStorageSetupActivity activity, int requestCode, int resultCode, Intent data) {

    }
}

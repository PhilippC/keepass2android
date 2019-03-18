package keepass2android.javafilestorage;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.os.Bundle;
import android.util.Log;


import com.microsoft.graph.core.ClientException;
import com.microsoft.graph.core.DefaultClientConfig;
import com.microsoft.graph.core.GraphErrorCodes;
import com.microsoft.graph.extensions.DriveItem;
import com.microsoft.graph.extensions.GraphServiceClient;
import com.microsoft.graph.extensions.IDriveItemCollectionPage;
import com.microsoft.graph.extensions.IDriveItemCollectionRequestBuilder;
import com.microsoft.graph.extensions.IDriveItemRequest;
import com.microsoft.graph.extensions.IDriveItemRequestBuilder;
import com.microsoft.graph.extensions.IGraphServiceClient;
import com.microsoft.identity.client.AuthenticationCallback;
import com.microsoft.identity.client.AuthenticationResult;
import com.microsoft.identity.client.MsalException;
import com.microsoft.identity.client.PublicClientApplication;
import com.microsoft.identity.client.User;

import java.io.FileNotFoundException;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.concurrent.CountDownLatch;

import keepass2android.javafilestorage.onedrive2.GraphServiceClientManager;


/**
 * Created by Philipp on 20.11.2016.
 */
public class OneDriveStorage2 extends JavaFileStorageBase
{
    PublicClientApplication mPublicClientApp;

    final HashMap<String /*userid*/, IGraphServiceClient> mClientByUser = new HashMap<String /*userid*/, IGraphServiceClient>();

    private static final String[] scopes = {"openid","offline_access", "https://graph.microsoft.com/Files.ReadWrite","https://graph.microsoft.com/User.Read"};


    public OneDriveStorage2(final Activity context, final String clientId) {

        mPublicClientApp = new PublicClientApplication(context, clientId);
        initAuthenticator(context);


    }


    @Override
    public boolean requiresSetup(String path)
    {

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

    private boolean isConnected(String path) {
        try {
            if (tryGetMsGraphClient(path) == null)
                try {
                    final CountDownLatch latch = new CountDownLatch(1);

                    Log.d("KP2AJ", "trying silent login");

                    String userId = extractUserId(path);
                    final MsalException[] _exception = {null};
                    final AuthenticationResult[] _result = {null};
                    User user = mPublicClientApp.getUser(userId);
                    mPublicClientApp.acquireTokenSilentAsync(scopes, user,
                            new AuthenticationCallback() {

                                @Override
                                public void onSuccess(AuthenticationResult authenticationResult) {
                                    _result[0] = authenticationResult;
                                    latch.countDown();
                                }

                                @Override
                                public void onError(MsalException exception) {
                                    _exception[0] = exception;
                                    latch.countDown();
                                }

                                @Override
                                public void onCancel() {
                                    latch.countDown();

                                }
                            });
                    latch.await();
                    if (_result[0] != null) {
                        buildClient(_result[0]);
                    } else if (_exception[0] != null){
                        _exception[0].printStackTrace();
                    }
                } catch (Exception e) {
                    e.printStackTrace();
                }
            return tryGetMsGraphClient(path) != null;
        }
        catch (Exception e)
        {
            return false;
        }

    }

    private IGraphServiceClient tryGetMsGraphClient(String path) throws Exception
    {
        String userId = extractUserId(path);
        if (mClientByUser.containsKey(userId))
            return mClientByUser.get(userId);
        return null;
    }

    private String extractUserId(String path) throws Exception {
        String pathWithoutProtocol = removeProtocol(path);
        String[] parts = pathWithoutProtocol.split("/",1);
        if (parts.length != 2 || ("".equals(parts[0])))
        {
            throw new Exception("path does not contain user");
        }
        return parts[0];
    }

    private void initAuthenticator(Activity activity) {


    }


    @Override
    public void prepareFileUsage(FileStorageSetupInitiatorActivity activity, String path, int requestCode, boolean alwaysReturnSuccess) {
        initAuthenticator((Activity)activity.getActivity());
        if (isConnected(path))
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



    private IGraphServiceClient buildClient(AuthenticationResult authenticationResult) throws InterruptedException {

        IGraphServiceClient newClient = new GraphServiceClient.Builder()
                .fromConfig(DefaultClientConfig.createWithAuthenticationProvider(new GraphServiceClientManager(authenticationResult.getAccessToken())))
                .buildClient();
        mClientByUser.put(authenticationResult.getUser().getUserIdentifier(), newClient);

        return newClient;
    }



    String removeProtocol(String path) throws Exception {
        if (path == null)
            return null;
        return path.substring(getProtocolId().length()+3);
    }

    @Override
    public String getDisplayName(String path) {

        if (path == null)
            return null;

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

    class ClientAndPath
    {
        public IGraphServiceClient client;
        public String oneDrivePath;
        public IDriveItemRequestBuilder getPathItem()
        {
            IDriveItemRequestBuilder pathItem = client.getDrive().getRoot();
            if ("".equals(oneDrivePath) == false) {
                pathItem = pathItem.getItemWithPath(oneDrivePath);
            }
            return pathItem;
        }
    }

    @Override
    public InputStream openFileForRead(String path) throws Exception {
        try {
            ClientAndPath clientAndpath = getOneDriveClientAndPath(path);
            logDebug("openFileForRead. Path="+path);
            InputStream result = clientAndpath.client.getDrive()
                    .getRoot()
                    .getItemWithPath(clientAndpath.oneDrivePath)
                    .getContent()
                    .buildRequest()
                    .get();
            logDebug("ok");
            return result;

        }
        catch (ClientException e)
        {
            throw convertException(e);
        }
    }

    private ClientAndPath getOneDriveClientAndPath(String path) throws Exception {
        ClientAndPath result = new ClientAndPath();

        String pathWithoutProtocol = removeProtocol(path);
        String[] parts = pathWithoutProtocol.split("/",2);
        if (parts.length != 2 || ("".equals(parts[0])))
        {
            throw new Exception("path does not contain user");
        }
        result.client = mClientByUser.get(parts[0]);
        result.oneDrivePath = parts[1];
        return result;

    }


    private Exception convertException(ClientException e) {
        if (e.isError(GraphErrorCodes.ItemNotFound))
            return new FileNotFoundException(e.getMessage());
        return e;
    }

    @Override
    public void uploadFile(String path, byte[] data, boolean writeTransactional) throws Exception {
        try {
            ClientAndPath clientAndPath = getOneDriveClientAndPath(path);
            clientAndPath.client.getDrive()
                    .getRoot()
                    .getItemWithPath(clientAndPath.oneDrivePath)
                    .getContent()
                    .buildRequest()
                    .put(data);
        } catch (ClientException e) {
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
            ClientAndPath clientAndPath = getOneDriveClientAndPath(parentPath);
            parentPath = clientAndPath.oneDrivePath;

            IDriveItemCollectionPage itemsPage = clientAndPath.getPathItem()
                    .getChildren()
                    .buildRequest()
                    .get();
            if (parentPath.endsWith("/"))
                parentPath = parentPath.substring(0,parentPath.length()-1);
            while (true)
            {
                List<DriveItem> items = itemsPage.getCurrentPage();
                if (items.isEmpty())
                    return result;

                for (DriveItem i: items)
                {
                    FileEntry e = getFileEntry(parentPath + "/" + i.name, i);
                    Log.d("KP2AJ", e.path);
                    result.add(e);
                }
                IDriveItemCollectionRequestBuilder nextPageReqBuilder = itemsPage.getNextPage();
                if (nextPageReqBuilder == null)
                    return result;
                itemsPage = nextPageReqBuilder.buildRequest().get();

            }
        } catch (ClientException e) {
            throw convertException(e);
        }
    }

    private FileEntry getFileEntry(String path, DriveItem i) {
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

            ClientAndPath clientAndPath = getOneDriveClientAndPath(filename);
            IDriveItemRequestBuilder pathItem = clientAndPath.getPathItem();

            IDriveItemRequest request = pathItem.buildRequest();
            DriveItem item = request.get();
            return getFileEntry(filename, item);
        } catch (ClientException e) {
            throw convertException(e);
        }
    }

    @Override
    public void delete(String path) throws Exception {
        try {
            ClientAndPath clientAndPath = getOneDriveClientAndPath(path);
            clientAndPath.client.getDrive()
                    .getRoot()
                    .getItemWithPath(clientAndPath.oneDrivePath)
                    .buildRequest()
                    .delete();
        } catch (ClientException e) {
            throw convertException(e);
        }
    }

    boolean acquireTokenRunning = false;
    @Override
    public void onStart(final FileStorageSetupActivity activity) {
        Log.d("KP2AJ", "onStart " + activity.getPath());
        if (activity.getProcessName().equals(PROCESS_NAME_SELECTFILE))
            activity.getState().putString(EXTRA_PATH, activity.getPath());

        String userId = activity.getState().getString("OneDriveUser");
        if (mClientByUser.containsKey(userId)) {
            finishActivityWithSuccess(activity);
            return;
        }


        JavaFileStorage.FileStorageSetupActivity storageSetupAct = activity;

        final CountDownLatch latch = new CountDownLatch(1);
        final AuthenticationResult[] _authenticationResult = {null};
        MsalException _exception[] = {null};

        if (!acquireTokenRunning) {
            acquireTokenRunning = true;

            mPublicClientApp.acquireToken((Activity) activity, scopes, new AuthenticationCallback() {
                @Override
                public void onSuccess(AuthenticationResult authenticationResult) {
                    Log.i(TAG, "authenticating successful");

                    try {
                        buildClient(authenticationResult);
                    } catch (InterruptedException e) {
                        e.printStackTrace();
                    }
                    activity.getState().putString(EXTRA_PATH, getProtocolPrefix() + authenticationResult.getUser().getUserIdentifier() + "/");

                    finishActivityWithSuccess(activity);
                    acquireTokenRunning = false;
                    return;
                }

                @Override
                public void onError(MsalException exception) {
                    Log.i(TAG, "authenticating not successful");
                    Intent data = new Intent();
                    data.putExtra(EXTRA_ERROR_MESSAGE, "authenticating not successful");
                    ((Activity) activity).setResult(Activity.RESULT_CANCELED, data);
                    ((Activity) activity).finish();
                    acquireTokenRunning = false;
                }

                @Override
                public void onCancel() {

                    Log.i(TAG, "authenticating cancelled");
                    Intent data = new Intent();
                    data.putExtra(EXTRA_ERROR_MESSAGE, "authenticating not cancelled");
                    ((Activity) activity).setResult(Activity.RESULT_CANCELED, data);
                    ((Activity) activity).finish();
                    acquireTokenRunning = false;
                }
            });
        }
    }

    @Override
    public void onActivityResult(FileStorageSetupActivity activity, int requestCode, int resultCode, Intent data) {
        mPublicClientApp.handleInteractiveRequestRedirect(requestCode, resultCode, data);
    }
}

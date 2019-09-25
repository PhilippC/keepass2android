package keepass2android.javafilestorage;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.os.Bundle;
import android.util.Base64;
import android.util.Log;


import com.microsoft.graph.core.ClientException;
import com.microsoft.graph.core.DefaultClientConfig;
import com.microsoft.graph.core.GraphErrorCodes;
import com.microsoft.graph.extensions.DriveItem;
import com.microsoft.graph.extensions.Folder;
import com.microsoft.graph.extensions.GraphServiceClient;
import com.microsoft.graph.extensions.IDriveItemCollectionPage;
import com.microsoft.graph.extensions.IDriveItemCollectionRequestBuilder;
import com.microsoft.graph.extensions.IDriveItemRequest;
import com.microsoft.graph.extensions.IDriveItemRequestBuilder;
import com.microsoft.graph.extensions.IDriveSharedWithMeCollectionPage;
import com.microsoft.graph.extensions.IDriveSharedWithMeCollectionRequestBuilder;
import com.microsoft.graph.extensions.IGraphServiceClient;
import com.microsoft.graph.http.GraphServiceException;
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


    public OneDriveStorage2(final Context context, final String clientId) {

        mPublicClientApp = new PublicClientApplication(context, clientId);

    }


    @Override
    public boolean requiresSetup(String path)
    {
        return false;
    }

    @Override
    public void startSelectFile(FileStorageSetupInitiatorActivity activity, boolean isForSave, int requestCode) {

        String path = getProtocolId()+":///";
        Log.d("KP2AJ", "startSelectFile "+path);
		activity.startSelectFileProcess(path, isForSave, requestCode);

    }

    private boolean isConnected(String path) {
        try {
            logDebug("isConnected? " + path);

            return tryGetMsGraphClient(path) != null;
        }
        catch (Exception e)
        {
            logDebug("exception in isConnected: " + e.toString());
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
        String[] parts = pathWithoutProtocol.split("/",2);
        logDebug("extractUserId for path " + path);
        logDebug("# parts: " + parts.length);
        if (parts.length < 1 || ("".equals(parts[0])))
        {
            throw new Exception("path does not contain user");
        }
        logDebug("parts[0]: " + parts[0]);
        return parts[0];
    }


    @Override
    public void prepareFileUsage(FileStorageSetupInitiatorActivity activity, String path, int requestCode, boolean alwaysReturnSuccess) {

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
        return "onedrive2";
    }

    @Override
    public void prepareFileUsage(Context appContext, String path) throws UserInteractionRequiredException {
        if (!isConnected(path))
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



    private IGraphServiceClient buildClient(AuthenticationResult authenticationResult) throws Exception {

        logDebug("buildClient...");
        IGraphServiceClient newClient = new GraphServiceClient.Builder()
                .fromConfig(DefaultClientConfig.createWithAuthenticationProvider(new GraphServiceClientManager(authenticationResult.getAccessToken())))
                .buildClient();
        logDebug("authToken = " + authenticationResult.getAccessToken());
        if (authenticationResult.getUser() == null)
            throw new Exception("authenticationResult.getUser() == null!");
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
        public String share;
        public IDriveItemRequestBuilder getPathItem() throws Exception {
            IDriveItemRequestBuilder pathItem;
            if (!hasShare()) {
                logDebug("p: " + oneDrivePath);
                for (StackTraceElement ste : Thread.currentThread().getStackTrace()) {
                    logDebug(ste.toString());
                }
                throw new Exception("Cannot get path item without share");
            }
            if ("me".equals(share))
                pathItem = client.getMe().getDrive().getRoot();
            else
                pathItem = client.getShares(share).getRoot();

            if ("".equals(oneDrivePath) == false) {
                pathItem = pathItem.getItemWithPath(oneDrivePath);
            }
            return pathItem;
        }

        public boolean hasShare() {
            return !(share == null || "".equals(share));
        }
    }

    @Override
    public InputStream openFileForRead(String path) throws Exception {
        try {
            ClientAndPath clientAndpath = getOneDriveClientAndPath(path);
            logDebug("openFileForRead. Path="+path);
            InputStream result = clientAndpath
                    .getPathItem()
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
        String[] parts = pathWithoutProtocol.split("/",3);
        if (parts.length < 2 || ("".equals(parts[0])))
        {
            throw new Exception("path does not contain user");
        }
        result.client = mClientByUser.get(parts[0]);
        if (result.client == null)
            throw new Exception("failed to get client for " + parts[0]);


        logDebug("building client for " + path + " results in " + parts.length + " segments");
        logDebug("share is " + parts[1]);
        result.share = parts[1];

        if (parts.length > 2) {
            result.oneDrivePath = parts[2];

        }
        return result;

    }


    private Exception convertException(ClientException e) {
        if (e.isError(GraphErrorCodes.ItemNotFound))
            return new FileNotFoundException(e.getMessage());
        if (e.getMessage().contains("\n\n404 : ")) //hacky solution to check for not found. errorCode was null in my tests so I had to find a workaround.
            return new FileNotFoundException(e.getMessage());
        return e;
    }

    @Override
    public void uploadFile(String path, byte[] data, boolean writeTransactional) throws Exception {
        try {
            ClientAndPath clientAndPath = getOneDriveClientAndPath(path);
            clientAndPath
                    .getPathItem()
                    .getContent()
                    .buildRequest()
                    .put(data);
        } catch (ClientException e) {
            throw convertException(e);
        }
    }

    @Override
    public String createFolder(String parentPath, String newDirName) throws Exception {
        try {
            DriveItem driveItem = new DriveItem();
            driveItem.name = newDirName;
            driveItem.folder = new Folder();

            ClientAndPath clientAndPath = getOneDriveClientAndPath(parentPath);


            logDebug("building request for " + clientAndPath.oneDrivePath);

            DriveItem res = clientAndPath.getPathItem()
                    .getChildren()
                    .buildRequest()
                    .post(driveItem);
            return createFilePath(parentPath, newDirName);
        } catch (ClientException e) {
            throw convertException(e);
        }
    }

    @Override
    public String createFilePath(String parentPath, String newFileName) throws Exception {
        String path = parentPath;
        if (!path.endsWith("/"))
            path = path + "/";
        path = path + newFileName;

        return path;
    }

    private List<FileEntry> listShares(String parentPath, IGraphServiceClient client) throws Exception {
        ArrayList<FileEntry> result = new ArrayList<FileEntry>();
        logDebug("listShares: " + (client == null));
        if (!parentPath.endsWith("/"))
            parentPath += "/";

        logDebug("listShares");
        FileEntry myEntry = getFileEntry(parentPath+"me/", client.getMe().getDrive().getRoot().buildRequest().get());
        //if ((myEntry.displayName == null) || "".equals(myEntry.displayName))
            myEntry.displayName = "My OneDrive";

        logDebug("myEntry.path = " + myEntry.path + ", isDir = " + myEntry.isDirectory);
        result.add(myEntry);

        IDriveSharedWithMeCollectionPage sharedWithMeCollectionPage = client.getMe().getDrive().getSharedWithMe().buildRequest().get();

        while (true) {
            List<DriveItem> sharedWithMeItems = sharedWithMeCollectionPage.getCurrentPage();
            if (sharedWithMeItems.isEmpty())
                break;

            for (DriveItem i : sharedWithMeItems) {
                Log.d("kp2aSHARE",i.name + " " + i.description + " " + i.id + " " + i.webUrl);
                String urlToEncode = i.webUrl;
                //calculate shareid according to https://docs.microsoft.com/en-us/graph/api/shares-get?view=graph-rest-1.0&tabs=java
                String shareId = "u!"+android.util.Base64.encodeToString(urlToEncode.getBytes(), Base64.NO_PADDING).replace('/','_').replace('+','_')
                        .replace("\n",""); //encodeToString adds a newline character add the end - remove
                Log.d("kp2aSHARE","shareId: "  +shareId);
                FileEntry sharedFileEntry = getFileEntry(parentPath + shareId +"/", i);
                result.add(sharedFileEntry);
/*
                try {
                    DriveItem x2 = client.shares(shareId).root().buildRequest().get();
                    Log.d("kp2aSHARE","x2: " + x2.name + " " + x2.description + " " + x2.id + " ");
                }
                catch (ClientException e)
                {
                    if (e.getCause() != null)
                        Log.d("kp2aSHARE","cause: " + e.getCause().toString());
                    Log.d("kp2aSHARE","exception: " + e.toString());
                }
                catch (Exception e)
                {
                    Log.d("kp2aSHARE","share item exc: " + e.toString());
                }
*/

            }
            IDriveSharedWithMeCollectionRequestBuilder b = sharedWithMeCollectionPage.getNextPage();
            if (b == null) break;
            sharedWithMeCollectionPage =b.buildRequest().get();
        }
        return result;
    }


    @Override
    public List<FileEntry> listFiles(String parentPath) throws Exception {
        try {
            ClientAndPath clientAndPath = getOneDriveClientAndPath(parentPath);

            logDebug("listing files for " + parentPath +", " + clientAndPath.share + clientAndPath.hasShare());
            if (!clientAndPath.hasShare())
            {
                logDebug("listing shares.");
                return listShares(parentPath, clientAndPath.client);
            }

            logDebug("listing regular children.");
            ArrayList<FileEntry> result = new ArrayList<FileEntry>();
            /*logDebug("parent before:" + parentPath);
            parentPath = parentPath.substring(getProtocolPrefix().length());
            logDebug("parent after: " + parentPath);*/

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
        e.path = path;
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

            if (((clientAndPath.oneDrivePath == null) || "".equals(clientAndPath.oneDrivePath))
                && !clientAndPath.hasShare())
            {
                FileEntry rootEntry = new FileEntry();
                rootEntry.displayName = "";
                rootEntry.isDirectory = true;
                return rootEntry;
            }

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
            clientAndPath.getPathItem()
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



        User user = null;
        try {
            String userId = extractUserId(activity.getPath());
            if (mClientByUser.containsKey(userId)) {
                finishActivityWithSuccess(activity);
                return;
            }

            logDebug("needs acquire token");

            Log.d("KP2AJ", "trying silent login " + activity.getPath());

            final MsalException[] _exception = {null};
            final AuthenticationResult[] _result = {null};
            logDebug("getting user for " + userId);
            user = mPublicClientApp.getUser(userId);
            logDebug("getting user ok.");

        } catch (Exception e) {
            logDebug(e.toString());
            e.printStackTrace();
        }
        if (user != null)
        {
            mPublicClientApp.acquireTokenSilentAsync(scopes, user,
                    new AuthenticationCallback() {
                        @Override
                        public void onSuccess(AuthenticationResult authenticationResult) {
                            successAuthCallback(authenticationResult, activity);
                        }

                        @Override
                        public void onError(MsalException exception) {
                            startInteractiveAcquireToken(activity);
                        }

                        @Override
                        public void onCancel() {

                            cancelAuthCallback((Activity) activity);
                        }
                    });
            return;
        }


        startInteractiveAcquireToken(activity);
    }

    private void startInteractiveAcquireToken(FileStorageSetupActivity activity) {
        if (!acquireTokenRunning) {
            acquireTokenRunning = true;

            mPublicClientApp.acquireToken((Activity) activity, scopes, new AuthenticationCallback() {
                @Override
                public void onSuccess(AuthenticationResult authenticationResult) {
                    successAuthCallback(authenticationResult, activity);
                }

                @Override
                public void onError(MsalException exception) {
                    errorAuthCallback((Activity) activity);
                }

                @Override
                public void onCancel() {

                    cancelAuthCallback((Activity) activity);
                }
            });
        }
    }

    private void successAuthCallback(AuthenticationResult authenticationResult, FileStorageSetupActivity activity) {
        Log.i(TAG, "authenticating successful");

        try {
            buildClient(authenticationResult);
        } catch (Exception e) {
            logDebug(e.toString());
            e.printStackTrace();
        }
        activity.getState().putString(EXTRA_PATH, getProtocolPrefix() + authenticationResult.getUser().getUserIdentifier() + "/");

        finishActivityWithSuccess(activity);
        acquireTokenRunning = false;
    }

    private void errorAuthCallback(Activity activity) {
        Log.i(TAG, "authenticating not successful");
        Intent data = new Intent();
        data.putExtra(EXTRA_ERROR_MESSAGE, "authenticating not successful");
        activity.setResult(Activity.RESULT_CANCELED, data);
        activity.finish();
        acquireTokenRunning = false;
    }

    private void cancelAuthCallback(Activity activity) {
        Log.i(TAG, "authenticating cancelled");
        Intent data = new Intent();
        data.putExtra(EXTRA_ERROR_MESSAGE, "authenticating not cancelled");
        activity.setResult(Activity.RESULT_CANCELED, data);
        activity.finish();
        acquireTokenRunning = false;
    }

    @Override
    public void onActivityResult(FileStorageSetupActivity activity, int requestCode, int resultCode, Intent data) {
        mPublicClientApp.handleInteractiveRequestRedirect(requestCode, resultCode, data);
    }
}

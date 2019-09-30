package keepass2android.javafilestorage;

import android.app.Activity;
import android.app.Application;
import android.content.Context;
import android.content.Intent;
import android.os.Bundle;
import android.util.Base64;
import android.util.Log;


import com.microsoft.graph.authentication.MSALAuthenticationProvider;
import com.microsoft.graph.core.ClientException;
import com.microsoft.graph.core.DefaultClientConfig;
import com.microsoft.graph.core.GraphErrorCodes;
import com.microsoft.graph.http.GraphServiceException;
import com.microsoft.graph.models.extensions.DriveItem;
import com.microsoft.graph.models.extensions.Folder;
import com.microsoft.graph.models.extensions.SharedDriveItem;
import com.microsoft.graph.models.extensions.User;
import com.microsoft.graph.requests.extensions.GraphServiceClient;
import com.microsoft.graph.requests.extensions.IDriveItemCollectionPage;
import com.microsoft.graph.requests.extensions.IDriveItemCollectionRequestBuilder;
import com.microsoft.graph.requests.extensions.IDriveItemRequest;
import com.microsoft.graph.requests.extensions.IDriveItemRequestBuilder;
import com.microsoft.graph.models.extensions.IGraphServiceClient;
import com.microsoft.graph.requests.extensions.IDriveSharedWithMeCollectionPage;
import com.microsoft.graph.requests.extensions.IDriveSharedWithMeCollectionRequestBuilder;
import com.microsoft.identity.client.AuthenticationCallback;
import com.microsoft.identity.client.AuthenticationResult;
import com.microsoft.identity.client.IAccount;
import com.microsoft.identity.client.Logger;
import com.microsoft.identity.client.exception.MsalClientException;
import com.microsoft.identity.client.exception.MsalException;
import com.microsoft.identity.client.PublicClientApplication;
import com.microsoft.identity.client.exception.MsalServiceException;
import com.microsoft.identity.client.exception.MsalUiRequiredException;

import java.io.FileNotFoundException;
import java.io.InputStream;
import java.net.URI;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;

public class OneDriveStorage2 extends JavaFileStorageBase
{

    Activity mDummyActivity = new Activity();

    private final Application mApplication;
    PublicClientApplication mPublicClientApp;

    final HashMap<String /*userid*/, IGraphServiceClient> mClientByUser = new HashMap<String /*userid*/, IGraphServiceClient>();

    private static final String[] scopes = {/*"openid", */"Files.ReadWrite", "User.Read.All","Group.Read.All"};

    public OneDriveStorage2(final Activity context, final String clientId) {

        mPublicClientApp = new PublicClientApplication(context, clientId);
        initAuthenticator(context);
        mApplication = context.getApplication();


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
                    IAccount account = mPublicClientApp.getAccount(userId);
                    mPublicClientApp.acquireTokenSilentAsync(scopes, account,
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
        Log.d(TAG, "userid for path " + path + " is " + userId);
        if (mClientByUser.containsKey(userId))
            return mClientByUser.get(userId);
        Log.d(TAG, "no client found for user");
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
        return "onedrive2";
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

        //TODO should we use a separate public client app per account?
        MSALAuthenticationProvider authProvider = new MSALAuthenticationProvider(
                        mDummyActivity, //it looks like the activity is only used to set the "current activity" in the lifecycle callbacks, the MS Sample app doesn't use a real activity either
                        mApplication,
                        mPublicClientApp,
                        scopes);
        IGraphServiceClient newClient = GraphServiceClient.builder()
                .authenticationProvider(authProvider)
                .buildClient();
        mClientByUser.put(authenticationResult.getAccount().getHomeAccountIdentifier().getIdentifier(), newClient);

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
                for (StackTraceElement ste : Thread.currentThread().getStackTrace()) {
                    logDebug(ste.toString());
                }
                throw new Exception("Cannot get path item without share");
            }
            if ("me".equals(share))
                pathItem = client.me().drive().root();
            else
                pathItem = client.shares(share).root();
            if ("".equals(oneDrivePath) == false) {
                pathItem = pathItem.itemWithPath(oneDrivePath);
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
            InputStream result = clientAndpath.getPathItem()
                    .content()
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
        result.oneDrivePath = parts[1];
        if (parts.length > 2)
            result.share = parts[2];
        return result;

    }


    private Exception convertException(ClientException e) {
        Log.d(TAG, "received exception.");
        if (e instanceof GraphServiceException)
        {
            Log.d(TAG, "exception is GraphServiceException. " + ((GraphServiceException) e).getResponseCode());
            if ((((GraphServiceException) e).getResponseCode() == 404
            || ((GraphServiceException)e).getServiceError().isError(GraphErrorCodes.ITEM_NOT_FOUND)))
                return new FileNotFoundException(e.getMessage());

        }
        return e;
    }

    @Override
    public void uploadFile(String path, byte[] data, boolean writeTransactional) throws Exception {
        try {
            ClientAndPath clientAndPath = getOneDriveClientAndPath(path);
            clientAndPath.getPathItem()
                    .content()
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
                    .children()
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

    @Override
    public List<FileEntry> listFiles(String parentPath) throws Exception {
        try {

            ClientAndPath clientAndPath = getOneDriveClientAndPath(parentPath);

            if (!clientAndPath.hasShare())
            {
                return listShares(parentPath, clientAndPath.client);
            }

            ArrayList<FileEntry> result = new ArrayList<FileEntry>();
            parentPath = clientAndPath.oneDrivePath;

            IDriveItemCollectionPage itemsPage = clientAndPath.getPathItem()
                    .children()
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

    private List<FileEntry> listShares(String parentPath, IGraphServiceClient client) throws Exception {
        ArrayList<FileEntry> result = new ArrayList<FileEntry>();
        logDebug("listShares: " + (client == null));
        if (!parentPath.endsWith("/"))
            parentPath += "/";

        logDebug("listShares");
        FileEntry myEntry = getFileEntry(parentPath+"me", client.me().drive().root().buildRequest().get());
        if ((myEntry.displayName == null) || "".equals(myEntry.displayName))
            myEntry.displayName = "My OneDrive";
        result.add(myEntry);

        IDriveSharedWithMeCollectionPage sharedWithMeCollectionPage = client.me().drive().sharedWithMe().buildRequest().get();

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
                FileEntry sharedFileEntry = getFileEntry(parentPath + shareId, i);
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
        logDebug( "onStart " + activity.getPath());
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
                    logDebug( "authenticating successful");

                    try {
                        buildClient(authenticationResult);
                    } catch (InterruptedException e) {
                        e.printStackTrace();
                    }
                    activity.getState().putString(EXTRA_PATH, getProtocolPrefix() + authenticationResult.getAccount().getHomeAccountIdentifier().getIdentifier() + "/");

                    finishActivityWithSuccess(activity);
                    acquireTokenRunning = false;
                    return;
                }

                @Override
                public void onError(MsalException exception) {
                    logDebug( "authenticating not successful");
                    Intent data = new Intent();
                    data.putExtra(EXTRA_ERROR_MESSAGE, "authenticating not successful");
                    ((Activity) activity).setResult(Activity.RESULT_CANCELED, data);
                    ((Activity) activity).finish();
                    acquireTokenRunning = false;
                }

                @Override
                public void onCancel() {

                    logDebug( "authenticating cancelled");
                    Intent data = new Intent();
                    data.putExtra(EXTRA_ERROR_MESSAGE, "authenticating cancelled");
                    ((Activity) activity).setResult(Activity.RESULT_CANCELED, data);
                    ((Activity) activity).finish();
                    acquireTokenRunning = false;
                }
            });
        }
    }

    @Override
    public void onActivityResult(FileStorageSetupActivity activity, int requestCode, int resultCode, Intent data) {
        logDebug( "handleInteractiveRequestRedirect");
        mPublicClientApp.handleInteractiveRequestRedirect(requestCode, resultCode, data);
    }
}

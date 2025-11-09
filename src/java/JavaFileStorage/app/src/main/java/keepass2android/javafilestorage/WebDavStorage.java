package keepass2android.javafilestorage;

import android.content.Context;
import java.math.BigInteger;
import android.content.Intent;

import android.os.Bundle;
import android.preference.PreferenceManager;
import android.util.Log;

import com.burgstaller.okhttp.AuthenticationCacheInterceptor;
import com.burgstaller.okhttp.CachingAuthenticatorDecorator;
import com.burgstaller.okhttp.DispatchingAuthenticator;
import com.burgstaller.okhttp.basic.BasicAuthenticator;
import com.burgstaller.okhttp.digest.CachingAuthenticator;
import com.burgstaller.okhttp.digest.DigestAuthenticator;

import okhttp3.Interceptor;
import okhttp3.Response;
import okhttp3.Request;

import java.io.ByteArrayInputStream;
import java.io.FileNotFoundException;
import java.io.IOException;
import java.io.InputStream;
import java.io.StringReader;
import java.io.UnsupportedEncodingException;
import java.net.URISyntaxException;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.security.KeyManagementException;
import java.security.KeyStore;
import java.security.KeyStoreException;
import java.security.NoSuchAlgorithmException;
import java.security.SecureRandom;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Date;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.TimeUnit;

import javax.net.ssl.SSLContext;
import javax.net.ssl.SSLSocketFactory;
import javax.net.ssl.TrustManager;
import javax.net.ssl.TrustManagerFactory;
import javax.net.ssl.X509TrustManager;

import keepass2android.javafilestorage.webdav.DecoratedHostnameVerifier;
import keepass2android.javafilestorage.webdav.DecoratedTrustManager;
import keepass2android.javafilestorage.webdav.PropfindXmlParser;
import keepass2android.javafilestorage.webdav.WebDavUtil;
import okhttp3.MediaType;
import okhttp3.MultipartBody;
import okhttp3.OkHttpClient;
import okhttp3.RequestBody;
import okhttp3.internal.tls.OkHostnameVerifier;
import okio.BufferedSink;

public class WebDavStorage extends JavaFileStorageBase {


    private final ICertificateErrorHandler mCertificateErrorHandler;
    private Context appContext;

    int chunkSize;

    public WebDavStorage(ICertificateErrorHandler certificateErrorHandler, int chunkSize, Context appContext)
    {
        this.chunkSize = chunkSize;
        this.appContext = appContext;

        mCertificateErrorHandler = certificateErrorHandler;
    }

    public void setUploadChunkSize(int chunkSize)
    {
        this.chunkSize = chunkSize;
    }

    public String buildFullPath(String url, String username, String password) throws UnsupportedEncodingException {
        String scheme = url.substring(0, url.indexOf("://"));
        url = url.substring(scheme.length() + 3);
        return scheme + "://" + encode(username)+":"+encode(password)+"@"+url;
    }

    public ConnectionInfo splitStringToConnectionInfo(String filename)
            throws UnsupportedEncodingException {
        ConnectionInfo ci = new ConnectionInfo();

        String scheme = filename.substring(0, filename.indexOf("://"));
        filename = filename.substring(scheme.length() + 3);
        int idxAt = filename.indexOf('@');
        if (idxAt >= 0)
        {
            String userPwd = filename.substring(0, idxAt);
            int idxColon = userPwd.indexOf(":");
            if (idxColon >= 0);
            {
                ci.username = decode(userPwd.substring(0, idxColon));
                ci.password = decode(userPwd.substring(idxColon + 1));
            }
        }

        ci.URL = scheme + "://" +filename.substring(filename.indexOf('@') + 1);
        return ci;
    }


    private static final String HTTP_PROTOCOL_ID = "http";
    private static final String HTTPS_PROTOCOL_ID = "https";

    @Override
    public boolean checkForFileChangeFast(String path,
                                          String previousFileVersion) throws Exception {
        String currentVersion = getCurrentFileVersionFast(path);
        if (currentVersion == null)
            return false;
        return currentVersion.equals(previousFileVersion) == false;
    }

    @Override
    public String getCurrentFileVersionFast(String path) {

        return null; // no simple way to get the version "fast"
    }


    @Override
    public InputStream openFileForRead(String path) throws Exception {
        try {
            ConnectionInfo ci = splitStringToConnectionInfo(path);

            Request request = new Request.Builder()
                    .url(new URL(ci.URL))
                    .method("GET", null)
                    .build();

            Response response = getClient(ci).newCall(request).execute();
            checkStatus(response);
            return response.body().byteStream();
        } catch (Exception e) {
            throw convertException(e);
        }
    }

    //client to be reused (connection pool/thread pool). We're building a custom client for each ConnectionInfo in getClient for actual usage
    final OkHttpClient baseClient = new OkHttpClient();

    private OkHttpClient getClient(ConnectionInfo ci) throws NoSuchAlgorithmException, KeyManagementException, KeyStoreException, IOException {

        if (ci.URL.startsWith("http://") && !PreferenceManager.getDefaultSharedPreferences(appContext).getBoolean("permit_cleartext_traffic", false))
        {
            throw new IOException("Cleartext HTTP is disabled by user preference. Go to app settings/File handling if you really want to use HTTP.");
        }


        OkHttpClient.Builder builder = baseClient.newBuilder();
        final Map<String, CachingAuthenticator> authCache = new ConcurrentHashMap<>();

        com.burgstaller.okhttp.digest.Credentials credentials = new com.burgstaller.okhttp.digest.Credentials(ci.username, ci.password);
        final BasicAuthenticator basicAuthenticator = new BasicAuthenticator(credentials);
        final DigestAuthenticator digestAuthenticator = new DigestAuthenticator(credentials);

        // note that all auth schemes should be registered as lowercase!
        DispatchingAuthenticator authenticator = new DispatchingAuthenticator.Builder()
                .with("digest", digestAuthenticator)
                .with("basic", basicAuthenticator)
                .build();

        builder = builder.authenticator(new CachingAuthenticatorDecorator(authenticator, authCache))
                .addInterceptor(new AuthenticationCacheInterceptor(authCache));
        if ((mCertificateErrorHandler != null) && (!mCertificateErrorHandler.alwaysFailOnValidationError())) {


            TrustManagerFactory trustManagerFactory = TrustManagerFactory.getInstance(
                    TrustManagerFactory.getDefaultAlgorithm());
            trustManagerFactory.init((KeyStore) null);
            TrustManager[] trustManagers = trustManagerFactory.getTrustManagers();
            if (trustManagers.length != 1 || !(trustManagers[0] instanceof X509TrustManager)) {
                throw new IllegalStateException("Unexpected default trust managers:"
                        + Arrays.toString(trustManagers));
            }
            X509TrustManager trustManager = (X509TrustManager) trustManagers[0];
            trustManager = new DecoratedTrustManager(trustManager, mCertificateErrorHandler);
            SSLContext sslContext = SSLContext.getInstance("TLS");
            sslContext.init(null, new TrustManager[] { trustManager }, null);
            SSLSocketFactory sslSocketFactory = sslContext.getSocketFactory();


            builder = builder.sslSocketFactory(sslSocketFactory, trustManager)
                             .hostnameVerifier(new DecoratedHostnameVerifier(OkHostnameVerifier.INSTANCE, mCertificateErrorHandler));


            builder.connectTimeout(25, TimeUnit.SECONDS);
            builder.readTimeout(25, TimeUnit.SECONDS);
            builder.writeTimeout(25, TimeUnit.SECONDS);
        }


        OkHttpClient client =  builder.build();


        return client;
    }

    public void renameOrMoveWebDavResource(String sourcePath, String destinationPath, boolean overwrite) throws Exception {

        ConnectionInfo sourceCi = splitStringToConnectionInfo(sourcePath);
        ConnectionInfo destinationCi = splitStringToConnectionInfo(destinationPath);

        Request.Builder requestBuilder = new Request.Builder()
                .url(new URL(sourceCi.URL))
                .method("MOVE", null) // "MOVE" is the HTTP method
                .header("Destination", destinationCi.URL); // New URI for the resource

        // Use delete-then-move strategy to avoid HTTP 409 conflicts
        if (overwrite) {
            try {
                // Try to delete the destination file first if it exists
                deleteFileIfExists(destinationCi);
            } catch (Exception e) {
                // Ignore deletion errors - the file might not exist or we might not have permission
                // The MOVE operation will fail if the destination can't be overwritten
                Log.d("WebDavStorage", "Failed to delete destination file before move (this may be normal): " + e.getMessage());
            }
        }

        // Add Overwrite header (but don't rely on it solely)
        if (overwrite) {
            requestBuilder.header("Overwrite", "T"); // 'T' for true
        } else {
            requestBuilder.header("Overwrite", "F"); // 'F' for false
        }

        Request request = requestBuilder.build();

        Response response = getClient(sourceCi).newCall(request).execute();

        // Check the status code
        if (response.isSuccessful()) {
            // WebDAV MOVE can return 201 (Created) if a new resource was created at dest,
            // or 204 (No Content) if moved to a pre-existing destination (e.g., just renamed).
            // A 200 OK might also be returned by some servers, though 201/204 are more common.

        }
        else
        {
            int statusCode = response.code();
            String errorMessage = "Rename/Move failed for " + sourceCi.URL + " to " + destinationCi.URL + ": " + statusCode + " " + response.message();

            // If we get a 409 conflict and overwrite is true, try retry with enhanced cleanup
            if (overwrite && statusCode == 409) {
                try {
                    response.close();
                    // Force delete destination and retry
                    deleteFileIfExists(destinationCi);
                    // Small delay to ensure server processes the deletion
                    Thread.sleep(100);

                    // Retry the MOVE operation
                    Response retryResponse = getClient(sourceCi).newCall(request).execute();
                    if (retryResponse.isSuccessful()) {
                        retryResponse.close();
                        return; // Success on retry
                    } else {
                        errorMessage = "Rename/Move failed even after retry for " + sourceCi.URL + " to " + destinationCi.URL + ": " + retryResponse.code() + " " + retryResponse.message();
                        retryResponse.close();
                    }
                } catch (Exception retryException) {
                    errorMessage = "Rename/Move failed and retry attempt also failed: " + errorMessage + " (Retry error: " + retryException.getMessage() + ")";
                }
            }

            throw new Exception(errorMessage);
        }
    }

    /**
     * Helper method to delete a file if it exists
     * Uses PROPFIND to check existence first to avoid errors on non-existent files
     */
    private void deleteFileIfExists(ConnectionInfo ci) throws Exception {
        try {
            // First check if file exists using PROPFIND
            if (fileExists(ci)) {
                // File exists, proceed with deletion
                Request request = new Request.Builder()
                        .url(new URL(ci.URL))
                        .delete()
                        .build();
                Response response = getClient(ci).newCall(request).execute();
                try {
                    // Accept 200 OK, 204 No Content, or 404 Not Found (already deleted)
                    if (!response.isSuccessful() && response.code() != 404) {
                        throw new Exception("Delete failed with status: " + response.code() + " " + response.message());
                    }
                } finally {
                    response.close();
                }
            }
        } catch (FileNotFoundException e) {
            // File doesn't exist, which is fine
            Log.d("WebDavStorage", "File does not exist, no deletion needed: " + ci.URL);
        }
    }

    /**
     * Helper method to check if a file exists using PROPFIND
     */
    private boolean fileExists(ConnectionInfo ci) throws Exception {
        try {
            Request request = new Request.Builder()
                    .url(new URL(ci.URL))
                    .method("PROPFIND", RequestBody.create(MediaType.parse("application/xml"),
                            "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\n" +
                            "<D:propfind xmlns:D=\"DAV:\">\n" +
                            "    <D:prop>\n" +
                            "        <D:resourcetype/>\n" +
                            "    </D:prop>\n" +
                            "</D:propfind>"))
                    .header("Depth", "0")
                    .header("Content-Type", "application/xml")
                    .build();

            Response response = getClient(ci).newCall(request).execute();
            try {
                // 200 OK means file exists, 404 means it doesn't exist
                if (response.isSuccessful()) {
                    return true;
                } else if (response.code() == 404) {
                    return false;
                } else {
                    // For other status codes, assume file exists to be safe
                    Log.w("WebDavStorage", "Unexpected status checking file existence: " + response.code() + " for " + ci.URL);
                    return true;
                }
            } finally {
                response.close();
            }
        } catch (Exception e) {
            // If PROPFIND fails, assume file exists to be safe
            Log.w("WebDavStorage", "Error checking file existence, assuming it exists: " + e.getMessage());
            return true;
        }
    }

    public static String generateRandomHexString(int length) {
        SecureRandom secureRandom = new SecureRandom();
        // Generate enough bytes to ensure we can get the desired number of hex characters.
        // Each byte converts to two hex characters.
        // For 8 hex characters, we need 4 bytes.
        int numBytes = (int) Math.ceil(length / 2.0);
        byte[] randomBytes = new byte[numBytes];
        secureRandom.nextBytes(randomBytes);

        // Convert the byte array to a hexadecimal string
        // BigInteger(1, randomBytes) treats the byte array as a positive number.
        // toString(16) converts it to a hexadecimal string.
        String hexString = new BigInteger(1, randomBytes).toString(16);

        // Pad with leading zeros if necessary (e.g., if the generated number is small)
        // and then take the first 'length' characters.
        // Using String.format to ensure leading zeros if the hexString is shorter.
        return String.format("%0" + length + "d", new BigInteger(hexString, 16)).substring(0, length);
    }

    @Override
    public void uploadFile(String path, byte[] data, boolean writeTransactional)
            throws Exception {

        if (writeTransactional)
        {
            String randomSuffix = ".tmp." + generateRandomHexString(8);
            try {
                // Upload to temporary file first
                uploadFile(path + randomSuffix, data, false);
                // Use enhanced move operation with delete-then-rename strategy
                renameOrMoveWebDavResource(path+randomSuffix, path, true);
            } catch (Exception e) {
                // If move fails, try to clean up the temporary file
                try {
                    ConnectionInfo tempCi = splitStringToConnectionInfo(path + randomSuffix);
                    deleteFileIfExists(tempCi);
                } catch (Exception cleanupException) {
                    Log.w("WebDavStorage", "Failed to cleanup temporary file after failed transaction: " + cleanupException.getMessage());
                }
                throw e;
            }
            return;
        }


        try {
            ConnectionInfo ci = splitStringToConnectionInfo(path);


            RequestBody requestBody;
            if (chunkSize > 0)
            {
                // use chunked upload
                requestBody = new RequestBody() {
                    @Override
                    public MediaType contentType() {
                        return MediaType.parse("application/binary");
                    }

                    @Override
                    public void writeTo(BufferedSink sink) throws IOException {
                        try (InputStream in = new ByteArrayInputStream(data)) {
                            byte[] buffer = new byte[chunkSize];
                            int read;
                            while ((read = in.read(buffer)) != -1) {
                                sink.write(buffer, 0, read);
                                sink.flush();
                            }
                        }
                    }

                    @Override
                    public long contentLength() {
                        return -1; // use chunked upload
                    }
                };
            }
            else
            {
                requestBody = RequestBody.create(data, MediaType.parse("application/binary"));
            }

            Request request = new Request.Builder()
                    .url(new URL(ci.URL))
                    .put(requestBody)
                    .build();

            Response response = getClient(ci).newCall(request).execute();
            checkStatus(response);
        } catch (Exception e) {
            throw convertException(e);
        }

    }

    @Override
    public String createFolder(String parentPath, String newDirName)
            throws Exception {

        try {
            String newFolder = createFilePath(parentPath, newDirName);
            ConnectionInfo ci = splitStringToConnectionInfo(newFolder);

            Request request = new Request.Builder()
                    .url(new URL(ci.URL))
                    .method("MKCOL", null)
                    .build();

            Response response = getClient(ci).newCall(request).execute();
            checkStatus(response);
            return newFolder;
        } catch (Exception e) {
            throw convertException(e);
        }


    }

    private String concatPaths(String parentPath, String newDirName) {
        String res = parentPath;
        if (!res.endsWith("/"))
            res += "/";
        res += newDirName;
        return res;
    }

    @Override
    public String createFilePath(String parentPath, String newFileName)
            throws Exception {
        if (parentPath.endsWith("/") == false)
            parentPath += "/";
        return parentPath + newFileName;
    }

    public List<FileEntry> listFiles(String parentPath, int depth) throws Exception {
    ArrayList<FileEntry> result = new ArrayList<>();
        try {
            if (parentPath.endsWith("/"))
                parentPath = parentPath.substring(0,parentPath.length()-1);

            ConnectionInfo ci = splitStringToConnectionInfo(parentPath);
            String requestBody = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                    "<d:propfind xmlns:d=\"DAV:\">\n" +
                    " <d:prop><d:displayname/><d:getlastmodified/><d:getcontentlength/></d:prop>\n" +
                    "</d:propfind>\n";
            Log.d("WEBDAV", "starting query for " + ci.URL);
            Request request = new Request.Builder()
                    .url(new URL(ci.URL))
                    .method("PROPFIND", RequestBody.create(MediaType.parse("application/xml"),requestBody))
                    .addHeader("Depth",String.valueOf(depth))

                    .build();

            Response response = getClient(ci).newCall(request).execute();

            checkStatus(response);

            String xml = response.body().string();

            PropfindXmlParser parser = new PropfindXmlParser();
            List<PropfindXmlParser.Response> responses = parser.parse(new StringReader(xml));

            for (PropfindXmlParser.Response r: responses)
            {
                PropfindXmlParser.Response.PropStat.Prop okprop  =r.getOkProp();
                if (okprop != null)
                {
                    FileEntry e = new FileEntry();
                    e.canRead = e.canWrite = true;
                    Date lastMod = WebDavUtil.parseDate(okprop.LastModified);
                    if (lastMod != null)
                        e.lastModifiedTime = lastMod.getTime();
                    if (okprop.ContentLength != null)
                    {
                        try {
                            e.sizeInBytes = Integer.parseInt(okprop.ContentLength);
                        } catch (NumberFormatException exc) {
                            e.sizeInBytes = -1;
                        }
                    }

                    e.isDirectory = r.href.endsWith("/") || okprop.IsCollection;



                    e.displayName = okprop.DisplayName;
                    if (e.displayName == null)
                    {
                        e.displayName = getDisplayNameFromHref(r.href);
                    }
                    e.path = r.href;

                    if (e.path.indexOf("://") == -1)
                    {
                        //relative path:
                        e.path = buildPathFromHref(parentPath, r.href);
                    }
                    if ( (parentPath.indexOf("@") != -1) && (e.path.indexOf("@") == -1))
                    {
                        //username/password not contained in .href response. Add it back from parentPath:
                        e.path = parentPath.substring(0, parentPath.indexOf("@")+1) + e.path.substring(e.path.indexOf("://")+3);
                    }

                    if ((depth == 1) && e.isDirectory)
                    {
                        String path = e.path;
                        if (!path.endsWith("/"))
                            path += "/";

                        String parentPathWithTrailingSlash = parentPath + "/";

                        //for depth==1 only list children, not directory itself
                        if (path.equals(parentPathWithTrailingSlash))
                            continue;
                    }

                    result.add(e);
                }
            }
            return result;


        } catch (Exception e) {
            throw convertException(e);
        }

    }

    private String buildPathFromHref(String parentPath, String href) throws UnsupportedEncodingException {
        String scheme = parentPath.substring(0, parentPath.indexOf("://"));
        String filename = parentPath.substring(scheme.length() + 3);
        String userPwd = filename.substring(0, filename.indexOf('@'));
        String username_enc = (userPwd.substring(0, userPwd.indexOf(":")));
        String password_enc = (userPwd.substring(userPwd.indexOf(":") + 1));


        String host = filename.substring(filename.indexOf('@')+1);
        int firstSlashPos = host.indexOf("/");
        if (firstSlashPos >= 0)
        {
            host = host.substring(0,firstSlashPos);
        }
        if (!href.startsWith("/"))
            href = "/" + href;

        return scheme + "://" + username_enc + ":" + password_enc + "@" + host + href;
    }

    @Override
    public List<FileEntry> listFiles(String parentPath) throws Exception {
        return listFiles(parentPath, 1);
            }

    private void checkStatus(Response response) throws Exception {
        if((response.code() < 200)
            || (response.code() >= 300))
        {
            if (response.code() == 404)
                throw new FileNotFoundException();
            throw new Exception("Received unexpected response: " + response.toString());
        }
    }


    private Exception convertException(Exception e) {

        return e;

    }

    @Override
    public FileEntry getFileEntry(String filename) throws Exception {
        List<FileEntry> list = listFiles(filename,0);
        if (list.size() != 1)
            throw new FileNotFoundException();
        return list.get(0);
    }

    @Override
    public void delete(String path) throws Exception {

        try {
            ConnectionInfo ci = splitStringToConnectionInfo(path);

            Request request = new Request.Builder()
                    .url(new URL(ci.URL))
                    .delete()
                    .build();

            Response response = getClient(ci).newCall(request).execute();

            checkStatus(response);
        } catch (Exception e) {
            throw convertException(e);
        }

    }

    @Override
    public void startSelectFile(
            JavaFileStorage.FileStorageSetupInitiatorActivity activity,
            boolean isForSave, int requestCode) {
        activity.performManualFileSelect(isForSave, requestCode, getProtocolId());
    }

    @Override
    protected String decode(String encodedString)
            throws UnsupportedEncodingException {
        return java.net.URLDecoder.decode(encodedString, UTF_8);
    }

    @Override
    protected String encode(final String unencoded)
            throws UnsupportedEncodingException {
        return java.net.URLEncoder.encode(unencoded, UTF_8);
    }


    @Override
    public void prepareFileUsage(JavaFileStorage.FileStorageSetupInitiatorActivity activity, String path, int requestCode, boolean alwaysReturnSuccess) {
        Intent intent = new Intent();
        intent.putExtra(EXTRA_PATH, path);
        activity.onImmediateResult(requestCode, RESULT_FILEUSAGE_PREPARED, intent);
    }

    @Override
    public String getProtocolId() {
        return HTTPS_PROTOCOL_ID;
    }

    @Override
    public void onResume(JavaFileStorage.FileStorageSetupActivity setupAct) {

    }

    @Override
    public boolean requiresSetup(String path) {
        return false;
    }

    @Override
    public void onCreate(FileStorageSetupActivity activity,
                         Bundle savedInstanceState) {

    }

    String getDisplayNameFromHref(String href)
    {
        if (href.endsWith("/"))
            href = href.substring(0, href.length()-1);
        int lastIndex = href.lastIndexOf("/");

        String displayName;

        if (lastIndex >= 0)
            displayName = href.substring(lastIndex + 1);
        else
            displayName = href;

        try {
            displayName = java.net.URLDecoder.decode(displayName, UTF_8);
        } catch (UnsupportedEncodingException e) {
        }

        return displayName;
    }

    @Override
    public String getDisplayName(String path) {

        try {
            ConnectionInfo ci = splitStringToConnectionInfo(path);
            try
            {
                return java.net.URLDecoder.decode(ci.URL, StandardCharsets.UTF_8);
            }
            catch (Exception e)
            {
                return  ci.URL;
            }
        }
        catch (Exception e)
        {
            return getDisplayNameFromHref(path);
        }

    }

    @Override
    public String getFilename(String path) throws Exception {
        if (path.endsWith("/"))
            path = path.substring(0, path.length() - 1);
        int lastIndex = path.lastIndexOf("/");
        if (lastIndex >= 0)
            return path.substring(lastIndex + 1);
        else
            return path;
    }

    @Override
    public void onStart(FileStorageSetupActivity activity) {

    }

    @Override
    public void onActivityResult(FileStorageSetupActivity activity,
                                 int requestCode, int resultCode, Intent data) {


    }


    @Override
    public void prepareFileUsage(Context appContext, String path) {

    }

}

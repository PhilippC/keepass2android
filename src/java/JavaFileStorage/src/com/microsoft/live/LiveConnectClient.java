//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

import java.beans.PropertyChangeEvent;
import java.beans.PropertyChangeListener;
import java.io.BufferedInputStream;
import java.io.BufferedOutputStream;
import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.Closeable;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileNotFoundException;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.io.UnsupportedEncodingException;
import java.net.URI;
import java.net.URISyntaxException;
import java.util.HashMap;
import java.util.Map;

import org.apache.http.Header;
import org.apache.http.HttpEntity;
import org.apache.http.HttpResponse;
import org.apache.http.HttpVersion;
import org.apache.http.client.HttpClient;
import org.apache.http.conn.ClientConnectionManager;
import org.apache.http.conn.params.ConnManagerParams;
import org.apache.http.conn.scheme.PlainSocketFactory;
import org.apache.http.conn.scheme.Scheme;
import org.apache.http.conn.scheme.SchemeRegistry;
import org.apache.http.conn.ssl.SSLSocketFactory;
import org.apache.http.entity.InputStreamEntity;
import org.apache.http.impl.client.DefaultHttpClient;
import org.apache.http.impl.conn.tsccm.ThreadSafeClientConnManager;
import org.apache.http.params.BasicHttpParams;
import org.apache.http.params.HttpConnectionParams;
import org.apache.http.params.HttpParams;
import org.apache.http.params.HttpProtocolParams;
import org.apache.http.protocol.HTTP;
import org.json.JSONException;
import org.json.JSONObject;

import android.os.AsyncTask;
import android.text.TextUtils;

/**
 * {@code LiveConnectClient} is a class that is responsible for making requests over to the
 * Live Connect REST API. In order to perform requests, a {@link LiveConnectSession} is required.
 * A {@link LiveConnectSession} can be created from a {@link LiveAuthClient}.
 *
 * {@code LiveConnectClient} provides methods to perform both synchronous and asynchronous calls
 * on the Live Connect REST API. A synchronous method's corresponding asynchronous method is
 * suffixed with "Async" (e.g., the synchronous method, get, has a corresponding asynchronous
 * method called, getAsync). Asynchronous methods require a call back listener that will be called
 * back on the main/UI thread on completion, error, or progress.
 */
public class LiveConnectClient {

    /** Gets the ContentLength when a request finishes and sets it in the given operation. */
    private static class ContentLengthObserver implements ApiRequest.Observer {
        private final LiveDownloadOperation operation;

        public ContentLengthObserver(LiveDownloadOperation operation) {
            assert operation != null;

            this.operation = operation;
        }

        @Override
        public void onComplete(HttpResponse response) {
            Header header = response.getFirstHeader(HTTP.CONTENT_LEN);

            // Sometimes this header is not included in the response.
            if (header == null) {
                return;
            }

            int contentLength = Integer.valueOf(header.getValue());

            this.operation.setContentLength(contentLength);
        }
    }

    /**
     * Listens to an {@link ApiRequestAsync} for onComplete and onError events and calls the proper
     * method on the given {@link LiveDownloadOperationListener} on a given event.
     */
    private static class DownloadObserver implements ApiRequestAsync.Observer<InputStream> {
        private final LiveDownloadOperationListener listener;
        private final LiveDownloadOperation operation;

        public DownloadObserver(LiveDownloadOperation operation,
                                LiveDownloadOperationListener listener) {
            assert operation != null;
            assert listener != null;

            this.operation = operation;
            this.listener = listener;
        }

        @Override
        public void onComplete(InputStream result) {
            this.operation.setStream(result);
            this.listener.onDownloadCompleted(this.operation);
        }

        @Override
        public void onError(LiveOperationException e) {
            this.listener.onDownloadFailed(e, this.operation);
        }
    }

    /**
     * Listens to an {@link ApiRequestAsync} for onComplete and onError events and calls the proper
     * method on the given {@link LiveDownloadOperationListener} on a given event. When the download
     * is complete this writes the results to a file, and publishes progress updates.
     */
    private static class FileDownloadObserver extends AsyncTask<InputStream, Integer, Runnable>
                                              implements ApiRequestAsync.Observer<InputStream> {
        private class OnErrorRunnable implements Runnable {
            private final LiveOperationException exception;

            public OnErrorRunnable(LiveOperationException exception) {
                this.exception = exception;
            }

            @Override
            public void run() {
                listener.onDownloadFailed(exception, operation);
            }
        }

        private final File file;
        private final LiveDownloadOperationListener listener;
        private final LiveDownloadOperation operation;

        public FileDownloadObserver(LiveDownloadOperation operation,
                                    LiveDownloadOperationListener listener,
                                    File file) {
            assert operation != null;
            assert listener != null;
            assert file != null;

            this.operation = operation;
            this.listener = listener;
            this.file = file;
        }

        @Override
        protected Runnable doInBackground(InputStream... params) {
            InputStream is = params[0];

            byte[] buffer = new byte[BUFFER_SIZE];

            OutputStream out;
            try {
                out = new BufferedOutputStream(new FileOutputStream(file));
            } catch (FileNotFoundException e) {
                LiveOperationException exception =
                        new LiveOperationException(ErrorMessages.CLIENT_ERROR, e);
                return new OnErrorRunnable(exception);
            }

            try {
                int totalBytes = operation.getContentLength();
                int bytesRemaining = totalBytes;

                int bytesRead;
                while ((bytesRead = is.read(buffer)) != -1) {
                    out.write(buffer, 0, bytesRead);

                    bytesRemaining -= bytesRead;
                    publishProgress(totalBytes, bytesRemaining);
                }
            } catch (IOException e) {
                LiveOperationException exception =
                        new LiveOperationException(ErrorMessages.CLIENT_ERROR, e);
                return new OnErrorRunnable(exception);
            } finally {
                closeSilently(out);
                closeSilently(is);
            }

            return new Runnable() {
                @Override
                public void run() {
                    listener.onDownloadCompleted(operation);
                }
            };
        }

        @Override
        protected void onPostExecute(Runnable result) {
            result.run();
        }

        @Override
        protected void onProgressUpdate(Integer... values) {
            int totalBytes = values[0];
            int bytesRemaining = values[1];

            assert totalBytes >= 0;
            assert bytesRemaining >= 0;
            assert totalBytes >= bytesRemaining;

            listener.onDownloadProgress(totalBytes, bytesRemaining, operation);
        }

        @Override
        public void onComplete(InputStream result) {
            this.execute(result);
        }

        @Override
        public void onError(LiveOperationException e) {
            this.listener.onDownloadFailed(e, this.operation);
        }
    }

    /**
     * Listens to an {@link ApiRequestAsync} for onComplete and onError events and calls the proper
     * method on the given {@link LiveOperationListener} on a given event.
     */
    private static class OperationObserver implements ApiRequestAsync.Observer<JSONObject> {

        private final LiveOperationListener listener;
        private final LiveOperation operation;

        public OperationObserver(LiveOperation operation,
                                 LiveOperationListener listener) {
            assert operation != null;
            assert listener != null;

            this.operation = operation;
            this.listener = listener;
        }

        @Override
        public void onComplete(JSONObject result) {
            this.operation.setResult(result);
            this.listener.onComplete(this.operation);
        }

        @Override
        public void onError(LiveOperationException e) {
            this.listener.onError(e, this.operation);
        }
    }

    /** non-instantiable class that contains static constants for parameter names. */
    private static final class ParamNames {
        public static final String ACCESS_TOKEN = "session.getAccessToken()";
        public static final String BODY = "body";
        public static final String DESTINATION = "destination";
        public static final String FILE = "file";
        public static final String FILENAME = "filename";
        public static final String OVERWRITE = "overwrite";
        public static final String PATH = "path";
        public static final String SESSION = "session";

        private ParamNames() { throw new AssertionError(ErrorMessages.NON_INSTANTIABLE_CLASS); }
    }

    private enum SessionState {
        LOGGED_IN {
            @Override
            public void check() {
                // nothing. valid state.
            }
        },
        LOGGED_OUT {
            @Override
            public void check() {
                throw new IllegalStateException(ErrorMessages.LOGGED_OUT);
            }
        };

        public abstract void check();
    }

    /**
     * Listens to an {@link ApiRequestAsync} for onComplete and onError events, and listens to an
     * {@link EntityEnclosingApiRequest} for onProgress events and calls the
     * proper {@link LiveUploadOperationListener} on such events.
     */
    private static class UploadRequestListener implements ApiRequestAsync.Observer<JSONObject>,
                                                          ApiRequestAsync.ProgressObserver {

        private final LiveUploadOperationListener listener;
        private final LiveOperation operation;

        public UploadRequestListener(LiveOperation operation,
                                     LiveUploadOperationListener listener) {
            assert operation != null;
            assert listener != null;

            this.operation = operation;
            this.listener = listener;
        }

        @Override
        public void onComplete(JSONObject result) {
            this.operation.setResult(result);
            this.listener.onUploadCompleted(this.operation);
        }

        @Override
        public void onError(LiveOperationException e) {
            assert e != null;

            this.listener.onUploadFailed(e, this.operation);
        }

        @Override
        public void onProgress(Long... values) {
            long totalBytes = values[0].longValue();
            long numBytesWritten = values[1].longValue();

            assert totalBytes >= 0L;
            assert numBytesWritten >= 0L;
            assert numBytesWritten <= totalBytes;

            long bytesRemaining = totalBytes - numBytesWritten;
            this.listener.onUploadProgress((int)totalBytes, (int)bytesRemaining, this.operation);
        }
    }

    private static int BUFFER_SIZE = 1 << 10;
    private static int CONNECT_TIMEOUT_IN_MS = 30 * 1000;

    /** The key used for HTTP MOVE and HTTP COPY requests. */
    private static final String DESTINATION_KEY = "destination";

    private static volatile HttpClient HTTP_CLIENT;
    private static Object HTTP_CLIENT_LOCK = new Object();

    /**
     * A LiveDownloadOperationListener that does nothing on each of the call backs.
     * This is used so when a null listener is passed in, this can be used, instead of null,
     * to avoid if (listener == null) checks.
     */
    private static final LiveDownloadOperationListener NULL_DOWNLOAD_OPERATION_LISTENER;

    /**
     * A LiveOperationListener that does nothing on each of the call backs.
     * This is used so when a null listener is passed in, this can be used, instead of null,
     * to avoid if (listener == null) checks.
     */
    private static final LiveOperationListener NULL_OPERATION_LISTENER;

    /**
     * A LiveUploadOperationListener that does nothing on each of the call backs.
     * This is used so when a null listener is passed in, this can be used, instead of null,
     * to avoid if (listener == null) checks.
     */
    private static final LiveUploadOperationListener NULL_UPLOAD_OPERATION_LISTENER;

    private static int SOCKET_TIMEOUT_IN_MS = 30 * 1000;

    static {
        NULL_DOWNLOAD_OPERATION_LISTENER = new LiveDownloadOperationListener() {
            @Override
            public void onDownloadCompleted(LiveDownloadOperation operation) {
                assert operation != null;
            }

            @Override
            public void onDownloadFailed(LiveOperationException exception,
                                         LiveDownloadOperation operation) {
                assert exception != null;
                assert operation != null;
            }

            @Override
            public void onDownloadProgress(int totalBytes,
                                           int bytesRemaining,
                                           LiveDownloadOperation operation) {
                assert totalBytes >= 0;
                assert bytesRemaining >= 0;
                assert totalBytes >= bytesRemaining;
                assert operation != null;
            }
        };

        NULL_OPERATION_LISTENER = new LiveOperationListener() {
            @Override
            public void onComplete(LiveOperation operation) {
                assert operation != null;
            }

            @Override
            public void onError(LiveOperationException exception, LiveOperation operation) {
                assert exception != null;
                assert operation != null;
            }
        };

        NULL_UPLOAD_OPERATION_LISTENER = new LiveUploadOperationListener() {
            @Override
            public void onUploadCompleted(LiveOperation operation) {
                assert operation != null;
            }

            @Override
            public void onUploadFailed(LiveOperationException exception,
                                       LiveOperation operation) {
                assert exception != null;
                assert operation != null;
            }

            @Override
            public void onUploadProgress(int totalBytes,
                                         int bytesRemaining,
                                         LiveOperation operation) {
                assert totalBytes >= 0;
                assert bytesRemaining >= 0;
                assert totalBytes >= bytesRemaining;
                assert operation != null;
            }
        };
    }

    /**
     * Checks to see if the given path is a valid uri.
     *
     * @param path to check.
     * @return the valid URI object.
     */
    private static URI assertIsUri(String path) {
        try {
            return new URI(path);
        } catch (URISyntaxException e) {
            String message = String.format(ErrorMessages.INVALID_URI, ParamNames.PATH);
            throw new IllegalArgumentException(message);
        }
    }

    /**
     * Checks to see if the path is null, empty, or a valid uri.
     *
     * This method will be used for Download and Upload requests.
     * This method will NOT be used for Copy, Delete, Get, Move, Post and Put requests.
     *
     * @param path object_id to check.
     * @throws IllegalArgumentException if the path is empty or an invalid uri.
     * @throws NullPointerException if the path is null.
     */
    private static void assertValidPath(String path) {
        LiveConnectUtils.assertNotNullOrEmpty(path, ParamNames.PATH);
        assertIsUri(path);
    }

    private static void closeSilently(Closeable c) {
        try {
            c.close();
        } catch (Exception e) {
            // Silently...ssshh
        }
    }

    /**
     * Checks to see if the path is null, empty, or is an absolute uri and throws
     * the proper exception if it is.
     *
     * This method will be used for Copy, Delete, Get, Move, Post, and Put requests.
     * This method will NOT be used for Download and Upload requests.
     *
     * @param path object_id to check.
     * @throws IllegalArgumentException if the path is empty or an absolute uri.
     * @throws NullPointerException if the path is null.
     */
    private static void assertValidRelativePath(String path) {
        LiveConnectUtils.assertNotNullOrEmpty(path, ParamNames.PATH);

        if (path.toLowerCase().startsWith("http") || path.toLowerCase().startsWith("https")) {
            String message = String.format(ErrorMessages.ABSOLUTE_PARAMETER, ParamNames.PATH);
            throw new IllegalArgumentException(message);
        }
    }

    /**
     * Creates a new JSONObject body that has one key-value pair.
     * @param key
     * @param value
     * @return a new JSONObject body with one key-value pair.
     */
    private static JSONObject createJsonBody(String key, String value) {
        Map<String, String> tempBody = new HashMap<String, String>();
        tempBody.put(key, value);
        return new JSONObject(tempBody);
    }

    private static HttpClient getHttpClient() {
        // The LiveConnectClients can share one HttpClient with a ThreadSafeConnManager.
        if (HTTP_CLIENT == null) {
            synchronized (HTTP_CLIENT_LOCK) {
                if (HTTP_CLIENT == null) {
                    HttpParams params = new BasicHttpParams();
                    HttpConnectionParams.setConnectionTimeout(params, CONNECT_TIMEOUT_IN_MS);
                    HttpConnectionParams.setSoTimeout(params, SOCKET_TIMEOUT_IN_MS);

                    ConnManagerParams.setMaxTotalConnections(params, 100);
                    HttpProtocolParams.setVersion(params, HttpVersion.HTTP_1_1);

                    SchemeRegistry schemeRegistry = new SchemeRegistry();
                    schemeRegistry.register(new Scheme("http",
                                                       PlainSocketFactory.getSocketFactory(),
                                                       80));
                    schemeRegistry.register(new Scheme("https",
                                                       SSLSocketFactory.getSocketFactory(),
                                                       443));

                    // Create an HttpClient with the ThreadSafeClientConnManager.
                    // This connection manager must be used if more than one thread will
                    // be using the HttpClient, which is a common scenario.
                    ClientConnectionManager cm =
                            new ThreadSafeClientConnManager(params, schemeRegistry);
                    HTTP_CLIENT = new DefaultHttpClient(cm, params);
                }
            }
        }

        return HTTP_CLIENT;
    }

    /**
     * Constructs a new LiveOperation and calls the listener's onError method.
     *
     * @param e
     * @param listener
     * @param userState arbitrary object that is used to determine the caller of the method.
     * @return a new LiveOperation
     */
    private static LiveOperation handleException(String method,
                                                 String path,
                                                 LiveOperationException e,
                                                 LiveOperationListener listener,
                                                 Object userState) {
        LiveOperation operation =
                new LiveOperation.Builder(method, path).userState(userState).build();
        OperationObserver requestListener =
                new OperationObserver(operation, listener);

        requestListener.onError(e);
        return operation;
    }

    /**
     * Constructs a new LiveOperation and calls the listener's onUploadFailed method.
     *
     * @param e
     * @param listener
     * @param userState arbitrary object that is used to determine the caller of the method.
     * @return a new LiveOperation
     */
    private static LiveOperation handleException(String method,
                                                    String path,
                                                    LiveOperationException e,
                                                    LiveUploadOperationListener listener,
                                                    Object userState) {
        LiveOperation operation =
                new LiveOperation.Builder(method, path).userState(userState).build();
        UploadRequestListener requestListener = new UploadRequestListener(operation, listener);

        requestListener.onError(e);
        return operation;
    }

    /**
     * Converts an InputStream to a {@code byte[]}.
     *
     * @param is to convert to a {@code byte[]}.
     * @return a new {@code byte[]} from the InputStream.
     * @throws IOException if there was an error reading or closing the InputStream.
     */
    private static byte[] toByteArray(InputStream is) throws IOException {
        ByteArrayOutputStream byteOut = new ByteArrayOutputStream();
        OutputStream out = new BufferedOutputStream(byteOut);
        is = new BufferedInputStream(is);
        byte[] buffer = new byte[BUFFER_SIZE];

        try {
            int bytesRead;
            while ((bytesRead = is.read(buffer)) != -1) {
                out.write(buffer, 0, bytesRead);
            }
        } finally {
            // we want to perform silent close operations
            closeSilently(is);
            closeSilently(out);
        }

        return byteOut.toByteArray();
    }

    /** Change this to mock the HTTP responses. */
    private HttpClient httpClient;

    private final LiveConnectSession session;
    private SessionState sessionState;

    /**
     * Constructs a new {@code LiveConnectClient} instance and initializes it.
     *
     * @param session that will be used to authenticate calls over to the Live Connect REST API.
     * @throws NullPointerException if session is null or if session.getAccessToken() is null.
     * @throws IllegalArgumentException if session.getAccessToken() is empty.
     */
    public LiveConnectClient(LiveConnectSession session) {
        LiveConnectUtils.assertNotNull(session, ParamNames.SESSION);

        String accessToken = session.getAccessToken();
        LiveConnectUtils.assertNotNullOrEmpty(accessToken, ParamNames.ACCESS_TOKEN);

        this.session = session;
        this.sessionState = SessionState.LOGGED_IN;

        // set a listener for the accessToken. If it is set to null, then the session was logged
        // out.
        this.session.addPropertyChangeListener("accessToken", new PropertyChangeListener() {
            @Override
            public void propertyChange(PropertyChangeEvent event) {
                String newValue = (String)event.getNewValue();

                if (TextUtils.isEmpty(newValue)) {
                    LiveConnectClient.this.sessionState = SessionState.LOGGED_OUT;
                } else {
                    LiveConnectClient.this.sessionState = SessionState.LOGGED_IN;
                }
            }
        });

        this.httpClient = getHttpClient();
    }

    /**
     * Performs a synchronous HTTP COPY on the Live Connect REST API.
     *
     * A COPY duplicates a resource.
     *
     * @param path object_id of the resource to copy.
     * @param destination the folder_id where the resource will be copied to.
     * @return The LiveOperation that contains the JSON result.
     * @throws LiveOperationException if there is an error during the execution of the request.
     * @throws IllegalArgumentException if the path or destination is empty or if the path is an
     *                                  absolute uri.
     * @throws NullPointerException if either the path or destination parameters are null.
     */
    public LiveOperation copy(String path, String destination) throws LiveOperationException {
        assertValidRelativePath(path);
        LiveConnectUtils.assertNotNullOrEmpty(destination, ParamNames.DESTINATION);

        CopyRequest request = this.createCopyRequest(path, destination);
        return execute(request);
    }

    /**
     * Performs an asynchronous HTTP COPY on the Live Connect REST API.
     *
     * A COPY duplicates a resource.
     *
     * {@link LiveOperationListener#onComplete(LiveOperation)} will be called on success.
     * Otherwise, {@link LiveOperationListener#onError(LiveOperationException, LiveOperation)} will
     * be called. Both of these methods will be called on the main/UI thread.
     *
     * @param path object_id of the resource to copy.
     * @param destination the folder_id where the resource will be copied to.
     * @param listener called on either completion or error during the copy request.
     * @return the LiveOperation associated with the request.
     * @throws IllegalArgumentException if the path or destination is empty or if the path is an
     *                                  absolute uri.
     * @throws NullPointerException if either the path or destination parameters are null.
     */
    public LiveOperation copyAsync(String path,
                                   String destination,
                                   LiveOperationListener listener) {
        return this.copyAsync(path, destination, listener, null);
    }

    /**
     * Performs an asynchronous HTTP COPY on the Live Connect REST API.
     *
     * A COPY duplicates a resource.
     *
     * {@link LiveOperationListener#onComplete(LiveOperation)} will be called on success.
     * Otherwise, {@link LiveOperationListener#onError(LiveOperationException, LiveOperation)} will
     * be called. Both of these methods will be called on the main/UI thread.
     *
     * @param path object_id of the resource to copy.
     * @param destination the folder_id where the resource will be copied to
     * @param listener called on either completion or error during the copy request.
     * @param userState arbitrary object that is used to determine the caller of the method.
     * @return the LiveOperation associated with the request.
     * @throws IllegalArgumentException if the path or destination is empty or if the path is an
     *                                  absolute uri.
     * @throws NullPointerException if either the path or destination parameters are null.
     */
    public LiveOperation copyAsync(String path,
                                   String destination,
                                   LiveOperationListener listener,
                                   Object userState) {
        assertValidRelativePath(path);
        LiveConnectUtils.assertNotNullOrEmpty(destination, ParamNames.DESTINATION);
        if (listener == null) {
            listener = NULL_OPERATION_LISTENER;
        }

        CopyRequest request;
        try {
            request = this.createCopyRequest(path, destination);
        } catch (LiveOperationException e) {
            return handleException(CopyRequest.METHOD, path, e, listener, userState);
        }

        return executeAsync(request, listener, userState);
    }

    /**
     * Performs a synchronous HTTP DELETE on the Live Connect REST API.
     *
     * HTTP DELETE deletes a resource.
     *
     * @param path object_id of the resource to delete.
     * @return The LiveOperation that contains the delete response
     * @throws LiveOperationException if there is an error during the execution of the request.
     * @throws IllegalArgumentException if the path is empty or an absolute uri.
     * @throws NullPointerException if the path is null.
     */
    public LiveOperation delete(String path) throws LiveOperationException {
        assertValidRelativePath(path);

        DeleteRequest request = new DeleteRequest(this.session, this.httpClient, path);

        return execute(request);
    }

    /**
     * Performs an asynchronous HTTP DELETE on the Live Connect REST API.
     *
     * HTTP DELETE deletes a resource.
     *
     * {@link LiveOperationListener#onComplete(LiveOperation)} will be called on success.
     * Otherwise, {@link LiveOperationListener#onError(LiveOperationException, LiveOperation)} will
     * be called. Both of these methods will be called on the main/UI thread.
     *
     * @param path object_id of the resource to delete.
     * @param listener called on either completion or error during the delete request.
     * @return the LiveOperation associated with the request.
     * @throws IllegalArgumentException if the path is empty or an absolute uri.
     * @throws NullPointerException if the path is null.
     */
    public LiveOperation deleteAsync(String path, LiveOperationListener listener) {
        return this.deleteAsync(path, listener, null);
    }

    /**
     * Performs an asynchronous HTTP DELETE on the Live Connect REST API.
     *
     * HTTP DELETE deletes a resource.
     *
     * {@link LiveOperationListener#onComplete(LiveOperation)} will be called on success.
     * Otherwise, {@link LiveOperationListener#onError(LiveOperationException, LiveOperation)} will
     * be called. Both of these methods will be called on the main/UI thread.
     *
     * @param path object_id of the resource to delete.
     * @param listener called on either completion or error during the delete request.
     * @param userState arbitrary object that is used to determine the caller of the method.
     * @return the LiveOperation associated with the request.
     * @throws IllegalArgumentException if the path is empty or an absolute uri.
     * @throws NullPointerException if the path is null.
     */
    public LiveOperation deleteAsync(String path,
                                     LiveOperationListener listener,
                                     Object userState) {
        assertValidRelativePath(path);
        if (listener == null) {
            listener = NULL_OPERATION_LISTENER;
        }

        DeleteRequest request = new DeleteRequest(this.session, this.httpClient, path);


        return executeAsync(request, listener, userState);
    }

    /**
     * Downloads a resource by performing a synchronous HTTP GET on the Live Connect REST API that
     * returns the response as an {@link InputStream}.
     *
     * @param path object_id of the resource to download.
     * @throws LiveOperationException if there is an error during the execution of the request.
     * @throws IllegalArgumentException if the path is empty or an invalid uri.
     * @throws NullPointerException if the path is null.
     */
    public LiveDownloadOperation download(String path) throws LiveOperationException {
        assertValidPath(path);

        DownloadRequest request = new DownloadRequest(this.session, this.httpClient, path);

        LiveDownloadOperation operation =
                new LiveDownloadOperation.Builder(request.getMethod(), request.getPath()).build();

        request.addObserver(new ContentLengthObserver(operation));

        InputStream stream = request.execute();
        operation.setStream(stream);

        return operation;
    }

    /**
     * Downloads a resource by performing an asynchronous HTTP GET on the Live Connect REST API that
     * returns the response as an {@link InputStream}.
     *
     * {@link LiveDownloadOperationListener#onDownloadCompleted(LiveDownloadOperation)} will be
     * called on success.
     * On any download progress
     * {@link LiveDownloadOperationListener#onDownloadProgress(int, int, LiveDownloadOperation)}
     * will be called.
     * Otherwise on error,
     * {@link LiveDownloadOperationListener#onDownloadFailed(LiveOperationException,
     * LiveDownloadOperation)} will
     * be called. All of these methods will be called on the main/UI thread.
     *
     * @param path object_id of the resource to download.
     * @param listener called on either completion or error during the download request.
     * @return the LiveOperation associated with the request.
     * @throws IllegalArgumentException if the path is empty or an invalid uri.
     * @throws NullPointerException if the path is null.
     */
    public LiveDownloadOperation downloadAsync(String path,
                                               LiveDownloadOperationListener listener) {
        return this.downloadAsync(path, listener, null);
    }

    /**
     * Downloads a resource by performing an asynchronous HTTP GET on the Live Connect REST API that
     * returns the response as an {@link InputStream}.
     *
     * {@link LiveDownloadOperationListener#onDownloadCompleted(LiveDownloadOperation)} will be
     * called on success.
     * On any download progress
     * {@link LiveDownloadOperationListener#onDownloadProgress(int, int, LiveDownloadOperation)}
     * will be called.
     * Otherwise on error,
     * {@link LiveDownloadOperationListener#onDownloadFailed(LiveOperationException,
     * LiveDownloadOperation)} will
     * be called. All of these methods will be called on the main/UI thread.
     *
     * @param path object_id of the resource to download.
     * @param listener called on either completion or error during the download request.
     * @param userState arbitrary object that is used to determine the caller of the method.
     * @return the LiveOperation associated with the request.
     * @throws IllegalArgumentException if the path is empty or an invalid uri.
     * @throws NullPointerException if the path is null.
     */
    public LiveDownloadOperation downloadAsync(String path,
                                               LiveDownloadOperationListener listener,
                                               Object userState) {
        assertValidPath(path);
        if (listener == null) {
            listener = NULL_DOWNLOAD_OPERATION_LISTENER;
        }

        DownloadRequest request = new DownloadRequest(this.session, this.httpClient, path);
        return executeAsync(request, listener, userState);
    }

    public LiveDownloadOperation downloadAsync(String path,
                                               File file,
                                               LiveDownloadOperationListener listener) {
        return this.downloadAsync(path, file, listener, null);
    }

    public LiveDownloadOperation downloadAsync(String path,
                                               File file,
                                               LiveDownloadOperationListener listener,
                                               Object userState) {
        assertValidPath(path);
        if (listener == null) {
            listener = NULL_DOWNLOAD_OPERATION_LISTENER;
        }

        DownloadRequest request = new DownloadRequest(this.session, this.httpClient, path);
        ApiRequestAsync<InputStream> asyncRequest = ApiRequestAsync.newInstance(request);

        LiveDownloadOperation operation =
                new LiveDownloadOperation.Builder(request.getMethod(), request.getPath())
                                         .userState(userState)
                                         .apiRequestAsync(asyncRequest)
                                         .build();

        request.addObserver(new ContentLengthObserver(operation));
        asyncRequest.addObserver(new FileDownloadObserver(operation, listener, file));

        asyncRequest.execute();

        return operation;
    }

    /**
     * Performs a synchronous HTTP GET on the Live Connect REST API.
     *
     * HTTP GET retrieves the representation of a resource.
     *
     * @param path object_id of the resource to retrieve.
     * @return The LiveOperation that contains the JSON result.
     * @throws LiveOperationException if there is an error during the execution of the request.
     * @throws IllegalArgumentException if the path is empty or an absolute uri.
     * @throws NullPointerException if the path is null.
     */
    public LiveOperation get(String path) throws LiveOperationException {
        assertValidRelativePath(path);

        GetRequest request = new GetRequest(this.session, this.httpClient, path);
        return execute(request);
    }

    /**
     * Performs an asynchronous HTTP GET on the Live Connect REST API.
     *
     * {@link LiveOperationListener#onComplete(LiveOperation)} will be called on success.
     * Otherwise, {@link LiveOperationListener#onError(LiveOperationException, LiveOperation)} will
     * be called. Both of these methods will be called on the main/UI thread.
     *
     * @param path of the resource to retrieve.
     * @param listener called on either completion or error during the get request.
     * @return the LiveOperation associated with the request.
     * @throws IllegalArgumentException if the path is empty or an absolute uri.
     * @throws NullPointerException if the path is null.
     */
    public LiveOperation getAsync(String path, LiveOperationListener listener) {
        return this.getAsync(path, listener, null);
    }

    /**
     * Performs an asynchronous HTTP GET on the Live Connect REST API.
     *
     * {@link LiveOperationListener#onComplete(LiveOperation)} will be called on success.
     * Otherwise, {@link LiveOperationListener#onError(LiveOperationException, LiveOperation)} will
     * be called. Both of these methods will be called on the main/UI thread.
     *
     * @param path object_id of the resource to retrieve.
     * @param listener called on either completion or error during the get request.
     * @param userState arbitrary object that is used to determine the caller of the method.
     * @return the LiveOperation associated with the request.
     * @throws IllegalArgumentException if the path is empty or an absolute uri.
     * @throws NullPointerException if the path is null.
     */
    public LiveOperation getAsync(String path, LiveOperationListener listener, Object userState) {
        assertValidRelativePath(path);
        if (listener == null) {
            listener = NULL_OPERATION_LISTENER;
        }

        GetRequest request = new GetRequest(this.session, this.httpClient, path);
        return executeAsync(request, listener, userState);
    }

    /** @return the {@link LiveConnectSession} instance used by this {@code LiveConnectClient}. */
    public LiveConnectSession getSession() {
        return this.session;
    }

    /**
     * Performs a synchronous HTTP MOVE on the Live Connect REST API.
     *
     * A MOVE moves the location of a resource.
     *
     * @param path object_id of the resource to move.
     * @param destination the folder_id to where the resource will be moved to.
     * @return The LiveOperation that contains the JSON result.
     * @throws LiveOperationException if there is an error during the execution of the request.
     * @throws IllegalArgumentException if the path or destination is empty or if the path is an
     *                                  absolute uri.
     * @throws NullPointerException if either the path or destination parameters are null.
     */
    public LiveOperation move(String path, String destination) throws LiveOperationException {
        assertValidRelativePath(path);
        LiveConnectUtils.assertNotNullOrEmpty(destination, ParamNames.DESTINATION);

        MoveRequest request = this.createMoveRequest(path, destination);
        return execute(request);
    }

    /**
     * Performs an asynchronous HTTP MOVE on the Live Connect REST API.
     *
     * A MOVE moves the location of a resource.
     *
     * {@link LiveOperationListener#onComplete(LiveOperation)} will be called on success.
     * Otherwise, {@link LiveOperationListener#onError(LiveOperationException, LiveOperation)} will
     * be called. Both of these methods will be called on the main/UI thread.
     *
     * @param path object_id of the resource to move.
     * @param destination the folder_id to where the resource will be moved to.
     * @param listener called on either completion or error during the copy request.
     * @return the LiveOperation associated with the request.
     * @throws IllegalArgumentException if the path or destination is empty or if the path is an
     *                                  absolute uri.
     * @throws NullPointerException if either the path or destination parameters are null.
     */
    public LiveOperation moveAsync(String path,
                                   String destination,
                                   LiveOperationListener listener) {
        return this.moveAsync(path, destination, listener, null);
    }

    /**
     * Performs an asynchronous HTTP MOVE on the Live Connect REST API.
     *
     * A MOVE moves the location of a resource.
     *
     * {@link LiveOperationListener#onComplete(LiveOperation)} will be called on success.
     * Otherwise, {@link LiveOperationListener#onError(LiveOperationException, LiveOperation)} will
     * be called. Both of these methods will be called on the main/UI thread.
     *
     * @param path object_id of the resource to move.
     * @param destination the folder_id to where the resource will be moved to.
     * @param listener called on either completion or error during the copy request.
     * @param userState arbitrary object that is used to determine the caller of the method.
     * @return the LiveOperation associated with the request.
     * @throws IllegalArgumentException if the path or destination is empty or if the path is an
     *                                  absolute uri.
     * @throws NullPointerException if either the path or destination parameters are null.
     */
    public LiveOperation moveAsync(String path,
                                   String destination,
                                   LiveOperationListener listener,
                                   Object userState) {
        assertValidRelativePath(path);
        LiveConnectUtils.assertNotNullOrEmpty(destination, ParamNames.DESTINATION);
        if (listener == null) {
            listener = NULL_OPERATION_LISTENER;
        }

        MoveRequest request;
        try {
            request = this.createMoveRequest(path, destination);
        } catch (LiveOperationException e) {
            return handleException(MoveRequest.METHOD, path, e, listener, userState);
        }

        return executeAsync(request, listener, userState);
    }

    /**
     * Performs a synchronous HTTP POST on the Live Connect REST API.
     *
     * A POST adds a new resource to a collection.
     *
     * @param path object_id of the post request.
     * @param body body of the post request.
     * @return a LiveOperation that contains the JSON result.
     * @throws LiveOperationException if there is an error during the execution of the request.
     * @throws IllegalArgumentException if the path is empty or is an absolute uri.
     * @throws NullPointerException if either the path or body parameters are null.
     */
    public LiveOperation post(String path, JSONObject body) throws LiveOperationException {
        assertValidRelativePath(path);
        LiveConnectUtils.assertNotNull(body, ParamNames.BODY);

        PostRequest request = createPostRequest(path, body);
        return execute(request);
    }

    /**
     * Performs a synchronous HTTP POST on the Live Connect REST API.
     *
     * A POST adds a new resource to a collection.
     *
     * @param path object_id of the post request.
     * @param body body of the post request.
     * @return a LiveOperation that contains the JSON result.
     * @throws LiveOperationException if there is an error during the execution of the request.
     * @throws IllegalArgumentException if the path is empty or is an absolute uri.
     * @throws NullPointerException if either the path or body parameters are null.
     */
    public LiveOperation post(String path, String body) throws LiveOperationException {
        LiveConnectUtils.assertNotNullOrEmpty(body, ParamNames.BODY);

        JSONObject jsonBody;
        try {
            jsonBody = new JSONObject(body.toString());
        } catch (JSONException e) {
            throw new LiveOperationException(ErrorMessages.CLIENT_ERROR, e);
        }

        return this.post(path, jsonBody);
    }

    /**
     * Performs an asynchronous HTTP POST on the Live Connect REST API.
     *
     * A POST adds a new resource to a collection.
     *
     * {@link LiveOperationListener#onComplete(LiveOperation)} will be called on success.
     * Otherwise, {@link LiveOperationListener#onError(LiveOperationException, LiveOperation)} will
     * be called. Both of these methods will be called on the main/UI thread.
     *
     * @param path object_id of the post request.
     * @param body body of the post request.
     * @param listener called on either completion or error during the copy request.
     * @return the LiveOperation associated with the request.
     * @throws IllegalArgumentException if the path is empty or is an absolute uri.
     * @throws NullPointerException if either the path or body parameters are null.
     */
    public LiveOperation postAsync(String path, JSONObject body, LiveOperationListener listener) {
        return this.postAsync(path, body, listener, null);
    }

    /**
     * Performs an asynchronous HTTP POST on the Live Connect REST API.
     *
     * A POST adds a new resource to a collection.
     *
     * {@link LiveOperationListener#onComplete(LiveOperation)} will be called on success.
     * Otherwise, {@link LiveOperationListener#onError(LiveOperationException, LiveOperation)} will
     * be called. Both of these methods will be called on the main/UI thread.
     *
     * @param path object_id of the post request.
     * @param body body of the post request.
     * @param listener called on either completion or error during the copy request.
     * @param userState arbitrary object that is used to determine the caller of the method.
     * @return the LiveOperation associated with the request.
     * @throws IllegalArgumentException if the path is empty or is an absolute uri.
     * @throws NullPointerException if either the path or body parameters are null.
     */
    public LiveOperation postAsync(String path,
                                   JSONObject body,
                                   LiveOperationListener listener,
                                   Object userState) {
        assertValidRelativePath(path);
        LiveConnectUtils.assertNotNull(body, ParamNames.BODY);
        if (listener == null) {
            listener = NULL_OPERATION_LISTENER;
        }

        PostRequest request;
        try {
            request = createPostRequest(path, body);
        } catch (LiveOperationException e) {
            return handleException(PostRequest.METHOD, path, e, listener, userState);
        }

        return executeAsync(request, listener, userState);
    }

    /**
     * Performs an asynchronous HTTP POST on the Live Connect REST API.
     *
     * A POST adds a new resource to a collection.
     *
     * {@link LiveOperationListener#onComplete(LiveOperation)} will be called on success.
     * Otherwise, {@link LiveOperationListener#onError(LiveOperationException, LiveOperation)} will
     * be called. Both of these methods will be called on the main/UI thread.
     *
     * @param path object_id of the post request.
     * @param body body of the post request.
     * @param listener called on either completion or error during the copy request.
     * @return the LiveOperation associated with the request.
     * @throws IllegalArgumentException if the path is empty or is an absolute uri.
     * @throws NullPointerException if either the path or body parameters are null.
     */
    public LiveOperation postAsync(String path, String body, LiveOperationListener listener) {
        return this.postAsync(path, body, listener, null);
    }

    /**
     * Performs an asynchronous HTTP POST on the Live Connect REST API.
     *
     * A POST adds a new resource to a collection.
     *
     * {@link LiveOperationListener#onComplete(LiveOperation)} will be called on success.
     * Otherwise, {@link LiveOperationListener#onError(LiveOperationException, LiveOperation)} will
     * be called. Both of these methods will be called on the main/UI thread.
     *
     * @param path object_id of the post request.
     * @param body body of the post request.
     * @param listener called on either completion or error during the copy request.
     * @param userState arbitrary object that is used to determine the caller of the method.
     * @return the LiveOperation associated with the request.
     * @throws IllegalArgumentException if the path is empty or is an absolute uri.
     * @throws NullPointerException if either the path or body parameters are null.
     */
    public LiveOperation postAsync(String path,
                                   String body,
                                   LiveOperationListener listener,
                                   Object userState) {
        LiveConnectUtils.assertNotNullOrEmpty(body, ParamNames.BODY);

        JSONObject jsonBody;
        try {
            jsonBody = new JSONObject(body.toString());
        } catch (JSONException e) {
            return handleException(PostRequest.METHOD,
                                   path,
                                   new LiveOperationException(ErrorMessages.CLIENT_ERROR, e),
                                   listener,
                                   userState);
        }

        return this.postAsync(path, jsonBody, listener, userState);
    }

    /**
     * Performs a synchronous HTTP PUT on the Live Connect REST API.
     *
     * A PUT updates a resource or if it does not exist, it creates a one.
     *
     * @param path object_id of the put request.
     * @param body body of the put request.
     * @return a LiveOperation that contains the JSON result.
     * @throws LiveOperationException if there is an error during the execution of the request.
     * @throws IllegalArgumentException if the path is empty or is an absolute uri.
     * @throws NullPointerException if either the path or body parameters are null.
     */
    public LiveOperation put(String path, JSONObject body) throws LiveOperationException {
        assertValidRelativePath(path);
        LiveConnectUtils.assertNotNull(body, ParamNames.BODY);

        PutRequest request = createPutRequest(path, body);
        return execute(request);
    }

    /**
     * Performs a synchronous HTTP PUT on the Live Connect REST API.
     *
     * A PUT updates a resource or if it does not exist, it creates a one.
     *
     * @param path object_id of the put request.
     * @param body body of the put request.
     * @return a LiveOperation that contains the JSON result.
     * @throws LiveOperationException if there is an error during the execution of the request.
     * @throws IllegalArgumentException if the path is empty or is an absolute uri.
     * @throws NullPointerException if either the path or body parameters are null.
     */
    public LiveOperation put(String path, String body) throws LiveOperationException {
        LiveConnectUtils.assertNotNullOrEmpty(body, ParamNames.BODY);

        JSONObject jsonBody;
        try {
            jsonBody = new JSONObject(body.toString());
        } catch (JSONException e) {
            throw new LiveOperationException(ErrorMessages.CLIENT_ERROR, e);
        }

        return this.put(path, jsonBody);
    }

    /**
     * Performs an asynchronous HTTP PUT on the Live Connect REST API.
     *
     * A PUT updates a resource or if it does not exist, it creates a one.
     *
     * {@link LiveOperationListener#onComplete(LiveOperation)} will be called on success.
     * Otherwise, {@link LiveOperationListener#onError(LiveOperationException, LiveOperation)} will
     * be called. Both of these methods will be called on the main/UI thread.
     *
     * @param path object_id of the put request.
     * @param body body of the put request.
     * @param listener called on either completion or error during the put request.
     * @return the LiveOperation associated with the request.
     * @throws IllegalArgumentException if the path is empty or is an absolute uri.
     * @throws NullPointerException if either the path or body parameters are null.
     */
    public LiveOperation putAsync(String path, JSONObject body, LiveOperationListener listener) {
        return this.putAsync(path, body, listener, null);
    }

    /**
     * Performs an asynchronous HTTP PUT on the Live Connect REST API.
     *
     * A PUT updates a resource or if it does not exist, it creates a one.
     *
     * {@link LiveOperationListener#onComplete(LiveOperation)} will be called on success.
     * Otherwise, {@link LiveOperationListener#onError(LiveOperationException, LiveOperation)} will
     * be called. Both of these methods will be called on the main/UI thread.
     *
     * @param path of the put request.
     * @param body of the put request.
     * @param listener called on either completion or error during the put request.
     * @param userState arbitrary object that is used to determine the caller of the method.
     * @return the LiveOperation associated with the request.
     * @throws IllegalArgumentException if the path is empty or is an absolute uri.
     * @throws NullPointerException if either the path or body parameters are null.
     */
    public LiveOperation putAsync(String path,
                                  JSONObject body,
                                  LiveOperationListener listener,
                                  Object userState) {
        assertValidRelativePath(path);
        LiveConnectUtils.assertNotNull(body, ParamNames.BODY);
        if (listener == null) {
            listener = NULL_OPERATION_LISTENER;
        }

        PutRequest request;
        try {
            request = createPutRequest(path, body);
        } catch (LiveOperationException e) {
            return handleException(PutRequest.METHOD, path, e, listener, userState);
        }

        return executeAsync(request, listener, userState);
    }

    /**
     * Performs an asynchronous HTTP PUT on the Live Connect REST API.
     *
     * A PUT updates a resource or if it does not exist, it creates a one.
     *
     * {@link LiveOperationListener#onComplete(LiveOperation)} will be called on success.
     * Otherwise, {@link LiveOperationListener#onError(LiveOperationException, LiveOperation)} will
     * be called. Both of these methods will be called on the main/UI thread.
     *
     * @param path object_id of the put request.
     * @param body body of the put request.
     * @param listener called on either completion or error during the put request.
     * @return the LiveOperation associated with the request.
     * @throws IllegalArgumentException if the path is empty or is an absolute uri.
     * @throws NullPointerException if either the path or body parameters are null.
     */
    public LiveOperation putAsync(String path, String body, LiveOperationListener listener) {
        return this.putAsync(path, body, listener, null);
    }

    /**
     * Performs an asynchronous HTTP PUT on the Live Connect REST API.
     *
     * A PUT updates a resource or if it does not exist, it creates a one.
     *
     * {@link LiveOperationListener#onComplete(LiveOperation)} will be called on success.
     * Otherwise, {@link LiveOperationListener#onError(LiveOperationException, LiveOperation)} will
     * be called. Both of these methods will be called on the main/UI thread.
     *
     * @param path object_id of the put request.
     * @param body body of the put request.
     * @param listener called on either completion or error during the put request.
     * @param userState arbitrary object that is used to determine the caller of the method.
     * @return the LiveOperation associated with the request.
     * @throws IllegalArgumentException if the path is empty or is an absolute uri.
     * @throws NullPointerException if either the path or body parameters are null.
     */
    public LiveOperation putAsync(String path,
                                  String body,
                                  LiveOperationListener listener,
                                  Object userState) {
        LiveConnectUtils.assertNotNullOrEmpty(body, ParamNames.BODY);
        JSONObject jsonBody;
        try {
            jsonBody = new JSONObject(body.toString());
        } catch (JSONException e) {
            return handleException(PutRequest.METHOD,
                                   path,
                                   new LiveOperationException(ErrorMessages.CLIENT_ERROR, e),
                                   listener,
                                   userState);
        }

        return this.putAsync(path, jsonBody, listener, userState);
    }

    /**
     * Uploads a resource by performing a synchronous HTTP PUT on the Live Connect REST API that
     * returns the response as an {@link InputStream}.
     *
     * If a file with the same name exists the upload will fail.
     *
     * @param path location to upload to.
     * @param filename name of the new resource.
     * @param file contents of the upload.
     * @return a LiveOperation that contains the JSON result.
     * @throws LiveOperationException if there is an error during the execution of the request.
     */
    public LiveOperation upload(String path,
                                String filename,
                                InputStream file) throws LiveOperationException {
        return this.upload(path, filename, file, OverwriteOption.DoNotOverwrite);
    }

    /**
     * Uploads a resource by performing a synchronous HTTP PUT on the Live Connect REST API that
     * returns the response as an {@link InputStream}.
     *
     * @param path location to upload to.
     * @param filename name of the new resource.
     * @param file contents of the upload.
     * @param overwrite specifies what to do when a file with the same name exists.
     * @return a LiveOperation that contains the JSON result.
     * @throws LiveOperationException if there is an error during the execution of the request.
     */
    public LiveOperation upload(String path,
                                String filename,
                                InputStream file,
                                OverwriteOption overwrite) throws LiveOperationException {
        assertValidPath(path);
        LiveConnectUtils.assertNotNullOrEmpty(filename, ParamNames.FILENAME);
        LiveConnectUtils.assertNotNull(file, ParamNames.FILE);
        LiveConnectUtils.assertNotNull(overwrite, ParamNames.OVERWRITE);

        // Currently, the API Service does not support chunked uploads,
        // so we must know the length of the InputStream, before we send it.
        // Load the stream into memory to get the length.
        byte[] bytes;
        try {
            bytes = LiveConnectClient.toByteArray(file);
        } catch (IOException e) {
            throw new LiveOperationException(ErrorMessages.CLIENT_ERROR, e);
        }

        UploadRequest request = createUploadRequest(path,
                                                    filename,
                                                    new ByteArrayInputStream(bytes),
                                                    bytes.length,
                                                    overwrite);
        return execute(request);
    }

    /**
     * Uploads a resource by performing a synchronous HTTP PUT on the Live Connect REST API that
     * returns the response as an {@link InputStream}.
     *
     * If a file with the same name exists the upload will fail.
     *
     * @param path location to upload to.
     * @param filename name of the new resource.
     * @param file contents of the upload.
     * @return a LiveOperation that contains the JSON result.
     * @throws LiveOperationException if there is an error during the execution of the request.
     */
    public LiveOperation upload(String path,
                                String filename,
                                File file) throws LiveOperationException {
        return this.upload(path, filename, file, OverwriteOption.DoNotOverwrite);
    }

    /**
     * Uploads a resource by performing a synchronous HTTP PUT on the Live Connect REST API that
     * returns the response as an {@link InputStream}.
     *
     * @param path location to upload to.
     * @param filename name of the new resource.
     * @param file contents of the upload.
     * @param overwrite specifies what to do when a file with the same name exists.
     * @return a LiveOperation that contains the JSON result.
     * @throws LiveOperationException if there is an error during the execution of the request.
     */
    public LiveOperation upload(String path,
                                String filename,
                                File file,
                                OverwriteOption overwrite) throws LiveOperationException {
        assertValidPath(path);
        LiveConnectUtils.assertNotNullOrEmpty(filename, ParamNames.FILENAME);
        LiveConnectUtils.assertNotNull(file, ParamNames.FILE);
        LiveConnectUtils.assertNotNull(overwrite, ParamNames.OVERWRITE);

        InputStream is = null;
        try {
            is = new FileInputStream(file);
        } catch (FileNotFoundException e) {
            throw new LiveOperationException(ErrorMessages.CLIENT_ERROR, e);
        }

        UploadRequest request;
            request = createUploadRequest(path,
                                          filename,
                                          is,
                                          file.length(),
                                          overwrite);
        return execute(request);
    }

    /**
     * Uploads a resource by performing an asynchronous HTTP PUT on the Live Connect REST API that
     * returns the response as an {@link InputStream}.
     *
     * {@link LiveUploadOperationListener#onUploadCompleted(LiveOperation)} will be called on
     * success.
     * {@link LiveUploadOperationListener#onUploadProgress(int, int, LiveOperation) will be called
     * on upload progress. Both of these methods will be called on the main/UI thread.
     * Otherwise,
     * {@link LiveUploadOperationListener#onUploadFailed(LiveOperationException, LiveOperation)}
     * will be called. This method will NOT be called on the main/UI thread.
     *
     * @param path location to upload to.
     * @param filename name of the new resource.
     * @param file contents of the upload.
     * @param overwrite specifies what to do when a file with the same name exists.
     * @param listener called on completion, on progress, or on an error of the upload request.
     * @param userState arbitrary object that is used to determine the caller of the method.
     * @return the LiveOperation associated with the request.
     */
    public LiveOperation uploadAsync(String path,
                                     String filename,
                                     InputStream file,
                                     OverwriteOption overwrite,
                                     LiveUploadOperationListener listener,
                                     Object userState) {
        assertValidPath(path);
        LiveConnectUtils.assertNotNullOrEmpty(filename, ParamNames.FILENAME);
        LiveConnectUtils.assertNotNull(file, ParamNames.FILE);
        LiveConnectUtils.assertNotNull(overwrite, ParamNames.OVERWRITE);
        if (listener == null) {
            listener = NULL_UPLOAD_OPERATION_LISTENER;
        }

        // Currently, the API Service does not support chunked uploads,
        // so we must know the length of the InputStream, before we send it.
        // Load the stream into memory to get the length.
        byte[] bytes;
        try {
            bytes = LiveConnectClient.toByteArray(file);
        } catch (IOException e) {
            LiveOperationException exception =
                    new LiveOperationException(ErrorMessages.CLIENT_ERROR, e);
            return handleException(UploadRequest.METHOD, path, exception, listener, userState);
        }

        UploadRequest request;
        try {
            request = createUploadRequest(path,
                                          filename,
                                          new ByteArrayInputStream(bytes),
                                          bytes.length,
                                          overwrite);
        } catch (LiveOperationException e) {
            return handleException(UploadRequest.METHOD, path, e, listener, userState);
        }

        ApiRequestAsync<JSONObject> asyncRequest = ApiRequestAsync.newInstance(request);

        LiveOperation operation = new LiveOperation.Builder(request.getMethod(), request.getPath())
                                                   .userState(userState)
                                                   .apiRequestAsync(asyncRequest)
                                                   .build();

        UploadRequestListener operationListener = new UploadRequestListener(operation, listener);

        asyncRequest.addObserver(operationListener);
        asyncRequest.addProgressObserver(operationListener);
        asyncRequest.execute();

        return operation;
    }

    /**
     * Uploads a resource by performing an asynchronous HTTP PUT on the Live Connect REST API that
     * returns the response as an {@link InputStream}.
     *
     * {@link LiveUploadOperationListener#onUploadCompleted(LiveOperation)} will be called on
     * success.
     * {@link LiveUploadOperationListener#onUploadProgress(int, int, LiveOperation) will be called
     * on upload progress. Both of these methods will be called on the main/UI thread.
     * Otherwise,
     * {@link LiveUploadOperationListener#onUploadFailed(LiveOperationException, LiveOperation)}
     * will be called. This method will NOT be called on the main/UI thread.
     *
     * If a file with the same name exists the upload will fail.
     *
     * @param path location to upload to.
     * @param filename name of the new resource.
     * @param input contents of the upload.
     * @param listener called on completion, on progress, or on an error of the upload request.
     * @return the LiveOperation associated with the request.
     */
    public LiveOperation uploadAsync(String path,
                                     String filename,
                                     InputStream input,
                                     LiveUploadOperationListener listener) {
        return this.uploadAsync(path, filename, input, listener, null);
    }

    /**
     * Uploads a resource by performing an asynchronous HTTP PUT on the Live Connect REST API that
     * returns the response as an {@link InputStream}.
     *
     * {@link LiveUploadOperationListener#onUploadCompleted(LiveOperation)} will be called on
     * success.
     * {@link LiveUploadOperationListener#onUploadProgress(int, int, LiveOperation) will be called
     * on upload progress. Both of these methods will be called on the main/UI thread.
     * Otherwise,
     * {@link LiveUploadOperationListener#onUploadFailed(LiveOperationException, LiveOperation)}
     * will be called. This method will NOT be called on the main/UI thread.
     *
     * If a file with the same name exists the upload will fail.
     *
     * @param path location to upload to.
     * @param filename name of the new resource.
     * @param file contents of the upload.
     * @param listener called on completion, on progress, or on an error of the upload request.
     * @param userState arbitrary object that is used to determine the caller of the method.
     * @return the LiveOperation associated with the request.
     */
    public LiveOperation uploadAsync(String path,
                                     String filename,
                                     InputStream input,
                                     LiveUploadOperationListener listener,
                                     Object userState) {
        return this.uploadAsync(
                path,
                filename,
                input,
                OverwriteOption.DoNotOverwrite,
                listener,
                userState);
    }

    /**
     * Uploads a resource by performing an asynchronous HTTP PUT on the Live Connect REST API that
     * returns the response as an {@link InputStream}.
     *
     * {@link LiveUploadOperationListener#onUploadCompleted(LiveOperation)} will be called on
     * success.
     * {@link LiveUploadOperationListener#onUploadProgress(int, int, LiveOperation) will be called
     * on upload progress. Both of these methods will be called on the main/UI thread.
     * Otherwise,
     * {@link LiveUploadOperationListener#onUploadFailed(LiveOperationException, LiveOperation)}
     * will be called. This method will NOT be called on the main/UI thread.
     *
     * If a file with the same name exists the upload will fail.
     *
     * @param path location to upload to.
     * @param filename name of the new resource.
     * @param file contents of the upload.
     * @param listener called on completion, on progress, or on an error of the upload request.
     * @return the LiveOperation associated with the request.
     */
    public LiveOperation uploadAsync(String path,
                                     String filename,
                                     File file,
                                     LiveUploadOperationListener listener) {
        return this.uploadAsync(path, filename, file, listener, null);
    }

    /**
     * Uploads a resource by performing an asynchronous HTTP PUT on the Live Connect REST API that
     * returns the response as an {@link InputStream}.
     *
     * {@link LiveUploadOperationListener#onUploadCompleted(LiveOperation)} will be called on
     * success.
     * {@link LiveUploadOperationListener#onUploadProgress(int, int, LiveOperation) will be called
     * on upload progress. Both of these methods will be called on the main/UI thread.
     * Otherwise,
     * {@link LiveUploadOperationListener#onUploadFailed(LiveOperationException, LiveOperation)}
     * will be called. This method will NOT be called on the main/UI thread.
     *
     * If a file with the same name exists the upload will fail.
     *
     * @param path location to upload to.
     * @param filename name of the new resource.
     * @param file contents of the upload.
     * @param listener called on completion, on progress, or on an error of the upload request.
     * @param userState arbitrary object that is used to determine the caller of the method.
     * @return the LiveOperation associated with the request.
     */
    public LiveOperation uploadAsync(String path,
                                     String filename,
                                     File file,
                                     LiveUploadOperationListener listener,
                                     Object userState) {
        return this.uploadAsync(
                path,
                filename,
                file,
                OverwriteOption.DoNotOverwrite,
                listener,
                userState);
    }

    /**
     * Uploads a resource by performing an asynchronous HTTP PUT on the Live Connect REST API that
     * returns the response as an {@link InputStream}.
     *
     * {@link LiveUploadOperationListener#onUploadCompleted(LiveOperation)} will be called on
     * success.
     * {@link LiveUploadOperationListener#onUploadProgress(int, int, LiveOperation) will be called
     * on upload progress. Both of these methods will be called on the main/UI thread.
     * Otherwise,
     * {@link LiveUploadOperationListener#onUploadFailed(LiveOperationException, LiveOperation)}
     * will be called. This method will NOT be called on the main/UI thread.
     *
     * @param path location to upload to.
     * @param filename name of the new resource.
     * @param file contents of the upload.
     * @param overwrite specifies what to do when a file with the same name exists.
     * @param listener called on completion, on progress, or on an error of the upload request.
     * @param userState arbitrary object that is used to determine the caller of the method.
     * @return the LiveOperation associated with the request.
     */
    public LiveOperation uploadAsync(String path,
                                     String filename,
                                     File file,
                                     OverwriteOption overwrite,
                                     LiveUploadOperationListener listener,
                                     Object userState) {
        assertValidPath(path);
        LiveConnectUtils.assertNotNullOrEmpty(filename, ParamNames.FILENAME);
        LiveConnectUtils.assertNotNull(file, ParamNames.FILE);
        LiveConnectUtils.assertNotNull(overwrite, ParamNames.OVERWRITE);
        if (listener == null) {
            listener = NULL_UPLOAD_OPERATION_LISTENER;
        }

        UploadRequest request;
        try {
            request = createUploadRequest(path,
                                          filename,
                                          new FileInputStream(file),
                                          file.length(),
                                          overwrite);
        } catch (LiveOperationException e) {
            return handleException(UploadRequest.METHOD, path, e, listener, userState);
        } catch (FileNotFoundException e) {
            LiveOperationException exception =
                    new LiveOperationException(ErrorMessages.CLIENT_ERROR, e);
            return handleException(UploadRequest.METHOD, path, exception, listener, userState);
        }

        ApiRequestAsync<JSONObject> asyncRequest = ApiRequestAsync.newInstance(request);

        LiveOperation operation = new LiveOperation.Builder(request.getMethod(), request.getPath())
                                                   .userState(userState)
                                                   .apiRequestAsync(asyncRequest)
                                                   .build();

        UploadRequestListener operationListener = new UploadRequestListener(operation, listener);

        asyncRequest.addObserver(operationListener);
        asyncRequest.addProgressObserver(operationListener);
        asyncRequest.execute();

        return operation;
    }

    /**
     * Sets the HttpClient that is used in requests.
     *
     * This is here to be able to mock the server for testing purposes.
     *
     * @param client
     */
    void setHttpClient(HttpClient client) {
        assert client != null;
        this.httpClient = client;
    }

    /**
     * Creates a {@link CopyRequest} and its json body.
     * @param path location of the request.
     * @param destination value for the json body.
     * @return a new {@link CopyRequest}.
     * @throws LiveOperationException if there is an error creating the request.
     */
    private CopyRequest createCopyRequest(String path,
                                          String destination) throws LiveOperationException {
        assert !TextUtils.isEmpty(path);
        assert !TextUtils.isEmpty(destination);

        JSONObject body = LiveConnectClient.createJsonBody(DESTINATION_KEY, destination);
        HttpEntity entity = createJsonEntity(body);
        return new CopyRequest(this.session, this.httpClient, path, entity);
    }

    private JsonEntity createJsonEntity(JSONObject body) throws LiveOperationException {
        assert body != null;

        try {
            return new JsonEntity(body);
        } catch (UnsupportedEncodingException e) {
            throw new LiveOperationException(ErrorMessages.CLIENT_ERROR, e);
        }
    }

    private MoveRequest createMoveRequest(String path,
                                          String destination) throws LiveOperationException {
        assert !TextUtils.isEmpty(path);
        assert !TextUtils.isEmpty(destination);

        JSONObject body = LiveConnectClient.createJsonBody(DESTINATION_KEY, destination);
        HttpEntity entity = createJsonEntity(body);
        return new MoveRequest(this.session, this.httpClient, path, entity);
    }

    private PostRequest createPostRequest(String path,
                                          JSONObject body) throws LiveOperationException {
        assert !TextUtils.isEmpty(path);
        assert body != null;

        HttpEntity entity = createJsonEntity(body);
        return new PostRequest(this.session, this.httpClient, path, entity);
    }

    private PutRequest createPutRequest(String path,
                                        JSONObject body) throws LiveOperationException {
        assert !TextUtils.isEmpty(path);
        assert body != null;

        HttpEntity entity = createJsonEntity(body);
        return new PutRequest(this.session, this.httpClient, path, entity);
    }

    private UploadRequest createUploadRequest(String path,
                                              String filename,
                                              InputStream is,
                                              long length,
                                              OverwriteOption overwrite) throws LiveOperationException {
        assert !TextUtils.isEmpty(path);
        assert !TextUtils.isEmpty(filename);
        assert is != null;

        InputStreamEntity entity = new InputStreamEntity(is, length);

        return new UploadRequest(this.session, this.httpClient, path, entity, filename, overwrite);
    }

    /**
     * Creates a new LiveOperation and executes it synchronously.
     *
     * @param request
     * @param listener
     * @param userState arbitrary object that is used to determine the caller of the method.
     * @return a new LiveOperation.
     */
    private LiveOperation execute(ApiRequest<JSONObject> request) throws LiveOperationException {
        this.sessionState.check();

        JSONObject result = request.execute();

        LiveOperation.Builder builder =
                new LiveOperation.Builder(request.getMethod(), request.getPath()).result(result);

        return builder.build();
    }

    /**
     * Creates a new LiveDownloadOperation and executes it asynchronously.
     *
     * @param request
     * @param listener
     * @param userState arbitrary object that is used to determine the caller of the method.
     * @return a new LiveDownloadOperation.
     */
    private LiveDownloadOperation executeAsync(ApiRequest<InputStream> request,
                                               LiveDownloadOperationListener listener,
                                               Object userState) {
        this.sessionState.check();

        ApiRequestAsync<InputStream> asyncRequest = ApiRequestAsync.newInstance(request);

        LiveDownloadOperation operation =
                new LiveDownloadOperation.Builder(request.getMethod(), request.getPath())
                                         .userState(userState)
                                         .apiRequestAsync(asyncRequest)
                                         .build();


        request.addObserver(new ContentLengthObserver(operation));
        asyncRequest.addObserver(new DownloadObserver(operation, listener));
        asyncRequest.execute();

        return operation;
    }

    /**
     * Creates a new LiveOperation and executes it asynchronously.
     *
     * @param request
     * @param listener
     * @param userState arbitrary object that is used to determine the caller of the method.
     * @return a new LiveOperation.
     */
    private LiveOperation executeAsync(ApiRequest<JSONObject> request,
                                       LiveOperationListener listener,
                                       Object userState) {
        this.sessionState.check();

        ApiRequestAsync<JSONObject> asyncRequest = ApiRequestAsync.newInstance(request);

        LiveOperation operation = new LiveOperation.Builder(request.getMethod(), request.getPath())
                                                   .userState(userState)
                                                   .apiRequestAsync(asyncRequest)
                                                   .build();

        asyncRequest.addObserver(new OperationObserver(operation, listener));
        asyncRequest.execute();

        return operation;
    }
}

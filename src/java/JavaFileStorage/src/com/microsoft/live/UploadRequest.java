//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

import org.apache.http.HttpEntity;
import org.apache.http.client.HttpClient;
import org.apache.http.client.methods.HttpGet;
import org.apache.http.client.methods.HttpPut;
import org.apache.http.client.methods.HttpUriRequest;
import org.json.JSONException;
import org.json.JSONObject;

import android.net.Uri;
import android.text.TextUtils;

class UploadRequest extends EntityEnclosingApiRequest<JSONObject> {

    public static final String METHOD = HttpPut.METHOD_NAME;

    private static final String FILE_PATH = "file.";
    private static final String ERROR_KEY = "error";
    private static final String UPLOAD_LOCATION_KEY = "upload_location";

    private HttpUriRequest currentRequest;
    private final String filename;

    /**
     * true if the given path refers to a File Object
     * (i.e., the path begins with "/file").
     */
    private final boolean isFileUpload;

    private final OverwriteOption overwrite;

    public UploadRequest(LiveConnectSession session,
                         HttpClient client,
                         String path,
                         HttpEntity entity,
                         String filename,
                         OverwriteOption overwrite) {
        super(session,
              client,
              JsonResponseHandler.INSTANCE,
              path,
              entity,
              ResponseCodes.SUPPRESS,
              Redirects.UNSUPPRESSED);

        assert !TextUtils.isEmpty(filename);

        this.filename = filename;
        this.overwrite = overwrite;

        String lowerCasePath = this.pathUri.getPath().toLowerCase();
        this.isFileUpload = lowerCasePath.indexOf(FILE_PATH) != -1;
    }

    @Override
    public String getMethod() {
        return METHOD;
    }

    @Override
    public JSONObject execute() throws LiveOperationException {
        UriBuilder uploadRequestUri;

        // if the path was relative, we have to retrieve the upload location, because if we don't,
        // we will proxy the upload request, which is a waste of resources.
        if (this.pathUri.isRelative()) {
            JSONObject response = this.getUploadLocation();

            // We could of tried to get the upload location on an invalid path.
            // If we did, just return that response.
            // If the user passes in a path that does contain an upload location, then
            // we need to throw an error.
            if (response.has(ERROR_KEY)) {
                return response;
            } else if (!response.has(UPLOAD_LOCATION_KEY)) {
                throw new LiveOperationException(ErrorMessages.MISSING_UPLOAD_LOCATION);
            }

            // once we have the file object, get the upload location
            String uploadLocation;
            try {
                uploadLocation = response.getString(UPLOAD_LOCATION_KEY);
            } catch (JSONException e) {
                throw new LiveOperationException(ErrorMessages.SERVER_ERROR, e);
            }

            uploadRequestUri = UriBuilder.newInstance(Uri.parse(uploadLocation));

            // The original path might have query parameters that were sent to the 
            // the upload location request, and those same query parameters will need
            // to be sent to the HttpPut upload request too. Also, the returned upload_location
            // *could* have query parameters on it. We want to keep those intact and in front of the
            // the client's query parameters.
            uploadRequestUri.appendQueryString(this.pathUri.getQuery());
        } else {
            uploadRequestUri = this.requestUri;
        }

        if (!this.isFileUpload) {
            // if it is not a file upload it is a folder upload and we must
            // add the file name to the upload location
            // and don't forget to set the overwrite query parameter
            uploadRequestUri.appendToPath(this.filename);
            this.overwrite.appendQueryParameterOnTo(uploadRequestUri);
        }

        HttpPut uploadRequest = new HttpPut(uploadRequestUri.toString());
        uploadRequest.setEntity(this.entity);

        this.currentRequest = uploadRequest;

        return super.execute();
    }

    @Override
    protected HttpUriRequest createHttpRequest() throws LiveOperationException {
        return this.currentRequest;
    }

    /**
     * Performs an HttpGet on the folder/file object to retrieve the upload_location
     *
     * @return
     * @throws LiveOperationException if there was an error getting the getUploadLocation
     */
    private JSONObject getUploadLocation() throws LiveOperationException {
        this.currentRequest = new HttpGet(this.requestUri.toString());
        return super.execute();
    }
}

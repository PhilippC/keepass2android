//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

import org.apache.http.client.HttpClient;
import org.apache.http.client.methods.HttpDelete;
import org.apache.http.client.methods.HttpUriRequest;
import org.json.JSONObject;

/**
 * DeleteRequest is a subclass of an ApiRequest and performs a delete request.
 */
class DeleteRequest extends ApiRequest<JSONObject> {

    public static final String METHOD = HttpDelete.METHOD_NAME;

    /**
     * Constructs a new DeleteRequest and initializes its member variables.
     *
     * @param session with the access_token
     * @param client to perform Http requests on
     * @param path of the request
     */
    public DeleteRequest(LiveConnectSession session, HttpClient client, String path) {
        super(session, client, JsonResponseHandler.INSTANCE, path);
    }

    /** @return the string "DELETE" */
    @Override
    public String getMethod() {
        return METHOD;
    }

    /**
     * Factory method override that constructs a HttpDelete request
     *
     * @return a HttpDelete request
     */
    @Override
    protected HttpUriRequest createHttpRequest() {
        return new HttpDelete(this.requestUri.toString());
    }
}

//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

import org.apache.http.client.HttpClient;
import org.apache.http.client.methods.HttpGet;
import org.apache.http.client.methods.HttpUriRequest;
import org.json.JSONObject;

/**
 * GetRequest is a subclass of an ApiRequest and performs a GET request.
 */
class GetRequest extends ApiRequest<JSONObject> {

    public static final String METHOD = HttpGet.METHOD_NAME;

    /**
     * Constructs a new GetRequest and initializes its member variables.
     *
     * @param session with the access_token
     * @param client to perform Http requests on
     * @param path of the request
     */
    public GetRequest(LiveConnectSession session, HttpClient client, String path) {
        super(session, client, JsonResponseHandler.INSTANCE, path);
    }

    /** @return the string "GET" */
    @Override
    public String getMethod() {
        return METHOD;
    }

    /**
     * Factory method override that constructs a HttpGet request
     *
     * @return a HttpGet request
     */
    @Override
    protected HttpUriRequest createHttpRequest() {
        return new HttpGet(this.requestUri.toString());
    }
}

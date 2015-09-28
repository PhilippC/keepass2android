//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

import org.apache.http.HttpEntity;
import org.apache.http.client.HttpClient;
import org.apache.http.client.methods.HttpPut;
import org.apache.http.client.methods.HttpUriRequest;
import org.json.JSONObject;

/**
 * PutRequest is a subclass of a BodyEnclosingApiRequest and performs a Put request.
 */
class PutRequest extends EntityEnclosingApiRequest<JSONObject> {

    public static final String METHOD = HttpPut.METHOD_NAME;

    /**
     * Constructs a new PutRequest and initializes its member variables.
     *
     * @param session with the access_token
     * @param client to make Http requests on
     * @param path of the request
     * @param entity body of the request
     */
    public PutRequest(LiveConnectSession session,
                      HttpClient client,
                      String path,
                      HttpEntity entity) {
        super(session, client, JsonResponseHandler.INSTANCE, path, entity);
    }

    /** @return the string "PUT" */
    @Override
    public String getMethod() {
        return METHOD;
    }

    /**
     * Factory method override that constructs a HttpPut and adds a body to it.
     *
     * @return a HttpPut with the properly body added to it.
     */
    @Override
    protected HttpUriRequest createHttpRequest() throws LiveOperationException {
        final HttpPut request = new HttpPut(this.requestUri.toString());

        request.setEntity(this.entity);

        return request;
    }
}

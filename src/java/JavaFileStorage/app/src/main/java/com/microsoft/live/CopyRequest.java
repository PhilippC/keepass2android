//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

import org.apache.http.HttpEntity;
import org.apache.http.client.HttpClient;
import org.apache.http.client.methods.HttpUriRequest;
import org.json.JSONObject;

/**
 * CopyRequest is a subclass of a BodyEnclosingApiRequest and performs a Copy request.
 */
class CopyRequest extends EntityEnclosingApiRequest<JSONObject> {

    public static final String METHOD = HttpCopy.METHOD_NAME;

    /**
     * Constructs a new CopyRequest and initializes its member variables.
     *
     * @param session with the access_token
     * @param client to make Http requests on
     * @param path of the request
     * @param entity body of the request
     */
    public CopyRequest(LiveConnectSession session,
                       HttpClient client,
                       String path,
                       HttpEntity entity) {
        super(session, client, JsonResponseHandler.INSTANCE, path, entity);
    }

    /** @return the string "COPY" */
    @Override
    public String getMethod() {
        return METHOD;
    }

    /**
     * Factory method override that constructs a HttpCopy and adds a body to it.
     *
     * @return a HttpCopy with the properly body added to it.
     */
    @Override
    protected HttpUriRequest createHttpRequest() throws LiveOperationException {
        final HttpCopy request = new HttpCopy(this.requestUri.toString());

        request.setEntity(this.entity);

        return request;
    }
}

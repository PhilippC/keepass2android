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
 * MoveRequest is a subclass of a BodyEnclosingApiRequest and performs a Move request.
 */
class MoveRequest extends EntityEnclosingApiRequest<JSONObject> {

    public static final String METHOD = HttpMove.METHOD_NAME;

    /**
     * Constructs a new MoveRequest and initializes its member variables.
     *
     * @param session with the access_token
     * @param client to make Http requests on
     * @param path of the request
     * @param entity body of the request
     */
    public MoveRequest(LiveConnectSession session,
                       HttpClient client,
                       String path,
                       HttpEntity entity) {
        super(session, client, JsonResponseHandler.INSTANCE, path, entity);
    }

    /** @return the string "MOVE" */
    @Override
    public String getMethod() {
        return METHOD;
    }

    /**
     * Factory method override that constructs a HttpMove and adds a body to it.
     *
     * @return a HttpMove with the properly body added to it.
     */
    @Override
    protected HttpUriRequest createHttpRequest() throws LiveOperationException {
        final HttpMove request = new HttpMove(this.requestUri.toString());

        request.setEntity(this.entity);

        return request;
    }
}

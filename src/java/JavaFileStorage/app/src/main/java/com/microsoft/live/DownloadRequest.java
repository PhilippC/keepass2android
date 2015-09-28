//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

import java.io.InputStream;

import org.apache.http.client.HttpClient;
import org.apache.http.client.methods.HttpGet;
import org.apache.http.client.methods.HttpUriRequest;

class DownloadRequest extends ApiRequest<InputStream> {

    public static final String METHOD = HttpGet.METHOD_NAME;

    public DownloadRequest(LiveConnectSession session, HttpClient client, String path) {
        super(session,
              client,
              InputStreamResponseHandler.INSTANCE,
              path,
              ResponseCodes.UNSUPPRESSED,
              Redirects.UNSUPPRESSED);
    }

    @Override
    public String getMethod() {
        return METHOD;
    }

    @Override
    protected HttpUriRequest createHttpRequest() throws LiveOperationException {
        return new HttpGet(this.requestUri.toString());
    }
}

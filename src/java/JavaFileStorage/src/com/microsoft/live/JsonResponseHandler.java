//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

import java.io.IOException;

import org.apache.http.HttpEntity;
import org.apache.http.HttpResponse;
import org.apache.http.client.ClientProtocolException;
import org.apache.http.client.ResponseHandler;
import org.apache.http.util.EntityUtils;
import org.json.JSONException;
import org.json.JSONObject;

import android.text.TextUtils;

/**
 * JsonResponseHandler returns a JSONObject from an HttpResponse.
 * Singleton--use INSTANCE.
 */
enum JsonResponseHandler implements ResponseHandler<JSONObject> {
    INSTANCE;

    @Override
    public JSONObject handleResponse(HttpResponse response)
            throws ClientProtocolException, IOException {
        final HttpEntity entity = response.getEntity();
        final String stringResponse;
        if (entity != null) {
            stringResponse = EntityUtils.toString(entity);
        } else {
            return null;
        }

        if (TextUtils.isEmpty(stringResponse)) {
            return new JSONObject();
        }

        try {
           return new JSONObject(stringResponse);
        } catch (JSONException e) {
            throw new IOException(e.getLocalizedMessage());
        }
    }
}

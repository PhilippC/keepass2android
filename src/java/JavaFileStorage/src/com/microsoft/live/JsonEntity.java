//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

import java.io.UnsupportedEncodingException;

import org.apache.http.entity.StringEntity;
import org.apache.http.protocol.HTTP;
import org.json.JSONObject;

/**
 * JsonEntity is an Entity that contains a Json body
 */
class JsonEntity extends StringEntity {

    public static final String CONTENT_TYPE = "application/json;charset=" + HTTP.UTF_8;

    /**
     * Constructs a new JsonEntity.
     *
     * @param body
     * @throws UnsupportedEncodingException
     */
    JsonEntity(JSONObject body) throws UnsupportedEncodingException {
        super(body.toString(), HTTP.UTF_8);

        this.setContentType(CONTENT_TYPE);
    }
}

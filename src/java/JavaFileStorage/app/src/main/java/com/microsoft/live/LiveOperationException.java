//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

/**
 * Represents errors that occur when making requests to the Representational State Transfer
 * (REST) API.
 */
public class LiveOperationException extends Exception {

    private static final long serialVersionUID = 4630383031651156731L;

    LiveOperationException(String message) {
        super(message);
    }

    LiveOperationException(String message, Throwable e) {
        super(message, e);
    }
}

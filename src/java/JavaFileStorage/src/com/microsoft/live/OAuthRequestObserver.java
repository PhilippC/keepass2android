//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

/**
 * An observer of an OAuth Request. It will be notified of an Exception or of a Response.
 */
interface OAuthRequestObserver {
    /**
     * Callback used on an exception.
     *
     * @param exception
     */
    public void onException(LiveAuthException exception);

    /**
     * Callback used on a response.
     *
     * @param response
     */
    public void onResponse(OAuthResponse response);
}

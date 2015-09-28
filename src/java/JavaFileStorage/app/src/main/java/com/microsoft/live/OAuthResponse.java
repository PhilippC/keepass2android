//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

/**
 * OAuthRespresent a response from an OAuth server.
 * Known implementors are OAuthSuccessfulResponse and OAuthErrorResponse.
 * Different OAuthResponses can be determined by using the OAuthResponseVisitor.
 */
interface OAuthResponse {

    /**
     * Calls visit() on the visitor.
     * This method is used to determine which OAuthResponse is being returned
     * without using instance of.
     *
     * @param visitor to visit the given OAuthResponse
     */
    public void accept(OAuthResponseVisitor visitor);

}

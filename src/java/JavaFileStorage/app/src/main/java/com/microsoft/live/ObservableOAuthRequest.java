//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

/**
 * An OAuth Request that can be observed, by adding observers that will be notified on any
 * exception or response.
 */
interface ObservableOAuthRequest {
    /**
     * Adds an observer to observe the OAuth request
     *
     * @param observer to add
     */
    public void addObserver(OAuthRequestObserver observer);

    /**
     * Removes an observer that is observing the OAuth request
     *
     * @param observer to remove
     * @return true if the observer was removed.
     */
    public boolean removeObserver(OAuthRequestObserver observer);
}

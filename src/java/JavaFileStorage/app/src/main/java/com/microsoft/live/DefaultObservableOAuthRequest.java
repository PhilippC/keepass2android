//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

import java.util.ArrayList;
import java.util.List;

/**
 * Default implementation of an ObserverableOAuthRequest.
 * Other classes that need to be observed can compose themselves out of this class.
 */
class DefaultObservableOAuthRequest implements ObservableOAuthRequest {

    private final List<OAuthRequestObserver> observers;

    public DefaultObservableOAuthRequest() {
        this.observers = new ArrayList<OAuthRequestObserver>();
    }

    @Override
    public void addObserver(OAuthRequestObserver observer) {
        this.observers.add(observer);
    }

    /**
     * Calls all the Observerable's observer's onException.
     *
     * @param exception to give to the observers
     */
    public void notifyObservers(LiveAuthException exception) {
        for (final OAuthRequestObserver observer : this.observers) {
            observer.onException(exception);
        }
    }

    /**
     * Calls all this Observable's observer's onResponse.
     *
     * @param response to give to the observers
     */
    public void notifyObservers(OAuthResponse response) {
        for (final OAuthRequestObserver observer : this.observers) {
            observer.onResponse(response);
        }
    }

    @Override
    public boolean removeObserver(OAuthRequestObserver observer) {
        return this.observers.remove(observer);
    }
}

//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

import android.os.AsyncTask;

/**
 * TokenRequestAsync performs an async token request. It takes in a TokenRequest,
 * executes it, checks the OAuthResponse, and then calls the given listener.
 */
class TokenRequestAsync extends AsyncTask<Void, Void, Void> implements ObservableOAuthRequest {

    private final DefaultObservableOAuthRequest observerable;

    /** Not null if there was an exception */
    private LiveAuthException exception;

    /** Not null if there was a response */
    private OAuthResponse response;

    private final TokenRequest request;

    /**
     * Constructs a new TokenRequestAsync and initializes its member variables
     *
     * @param request to perform
     */
    public TokenRequestAsync(TokenRequest request) {
        assert request != null;

        this.observerable = new DefaultObservableOAuthRequest();
        this.request = request;
    }

    @Override
    public void addObserver(OAuthRequestObserver observer) {
        this.observerable.addObserver(observer);
    }

    @Override
    public boolean removeObserver(OAuthRequestObserver observer) {
        return this.observerable.removeObserver(observer);
    }

    @Override
    protected Void doInBackground(Void... params) {
        try {
            this.response = this.request.execute();
        } catch (LiveAuthException e) {
            this.exception = e;
        }

        return null;
    }

    @Override
    protected void onPostExecute(Void result) {
        super.onPostExecute(result);

        if (this.response != null) {
            this.observerable.notifyObservers(this.response);
        } else if (this.exception != null) {
            this.observerable.notifyObservers(this.exception);
        } else {
            final LiveAuthException exception = new LiveAuthException(ErrorMessages.CLIENT_ERROR);
            this.observerable.notifyObservers(exception);
        }
    }

	public void executeSynchronous() {
		Void result = doInBackground();
		onPostExecute(result);
		
	}
}

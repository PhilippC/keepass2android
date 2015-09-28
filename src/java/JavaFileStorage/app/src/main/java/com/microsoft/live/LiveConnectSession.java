//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

import java.beans.PropertyChangeListener;
import java.beans.PropertyChangeSupport;
import java.util.Arrays;
import java.util.Calendar;
import java.util.Collections;
import java.util.Date;
import java.util.HashSet;
import java.util.Set;

/**
 * Represents a Live Connect session.
 */
public class LiveConnectSession {

    private String accessToken;
    private String authenticationToken;

    /** Keeps track of all the listeners, and fires the property change events */
    private final PropertyChangeSupport changeSupport;

    /**
     * The LiveAuthClient that created this object.
     * This is needed in order to perform a refresh request.
     * There is a one-to-one relationship between the LiveConnectSession and LiveAuthClient.
     */
    private final LiveAuthClient creator;

    private Date expiresIn;
    private String refreshToken;
    private Set<String> scopes;
    private String tokenType;

    /**
     * Constructors a new LiveConnectSession, and sets its creator to the passed in
     * LiveAuthClient. All other member variables are left uninitialized.
     *
     * @param creator
     */
    LiveConnectSession(LiveAuthClient creator) {
        assert creator != null;

        this.creator = creator;
        this.changeSupport = new PropertyChangeSupport(this);
    }

    /**
     * Adds a {@link PropertyChangeListener} to the session that receives notification when any
     * property is changed.
     *
     * @param listener
     */
    public void addPropertyChangeListener(PropertyChangeListener listener) {
        if (listener == null) {
            return;
        }

        this.changeSupport.addPropertyChangeListener(listener);
    }

    /**
     * Adds a {@link PropertyChangeListener} to the session that receives notification when a
     * specific property is changed.
     *
     * @param propertyName
     * @param listener
     */
    public void addPropertyChangeListener(String propertyName, PropertyChangeListener listener) {
        if (listener == null) {
            return;
        }

        this.changeSupport.addPropertyChangeListener(propertyName, listener);
    }

    /**
     * @return The access token for the signed-in, connected user.
     */
    public String getAccessToken() {
        return this.accessToken;
    }

    /**
     * @return A user-specific token that provides information to an app so that it can validate
     *         the user.
     */
    public String getAuthenticationToken() {
        return this.authenticationToken;
    }

    /**
     * @return The exact time when a session expires.
     */
    public Date getExpiresIn() {
        // Defensive copy
        return new Date(this.expiresIn.getTime());
    }

    /**
     * @return An array of all PropertyChangeListeners for this session.
     */
    public PropertyChangeListener[] getPropertyChangeListeners() {
        return this.changeSupport.getPropertyChangeListeners();
    }

    /**
     * @param propertyName
     * @return An array of all PropertyChangeListeners for a specific property for this session.
     */
    public PropertyChangeListener[] getPropertyChangeListeners(String propertyName) {
        return this.changeSupport.getPropertyChangeListeners(propertyName);
    }

    /**
     * @return A user-specific refresh token that the app can use to refresh the access token.
     */
    public String getRefreshToken() {
        return this.refreshToken;
    }

    /**
     * @return The scopes that the user has consented to.
     */
    public Iterable<String> getScopes() {
        // Defensive copy is not necessary, because this.scopes is an unmodifiableSet
        return this.scopes;
    }

    /**
     * @return The type of token.
     */
    public String getTokenType() {
        return this.tokenType;
    }

    /**
     * @return {@code true} if the session is expired.
     */
    public boolean isExpired() {
        if (this.expiresIn == null) {
            return true;
        }

        final Date now = new Date();

        return now.after(this.expiresIn);
    }

    /**
     * Removes a PropertyChangeListeners on a session.
     * @param listener
     */
    public void removePropertyChangeListener(PropertyChangeListener listener) {
        if (listener == null) {
            return;
        }

        this.changeSupport.removePropertyChangeListener(listener);
    }

    /**
     * Removes a PropertyChangeListener for a specific property on a session.
     * @param propertyName
     * @param listener
     */
    public void removePropertyChangeListener(String propertyName,
                                             PropertyChangeListener listener) {
        if (listener == null) {
            return;
        }

        this.changeSupport.removePropertyChangeListener(propertyName, listener);
    }

    @Override
    public String toString() {
        return String.format("LiveConnectSession [accessToken=%s, authenticationToken=%s, expiresIn=%s, refreshToken=%s, scopes=%s, tokenType=%s]",
                             this.accessToken,
                             this.authenticationToken,
                             this.expiresIn,
                             this.refreshToken,
                             this.scopes,
                             this.tokenType);
    }

    boolean contains(Iterable<String> scopes) {
        if (scopes == null) {
            return true;
        } else if (this.scopes == null) {
            return false;
        }

        for (String scope : scopes) {
            if (!this.scopes.contains(scope)) {
                return false;
            }
        }

        return true;
    }

    /**
     * Fills in the LiveConnectSession with the OAuthResponse.
     * WARNING: The OAuthResponse must not contain OAuth.ERROR.
     *
     * @param response to load from
     */
    void loadFromOAuthResponse(OAuthSuccessfulResponse response) {
        this.accessToken = response.getAccessToken();
        this.tokenType = response.getTokenType().toString().toLowerCase();

        if (response.hasAuthenticationToken()) {
            this.authenticationToken = response.getAuthenticationToken();
        }

        if (response.hasExpiresIn()) {
            final Calendar calendar = Calendar.getInstance();
            calendar.add(Calendar.SECOND, response.getExpiresIn());
            this.setExpiresIn(calendar.getTime());
        }

        if (response.hasRefreshToken()) {
            this.refreshToken = response.getRefreshToken();
        }

        if (response.hasScope()) {
            final String scopeString = response.getScope();
            this.setScopes(Arrays.asList(scopeString.split(OAuth.SCOPE_DELIMITER)));
        }
    }

    /**
     * Refreshes this LiveConnectSession
     *
     * @return true if it was able to refresh the refresh token.
     */
    boolean refresh() {
        return this.creator.refresh();
    }

    void setAccessToken(String accessToken) {
        final String oldValue = this.accessToken;
        this.accessToken = accessToken;

        this.changeSupport.firePropertyChange("accessToken", oldValue, this.accessToken);
    }

    void setAuthenticationToken(String authenticationToken) {
        final String oldValue = this.authenticationToken;
        this.authenticationToken = authenticationToken;

        this.changeSupport.firePropertyChange("authenticationToken",
                                              oldValue,
                                              this.authenticationToken);
    }

    void setExpiresIn(Date expiresIn) {
        final Date oldValue = this.expiresIn;
        this.expiresIn = new Date(expiresIn.getTime());

        this.changeSupport.firePropertyChange("expiresIn", oldValue, this.expiresIn);
    }

    void setRefreshToken(String refreshToken) {
        final String oldValue = this.refreshToken;
        this.refreshToken = refreshToken;

        this.changeSupport.firePropertyChange("refreshToken", oldValue, this.refreshToken);
    }

    void setScopes(Iterable<String> scopes) {
        final Iterable<String> oldValue = this.scopes;

        // Defensive copy
        this.scopes = new HashSet<String>();
        if (scopes != null) {
            for (String scope : scopes) {
                this.scopes.add(scope);
            }
        }

        this.scopes = Collections.unmodifiableSet(this.scopes);

        this.changeSupport.firePropertyChange("scopes", oldValue, this.scopes);
    }

    void setTokenType(String tokenType) {
        final String oldValue = this.tokenType;
        this.tokenType = tokenType;

        this.changeSupport.firePropertyChange("tokenType", oldValue, this.tokenType);
    }

    boolean willExpireInSecs(int secs) {
        final Calendar calendar = Calendar.getInstance();
        calendar.add(Calendar.SECOND, secs);

        final Date future = calendar.getTime();

        // if add secs seconds to the current time and it is after the expired time
        // then it is almost expired.
        return future.after(this.expiresIn);
    }
}

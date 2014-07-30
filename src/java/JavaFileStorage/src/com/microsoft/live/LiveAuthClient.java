//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

import java.util.Arrays;
import java.util.Collections;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

import org.apache.http.client.HttpClient;
import org.apache.http.impl.client.DefaultHttpClient;

import android.app.Activity;
import android.app.Dialog;
import android.content.Context;
import android.content.SharedPreferences;
import android.content.SharedPreferences.Editor;
import android.net.Uri;
import android.os.AsyncTask;
import android.text.TextUtils;
import android.util.Log;
import android.webkit.CookieManager;
import android.webkit.CookieSyncManager;

import com.microsoft.live.OAuth.ErrorType;

/**
 * {@code LiveAuthClient} is a class responsible for retrieving a {@link LiveConnectSession}, which
 * can be given to a {@link LiveConnectClient} in order to make requests to the Live Connect API.
 */
public class LiveAuthClient {

    private static class AuthCompleteRunnable extends AuthListenerCaller implements Runnable {

        private final LiveStatus status;
        private final LiveConnectSession session;

        public AuthCompleteRunnable(LiveAuthListener listener,
                                    Object userState,
                                    LiveStatus status,
                                    LiveConnectSession session) {
            super(listener, userState);
            this.status = status;
            this.session = session;
        }

        @Override
        public void run() {
            listener.onAuthComplete(status, session, userState);
        }
    }

    private static class AuthErrorRunnable extends AuthListenerCaller implements Runnable {

        private final LiveAuthException exception;

        public AuthErrorRunnable(LiveAuthListener listener,
                                 Object userState,
                                 LiveAuthException exception) {
            super(listener, userState);
            this.exception = exception;
        }

        @Override
        public void run() {
            listener.onAuthError(exception, userState);
        }

    }

    private static abstract class AuthListenerCaller {
        protected final LiveAuthListener listener;
        protected final Object userState;

        public AuthListenerCaller(LiveAuthListener listener, Object userState) {
            this.listener = listener;
            this.userState = userState;
        }
    }

    /**
     * This class observes an {@link OAuthRequest} and calls the appropriate Listener method.
     * On a successful response, it will call the
     * {@link LiveAuthListener#onAuthComplete(LiveStatus, LiveConnectSession, Object)}.
     * On an exception or an unsuccessful response, it will call
     * {@link LiveAuthListener#onAuthError(LiveAuthException, Object)}.
     */
    private class ListenerCallerObserver extends AuthListenerCaller
                                         implements OAuthRequestObserver,
                                                    OAuthResponseVisitor {

        public ListenerCallerObserver(LiveAuthListener listener, Object userState) {
            super(listener, userState);
        }

        @Override
        public void onException(LiveAuthException exception) {
            new AuthErrorRunnable(listener, userState, exception).run();
        }

        @Override
        public void onResponse(OAuthResponse response) {
            response.accept(this);
        }

        @Override
        public void visit(OAuthErrorResponse response) {
            String error = response.getError().toString().toLowerCase();
            String errorDescription = response.getErrorDescription();
            String errorUri = response.getErrorUri();
            LiveAuthException exception = new LiveAuthException(error,
                                                                errorDescription,
                                                                errorUri);

            new AuthErrorRunnable(listener, userState, exception).run();
        }

        @Override
        public void visit(OAuthSuccessfulResponse response) {
            session.loadFromOAuthResponse(response);

            new AuthCompleteRunnable(listener, userState, LiveStatus.CONNECTED, session).run();
        }
    }

    /** Observer that will, depending on the response, save or clear the refresh token. */
    private class RefreshTokenWriter implements OAuthRequestObserver, OAuthResponseVisitor {

        @Override
        public void onException(LiveAuthException exception) { }

        @Override
        public void onResponse(OAuthResponse response) {
            response.accept(this);
        }

        @Override
        public void visit(OAuthErrorResponse response) {
            if (response.getError() == ErrorType.INVALID_GRANT) {
                LiveAuthClient.this.clearRefreshTokenFromPreferences();
            }
        }

        @Override
        public void visit(OAuthSuccessfulResponse response) {
            String refreshToken = response.getRefreshToken();
            if (!TextUtils.isEmpty(refreshToken)) {
                this.saveRefreshTokenToPerferences(refreshToken);
            }
        }

        private boolean saveRefreshTokenToPerferences(String refreshToken) {
            assert !TextUtils.isEmpty(refreshToken);
            Log.w("MYLIVE", "saveRefreshTokenToPerferences");

            SharedPreferences settings =
                    applicationContext.getSharedPreferences(PreferencesConstants.FILE_NAME,
                                                            Context.MODE_PRIVATE);
            Editor editor = settings.edit();
            editor.putString(PreferencesConstants.REFRESH_TOKEN_KEY, refreshToken);
            

            boolean res = editor.commit();
            Log.w("MYLIVE", "saveRefreshTokenToPerferences done for token "+refreshToken+" res="+res);
            
            return res;
        }
    }

    /**
     * An {@link OAuthResponseVisitor} that checks the {@link OAuthResponse} and if it is a
     * successful response, it loads the response into the given session.
     */
    private static class SessionRefresher implements OAuthResponseVisitor {

        private final LiveConnectSession session;
        private boolean visitedSuccessfulResponse;

        public SessionRefresher(LiveConnectSession session) {
            assert session != null;

            this.session = session;
            this.visitedSuccessfulResponse = false;
        }

        @Override
        public void visit(OAuthErrorResponse response) {
            this.visitedSuccessfulResponse = false;
        }

        @Override
        public void visit(OAuthSuccessfulResponse response) {
            this.session.loadFromOAuthResponse(response);
            this.visitedSuccessfulResponse = true;
        }

        public boolean visitedSuccessfulResponse() {
            return this.visitedSuccessfulResponse;
        }
    }

    /**
     * A LiveAuthListener that does nothing on each of the call backs.
     * This is used so when a null listener is passed in, this can be used, instead of null,
     * to avoid if (listener == null) checks.
     */
    private static final LiveAuthListener NULL_LISTENER = new LiveAuthListener() {
        @Override
        public void onAuthComplete(LiveStatus status, LiveConnectSession session, Object sender) { }
        @Override
        public void onAuthError(LiveAuthException exception, Object sender) { }
    };

    private final Context applicationContext;
    private final String clientId;
    private boolean hasPendingLoginRequest;

    /**
     * Responsible for all network (i.e., HTTP) calls.
     * Tests will want to change this to mock the network and HTTP responses.
     * @see #setHttpClient(HttpClient)
     */
    private HttpClient httpClient;

    /** saved from initialize and used in the login call if login's scopes are null. */
    private Set<String> scopesFromInitialize;

    /** One-to-one relationship between LiveAuthClient and LiveConnectSession. */
    private final LiveConnectSession session;

    {
        this.httpClient = new DefaultHttpClient();
        this.hasPendingLoginRequest = false;
        this.session = new LiveConnectSession(this);
    }

    /**
     * Constructs a new {@code LiveAuthClient} instance and initializes its member variables.
     *
     * @param context Context of the Application used to save any refresh_token.
     * @param clientId The client_id of the Live Connect Application to login to.
     */
    public LiveAuthClient(Context context, String clientId) {
        LiveConnectUtils.assertNotNull(context, "context");
        LiveConnectUtils.assertNotNullOrEmpty(clientId, "clientId");

        this.applicationContext = context.getApplicationContext();
        this.clientId = clientId;
    }

    /** @return the client_id of the Live Connect application. */
    public String getClientId() {
        return this.clientId;
    }

    /**
     * Initializes a new {@link LiveConnectSession} with the given scopes.
     *
     * The {@link LiveConnectSession} will be returned by calling
     * {@link LiveAuthListener#onAuthComplete(LiveStatus, LiveConnectSession, Object)}.
     * Otherwise, the {@link LiveAuthListener#onAuthError(LiveAuthException, Object)} will be
     * called. These methods will be called on the main/UI thread.
     *
     * If the wl.offline_access scope is used, a refresh_token is stored in the given
     * {@link Activity}'s {@link SharedPerfences}.
     *
     * @param scopes to initialize the {@link LiveConnectSession} with.
     *        See <a href="http://msdn.microsoft.com/en-us/library/hh243646.aspx">MSDN Live Connect
     *        Reference's Scopes and permissions</a> for a list of scopes and explanations.
     * @param listener called on either completion or error during the initialize process.
     */
    public void initialize(Iterable<String> scopes, LiveAuthListener listener) {
        this.initialize(scopes, listener, null);
    }

    /**
     * Initializes a new {@link LiveConnectSession} with the given scopes.
     *
     * The {@link LiveConnectSession} will be returned by calling
     * {@link LiveAuthListener#onAuthComplete(LiveStatus, LiveConnectSession, Object)}.
     * Otherwise, the {@link LiveAuthListener#onAuthError(LiveAuthException, Object)} will be
     * called. These methods will be called on the main/UI thread.
     *
     * If the wl.offline_access scope is used, a refresh_token is stored in the given
     * {@link Activity}'s {@link SharedPerfences}.
     *
     * @param scopes to initialize the {@link LiveConnectSession} with.
     *        See <a href="http://msdn.microsoft.com/en-us/library/hh243646.aspx">MSDN Live Connect
     *        Reference's Scopes and permissions</a> for a list of scopes and explanations.
     * @param listener called on either completion or error during the initialize process
     * @param userState arbitrary object that is used to determine the caller of the method.
     */
    public void initialize(Iterable<String> scopes, LiveAuthListener listener, Object userState) {
        TokenRequestAsync asyncRequest = getInitializeRequest(scopes, listener,
				userState);
        if (asyncRequest == null)
        {
        	return;
        }

        asyncRequest.execute();
    }
    
    public void initializeSynchronous(Iterable<String> scopes, LiveAuthListener listener, Object userState) {
        TokenRequestAsync asyncRequest = getInitializeRequest(scopes, listener,
				userState);
        if (asyncRequest == null)
        {
        	return;
        }

        asyncRequest.executeSynchronous();
    }

	private TokenRequestAsync getInitializeRequest(Iterable<String> scopes,
			LiveAuthListener listener, Object userState) {
		if (listener == null) {
            listener = NULL_LISTENER;
        }

        if (scopes == null) {
            scopes = Arrays.asList(new String[0]);
        }

        // copy scopes for login
        this.scopesFromInitialize = new HashSet<String>();
        for (String scope : scopes) {
            this.scopesFromInitialize.add(scope);
        }
        this.scopesFromInitialize = Collections.unmodifiableSet(this.scopesFromInitialize);

        String refreshToken = this.getRefreshTokenFromPreferences();

        if (refreshToken == null) {
            listener.onAuthComplete(LiveStatus.UNKNOWN, null, userState);
            return null;
        }

        RefreshAccessTokenRequest request =
                new RefreshAccessTokenRequest(this.httpClient,
                                              this.clientId,
                                              refreshToken,
                                              TextUtils.join(OAuth.SCOPE_DELIMITER, scopes));
        TokenRequestAsync asyncRequest = new TokenRequestAsync(request);

        asyncRequest.addObserver(new ListenerCallerObserver(listener, userState));
        asyncRequest.addObserver(new RefreshTokenWriter());
		return asyncRequest;
	}

    /**
     * Initializes a new {@link LiveConnectSession} with the given scopes.
     *
     * The {@link LiveConnectSession} will be returned by calling
     * {@link LiveAuthListener#onAuthComplete(LiveStatus, LiveConnectSession, Object)}.
     * Otherwise, the {@link LiveAuthListener#onAuthError(LiveAuthException, Object)} will be
     * called. These methods will be called on the main/UI thread.
     *
     * If the wl.offline_access scope is used, a refresh_token is stored in the given
     * {@link Activity}'s {@link SharedPerfences}.
     *
     * This initialize will use the last successfully used scopes from either a login or initialize.
     *
     * @param listener called on either completion or error during the initialize process.
     */
    public void initialize(LiveAuthListener listener) {
        this.initialize(listener, null);
    }

    /**
     * Initializes a new {@link LiveConnectSession} with the given scopes.
     *
     * The {@link LiveConnectSession} will be returned by calling
     * {@link LiveAuthListener#onAuthComplete(LiveStatus, LiveConnectSession, Object)}.
     * Otherwise, the {@link LiveAuthListener#onAuthError(LiveAuthException, Object)} will be
     * called. These methods will be called on the main/UI thread.
     *
     * If the wl.offline_access scope is used, a refresh_token is stored in the given
     * {@link Activity}'s {@link SharedPerfences}.
     *
     * This initialize will use the last successfully used scopes from either a login or initialize.
     *
     * @param listener called on either completion or error during the initialize process.
     * @param userState arbitrary object that is used to determine the caller of the method.
     */
    public void initialize(LiveAuthListener listener, Object userState) {
        this.initialize(null, listener, userState);
    }

    /**
     * Logs in an user with the given scopes.
     *
     * login displays a {@link Dialog} that will prompt the
     * user for a username and password, and ask for consent to use the given scopes.
     * A {@link LiveConnectSession} will be returned by calling
     * {@link LiveAuthListener#onAuthComplete(LiveStatus, LiveConnectSession, Object)}.
     * Otherwise, the {@link LiveAuthListener#onAuthError(LiveAuthException, Object)} will be
     * called. These methods will be called on the main/UI thread.
     *
     * @param activity {@link Activity} instance to display the Login dialog on.
     * @param scopes to initialize the {@link LiveConnectSession} with.
     *        See <a href="http://msdn.microsoft.com/en-us/library/hh243646.aspx">MSDN Live Connect
     *        Reference's Scopes and permissions</a> for a list of scopes and explanations.
     * @param listener called on either completion or error during the login process.
     * @throws IllegalStateException if there is a pending login request.
     */
    public void login(Activity activity, Iterable<String> scopes, LiveAuthListener listener) {
        this.login(activity, scopes, listener, null);
    }

    /**
     * Logs in an user with the given scopes.
     *
     * login displays a {@link Dialog} that will prompt the
     * user for a username and password, and ask for consent to use the given scopes.
     * A {@link LiveConnectSession} will be returned by calling
     * {@link LiveAuthListener#onAuthComplete(LiveStatus, LiveConnectSession, Object)}.
     * Otherwise, the {@link LiveAuthListener#onAuthError(LiveAuthException, Object)} will be
     * called. These methods will be called on the main/UI thread.
     *
     * @param activity {@link Activity} instance to display the Login dialog on
     * @param scopes to initialize the {@link LiveConnectSession} with.
     *        See <a href="http://msdn.microsoft.com/en-us/library/hh243646.aspx">MSDN Live Connect
     *        Reference's Scopes and permissions</a> for a list of scopes and explanations.
     * @param listener called on either completion or error during the login process.
     * @param userState arbitrary object that is used to determine the caller of the method.
     * @throws IllegalStateException if there is a pending login request.
     */
    public void login(Activity activity,
                      Iterable<String> scopes,
                      LiveAuthListener listener,
                      Object userState) {
        LiveConnectUtils.assertNotNull(activity, "activity");

        if (listener == null) {
            listener = NULL_LISTENER;
        }

        if (this.hasPendingLoginRequest) {
            throw new IllegalStateException(ErrorMessages.LOGIN_IN_PROGRESS);
        }

        // if no scopes were passed in, use the scopes from initialize or if those are empty,
        // create an empty list
        if (scopes == null) {
            if (this.scopesFromInitialize == null) {
                scopes = Arrays.asList(new String[0]);
            } else {
                scopes = this.scopesFromInitialize;
            }
        }

        // if the session is valid and contains all the scopes, do not display the login ui.
        boolean showDialog = this.session.isExpired() ||
                             !this.session.contains(scopes);
        if (!showDialog) {
            listener.onAuthComplete(LiveStatus.CONNECTED, this.session, userState);
            return;
        }

        String scope = TextUtils.join(OAuth.SCOPE_DELIMITER, scopes);
        String redirectUri = Config.INSTANCE.getOAuthDesktopUri().toString();
        AuthorizationRequest request = new AuthorizationRequest(activity,
                                                                this.httpClient,
                                                                this.clientId,
                                                                redirectUri,
                                                                scope);

        request.addObserver(new ListenerCallerObserver(listener, userState));
        request.addObserver(new RefreshTokenWriter());
        request.addObserver(new OAuthRequestObserver() {
            @Override
            public void onException(LiveAuthException exception) {
                LiveAuthClient.this.hasPendingLoginRequest = false;
            }

            @Override
            public void onResponse(OAuthResponse response) {
                LiveAuthClient.this.hasPendingLoginRequest = false;
            }
        });

        this.hasPendingLoginRequest = true;

        request.execute();
    }

    /**
     * Logs out the given user.
     *
     * Also, this method clears the previously created {@link LiveConnectSession}.
     * {@link LiveAuthListener#onAuthComplete(LiveStatus, LiveConnectSession, Object)} will be
     * called on completion. Otherwise,
     * {@link LiveAuthListener#onAuthError(LiveAuthException, Object)} will be called.
     *
     * @param listener called on either completion or error during the logout process.
     */
    public void logout(LiveAuthListener listener) {
        this.logout(listener, null);
    }

    /**
     * Logs out the given user.
     *
     * Also, this method clears the previously created {@link LiveConnectSession}.
     * {@link LiveAuthListener#onAuthComplete(LiveStatus, LiveConnectSession, Object)} will be
     * called on completion. Otherwise,
     * {@link LiveAuthListener#onAuthError(LiveAuthException, Object)} will be called.
     *
     * @param listener called on either completion or error during the logout process.
     * @param userState arbitrary object that is used to determine the caller of the method.
     */
    public void logout(LiveAuthListener listener, Object userState) {
        if (listener == null) {
            listener = NULL_LISTENER;
        }

        session.setAccessToken(null);
        session.setAuthenticationToken(null);
        session.setRefreshToken(null);
        session.setScopes(null);
        session.setTokenType(null);

        clearRefreshTokenFromPreferences();

        CookieSyncManager cookieSyncManager =
                CookieSyncManager.createInstance(this.applicationContext);
        CookieManager manager = CookieManager.getInstance();
        Uri logoutUri = Config.INSTANCE.getOAuthLogoutUri();
        String url = logoutUri.toString();
        String domain = logoutUri.getHost();

        List<String> cookieKeys = this.getCookieKeysFromPreferences();
        for (String cookieKey : cookieKeys) {
            String value = TextUtils.join("", new String[] {
               cookieKey,
               "=; expires=Thu, 30-Oct-1980 16:00:00 GMT;domain=",
               domain,
               ";path=/;version=1"
            });

            manager.setCookie(url, value);
        }

        cookieSyncManager.sync();
        listener.onAuthComplete(LiveStatus.UNKNOWN, null, userState);
    }

    /** @return The {@link HttpClient} instance used by this {@code LiveAuthClient}. */
    HttpClient getHttpClient() {
        return this.httpClient;
    }

    /** @return The {@link LiveConnectSession} instance that this {@code LiveAuthClient} created. */
    LiveConnectSession getSession() {
        return session;
    }

    /**
     * Refreshes the previously created session.
     *
     * @return true if the session was successfully refreshed.
     */
    boolean refresh() {
        String scope = TextUtils.join(OAuth.SCOPE_DELIMITER, this.session.getScopes());
        String refreshToken = this.session.getRefreshToken();

        if (TextUtils.isEmpty(refreshToken)) {
            return false;
        }

        RefreshAccessTokenRequest request =
                new RefreshAccessTokenRequest(this.httpClient, this.clientId, refreshToken, scope);

        OAuthResponse response;
        try {
            response = request.execute();
        } catch (LiveAuthException e) {
            return false;
        }

        SessionRefresher refresher = new SessionRefresher(this.session);
        response.accept(refresher);
        response.accept(new RefreshTokenWriter());

        return refresher.visitedSuccessfulResponse();
    }

    /**
     * Sets the {@link HttpClient} that is used for HTTP requests by this {@code LiveAuthClient}.
     * Tests will want to change this to mock the network/HTTP responses.
     * @param client The new HttpClient to be set.
     */
    void setHttpClient(HttpClient client) {
        assert client != null;
        this.httpClient = client;
    }

    /**
     * Clears the refresh token from this {@code LiveAuthClient}'s
     * {@link Activity#getPreferences(int)}.
     *
     * @return true if the refresh token was successfully cleared.
     */
    private boolean clearRefreshTokenFromPreferences() {
        SharedPreferences settings = getSharedPreferences();
        Editor editor = settings.edit();
        editor.remove(PreferencesConstants.REFRESH_TOKEN_KEY);

        return editor.commit();
    }

    private SharedPreferences getSharedPreferences() {
        return applicationContext.getSharedPreferences(PreferencesConstants.FILE_NAME,
                                                       Context.MODE_PRIVATE);
    }

    private List<String> getCookieKeysFromPreferences() {
        SharedPreferences settings = getSharedPreferences();
        String cookieKeys = settings.getString(PreferencesConstants.COOKIES_KEY, "");

        return Arrays.asList(TextUtils.split(cookieKeys, PreferencesConstants.COOKIE_DELIMITER));
    }

    /**
     * Retrieves the refresh token from this {@code LiveAuthClient}'s
     * {@link Activity#getPreferences(int)}.
     *
     * @return the refresh token from persistent storage.
     */
    private String getRefreshTokenFromPreferences() {
        SharedPreferences settings = getSharedPreferences();
        return settings.getString(PreferencesConstants.REFRESH_TOKEN_KEY, null);
    }
}

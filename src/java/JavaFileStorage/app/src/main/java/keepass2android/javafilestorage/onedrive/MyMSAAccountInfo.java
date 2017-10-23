package keepass2android.javafilestorage.onedrive;

/**
 * Created by Philipp on 22.11.2016.
 */

import com.microsoft.services.msa.LiveConnectSession;
import com.onedrive.sdk.authentication.AccountType;
import com.onedrive.sdk.authentication.IAccountInfo;
import com.onedrive.sdk.authentication.MSAAccountInfo;
import com.onedrive.sdk.authentication.MSAAuthenticator;
import com.onedrive.sdk.logger.ILogger;

import com.microsoft.services.msa.LiveConnectSession;
import com.onedrive.sdk.logger.ILogger;

/**
 * Account information for a MSA based account.
 */
public class MyMSAAccountInfo implements IAccountInfo {

    /**
     * The service root for the OneDrive personal API.
     */
    public static final String ONE_DRIVE_PERSONAL_SERVICE_ROOT = "https://api.onedrive.com/v1.0";

    /**
     * The authenticator that can refresh this account.
     */
    private final MyMSAAuthenticator mAuthenticator;

    /**
     * The session this account is based off of.
     */
    private LiveConnectSession mSession;

    /**
     * The logger.
     */
    private final ILogger mLogger;

    /**
     * Creates an MSAAccountInfo object.
     * @param authenticator The authenticator that this account info was created from.
     * @param liveConnectSession The session this account is based off of.
     * @param logger The logger.
     */
    public MyMSAAccountInfo(final MyMSAAuthenticator authenticator,
                          final LiveConnectSession liveConnectSession,
                          final ILogger logger) {
        mAuthenticator = authenticator;
        mSession = liveConnectSession;
        mLogger = logger;
    }

    /**
     * Get the type of the account.
     * @return The MicrosoftAccount account type.
     */
    @Override
    public AccountType getAccountType() {
        return AccountType.MicrosoftAccount;
    }

    /**
     * Get the access token for requests against the service root.
     * @return The access token for requests against the service root.
     */
    @Override
    public String getAccessToken() {
        return mSession.getAccessToken();
    }

    /**
     * Get the OneDrive service root for this account.
     * @return the OneDrive service root for this account.
     */
    @Override
    public String getServiceRoot() {
        return ONE_DRIVE_PERSONAL_SERVICE_ROOT;
    }

    /**
     * Indicates if the account access token is expired and needs to be refreshed.
     * @return true if refresh() needs to be called and
     *         false if the account is still valid.
     */
    @Override
    public boolean isExpired() {
        return mSession.isExpired();
    }

    /**
     * Refreshes the authentication token for this account info.
     */
    @Override
    public void refresh() {
        mLogger.logDebug("Refreshing access token...");
        final MyMSAAccountInfo newInfo = (MyMSAAccountInfo)mAuthenticator.loginSilent();
        mSession = newInfo.mSession;
    }
}

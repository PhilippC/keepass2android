package keepass2android.javafilestorage.onedrive2;

import com.microsoft.graph.authentication.IAuthenticationProvider;
import com.microsoft.graph.core.DefaultClientConfig;
import com.microsoft.graph.core.IClientConfig;
import com.microsoft.graph.extensions.GraphServiceClient;
import com.microsoft.graph.extensions.IGraphServiceClient;
import com.microsoft.graph.http.IHttpRequest;

/**
 * Singleton class that manages a GraphServiceClient object.
 * It implements IAuthentication provider to authenticate requests using an access token.
 */
public class GraphServiceClientManager implements IAuthenticationProvider {
    private IGraphServiceClient mGraphServiceClient;
    private String mAccessToken;

    public GraphServiceClientManager(String accessToken) {
        mAccessToken = accessToken;
    }

    @Override
    public void authenticateRequest(IHttpRequest request)  {
        try {
            request.addHeader("Authorization", "Bearer " + mAccessToken);
        } catch (NullPointerException e) {
            e.printStackTrace();
        }
    }

    public synchronized IGraphServiceClient getGraphServiceClient() {
        return getGraphServiceClient(this);
    }

    public synchronized IGraphServiceClient getGraphServiceClient(IAuthenticationProvider authenticationProvider) {
        if (mGraphServiceClient == null) {
            IClientConfig clientConfig = DefaultClientConfig.createWithAuthenticationProvider(
                    authenticationProvider
            );
            mGraphServiceClient = new GraphServiceClient.Builder().fromConfig(clientConfig).buildClient();
        }

        return mGraphServiceClient;
    }
}

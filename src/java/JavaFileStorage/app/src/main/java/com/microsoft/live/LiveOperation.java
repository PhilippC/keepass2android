//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

import org.json.JSONObject;

import android.text.TextUtils;

/**
 * Represents data returned from the Live Connect Representational State Transfer (REST) API
 * services.
 */
public class LiveOperation {
    static class Builder {
        private ApiRequestAsync<JSONObject> apiRequestAsync;
        private final String method;
        private final String path;
        private JSONObject result;
        private Object userState;

        public Builder(String method, String path) {
            assert !TextUtils.isEmpty(method);
            assert !TextUtils.isEmpty(path);

            this.method = method;
            this.path = path;
        }

        /**
         * Set if the operation to build is an async operation.
         *
         * @param apiRequestAsync
         * @return this Builder
         */
        public Builder apiRequestAsync(ApiRequestAsync<JSONObject> apiRequestAsync) {
            assert apiRequestAsync != null;

            this.apiRequestAsync = apiRequestAsync;
            return this;
        }

        public LiveOperation build() {
            return new LiveOperation(this);
        }

        public Builder result(JSONObject result) {
            assert result != null;
            this.result = result;
            return this;
        }

        public Builder userState(Object userState) {
            this.userState = userState;
            return this;
        }
    }

    private final ApiRequestAsync<JSONObject> apiRequestAsync;
    private final String method;
    private final String path;
    private JSONObject result;
    private final Object userState;

    private LiveOperation(Builder builder) {
        this.apiRequestAsync = builder.apiRequestAsync;
        this.method = builder.method;
        this.path = builder.path;
        this.result = builder.result;
        this.userState = builder.userState;
    }

    /** Cancels the pending request. */
    public void cancel() {
        final boolean isCancelable = this.apiRequestAsync != null;
        if (isCancelable) {
            this.apiRequestAsync.cancel(true);
        }
    }

    /**
     * @return The type of HTTP method used to make the call.
     */
    public String getMethod() {
        return this.method;
    }

    /**
     * @return The path to which the call was made.
     */
    public String getPath() {
        return this.path;
    }

    /**
     * @return The raw result of the operation in the requested format.
     */
    public String getRawResult() {
        JSONObject result = this.getResult();
        if (result == null) {
            return null;
        }

        return result.toString();
    }

    /**
     * @return The JSON object that is the result of the requesting operation.
     */
    public JSONObject getResult() {
        return this.result;
    }

    /**
     * @return The user state that was passed in.
     */
    public Object getUserState() {
        return this.userState;
    }

    void setResult(JSONObject result) {
        assert result != null;

        this.result = result;
    }
}

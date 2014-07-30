//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

import java.util.Iterator;
import java.util.LinkedList;

import android.net.Uri;
import android.text.TextUtils;
import android.util.Log;

/**
 * Class for building URIs. The most useful benefit of this class is its query parameter
 * management. It stores all the query parameters in a LinkedList, so parameters can 
 * be looked up, removed, and added easily.
 */
class UriBuilder {
    
    public static class QueryParameter {
        private final String key;
        private final String value;
        
        /**
         * Constructs a query parameter with no value (e.g., download).
         * 
         * @param key
         */
        public QueryParameter(String key) {
            assert key != null;
            
            this.key = key;
            this.value = null;
        }
        
        public QueryParameter(String key, String value) {
            assert key != null;
            assert value != null;
            
            this.key = key;
            this.value = value;
        }
        
        public String getKey() {
            return this.key;
        }
        
        public String getValue() {
            return this.value;
        }
        
        public boolean hasValue() {
            return this.value != null;
        }
        
        @Override
        public String toString() {
            if (this.hasValue()) {
                return this.key + "=" + this.value;
            }
            
            return this.key;
        }
    }
    
    private static final String EQUAL = "=";
    private static final String AMPERSAND = "&";
    private static final char FORWARD_SLASH = '/';
    
    private String scheme;
    private String host;
    private StringBuilder path;
    
    private final LinkedList<QueryParameter> queryParameters;
    
    /**
     * Constructs a new UriBuilder from the given Uri.
     * 
     * @return a new Uri Builder based off the given Uri.
     */
    public static UriBuilder newInstance(Uri uri) {
        return new UriBuilder().scheme(uri.getScheme())
                               .host(uri.getHost())
                               .path(uri.getPath())
                               .query(uri.getQuery());
    }
    
    public UriBuilder() {
        this.queryParameters = new LinkedList<QueryParameter>();
    }
    
    /**
     * Appends a new query parameter to the UriBuilder's query string.
     * 
     * (e.g., appendQueryParameter("k1", "v1") when UriBuilder's query string is
     * k2=v2&k3=v3 results in k2=v2&k3=v3&k1=v1).
     * 
     * @param key Key of the new query parameter.
     * @param value Value of the new query parameter.
     * @return this UriBuilder object. Useful for chaining.
     */
    public UriBuilder appendQueryParameter(String key, String value) {
        assert key != null;
        assert value != null;
        
        this.queryParameters.add(new QueryParameter(key, value));
        
        return this;
    }

    /**
     * Appends the given query string on to the existing UriBuilder's query parameters.
     * 
     * (e.g., UriBuilder's queryString k1=v1&k2=v2 and given queryString k3=v3&k4=v4, results in
     * k1=v1&k2=v2&k3=v3&k4=v4).
     * 
     * @param queryString Key-Value pairs separated by & and = (e.g., k1=v1&k2=v2&k3=k3).
     * @return this UriBuilder object. Useful for chaining.
     */
    public UriBuilder appendQueryString(String queryString) {
        if (queryString == null) {
            return this;
        }
        
        String[] pairs = TextUtils.split(queryString, UriBuilder.AMPERSAND); 
        for(String pair : pairs) {
            String[] splitPair = TextUtils.split(pair, UriBuilder.EQUAL);
            if (splitPair.length == 2) {
                String key = splitPair[0];
                String value = splitPair[1];
                
                this.queryParameters.add(new QueryParameter(key, value));
            } else if (splitPair.length == 1){
                String key = splitPair[0];
            
                this.queryParameters.add(new QueryParameter(key));
            } else {
                Log.w("com.microsoft.live.UriBuilder", "Invalid query parameter: " + pair);
            }
        }
        
        return this;
    }

    /**
     * Appends the given path to the UriBuilder's current path.
     * 
     * @param path The path to append onto this UriBuilder's path.
     * @return this UriBuilder object. Useful for chaining.
     */
    public UriBuilder appendToPath(String path) {
        assert path != null;
        
        if (this.path == null) {
            this.path = new StringBuilder(path);
        } else {
            boolean endsWithSlash = TextUtils.isEmpty(this.path) ? false :
                    this.path.charAt(this.path.length() - 1) == UriBuilder.FORWARD_SLASH;
            boolean pathIsEmpty = TextUtils.isEmpty(path);
            boolean beginsWithSlash =
                    pathIsEmpty ? false : path.charAt(0) == UriBuilder.FORWARD_SLASH;
            
            if (endsWithSlash && beginsWithSlash) {
                if (path.length() > 1) {
                    this.path.append(path.substring(1));
                    
                }
            } else if (!endsWithSlash && !beginsWithSlash) {
                if (!pathIsEmpty) {
                    this.path.append(UriBuilder.FORWARD_SLASH).append(path);
                }
            } else {
                this.path.append(path);
            }
        }
        
        return this;
    }

    /**
     * Builds the Uri by converting into a android.net.Uri object.
     * 
     * @return a new android.net.Uri defined by what was given to the builder.
     */
    public Uri build() {
        return new Uri.Builder().scheme(this.scheme)
                                .authority(this.host)
                                .path(this.path == null ? "" : this.path.toString())
                                .encodedQuery(TextUtils.join("&", this.queryParameters))
                                .build();
    }

    /**
     * Sets the host part of the Uri.
     * 
     * @return this UriBuilder object. Useful for chaining.
     */
    public UriBuilder host(String host) {
        assert host != null;
        this.host = host;
        
        return this;
    }
    
    /**
     * Sets the path and removes any previously existing path.
     * 
     * @param path The path to set on this UriBuilder.
     * @return this UriBuilder object. Useful for chaining.
     */
    public UriBuilder path(String path) {
        assert path != null;
        this.path = new StringBuilder(path);
        
        return this;
    }

    /**
     * Takes a query string and puts it in the Uri Builder's query string removing
     * any existing query parameters.
     * 
     * @param queryString Key-Value pairs separated by & and = (e.g., k1=v1&k2=v2&k3=k3).
     * @return this UriBuilder object. Useful for chaining.
     */
    public UriBuilder query(String queryString) {
        this.queryParameters.clear();
        
        return this.appendQueryString(queryString);
    }
    
    /**
     * Removes all query parameters from the UriBuilder that has the given key.
     * 
     * (e.g., removeQueryParametersWithKey("k1") when UriBuilder's query string of k1=v1&k2=v2&k1=v3
     * results in k2=v2).
     * 
     * @param key Query parameter's key to remove
     * @return this UriBuilder object. Useful for chaining.
     */
    public UriBuilder removeQueryParametersWithKey(String key) {
        // There could be multiple query parameters with this key and
        // we want to remove all of them.
        Iterator<QueryParameter> it = this.queryParameters.iterator();
        
        while (it.hasNext()) {
            QueryParameter qp = it.next();
            if (qp.getKey().equals(key)) {
                it.remove();
            }
        }
        
        return this;
    }

    /**
     * Sets the scheme part of the Uri.
     * 
     * @return this UriBuilder object. Useful for chaining.
     */
    public UriBuilder scheme(String scheme) {
        assert scheme != null;
        this.scheme = scheme;
        
        return this;
    }

    /**
     * Returns the URI in string format (e.g., http://foo.com/bar?k1=v2).
     */
    @Override
    public String toString() {
        return this.build().toString();
    }
}

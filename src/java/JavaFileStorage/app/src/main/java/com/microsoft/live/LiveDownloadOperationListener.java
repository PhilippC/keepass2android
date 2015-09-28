//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

/**
 * Represents any functionality related to downloads that works with the Live Connect
 * Representational State Transfer (REST) API.
 */
public interface LiveDownloadOperationListener {

    /**
     * Called when the associated download operation call completes.
     * @param operation The {@link LiveDownloadOperation} object.
     */
    public void onDownloadCompleted(LiveDownloadOperation operation);

    /**
     * Called when the associated download operation call fails.
     * @param exception The error returned by the REST operation call.
     * @param operation The {@link LiveDownloadOperation} object.
     */
    public void onDownloadFailed(LiveOperationException exception,
                                 LiveDownloadOperation operation);

    /**
     * Updates the progression of the download.
     * @param totalBytes The total bytes downloaded.
     * @param bytesRemaining The bytes remaining to download.
     * @param operation The {@link LiveDownloadOperation} object.
     */
    public void onDownloadProgress(int totalBytes,
                                   int bytesRemaining,
                                   LiveDownloadOperation operation);
}

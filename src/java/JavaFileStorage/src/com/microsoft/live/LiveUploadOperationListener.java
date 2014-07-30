//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

/**
 * Represents any functionality related to uploads that works with the Live Connect
 * Representational State Transfer (REST) API.
 */
public interface LiveUploadOperationListener {

    /**
     * Called when the associated upload operation call completes.
     * @param operation The {@link LiveOperation} object.
     */
    public void onUploadCompleted(LiveOperation operation);

    /**
     * Called when the associated upload operation call fails.
     * @param exception The error returned by the REST operation call.
     * @param operation The {@link LiveOperation} object.
     */
    public void onUploadFailed(LiveOperationException exception, LiveOperation operation);

    /**
     * Called arbitrarily during the progress of the upload request.
     * @param totalBytes The total bytes downloaded.
     * @param bytesRemaining The bytes remaining to download.
     * @param operation The {@link LiveOperation} object.
     */
    public void onUploadProgress(int totalBytes, int bytesRemaining, LiveOperation operation);
}

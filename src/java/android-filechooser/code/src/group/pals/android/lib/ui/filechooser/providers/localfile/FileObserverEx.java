/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.providers.localfile;

import group.pals.android.lib.ui.filechooser.BuildConfig;
import group.pals.android.lib.ui.filechooser.utils.Utils;
import android.content.Context;
import android.net.Uri;
import android.os.Build;
import android.os.FileObserver;
import android.os.Handler;
import android.os.HandlerThread;
import android.os.Message;
import android.os.SystemClock;
import android.util.Log;

/**
 * Extended class of {@link FileObserver}, to watch for changes of a directory
 * and notify clients of {@link LocalFileProvider} about those changes.
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public class FileObserverEx extends FileObserver {

    private static final String CLASSNAME = FileObserverEx.class.getName();

    private static final int FILE_OBSERVER_MASK = FileObserver.CREATE
            | FileObserver.DELETE | FileObserver.DELETE_SELF
            | FileObserver.MOVE_SELF | FileObserver.MOVED_FROM
            | FileObserver.MOVED_TO | FileObserver.ATTRIB | FileObserver.MODIFY;

    private static final long MIN_TIME_BETWEEN_EVENTS = 5000;
    private static final int MSG_NOTIFY_CHANGES = 0;
    /**
     * An unknown event, most likely a bug of the system.
     */
    private static final int FILE_OBSERVER_UNKNOWN_EVENT = 32768;

    private final HandlerThread mHandlerThread = new HandlerThread(CLASSNAME);
    private final Handler mHandler;
    private long mLastEventTime = SystemClock.elapsedRealtime();
    private boolean mWatching = false;

    /**
     * Creates new instance.
     * 
     * @param context
     *            the context.
     * @param path
     *            the path to the directory that you want to watch for changes.
     */
    public FileObserverEx(final Context context, final String path,
            final Uri notificationUri) {
        super(path, FILE_OBSERVER_MASK);

        mHandlerThread.start();
        mHandler = new Handler(mHandlerThread.getLooper()) {

            @Override
            public void handleMessage(Message msg) {
                if (Utils.doLog())
                    Log.d(CLASSNAME,
                            String.format(
                                    "mHandler.handleMessage() >> path = '%s' | what = %,d",
                                    path, msg.what));

                switch (msg.what) {
                case MSG_NOTIFY_CHANGES:
                    context.getContentResolver().notifyChange(notificationUri,
                            null);
                    mLastEventTime = SystemClock.elapsedRealtime();
                    break;
                }
            }// handleMessage()
        };
    }// FileObserverEx()

    @Override
    public void onEvent(int event, String path) {
        /*
         * Some bugs of Android...
         */
        if (!mWatching || event == FILE_OBSERVER_UNKNOWN_EVENT || path == null
                || mHandler.hasMessages(MSG_NOTIFY_CHANGES)
                || !mHandlerThread.isAlive() || mHandlerThread.isInterrupted())
            return;

        try {
            if (SystemClock.elapsedRealtime() - mLastEventTime <= MIN_TIME_BETWEEN_EVENTS)
                mHandler.sendEmptyMessageDelayed(
                        MSG_NOTIFY_CHANGES,
                        Math.max(
                                1,
                                MIN_TIME_BETWEEN_EVENTS
                                        - (SystemClock.elapsedRealtime() - mLastEventTime)));
            else
                mHandler.sendEmptyMessage(MSG_NOTIFY_CHANGES);
        } catch (Throwable t) {
            mWatching = false;
            if (Utils.doLog())
                Log.e(CLASSNAME, "onEvent() >> " + t);
        }
    }// onEvent()

    @Override
    public void startWatching() {
        super.startWatching();

        if (Utils.doLog())
            Log.d(CLASSNAME, String.format("startWatching() >> %s", hashCode()));

        mWatching = true;
    }// startWatching()

    @Override
    public void stopWatching() {
        super.stopWatching();

        if (Utils.doLog())
            Log.d(CLASSNAME, String.format("stopWatching() >> %s", hashCode()));

        mWatching = false;

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.ECLAIR)
            HandlerThreadCompat_v5.quit(mHandlerThread);
        mHandlerThread.interrupt();
    }// stopWatching()

}

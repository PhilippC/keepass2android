/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.utils.ui;

import group.pals.android.lib.ui.filechooser.R;
import android.app.ProgressDialog;
import android.content.Context;
import android.content.DialogInterface;
import android.os.AsyncTask;
import android.os.Handler;
import android.util.Log;

/**
 * An {@link AsyncTask}, used to show {@link ProgressDialog} while doing some
 * background tasks.
 * 
 * @author Hai Bison
 * @since v2.1 alpha
 */

public abstract class LoadingDialog<Params, Progress, Result> extends
        AsyncTask<Params, Progress, Result> {

    private static final String CLASSNAME = LoadingDialog.class.getName();

    private final ProgressDialog mDialog;
    /**
     * Default is {@code 500}ms
     */
    private int mDelayTime = 500;
    /**
     * Flag to use along with {@link #mDelayTime}
     */
    private boolean mFinished = false;

    private Throwable mLastException;

    /**
     * Creates new {@link LoadingDialog}
     * 
     * @param context
     *            {@link Context}
     * @param msg
     *            message will be shown in the dialog.
     * @param cancelable
     *            as the name means.
     */
    public LoadingDialog(Context context, String msg, boolean cancelable) {
        mDialog = new ProgressDialog(context);
        mDialog.setMessage(msg);
        mDialog.setIndeterminate(true);
        mDialog.setCancelable(cancelable);
        if (cancelable) {
            mDialog.setCanceledOnTouchOutside(true);
            mDialog.setOnCancelListener(new DialogInterface.OnCancelListener() {

                @Override
                public void onCancel(DialogInterface dialog) {
                    cancel(true);
                }
            });
        }
    }// LoadingDialog()

    /**
     * Creates new {@link LoadingDialog}
     * 
     * @param context
     *            {@link Context}
     * @param msgId
     *            resource id of the message will be shown in the dialog.
     * @param cancelable
     *            as the name means.
     */
    public LoadingDialog(Context context, int msgId, boolean cancelable) {
        this(context, context.getString(msgId), cancelable);
    }// LoadingDialog()

    /**
     * Creates new {@link LoadingDialog} showing "Loading..." (
     * {@link R.string#afc_msg_loading}).
     * 
     * @param context
     *            {@link Context}
     * @param cancelable
     *            as the name means.
     */
    public LoadingDialog(Context context, boolean cancelable) {
        this(context, context.getString(R.string.afc_msg_loading), cancelable);
    }// LoadingDialog()

    /**
     * If you override this method, you must call {@code super.onPreExecute()}
     * at beginning of the method.
     */
    @Override
    protected void onPreExecute() {
        new Handler().postDelayed(new Runnable() {

            @Override
            public void run() {
                if (!mFinished) {
                    try {
                        /*
                         * sometime the activity has been finished before we
                         * show this dialog, it will raise error
                         */
                        mDialog.show();
                    } catch (Throwable t) {
                        // TODO
                        Log.e(CLASSNAME, "onPreExecute() - show dialog: " + t);
                    }
                }
            }
        }, getDelayTime());
    }// onPreExecute()

    /**
     * If you override this method, you must call
     * {@code super.onPostExecute(result)} at beginning of the method.
     */
    @Override
    protected void onPostExecute(Result result) {
        doFinish();
    }// onPostExecute()

    /**
     * If you override this method, you must call {@code super.onCancelled()} at
     * beginning of the method.
     */
    @Override
    protected void onCancelled() {
        doFinish();
        super.onCancelled();
    }// onCancelled()

    private void doFinish() {
        mFinished = true;
        try {
            /*
             * Sometime the activity has been finished before we dismiss this
             * dialog, it will raise error.
             */
            mDialog.dismiss();
        } catch (Throwable t) {
            // TODO
            Log.e(CLASSNAME, "doFinish() - dismiss dialog: " + t);
        }
    }// doFinish()

    /**
     * Gets the delay time before showing the dialog.
     * 
     * @return the delay time
     */
    public int getDelayTime() {
        return mDelayTime;
    }// getDelayTime()

    /**
     * Sets the delay time before showing the dialog.
     * 
     * @param delayTime
     *            the delay time to set
     * @return the instance of this dialog, for chaining multiple calls into a
     *         single statement.
     */
    public LoadingDialog<Params, Progress, Result> setDelayTime(int delayTime) {
        mDelayTime = delayTime >= 0 ? delayTime : 0;
        return this;
    }// setDelayTime()

    /**
     * Sets last exception. This method is useful in case an exception raises
     * inside {@link #doInBackground(Void...)}
     * 
     * @param t
     *            {@link Throwable}
     */
    protected void setLastException(Throwable t) {
        mLastException = t;
    }// setLastException()

    /**
     * Gets last exception.
     * 
     * @return {@link Throwable}
     */
    protected Throwable getLastException() {
        return mLastException;
    }// getLastException()

}

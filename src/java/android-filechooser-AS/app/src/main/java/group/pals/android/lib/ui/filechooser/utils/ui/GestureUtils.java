/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.utils.ui;

import group.pals.android.lib.ui.filechooser.BuildConfig;
import android.graphics.Rect;
import android.util.Log;
import android.view.GestureDetector;
import android.view.MotionEvent;
import android.view.View;
import android.widget.AbsListView;

/**
 * Utilities for user's gesture.
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public class GestureUtils {

    private static final String CLASSNAME = GestureUtils.class.getName();

    /**
     * The fling direction.
     * 
     * @author Hai Bison
     * @since v5.1 beta
     */
    public static enum FlingDirection {
        LEFT_TO_RIGHT, RIGHT_TO_LEFT, UNKNOWN
    }// FlingDirection

    /**
     * Calculates fling direction from two {@link MotionEvent} and their
     * velocity.
     * 
     * @param e1
     *            {@link MotionEvent}
     * @param e2
     *            {@link MotionEvent}
     * @param velocityX
     *            the X velocity.
     * @param velocityY
     *            the Y velocity.
     * @return {@link FlingDirection}
     */
    public static FlingDirection calcFlingDirection(MotionEvent e1,
            MotionEvent e2, float velocityX, float velocityY) {
        if (e1 == null || e2 == null)
            return FlingDirection.UNKNOWN;

        final int _max_y_distance = 19;// 10 is too short :-D
        final int _min_x_distance = 80;
        final int _min_x_velocity = 200;
        if (Math.abs(e1.getY() - e2.getY()) < _max_y_distance
                && Math.abs(e1.getX() - e2.getX()) > _min_x_distance
                && Math.abs(velocityX) > _min_x_velocity) {
            return velocityX <= 0 ? FlingDirection.LEFT_TO_RIGHT
                    : FlingDirection.RIGHT_TO_LEFT;
        }

        return FlingDirection.UNKNOWN;
    }// calcFlingDirection()

    /**
     * Interface for user's gesture.
     * 
     * @author Hai Bison
     * @since v5.1 beta
     */
    public static interface OnGestureListener {

        /**
         * Will be called after the user did a single tap.
         * 
         * @param view
         *            the selected view.
         * @param data
         *            the data.
         * @return {@code true} if you want to handle the event, otherwise
         *         {@code false}.
         */
        boolean onSingleTapConfirmed(View view, Object data);

        /**
         * Will be notified after the user flung the view.
         * 
         * @param view
         *            the selected view.
         * @param data
         *            the data.
         * @param flingDirection
         *            {@link FlingDirection}.
         * @return {@code true} if you handled this event, {@code false} if you
         *         want to let default handler handle it.
         */
        boolean onFling(View view, Object data, FlingDirection flingDirection);
    }// OnGestureListener

    /**
     * An adapter of {@link OnGestureListener}.
     * 
     * @author Hai Bison
     * @since v5.1 beta
     */
    public static class SimpleOnGestureListener implements OnGestureListener {

        @Override
        public boolean onSingleTapConfirmed(View view, Object data) {
            return false;
        }

        @Override
        public boolean onFling(View view, Object data,
                FlingDirection flingDirection) {
            return false;
        }
    }// SimpleOnGestureListener

    /**
     * Adds a gesture listener to {@code listView}.
     * 
     * @param listView
     *            {@link AbsListView}.
     * @param listener
     *            {@link OnGestureListener}.
     */
    public static void setupGestureDetector(final AbsListView listView,
            final OnGestureListener listener) {
        final GestureDetector _gestureDetector = new GestureDetector(
                listView.getContext(),
                new GestureDetector.SimpleOnGestureListener() {

                    private Object getData(float x, float y) {
                        int i = getSubViewId(x, y);
                        if (i >= 0)
                            return listView.getItemAtPosition(listView
                                    .getFirstVisiblePosition() + i);
                        return null;
                    }// getSubView()

                    private View getSubView(float x, float y) {
                        int i = getSubViewId(x, y);
                        if (i >= 0)
                            return listView.getChildAt(i);
                        return null;
                    }// getSubView()

                    private int getSubViewId(float x, float y) {
                        Rect r = new Rect();
                        for (int i = 0; i < listView.getChildCount(); i++) {
                            listView.getChildAt(i).getHitRect(r);
                            if (r.contains((int) x, (int) y)) {
                                if (BuildConfig.DEBUG)
                                    Log.d(CLASSNAME,
                                            String.format(
                                                    "getSubViewId() -- left-top-right-bottom = %d-%d-%d-%d",
                                                    r.left, r.top, r.right,
                                                    r.bottom));
                                return i;
                            }
                        }

                        return -1;
                    }// getSubViewId()

                    @Override
                    public boolean onSingleTapConfirmed(MotionEvent e) {
                        if (BuildConfig.DEBUG)
                            Log.d(CLASSNAME,
                                    String.format(
                                            "onSingleTapConfirmed() -- x = %.2f -- y = %.2f",
                                            e.getX(), e.getY()));
                        return listener == null ? false : listener
                                .onSingleTapConfirmed(
                                        getSubView(e.getX(), e.getY()),
                                        getData(e.getX(), e.getY()));
                    }// onSingleTapConfirmed()

                    @Override
                    public boolean onFling(MotionEvent e1, MotionEvent e2,
                            float velocityX, float velocityY) {
                        if (listener == null || e1 == null || e2 == null)
                            return false;

                        FlingDirection fd = calcFlingDirection(e1, e2,
                                velocityX, velocityY);
                        if (!FlingDirection.UNKNOWN.equals(fd)) {
                            if (listener.onFling(
                                    getSubView(e1.getX(), e1.getY()),
                                    getData(e1.getX(), e1.getY()), fd)) {
                                MotionEvent cancelEvent = MotionEvent
                                        .obtain(e1);
                                cancelEvent
                                        .setAction(MotionEvent.ACTION_CANCEL);
                                listView.onTouchEvent(cancelEvent);
                            }
                        }

                        /*
                         * Always return false to let the default handler draw
                         * the item properly.
                         */
                        return false;
                    }// onFling()
                });// _gestureDetector

        listView.setOnTouchListener(new View.OnTouchListener() {

            @Override
            public boolean onTouch(View v, MotionEvent event) {
                return _gestureDetector.onTouchEvent(event);
            }
        });
    }// setupGestureDetector()

}

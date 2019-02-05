/*
 * Copyright (C) 2008 The Android Open Source Project
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not
 * use this file except in compliance with the License. You may obtain a copy of
 * the License at
 * 
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations under
 * the License.
 */

package keepass2android.softkeyboard;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.inputmethodservice.Keyboard;
import android.inputmethodservice.Keyboard.Key;
import android.os.Handler;
import android.os.Message;
import android.os.SystemClock;
import android.text.TextUtils;
import android.util.AttributeSet;
import android.view.MotionEvent;

import java.util.List;

public class LatinKeyboardView extends LatinKeyboardBaseView {

    static final int KEYCODE_OPTIONS = -100;
    static final int KEYCODE_OPTIONS_LONGPRESS = -101;
    static final int KEYCODE_F1 = -103;
    static final int KEYCODE_NEXT_LANGUAGE = -104;
    static final int KEYCODE_PREV_LANGUAGE = -105;
    static final int KEYCODE_KP2A = -200;
    static final int KEYCODE_KP2A_USER = -201;
    static final int KEYCODE_KP2A_PASSWORD = -202;
    static final int KEYCODE_KP2A_ALPHA = -203;
    static final int KEYCODE_KP2A_SWITCH = -204;
    static final int KEYCODE_KP2A_LOCK = -205;
    static final int KEYCODE_KP2A_NEXTFIELDS = -206;

    private Keyboard mPhoneKeyboard;

    /** Whether we've started dropping move events because we found a big jump */
    private boolean mDroppingEvents;
    /**
     * Whether multi-touch disambiguation needs to be disabled if a real multi-touch event has
     * occured
     */
    private boolean mDisableDisambiguation;
    /** The distance threshold at which we start treating the touch session as a multi-touch */
    private int mJumpThresholdSquare = Integer.MAX_VALUE;
    /** The y coordinate of the last row */
    private int mLastRowY;

    public LatinKeyboardView(Context context, AttributeSet attrs) {
        this(context, attrs, 0);
    }

    public LatinKeyboardView(Context context, AttributeSet attrs, int defStyle) {
        super(context, attrs, defStyle);
    }

    public void setPhoneKeyboard(Keyboard phoneKeyboard) {
        mPhoneKeyboard = phoneKeyboard;
    }

    @Override
    public void setPreviewEnabled(boolean previewEnabled) {
        if (getKeyboard() == mPhoneKeyboard) {
            // Phone keyboard never shows popup preview (except language switch).
            super.setPreviewEnabled(false);
        } else {
            super.setPreviewEnabled(previewEnabled);
        }
    }

    @Override
    public void setKeyboard(Keyboard newKeyboard) {
        final Keyboard oldKeyboard = getKeyboard();
        if (oldKeyboard instanceof LatinKeyboard) {
            // Reset old keyboard state before switching to new keyboard.
            ((LatinKeyboard)oldKeyboard).keyReleased();
        }
        super.setKeyboard(newKeyboard);
        // One-seventh of the keyboard width seems like a reasonable threshold
        mJumpThresholdSquare = newKeyboard.getMinWidth() / 7;
        mJumpThresholdSquare *= mJumpThresholdSquare;
        // Assuming there are 4 rows, this is the coordinate of the last row
        mLastRowY = (newKeyboard.getHeight() * 3) / 4;
        setKeyboardLocal(newKeyboard);
    }

    @Override
    protected boolean onLongPress(Key key) {
        int primaryCode = key.codes[0];
        if (primaryCode == KEYCODE_OPTIONS) {
            return invokeOnKey(KEYCODE_OPTIONS_LONGPRESS);
        } else if (primaryCode == '0' && getKeyboard() == mPhoneKeyboard) {
            // Long pressing on 0 in phone number keypad gives you a '+'.
            return invokeOnKey('+');
        } else {
            return super.onLongPress(key);
        }
    }

    private boolean invokeOnKey(int primaryCode) {
        getOnKeyboardActionListener().onKey(primaryCode, null,
                LatinKeyboardBaseView.NOT_A_TOUCH_COORDINATE,
                LatinKeyboardBaseView.NOT_A_TOUCH_COORDINATE);
        return true;
    }

    @Override
    protected CharSequence adjustCase(CharSequence label) {
        Keyboard keyboard = getKeyboard();
        if (keyboard.isShifted()
                && keyboard instanceof LatinKeyboard
                && ((LatinKeyboard) keyboard).isAlphaKeyboard()
                && !TextUtils.isEmpty(label) && label.length() < 3
                && Character.isLowerCase(label.charAt(0))) {
            return label.toString().toUpperCase(getKeyboardLocale());
        }
        return label;
    }

    public boolean setShiftLocked(boolean shiftLocked) {
        Keyboard keyboard = getKeyboard();
        if (keyboard instanceof LatinKeyboard) {
            ((LatinKeyboard)keyboard).setShiftLocked(shiftLocked);
            invalidateAllKeys();
            return true;
        }
        return false;
    }

    /**
     * This function checks to see if we need to handle any sudden jumps in the pointer location
     * that could be due to a multi-touch being treated as a move by the firmware or hardware.
     * Once a sudden jump is detected, all subsequent move events are discarded
     * until an UP is received.<P>
     * When a sudden jump is detected, an UP event is simulated at the last position and when
     * the sudden moves subside, a DOWN event is simulated for the second key.
     * @param me the motion event
     * @return true if the event was consumed, so that it doesn't continue to be handled by
     * KeyboardView.
     */
    private boolean handleSuddenJump(MotionEvent me) {
        final int action = me.getAction();
        final int x = (int) me.getX();
        final int y = (int) me.getY();
        boolean result = false;

        // Real multi-touch event? Stop looking for sudden jumps
        if (me.getPointerCount() > 1) {
            mDisableDisambiguation = true;
        }
        if (mDisableDisambiguation) {
            // If UP, reset the multi-touch flag
            if (action == MotionEvent.ACTION_UP) mDisableDisambiguation = false;
            return false;
        }

        switch (action) {
        case MotionEvent.ACTION_DOWN:
            // Reset the "session"
            mDroppingEvents = false;
            mDisableDisambiguation = false;
            break;
        case MotionEvent.ACTION_MOVE:
            // Is this a big jump?
            final int distanceSquare = (mLastX - x) * (mLastX - x) + (mLastY - y) * (mLastY - y);
            // Check the distance and also if the move is not entirely within the bottom row
            // If it's only in the bottom row, it might be an intentional slide gesture
            // for language switching
            if (distanceSquare > mJumpThresholdSquare
                    && (mLastY < mLastRowY || y < mLastRowY)) {
                // If we're not yet dropping events, start dropping and send an UP event
                if (!mDroppingEvents) {
                    mDroppingEvents = true;
                    // Send an up event
                    MotionEvent translated = MotionEvent.obtain(me.getEventTime(), me.getEventTime(),
                            MotionEvent.ACTION_UP,
                            mLastX, mLastY, me.getMetaState());
                    super.onTouchEvent(translated);
                    translated.recycle();
                }
                result = true;
            } else if (mDroppingEvents) {
                // If moves are small and we're already dropping events, continue dropping
                result = true;
            }
            break;
        case MotionEvent.ACTION_UP:
            if (mDroppingEvents) {
                // Send a down event first, as we dropped a bunch of sudden jumps and assume that
                // the user is releasing the touch on the second key.
                MotionEvent translated = MotionEvent.obtain(me.getEventTime(), me.getEventTime(),
                        MotionEvent.ACTION_DOWN,
                        x, y, me.getMetaState());
                super.onTouchEvent(translated);
                translated.recycle();
                mDroppingEvents = false;
                // Let the up event get processed as well, result = false
            }
            break;
        }
        // Track the previous coordinate
        mLastX = x;
        mLastY = y;
        return result;
    }

    @Override
    public boolean onTouchEvent(MotionEvent me) {
        LatinKeyboard keyboard = (LatinKeyboard) getKeyboard();
        if (DEBUG_LINE) {
            mLastX = (int) me.getX();
            mLastY = (int) me.getY();
            invalidate();
        }

        // If there was a sudden jump, return without processing the actual motion event.
        if (handleSuddenJump(me))
            return true;

        // Reset any bounding box controls in the keyboard
        if (me.getAction() == MotionEvent.ACTION_DOWN) {
            keyboard.keyReleased();
        }

        if (me.getAction() == MotionEvent.ACTION_UP) {
            int languageDirection = keyboard.getLanguageChangeDirection();
            if (languageDirection != 0) {
                getOnKeyboardActionListener().onKey(
                        languageDirection == 1 ? KEYCODE_NEXT_LANGUAGE : KEYCODE_PREV_LANGUAGE,
                        null, mLastX, mLastY);
                me.setAction(MotionEvent.ACTION_CANCEL);
                keyboard.keyReleased();
                return super.onTouchEvent(me);
            }
        }

        return super.onTouchEvent(me);
    }

    /****************************  INSTRUMENTATION  *******************************/

    static final boolean DEBUG_AUTO_PLAY = false;
    static final boolean DEBUG_LINE = false;
    private static final int MSG_TOUCH_DOWN = 1;
    private static final int MSG_TOUCH_UP = 2;

    Handler mHandler2;

    private String mStringToPlay;
    private int mStringIndex;
    private boolean mDownDelivered;
    private Key[] mAsciiKeys = new Key[256];
    private boolean mPlaying;
    private int mLastX;
    private int mLastY;
    private Paint mPaint;

    private void setKeyboardLocal(Keyboard k) {
        if (DEBUG_AUTO_PLAY) {
            findKeys();
            if (mHandler2 == null) {
                mHandler2 = new Handler() {
                    @Override
                    public void handleMessage(Message msg) {
                        removeMessages(MSG_TOUCH_DOWN);
                        removeMessages(MSG_TOUCH_UP);
                        if (mPlaying == false) return;

                        switch (msg.what) {
                            case MSG_TOUCH_DOWN:
                                if (mStringIndex >= mStringToPlay.length()) {
                                    mPlaying = false;
                                    return;
                                }
                                char c = mStringToPlay.charAt(mStringIndex);
                                while (c > 255 || mAsciiKeys[c] == null) {
                                    mStringIndex++;
                                    if (mStringIndex >= mStringToPlay.length()) {
                                        mPlaying = false;
                                        return;
                                    }
                                    c = mStringToPlay.charAt(mStringIndex);
                                }
                                int x = mAsciiKeys[c].x + 10;
                                int y = mAsciiKeys[c].y + 26;
                                MotionEvent me = MotionEvent.obtain(SystemClock.uptimeMillis(),
                                        SystemClock.uptimeMillis(),
                                        MotionEvent.ACTION_DOWN, x, y, 0);
                                LatinKeyboardView.this.dispatchTouchEvent(me);
                                me.recycle();
                                sendEmptyMessageDelayed(MSG_TOUCH_UP, 500); // Deliver up in 500ms if nothing else
                                // happens
                                mDownDelivered = true;
                                break;
                            case MSG_TOUCH_UP:
                                char cUp = mStringToPlay.charAt(mStringIndex);
                                int x2 = mAsciiKeys[cUp].x + 10;
                                int y2 = mAsciiKeys[cUp].y + 26;
                                mStringIndex++;

                                MotionEvent me2 = MotionEvent.obtain(SystemClock.uptimeMillis(),
                                        SystemClock.uptimeMillis(),
                                        MotionEvent.ACTION_UP, x2, y2, 0);
                                LatinKeyboardView.this.dispatchTouchEvent(me2);
                                me2.recycle();
                                sendEmptyMessageDelayed(MSG_TOUCH_DOWN, 500); // Deliver up in 500ms if nothing else
                                // happens
                                mDownDelivered = false;
                                break;
                        }
                    }
                };

            }
        }
    }

    private void findKeys() {
        List<Key> keys = getKeyboard().getKeys();
        // Get the keys on this keyboard
        for (int i = 0; i < keys.size(); i++) {
            int code = keys.get(i).codes[0];
            if (code >= 0 && code <= 255) {
                mAsciiKeys[code] = keys.get(i);
            }
        }
    }

    public void startPlaying(String s) {
        if (DEBUG_AUTO_PLAY) {
            if (s == null) return;
            mStringToPlay = s.toLowerCase();
            mPlaying = true;
            mDownDelivered = false;
            mStringIndex = 0;
            mHandler2.sendEmptyMessageDelayed(MSG_TOUCH_DOWN, 10);
        }
    }

    @Override
    public void draw(Canvas c) {
        LatinIMEUtil.GCUtils.getInstance().reset();
        boolean tryGC = true;
        for (int i = 0; i < LatinIMEUtil.GCUtils.GC_TRY_LOOP_MAX && tryGC; ++i) {
            try {
                super.draw(c);
                tryGC = false;
            } catch (OutOfMemoryError e) {
                tryGC = LatinIMEUtil.GCUtils.getInstance().tryGCOrWait("LatinKeyboardView", e);
            }
        }
        if (DEBUG_AUTO_PLAY) {
            if (mPlaying) {
                mHandler2.removeMessages(MSG_TOUCH_DOWN);
                mHandler2.removeMessages(MSG_TOUCH_UP);
                if (mDownDelivered) {
                    mHandler2.sendEmptyMessageDelayed(MSG_TOUCH_UP, 20);
                } else {
                    mHandler2.sendEmptyMessageDelayed(MSG_TOUCH_DOWN, 20);
                }
            }
        }
        if (DEBUG_LINE) {
            if (mPaint == null) {
                mPaint = new Paint();
                mPaint.setColor(0x80FFFFFF);
                mPaint.setAntiAlias(false);
            }
            c.drawLine(mLastX, 0, mLastX, getHeight(), mPaint);
            c.drawLine(0, mLastY, getWidth(), mLastY, mPaint);
        }
    }
}

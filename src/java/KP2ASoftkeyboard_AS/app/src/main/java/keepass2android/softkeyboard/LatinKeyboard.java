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

import android.annotation.SuppressLint;
import android.content.Context;
import android.content.res.Resources;
import android.content.res.TypedArray;
import android.content.res.XmlResourceParser;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.ColorFilter;
import android.graphics.Paint;
import android.graphics.Paint.Align;
import android.graphics.PixelFormat;
import android.graphics.PorterDuff;
import android.graphics.Rect;
import android.graphics.drawable.BitmapDrawable;
import android.graphics.drawable.Drawable;
import android.inputmethodservice.Keyboard;
import android.text.TextPaint;
import android.util.Log;
import android.view.ViewConfiguration;
import android.view.inputmethod.EditorInfo;

import java.util.List;
import java.util.Locale;

public class LatinKeyboard extends Keyboard {

    private static final boolean DEBUG_PREFERRED_LETTER = false;
    private static final String TAG = "LatinKeyboard";
    private static final int OPACITY_FULLY_OPAQUE = 255;
    private static final int SPACE_LED_LENGTH_PERCENT = 80;

    private Drawable mShiftLockIcon;
    private Drawable mShiftLockPreviewIcon;
    private Drawable mOldShiftIcon;
    private Drawable mSpaceIcon;
    private Drawable mSpaceAutoCompletionIndicator;
    private Drawable mSpacePreviewIcon;
    private Drawable mMicIcon;
    private Drawable mMicPreviewIcon;
    private Drawable m123MicIcon;
    private Drawable m123MicPreviewIcon;
    private final Drawable mButtonArrowLeftIcon;
    private final Drawable mButtonArrowRightIcon;
    private Key mShiftKey;
    private Key mEnterKey;
    private Key mF1Key;
    private final Drawable mHintIcon;
    private Key mSpaceKey;
    private Key m123Key;
    private final int NUMBER_HINT_COUNT = 10;
    private Key[] mNumberHintKeys;
    private Drawable[] mNumberHintIcons = new Drawable[NUMBER_HINT_COUNT];
    private final int[] mSpaceKeyIndexArray;
    private int mSpaceDragStartX;
    private int mSpaceDragLastDiff;
    private Locale mLocale;
    private LanguageSwitcher mLanguageSwitcher;
    private final Resources mRes;
    private final Context mContext;
    private int mMode;
    // Whether this keyboard has voice icon on it
    private boolean mHasVoiceButton;
    // Whether voice icon is enabled at all
    
    private final boolean mIsAlphaKeyboard;
    private CharSequence m123Label;
    private boolean mCurrentlyInSpace;
    private SlidingLocaleDrawable mSlidingLocaleIcon;
    private int[] mPrefLetterFrequencies;
    private int mPrefLetter;
    private int mPrefLetterX;
    private int mPrefLetterY;
    private int mPrefDistance;

    // TODO: generalize for any keyboardId
    private boolean mIsBlackSym;

    // TODO: remove this attribute when either Keyboard.mDefaultVerticalGap or Key.parent becomes
    // non-private.
    private final int mVerticalGap;

    private static final int SHIFT_OFF = 0;
    private static final int SHIFT_ON = 1;
    private static final int SHIFT_LOCKED = 2;
    
    private int mShiftState = SHIFT_OFF;

    private static final float SPACEBAR_DRAG_THRESHOLD = 0.8f;
    private static final float OVERLAP_PERCENTAGE_LOW_PROB = 0.70f;
    private static final float OVERLAP_PERCENTAGE_HIGH_PROB = 0.85f;
    // Minimum width of space key preview (proportional to keyboard width)
    private static final float SPACEBAR_POPUP_MIN_RATIO = 0.4f;
    // Height in space key the language name will be drawn. (proportional to space key height)
    private static final float SPACEBAR_LANGUAGE_BASELINE = 0.6f;
    // If the full language name needs to be smaller than this value to be drawn on space key,
    // its short language name will be used instead.
    private static final float MINIMUM_SCALE_OF_LANGUAGE_NAME = 0.8f;

    private static int sSpacebarVerticalCorrection;

    public LatinKeyboard(Context context, int xmlLayoutResId) {
        this(context, xmlLayoutResId, 0);
    }

    public LatinKeyboard(Context context, int xmlLayoutResId, int mode) {
        super(context, xmlLayoutResId, mode);
        final Resources res = context.getResources();
        mContext = context;
        mMode = mode;
        mRes = res;
        mShiftLockIcon = res.getDrawable(R.drawable.sym_keyboard_shift_locked);
        mShiftLockPreviewIcon = res.getDrawable(R.drawable.sym_keyboard_feedback_shift_locked);
        setDefaultBounds(mShiftLockPreviewIcon);
        mSpaceIcon = res.getDrawable(R.drawable.sym_keyboard_space);
        mSpaceAutoCompletionIndicator = res.getDrawable(R.drawable.sym_keyboard_space_led);
        mSpacePreviewIcon = res.getDrawable(R.drawable.sym_keyboard_feedback_space);
        mMicIcon = res.getDrawable(R.drawable.sym_keyboard_mic);
        mMicPreviewIcon = res.getDrawable(R.drawable.sym_keyboard_feedback_mic);
        setDefaultBounds(mMicPreviewIcon);
        mButtonArrowLeftIcon = res.getDrawable(R.drawable.sym_keyboard_language_arrows_left);
        mButtonArrowRightIcon = res.getDrawable(R.drawable.sym_keyboard_language_arrows_right);
        m123MicIcon = res.getDrawable(R.drawable.sym_keyboard_123_mic);
        m123MicPreviewIcon = res.getDrawable(R.drawable.sym_keyboard_feedback_123_mic);
        mHintIcon = res.getDrawable(R.drawable.hint_popup);
        setDefaultBounds(m123MicPreviewIcon);
        sSpacebarVerticalCorrection = res.getDimensionPixelOffset(
                R.dimen.spacebar_vertical_correction);
        mIsAlphaKeyboard = xmlLayoutResId == R.xml.kbd_qwerty
                || xmlLayoutResId == R.xml.kbd_qwerty_black;
        // The index of space key is available only after Keyboard constructor has finished.
        mSpaceKeyIndexArray = new int[] { indexOf(KP2AKeyboard.KEYCODE_SPACE) };
        initializeNumberHintResources(context);
        // TODO remove this initialization after cleanup
        mVerticalGap = super.getVerticalGap();
    }

    private void initializeNumberHintResources(Context context) {
        final Resources res = context.getResources();
        mNumberHintIcons[0] = res.getDrawable(R.drawable.keyboard_hint_0);
        mNumberHintIcons[1] = res.getDrawable(R.drawable.keyboard_hint_1);
        mNumberHintIcons[2] = res.getDrawable(R.drawable.keyboard_hint_2);
        mNumberHintIcons[3] = res.getDrawable(R.drawable.keyboard_hint_3);
        mNumberHintIcons[4] = res.getDrawable(R.drawable.keyboard_hint_4);
        mNumberHintIcons[5] = res.getDrawable(R.drawable.keyboard_hint_5);
        mNumberHintIcons[6] = res.getDrawable(R.drawable.keyboard_hint_6);
        mNumberHintIcons[7] = res.getDrawable(R.drawable.keyboard_hint_7);
        mNumberHintIcons[8] = res.getDrawable(R.drawable.keyboard_hint_8);
        mNumberHintIcons[9] = res.getDrawable(R.drawable.keyboard_hint_9);
    }

    @Override
    protected Key createKeyFromXml(Resources res, Row parent, int x, int y, 
            XmlResourceParser parser) {
        Key key = new LatinKey(res, parent, x, y, parser);
        switch (key.codes[0]) {
        case KP2AKeyboard.KEYCODE_ENTER:
            mEnterKey = key;
            break;
        case LatinKeyboardView.KEYCODE_F1:
            mF1Key = key;
            break;
        case KP2AKeyboard.KEYCODE_SPACE:
            mSpaceKey = key;
            break;
        case KEYCODE_MODE_CHANGE:
            m123Key = key;
            m123Label = key.label;
            break;
        }

        // For number hints on the upper-right corner of key
        if (mNumberHintKeys == null) {
            // NOTE: This protected method is being called from the base class constructor before
            // mNumberHintKeys gets initialized.
            mNumberHintKeys = new Key[NUMBER_HINT_COUNT];
        }
        int hintNumber = -1;
        if (LatinKeyboardBaseView.isNumberAtLeftmostPopupChar(key)) {
            hintNumber = key.popupCharacters.charAt(0) - '0';
        } else if (LatinKeyboardBaseView.isNumberAtRightmostPopupChar(key)) {
            hintNumber = key.popupCharacters.charAt(key.popupCharacters.length() - 1) - '0';
        }
        if (hintNumber >= 0 && hintNumber <= 9) {
            mNumberHintKeys[hintNumber] = key;
        }

        return key;
    }

    void setImeOptions(Resources res, int mode, int options) {
        mMode = mode;
        // TODO should clean up this method
        if (mEnterKey != null) {
            // Reset some of the rarely used attributes.
            mEnterKey.popupCharacters = null;
            mEnterKey.popupResId = 0;
            mEnterKey.text = null;
            switch (options&(EditorInfo.IME_MASK_ACTION|EditorInfo.IME_FLAG_NO_ENTER_ACTION)) {
                case EditorInfo.IME_ACTION_GO:
                    mEnterKey.iconPreview = null;
                    mEnterKey.icon = null;
                    mEnterKey.label = res.getText(R.string.label_go_key);
                    break;
                case EditorInfo.IME_ACTION_NEXT:
                    mEnterKey.iconPreview = null;
                    mEnterKey.icon = null;
                    mEnterKey.label = res.getText(R.string.label_next_key);
                    break;
                case EditorInfo.IME_ACTION_DONE:
                    mEnterKey.iconPreview = null;
                    mEnterKey.icon = null;
                    mEnterKey.label = res.getText(R.string.label_done_key);
                    break;
                case EditorInfo.IME_ACTION_SEARCH:
                    mEnterKey.iconPreview = res.getDrawable(
                            R.drawable.sym_keyboard_feedback_search);
                    mEnterKey.icon = res.getDrawable(mIsBlackSym ?
                            R.drawable.sym_bkeyboard_search : R.drawable.sym_keyboard_search);
                    mEnterKey.label = null;
                    break;
                case EditorInfo.IME_ACTION_SEND:
                    mEnterKey.iconPreview = null;
                    mEnterKey.icon = null;
                    mEnterKey.label = res.getText(R.string.label_send_key);
                    break;
                default:
                    if (mode == KeyboardSwitcher.MODE_IM) {
                        mEnterKey.icon = mHintIcon;
                        mEnterKey.iconPreview = null;
                        mEnterKey.label = ":-)";
                        mEnterKey.text = ":-) ";
                        mEnterKey.popupResId = R.xml.popup_smileys;
                    } else {
                        mEnterKey.iconPreview = res.getDrawable(
                                R.drawable.sym_keyboard_feedback_return);
                        mEnterKey.icon = res.getDrawable(mIsBlackSym ?
                                R.drawable.sym_bkeyboard_return : R.drawable.sym_keyboard_return);
                        mEnterKey.label = null;
                    }
                    break;
            }
            // Set the initial size of the preview icon
            if (mEnterKey.iconPreview != null) {
                setDefaultBounds(mEnterKey.iconPreview);
            }
        }
    }
    
    void enableShiftLock() {
        int index = getShiftKeyIndex();
        if (index >= 0) {
            mShiftKey = getKeys().get(index);
            if (mShiftKey instanceof LatinKey) {
                ((LatinKey)mShiftKey).enableShiftLock();
            }
            mOldShiftIcon = mShiftKey.icon;
        }
    }

    void setShiftLocked(boolean shiftLocked) {
        if (mShiftKey != null) {
            if (shiftLocked) {
                mShiftKey.on = true;
                mShiftKey.icon = mShiftLockIcon;
                mShiftState = SHIFT_LOCKED;
            } else {
                mShiftKey.on = false;
                mShiftKey.icon = mShiftLockIcon;
                mShiftState = SHIFT_ON;
            }
        }
    }

    boolean isShiftLocked() {
        return mShiftState == SHIFT_LOCKED;
    }
    
    @Override
    public boolean setShifted(boolean shiftState) {
        boolean shiftChanged = false;
        if (mShiftKey != null) {
            if (shiftState == false) {
                shiftChanged = mShiftState != SHIFT_OFF;
                mShiftState = SHIFT_OFF;
                mShiftKey.on = false;
                mShiftKey.icon = mOldShiftIcon;
            } else {
                if (mShiftState == SHIFT_OFF) {
                    shiftChanged = mShiftState == SHIFT_OFF;
                    mShiftState = SHIFT_ON;
                    mShiftKey.icon = mShiftLockIcon;
                }
            }
        } else {
            return super.setShifted(shiftState);
        }
        return shiftChanged;
    }

    @Override
    public boolean isShifted() {
        if (mShiftKey != null) {
            return mShiftState != SHIFT_OFF;
        } else {
            return super.isShifted();
        }
    }

    /* package */ boolean isAlphaKeyboard() {
        return mIsAlphaKeyboard;
    }

    public void setColorOfSymbolIcons(boolean isAutoCompletion, boolean isBlack) {
        mIsBlackSym = isBlack;
        if (isBlack) {
            mShiftLockIcon = mRes.getDrawable(R.drawable.sym_bkeyboard_shift_locked);
            mSpaceIcon = mRes.getDrawable(R.drawable.sym_bkeyboard_space);
            mMicIcon = mRes.getDrawable(R.drawable.sym_bkeyboard_mic);
            m123MicIcon = mRes.getDrawable(R.drawable.sym_bkeyboard_123_mic);
        } else {
            mShiftLockIcon = mRes.getDrawable(R.drawable.sym_keyboard_shift_locked);
            mSpaceIcon = mRes.getDrawable(R.drawable.sym_keyboard_space);
            mMicIcon = mRes.getDrawable(R.drawable.sym_keyboard_mic);
            m123MicIcon = mRes.getDrawable(R.drawable.sym_keyboard_123_mic);
        }
        updateDynamicKeys();
        if (mSpaceKey != null) {
            updateSpaceBarForLocale(isAutoCompletion, isBlack);
        }
        updateNumberHintKeys();
    }

    private void setDefaultBounds(Drawable drawable) {
        drawable.setBounds(0, 0, drawable.getIntrinsicWidth(), drawable.getIntrinsicHeight());
    }

    
    private void updateDynamicKeys() {
        update123Key();
        updateF1Key();
    }

    private void update123Key() {
        // Update KEYCODE_MODE_CHANGE key only on alphabet mode, not on symbol mode.
        if (m123Key != null && mIsAlphaKeyboard) {
            
            m123Key.icon = null;
            m123Key.iconPreview = null;
            m123Key.label = m123Label;
        
        }
    }

    private void updateF1Key() {
        // Update KEYCODE_F1 key. Please note that some keyboard layouts have no F1 key.
        if (mF1Key == null)
            return;

        if (mIsAlphaKeyboard) {
            if (mMode == KeyboardSwitcher.MODE_URL) {
                setNonMicF1Key(mF1Key, "/", R.xml.popup_slash);
            } else if (mMode == KeyboardSwitcher.MODE_EMAIL) {
                setNonMicF1Key(mF1Key, "@", R.xml.popup_at);
            } else {
                setNonMicF1Key(mF1Key, ",", R.xml.popup_comma);

            }
        } else {  // Symbols keyboard
            setNonMicF1Key(mF1Key, ",", R.xml.popup_comma);
        }
    }
    private void setNonMicF1Key(Key key, String label, int popupResId) {
        key.label = label;
        key.codes = new int[] { label.charAt(0) };
        key.popupResId = popupResId;
        key.icon = mHintIcon;
        key.iconPreview = null;
    }

    public boolean isF1Key(Key key) {
        return key == mF1Key;
    }

    public static boolean hasPuncOrSmileysPopup(Key key) {
        return key.popupResId == R.xml.popup_punctuation || key.popupResId == R.xml.popup_smileys;
    }

    /**
     * @return a key which should be invalidated.
     */
    public Key onAutoCompletionStateChanged(boolean isAutoCompletion) {
        updateSpaceBarForLocale(isAutoCompletion, mIsBlackSym);
        return mSpaceKey;
    }

    private void updateNumberHintKeys() {
        for (int i = 0; i < mNumberHintKeys.length; ++i) {
            if (mNumberHintKeys[i] != null) {
                mNumberHintKeys[i].icon = mNumberHintIcons[i];
            }
        }
    }

    public boolean isLanguageSwitchEnabled() {
        return mLocale != null;
    }

    private void updateSpaceBarForLocale(boolean isAutoCompletion, boolean isBlack) {
        // If application locales are explicitly selected.
    	try
    	{
    		if (mLocale != null) {
                mSpaceKey.icon = new BitmapDrawable(mRes,
                        drawSpaceBar(OPACITY_FULLY_OPAQUE, isAutoCompletion, isBlack));
            } else {
                // sym_keyboard_space_led can be shared with Black and White symbol themes.
                if (isAutoCompletion) {
                    mSpaceKey.icon = new BitmapDrawable(mRes,
                            drawSpaceBar(OPACITY_FULLY_OPAQUE, isAutoCompletion, isBlack));
                } else {
                    mSpaceKey.icon = isBlack ? mRes.getDrawable(R.drawable.sym_bkeyboard_space)
                            : mRes.getDrawable(R.drawable.sym_keyboard_space);
                }
            }	
    	}
    	catch (NullPointerException e)
    	{
    		//this exception was reported through Google Play. I need further info to resolve.
    		android.util.Log.e("KP2A", "Please report this error to crocoapps@gmail.com");
    		android.util.Log.e("KP2A", e.toString());
    		android.util.Log.e("KP2A", ((mSpaceKey==null) ? "1" : "0") 
    			+ ((mRes==null) ? "1" : "0"));
    	}
        
    }

    // Compute width of text with specified text size using paint.
    private static int getTextWidth(Paint paint, String text, float textSize, Rect bounds) {
        paint.setTextSize(textSize);
        paint.getTextBounds(text, 0, text.length(), bounds);
        return bounds.width();
    }

    // Overlay two images: mainIcon and hintIcon.
    private Bitmap drawSynthesizedSettingsHintImage(
            int width, int height, Drawable mainIcon, Drawable hintIcon) {
        if (mainIcon == null || hintIcon == null)
            return null;
        Rect hintIconPadding = new Rect(0, 0, 0, 0);
        hintIcon.getPadding(hintIconPadding);
        final Bitmap buffer = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888);
        final Canvas canvas = new Canvas(buffer);
        canvas.drawColor(mRes.getColor(R.color.latinkeyboard_transparent), PorterDuff.Mode.CLEAR);

        // Draw main icon at the center of the key visual
        // Assuming the hintIcon shares the same padding with the key's background drawable
        final int drawableX = (width + hintIconPadding.left - hintIconPadding.right
                - mainIcon.getIntrinsicWidth()) / 2;
        final int drawableY = (height + hintIconPadding.top - hintIconPadding.bottom
                - mainIcon.getIntrinsicHeight()) / 2;
        setDefaultBounds(mainIcon);
        canvas.translate(drawableX, drawableY);
        mainIcon.draw(canvas);
        canvas.translate(-drawableX, -drawableY);

        // Draw hint icon fully in the key
        hintIcon.setBounds(0, 0, width, height);
        hintIcon.draw(canvas);
        return buffer;
    }

    // Layout local language name and left and right arrow on space bar.
    private static String layoutSpaceBar(Paint paint, Locale locale, Drawable lArrow,
            Drawable rArrow, int width, int height, float origTextSize,
            boolean allowVariableTextSize) {
        final float arrowWidth = lArrow.getIntrinsicWidth();
        final float arrowHeight = lArrow.getIntrinsicHeight();
        final float maxTextWidth = width - (arrowWidth + arrowWidth);
        final Rect bounds = new Rect();

        // Estimate appropriate language name text size to fit in maxTextWidth.
        String language = LanguageSwitcher.toTitleCase(locale.getDisplayLanguage(locale), locale);
        int textWidth = getTextWidth(paint, language, origTextSize, bounds);
        // Assuming text width and text size are proportional to each other.
        float textSize = origTextSize * Math.min(maxTextWidth / textWidth, 1.0f);

        final boolean useShortName;
        if (allowVariableTextSize) {
            textWidth = getTextWidth(paint, language, textSize, bounds);
            // If text size goes too small or text does not fit, use short name
            useShortName = textSize / origTextSize < MINIMUM_SCALE_OF_LANGUAGE_NAME
                    || textWidth > maxTextWidth;
        } else {
            useShortName = textWidth > maxTextWidth;
            textSize = origTextSize;
        }
        if (useShortName) {
            language = LanguageSwitcher.toTitleCase(locale.getLanguage(), locale);
            textWidth = getTextWidth(paint, language, origTextSize, bounds);
            textSize = origTextSize * Math.min(maxTextWidth / textWidth, 1.0f);
        }
        paint.setTextSize(textSize);

        // Place left and right arrow just before and after language text.
        final float baseline = height * SPACEBAR_LANGUAGE_BASELINE;
        final int top = (int)(baseline - arrowHeight);
        final float remains = (width - textWidth) / 2;
        lArrow.setBounds((int)(remains - arrowWidth), top, (int)remains, (int)baseline);
        rArrow.setBounds((int)(remains + textWidth), top, (int)(remains + textWidth + arrowWidth),
                (int)baseline);

        return language;
    }

    private Bitmap drawSpaceBar(int opacity, boolean isAutoCompletion, boolean isBlack) {
        final int width = mSpaceKey.width;
        final int height = mSpaceIcon.getIntrinsicHeight();
        final Bitmap buffer = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888);
        final Canvas canvas = new Canvas(buffer);
        canvas.drawColor(mRes.getColor(R.color.latinkeyboard_transparent), PorterDuff.Mode.CLEAR);

        // If application locales are explicitly selected.
        if (mLocale != null) {
            final Paint paint = new Paint();
            paint.setAlpha(opacity);
            paint.setAntiAlias(true);
            paint.setTextAlign(Align.CENTER);

            final boolean allowVariableTextSize = true;
            final String language = layoutSpaceBar(paint, mLanguageSwitcher.getInputLocale(),
                    mButtonArrowLeftIcon, mButtonArrowRightIcon, width, height,
                    getTextSizeFromTheme(android.R.style.TextAppearance_Small, 14),
                    allowVariableTextSize);

            // Draw language text with shadow
            final int shadowColor = mRes.getColor(isBlack
                    ? R.color.latinkeyboard_bar_language_shadow_black
                    : R.color.latinkeyboard_bar_language_shadow_white);
            final float baseline = height * SPACEBAR_LANGUAGE_BASELINE;
            final float descent = paint.descent();
            paint.setColor(shadowColor);
            canvas.drawText(language, width / 2, baseline - descent - 1, paint);
            paint.setColor(mRes.getColor(R.color.latinkeyboard_bar_language_text));
            canvas.drawText(language, width / 2, baseline - descent, paint);

            // Put arrows that are already layed out on either side of the text
            if (mLanguageSwitcher.getLocaleCount() > 1) {
                mButtonArrowLeftIcon.draw(canvas);
                mButtonArrowRightIcon.draw(canvas);
            }
        }

        // Draw the spacebar icon at the bottom
        if (isAutoCompletion) {
            final int iconWidth = width * SPACE_LED_LENGTH_PERCENT / 100;
            final int iconHeight = mSpaceAutoCompletionIndicator.getIntrinsicHeight();
            int x = (width - iconWidth) / 2;
            int y = height - iconHeight;
            mSpaceAutoCompletionIndicator.setBounds(x, y, x + iconWidth, y + iconHeight);
            mSpaceAutoCompletionIndicator.draw(canvas);
        } else {
            final int iconWidth = mSpaceIcon.getIntrinsicWidth();
            final int iconHeight = mSpaceIcon.getIntrinsicHeight();
            int x = (width - iconWidth) / 2;
            int y = height - iconHeight;
            mSpaceIcon.setBounds(x, y, x + iconWidth, y + iconHeight);
            mSpaceIcon.draw(canvas);
        }
        return buffer;
    }

    private void updateLocaleDrag(int diff) {
        if (mSlidingLocaleIcon == null) {
            final int width = Math.max(mSpaceKey.width,
                    (int)(getMinWidth() * SPACEBAR_POPUP_MIN_RATIO));
            final int height = mSpacePreviewIcon.getIntrinsicHeight();
            mSlidingLocaleIcon = new SlidingLocaleDrawable(mSpacePreviewIcon, width, height);
            mSlidingLocaleIcon.setBounds(0, 0, width, height);
            mSpaceKey.iconPreview = mSlidingLocaleIcon;
        }
        mSlidingLocaleIcon.setDiff(diff);
        if (Math.abs(diff) == Integer.MAX_VALUE) {
            mSpaceKey.iconPreview = mSpacePreviewIcon;
        } else {
            mSpaceKey.iconPreview = mSlidingLocaleIcon;
        }
        mSpaceKey.iconPreview.invalidateSelf();
    }

    public int getLanguageChangeDirection() {
        if (mSpaceKey == null || mLanguageSwitcher.getLocaleCount() < 2
                || Math.abs(mSpaceDragLastDiff) < mSpaceKey.width * SPACEBAR_DRAG_THRESHOLD ) {
            return 0; // No change
        }
        return mSpaceDragLastDiff > 0 ? 1 : -1;
    }

    public void setLanguageSwitcher(LanguageSwitcher switcher, boolean isAutoCompletion,
            boolean isBlackSym) {
        mLanguageSwitcher = switcher;
        Locale locale = mLanguageSwitcher.getLocaleCount() > 0
                ? mLanguageSwitcher.getInputLocale()
                : null;
        // If the language count is 1 and is the same as the system language, don't show it.
        if (locale != null
                && mLanguageSwitcher.getLocaleCount() == 1
                && mLanguageSwitcher.getSystemLocale().getLanguage()
                   .equalsIgnoreCase(locale.getLanguage())) {
            locale = null;
        }
        mLocale = locale;
        setColorOfSymbolIcons(isAutoCompletion, isBlackSym);
    }

    public Locale getInputLocale() {
        return (mLocale != null) ? mLocale : mLanguageSwitcher.getSystemLocale();
    }

    boolean isCurrentlyInSpace() {
        return mCurrentlyInSpace;
    }

    void setPreferredLetters(int[] frequencies) {
        mPrefLetterFrequencies = frequencies;
        mPrefLetter = 0;
    }

    void keyReleased() {
        mCurrentlyInSpace = false;
        mSpaceDragLastDiff = 0;
        mPrefLetter = 0;
        mPrefLetterX = 0;
        mPrefLetterY = 0;
        mPrefDistance = Integer.MAX_VALUE;
        if (mSpaceKey != null) {
            updateLocaleDrag(Integer.MAX_VALUE);
        }
    }

    /**
     * Does the magic of locking the touch gesture into the spacebar when
     * switching input languages.
     */
    boolean isInside(LatinKey key, int x, int y) {
        final int code = key.codes[0];
        if (code == KEYCODE_SHIFT ||
                code == KEYCODE_DELETE) {
            y -= key.height / 10;
            if (code == KEYCODE_SHIFT) x += key.width / 6;
            if (code == KEYCODE_DELETE) x -= key.width / 6;
        } else if (code == KP2AKeyboard.KEYCODE_SPACE) {
            y += LatinKeyboard.sSpacebarVerticalCorrection;
            if (mLanguageSwitcher.getLocaleCount() > 1) {
                if (mCurrentlyInSpace) {
                    int diff = x - mSpaceDragStartX;
                    if (Math.abs(diff - mSpaceDragLastDiff) > 0) {
                        updateLocaleDrag(diff);
                    }
                    mSpaceDragLastDiff = diff;
                    return true;
                } else {
                    boolean insideSpace = key.isInsideSuper(x, y);
                    if (insideSpace) {
                        mCurrentlyInSpace = true;
                        mSpaceDragStartX = x;
                        updateLocaleDrag(0);
                    }
                    return insideSpace;
                }
            }
        } else if (mPrefLetterFrequencies != null) {
            // New coordinate? Reset
            if (mPrefLetterX != x || mPrefLetterY != y) {
                mPrefLetter = 0;
                mPrefDistance = Integer.MAX_VALUE;
            }
            // Handle preferred next letter
            final int[] pref = mPrefLetterFrequencies;
            if (mPrefLetter > 0) {
                if (DEBUG_PREFERRED_LETTER) {
                    if (mPrefLetter == code && !key.isInsideSuper(x, y)) {
                        Log.d(TAG, "CORRECTED !!!!!!");
                    }
                }
                return mPrefLetter == code;
            } else {
                final boolean inside = key.isInsideSuper(x, y);
                int[] nearby = getNearestKeys(x, y);
                List<Key> nearbyKeys = getKeys();
                if (inside) {
                    // If it's a preferred letter
                    if (inPrefList(code, pref)) {
                        // Check if its frequency is much lower than a nearby key
                        mPrefLetter = code;
                        mPrefLetterX = x;
                        mPrefLetterY = y;
                        for (int i = 0; i < nearby.length; i++) {
                            Key k = nearbyKeys.get(nearby[i]);
                            if (k != key && inPrefList(k.codes[0], pref)) {
                                final int dist = distanceFrom(k, x, y);
                                if (dist < (int) (k.width * OVERLAP_PERCENTAGE_LOW_PROB) &&
                                        (pref[k.codes[0]] > pref[mPrefLetter] * 3))  {
                                    mPrefLetter = k.codes[0];
                                    mPrefDistance = dist;
                                    if (DEBUG_PREFERRED_LETTER) {
                                        Log.d(TAG, "CORRECTED ALTHOUGH PREFERRED !!!!!!");
                                    }
                                    break;
                                }
                            }
                        }

                        return mPrefLetter == code;
                    }
                }

                // Get the surrounding keys and intersect with the preferred list
                // For all in the intersection
                //   if distance from touch point is within a reasonable distance
                //       make this the pref letter
                // If no pref letter
                //   return inside;
                // else return thiskey == prefletter;

                for (int i = 0; i < nearby.length; i++) {
                    Key k = nearbyKeys.get(nearby[i]);
                    if (inPrefList(k.codes[0], pref)) {
                        final int dist = distanceFrom(k, x, y);
                        if (dist < (int) (k.width * OVERLAP_PERCENTAGE_HIGH_PROB)
                                && dist < mPrefDistance)  {
                            mPrefLetter = k.codes[0];
                            mPrefLetterX = x;
                            mPrefLetterY = y;
                            mPrefDistance = dist;
                        }
                    }
                }
                // Didn't find any
                if (mPrefLetter == 0) {
                    return inside;
                } else {
                    return mPrefLetter == code;
                }
            }
        }

        // Lock into the spacebar
        if (mCurrentlyInSpace) return false;

        return key.isInsideSuper(x, y);
    }

    private boolean inPrefList(int code, int[] pref) {
        if (code < pref.length && code >= 0) return pref[code] > 0;
        return false;
    }

    private int distanceFrom(Key k, int x, int y) {
        if (y > k.y && y < k.y + k.height) {
            return Math.abs(k.x + k.width / 2 - x);
        } else {
            return Integer.MAX_VALUE;
        }
    }

    @Override
    public int[] getNearestKeys(int x, int y) {
        if (mCurrentlyInSpace) {
            return mSpaceKeyIndexArray;
        } else {
            // Avoid dead pixels at edges of the keyboard
            return super.getNearestKeys(Math.max(0, Math.min(x, getMinWidth() - 1)),
                    Math.max(0, Math.min(y, getHeight() - 1)));
        }
    }

    private int indexOf(int code) {
        List<Key> keys = getKeys();
        int count = keys.size();
        for (int i = 0; i < count; i++) {
            if (keys.get(i).codes[0] == code) return i;
        }
        return -1;
    }

    private int getTextSizeFromTheme(int style, int defValue) {
        TypedArray array = mContext.getTheme().obtainStyledAttributes(
                style, new int[] { android.R.attr.textSize });
        int textSize = array.getDimensionPixelSize(array.getResourceId(0, 0), defValue);
        return textSize;
    }

    // TODO LatinKey could be static class
    class LatinKey extends Keyboard.Key {

        // functional normal state (with properties)
        private final int[] KEY_STATE_FUNCTIONAL_NORMAL = {
                android.R.attr.state_single
        };

        // functional pressed state (with properties)
        private final int[] KEY_STATE_FUNCTIONAL_PRESSED = {
                android.R.attr.state_single,
                android.R.attr.state_pressed
        };

        private boolean mShiftLockEnabled;

        public LatinKey(Resources res, Keyboard.Row parent, int x, int y, 
                XmlResourceParser parser) {
            super(res, parent, x, y, parser);
            if (popupCharacters != null && popupCharacters.length() == 0) {
                // If there is a keyboard with no keys specified in popupCharacters
                popupResId = 0;
            }
        }

        private void enableShiftLock() {
            mShiftLockEnabled = true;
        }

        // sticky is used for shift key.  If a key is not sticky and is modifier,
        // the key will be treated as functional.
        private boolean isFunctionalKey() {
            return !sticky && modifier;
        }

        @Override
        public void onReleased(boolean inside) {
            if (!mShiftLockEnabled) {
                super.onReleased(inside);
            } else {
                pressed = !pressed;
            }
        }

        /**
         * Overriding this method so that we can reduce the target area for certain keys.
         */
        @Override
        public boolean isInside(int x, int y) {
            // TODO This should be done by parent.isInside(this, x, y)
            // if Key.parent were protected.
            boolean result = LatinKeyboard.this.isInside(this, x, y);
            return result;
        }

        boolean isInsideSuper(int x, int y) {
            return super.isInside(x, y);
        }

        @Override
        public int[] getCurrentDrawableState() {
            if (isFunctionalKey()) {
                if (pressed) {
                    return KEY_STATE_FUNCTIONAL_PRESSED;
                } else {
                    return KEY_STATE_FUNCTIONAL_NORMAL;
                }
            }
            return super.getCurrentDrawableState();
        }

        @Override
        public int squaredDistanceFrom(int x, int y) {
            // We should count vertical gap between rows to calculate the center of this Key.
            final int verticalGap = LatinKeyboard.this.mVerticalGap;
            final int xDist = this.x + width / 2 - x;
            final int yDist = this.y + (height + verticalGap) / 2 - y;
            return xDist * xDist + yDist * yDist;
        }
    }

    /**
     * Animation to be displayed on the spacebar preview popup when switching 
     * languages by swiping the spacebar. It draws the current, previous and
     * next languages and moves them by the delta of touch movement on the spacebar.
     */
    class SlidingLocaleDrawable extends Drawable {

        private final int mWidth;
        private final int mHeight;
        private final Drawable mBackground;
        private final TextPaint mTextPaint;
        private final int mMiddleX;
        private final Drawable mLeftDrawable;
        private final Drawable mRightDrawable;
        private final int mThreshold;
        private int mDiff;
        private boolean mHitThreshold;
        private String mCurrentLanguage;
        private String mNextLanguage;
        private String mPrevLanguage;

        @SuppressLint("ResourceAsColor")
		public SlidingLocaleDrawable(Drawable background, int width, int height) {
            mBackground = background;
            setDefaultBounds(mBackground);
            mWidth = width;
            mHeight = height;
            mTextPaint = new TextPaint();
            mTextPaint.setTextSize(getTextSizeFromTheme(android.R.style.TextAppearance_Medium, 18));
            
            mTextPaint.setColor(R.color.latinkeyboard_transparent);
            mTextPaint.setTextAlign(Align.CENTER);
            mTextPaint.setAlpha(OPACITY_FULLY_OPAQUE);
            mTextPaint.setAntiAlias(true);
            mMiddleX = (mWidth - mBackground.getIntrinsicWidth()) / 2;
            mLeftDrawable =
                    mRes.getDrawable(R.drawable.sym_keyboard_feedback_language_arrows_left);
            mRightDrawable =
                    mRes.getDrawable(R.drawable.sym_keyboard_feedback_language_arrows_right);
            mThreshold = ViewConfiguration.get(mContext).getScaledTouchSlop();
        }

        private void setDiff(int diff) {
            if (diff == Integer.MAX_VALUE) {
                mHitThreshold = false;
                mCurrentLanguage = null;
                return;
            }
            mDiff = diff;
            if (mDiff > mWidth) mDiff = mWidth;
            if (mDiff < -mWidth) mDiff = -mWidth;
            if (Math.abs(mDiff) > mThreshold) mHitThreshold = true;
            invalidateSelf();
        }

        private String getLanguageName(Locale locale) {
            return LanguageSwitcher.toTitleCase(locale.getDisplayLanguage(locale), locale);
        }

        @Override
        public void draw(Canvas canvas) {
            canvas.save();
            if (mHitThreshold) {
                Paint paint = mTextPaint;
                final int width = mWidth;
                final int height = mHeight;
                final int diff = mDiff;
                final Drawable lArrow = mLeftDrawable;
                final Drawable rArrow = mRightDrawable;
                canvas.clipRect(0, 0, width, height);
                if (mCurrentLanguage == null) {
                    final LanguageSwitcher languageSwitcher = mLanguageSwitcher;
                    mCurrentLanguage = getLanguageName(languageSwitcher.getInputLocale());
                    mNextLanguage = getLanguageName(languageSwitcher.getNextInputLocale());
                    mPrevLanguage = getLanguageName(languageSwitcher.getPrevInputLocale());
                }
                // Draw language text with shadow
                final float baseline = mHeight * SPACEBAR_LANGUAGE_BASELINE - paint.descent();
                paint.setColor(mRes.getColor(R.color.latinkeyboard_feedback_language_text));
                canvas.drawText(mCurrentLanguage, width / 2 + diff, baseline, paint);
                canvas.drawText(mNextLanguage, diff - width / 2, baseline, paint);
                canvas.drawText(mPrevLanguage, diff + width + width / 2, baseline, paint);

                setDefaultBounds(lArrow);
                rArrow.setBounds(width - rArrow.getIntrinsicWidth(), 0, width,
                        rArrow.getIntrinsicHeight());
                lArrow.draw(canvas);
                rArrow.draw(canvas);
            }
            if (mBackground != null) {
                canvas.translate(mMiddleX, 0);
                mBackground.draw(canvas);
            }
            canvas.restore();
        }

        @Override
        public int getOpacity() {
            return PixelFormat.TRANSLUCENT;
        }

        @Override
        public void setAlpha(int alpha) {
            // Ignore
        }

        @Override
        public void setColorFilter(ColorFilter cf) {
            // Ignore
        }

        @Override
        public int getIntrinsicWidth() {
            return mWidth;
        }

        @Override
        public int getIntrinsicHeight() {
            return mHeight;
        }
    }
}

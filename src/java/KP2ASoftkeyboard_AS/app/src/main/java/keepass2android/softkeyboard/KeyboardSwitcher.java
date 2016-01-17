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

import android.content.SharedPreferences;
import android.content.res.Configuration;
import android.content.res.Resources;
import android.preference.PreferenceManager;
import android.util.Log;
import android.view.InflateException;

import java.lang.ref.SoftReference;
import java.util.Arrays;
import java.util.HashMap;
import java.util.Locale;

public class KeyboardSwitcher implements SharedPreferences.OnSharedPreferenceChangeListener {

    public static final int MODE_NONE = 0;
    public static final int MODE_TEXT = 1;
    public static final int MODE_SYMBOLS = 2;
    public static final int MODE_PHONE = 3;
    public static final int MODE_URL = 4;
    public static final int MODE_EMAIL = 5;
    public static final int MODE_IM = 6;
    public static final int MODE_WEB = 7;
    public static final int MODE_KP2A = 8;

    // Main keyboard layouts without the settings key
    public static final int KEYBOARDMODE_NORMAL = R.id.mode_normal;
    public static final int KEYBOARDMODE_URL = R.id.mode_url;
    public static final int KEYBOARDMODE_EMAIL = R.id.mode_email;
    public static final int KEYBOARDMODE_IM = R.id.mode_im;
    public static final int KEYBOARDMODE_WEB = R.id.mode_webentry;
    // Main keyboard layouts with the settings key
    public static final int KEYBOARDMODE_NORMAL_WITH_SETTINGS_KEY =
            R.id.mode_normal_with_settings_key;
    public static final int KEYBOARDMODE_URL_WITH_SETTINGS_KEY =
            R.id.mode_url_with_settings_key;
    public static final int KEYBOARDMODE_EMAIL_WITH_SETTINGS_KEY =
            R.id.mode_email_with_settings_key;
    public static final int KEYBOARDMODE_IM_WITH_SETTINGS_KEY =
            R.id.mode_im_with_settings_key;
    public static final int KEYBOARDMODE_WEB_WITH_SETTINGS_KEY =
            R.id.mode_webentry_with_settings_key;

    // Symbols keyboard layout without the settings key
    public static final int KEYBOARDMODE_SYMBOLS = R.id.mode_symbols;
    // Symbols keyboard layout with the settings key
    public static final int KEYBOARDMODE_SYMBOLS_WITH_SETTINGS_KEY =
            R.id.mode_symbols_with_settings_key;

    public static final String DEFAULT_LAYOUT_ID = "4";
    public static final String PREF_KEYBOARD_LAYOUT = "pref_keyboard_layout_20100902";
    private static final int[] THEMES = new int [] {
        R.layout.input_basic, R.layout.input_basic_highcontrast, R.layout.input_stone_normal,
        R.layout.input_stone_bold, R.layout.input_gingerbread};

    // Ids for each characters' color in the keyboard
    private static final int CHAR_THEME_COLOR_WHITE = 0;
    private static final int CHAR_THEME_COLOR_BLACK = 1;

    // Tables which contains resource ids for each character theme color
    private static final int[] KBD_PHONE = new int[] {R.xml.kbd_phone, R.xml.kbd_phone_black};
    private static final int[] KBD_PHONE_SYMBOLS = new int[] {
        R.xml.kbd_phone_symbols, R.xml.kbd_phone_symbols_black};
    private static final int[] KBD_SYMBOLS = new int[] {
        R.xml.kbd_symbols, R.xml.kbd_symbols_black};
    private static final int[] KBD_SYMBOLS_SHIFT = new int[] {
        R.xml.kbd_symbols_shift, R.xml.kbd_symbols_shift_black};
    private static final int[] KBD_QWERTY = new int[] {R.xml.kbd_qwerty, R.xml.kbd_qwerty_black};
    
    private static final int[] KBD_KP2A = new int[] {R.xml.kbd_kp2a, R.xml.kbd_kp2a_black};

    private LatinKeyboardView mInputView;
    private static final int[] ALPHABET_MODES = {
        KEYBOARDMODE_NORMAL,
        KEYBOARDMODE_URL,
        KEYBOARDMODE_EMAIL,
        KEYBOARDMODE_IM,
        KEYBOARDMODE_WEB,
        KEYBOARDMODE_NORMAL_WITH_SETTINGS_KEY,
        KEYBOARDMODE_URL_WITH_SETTINGS_KEY,
        KEYBOARDMODE_EMAIL_WITH_SETTINGS_KEY,
        KEYBOARDMODE_IM_WITH_SETTINGS_KEY,
        KEYBOARDMODE_WEB_WITH_SETTINGS_KEY };

    private KP2AKeyboard mInputMethodService;

    private KeyboardId mSymbolsId;
    private KeyboardId mSymbolsShiftedId;

    private KeyboardId mCurrentId;
    private final HashMap<KeyboardId, SoftReference<LatinKeyboard>> mKeyboards =
            new HashMap<KeyboardId, SoftReference<LatinKeyboard>>();

    private int mMode = MODE_NONE; /** One of the MODE_XXX values */
    private int mImeOptions;
    private boolean mIsSymbols;
    /** mIsAutoCompletionActive indicates that auto completed word will be input instead of
     * what user actually typed. */
    private boolean mIsAutoCompletionActive;
    private boolean mPreferSymbols;

    private static final int AUTO_MODE_SWITCH_STATE_ALPHA = 0;
    private static final int AUTO_MODE_SWITCH_STATE_SYMBOL_BEGIN = 1;
    private static final int AUTO_MODE_SWITCH_STATE_SYMBOL = 2;
    // The following states are used only on the distinct multi-touch panel devices.
    private static final int AUTO_MODE_SWITCH_STATE_MOMENTARY = 3;
    private static final int AUTO_MODE_SWITCH_STATE_CHORDING = 4;
    private int mAutoModeSwitchState = AUTO_MODE_SWITCH_STATE_ALPHA;

    // Indicates whether or not we have the settings key
    private boolean mHasSettingsKey;
    private static final int SETTINGS_KEY_MODE_AUTO = R.string.settings_key_mode_auto;
    private static final int SETTINGS_KEY_MODE_ALWAYS_SHOW = R.string.settings_key_mode_always_show;
    // NOTE: No need to have SETTINGS_KEY_MODE_ALWAYS_HIDE here because it's not being referred to
    // in the source code now.
    // Default is SETTINGS_KEY_MODE_AUTO.
    private static final int DEFAULT_SETTINGS_KEY_MODE = SETTINGS_KEY_MODE_AUTO;

    private int mLastDisplayWidth;
    private LanguageSwitcher mLanguageSwitcher;
    private Locale mInputLocale;

    private int mLayoutId;

    private static final KeyboardSwitcher sInstance = new KeyboardSwitcher();

    public static KeyboardSwitcher getInstance() {
        return sInstance;
    }

    private KeyboardSwitcher() {
        // Intentional empty constructor for singleton.
    }

    public static void init(KP2AKeyboard ims) {
        sInstance.mInputMethodService = ims;

        final SharedPreferences prefs = PreferenceManager.getDefaultSharedPreferences(ims);
        sInstance.mLayoutId = Integer.valueOf(
                prefs.getString(PREF_KEYBOARD_LAYOUT, DEFAULT_LAYOUT_ID));
        sInstance.updateSettingsKeyState(prefs);
        prefs.registerOnSharedPreferenceChangeListener(sInstance);

        sInstance.mSymbolsId = sInstance.makeSymbolsId();
        sInstance.mSymbolsShiftedId = sInstance.makeSymbolsShiftedId();
    }

    /**
     * Sets the input locale, when there are multiple locales for input.
     * If no locale switching is required, then the locale should be set to null.
     * @param locale the current input locale, or null for default locale with no locale 
     * button.
     */
    public void setLanguageSwitcher(LanguageSwitcher languageSwitcher) {
        mLanguageSwitcher = languageSwitcher;
        mInputLocale = mLanguageSwitcher.getInputLocale();
    }

    private KeyboardId makeSymbolsId() {
        return new KeyboardId(KBD_SYMBOLS[getCharColorId()], mHasSettingsKey ?
                KEYBOARDMODE_SYMBOLS_WITH_SETTINGS_KEY : KEYBOARDMODE_SYMBOLS,
                false);
    }

    private KeyboardId makeSymbolsShiftedId() {
        return new KeyboardId(KBD_SYMBOLS_SHIFT[getCharColorId()], mHasSettingsKey ?
                KEYBOARDMODE_SYMBOLS_WITH_SETTINGS_KEY : KEYBOARDMODE_SYMBOLS,
                false);
    }

    public void makeKeyboards(boolean forceCreate) {
        mSymbolsId = makeSymbolsId();
        mSymbolsShiftedId = makeSymbolsShiftedId();

        if (forceCreate) mKeyboards.clear();
        // Configuration change is coming after the keyboard gets recreated. So don't rely on that.
        // If keyboards have already been made, check if we have a screen width change and 
        // create the keyboard layouts again at the correct orientation
        int displayWidth = mInputMethodService.getMaxWidth();
        if (displayWidth == mLastDisplayWidth) return;
        mLastDisplayWidth = displayWidth;
        if (!forceCreate) mKeyboards.clear();
    }

    /**
     * Represents the parameters necessary to construct a new LatinKeyboard,
     * which also serve as a unique identifier for each keyboard type.
     */
    private static class KeyboardId {
        // TODO: should have locale and portrait/landscape orientation?
        public final int mXml;
        public final int mKeyboardMode; /** A KEYBOARDMODE_XXX value */
        public final boolean mEnableShiftLock;

        private final int mHashCode;

        public KeyboardId(int xml, int mode, boolean enableShiftLock) {
            this.mXml = xml;
            this.mKeyboardMode = mode;
            this.mEnableShiftLock = enableShiftLock;

            this.mHashCode = Arrays.hashCode(new Object[] {
               xml, mode, enableShiftLock
            });
        }

        public KeyboardId(int xml) {
            this(xml, 0, false);
        }

        @Override
        public boolean equals(Object other) {
            return other instanceof KeyboardId && equals((KeyboardId) other);
        }

        private boolean equals(KeyboardId other) {
            return other.mXml == this.mXml
                && other.mKeyboardMode == this.mKeyboardMode
                && other.mEnableShiftLock == this.mEnableShiftLock
                ;
        }

        @Override
        public int hashCode() {
            return mHashCode;
        }
    }


    public void setKeyboardMode(int mode, int imeOptions) {
    	Log.d("KP2AK", "Switcher.SetKeyboardMode: " + mode);
        mAutoModeSwitchState = AUTO_MODE_SWITCH_STATE_ALPHA;
        mPreferSymbols = mode == MODE_SYMBOLS;
        if (mode == MODE_SYMBOLS) {
            mode = MODE_TEXT;
        }
        try {
            setKeyboardMode(mode, imeOptions, mPreferSymbols);
        } catch (RuntimeException e) {
            LatinImeLogger.logOnException(mode + "," + imeOptions + "," + mPreferSymbols, e);
        }
    }

    private void setKeyboardMode(int mode, int imeOptions, boolean isSymbols) {
        if (mInputView == null) return;
        mMode = mode;
        mImeOptions = imeOptions;
        mIsSymbols = isSymbols;

        mInputView.setPreviewEnabled(mInputMethodService.getPopupOn());
        KeyboardId id = getKeyboardId(mode, imeOptions, isSymbols);
        LatinKeyboard keyboard = null;
        keyboard = getKeyboard(id);

        if (mode == MODE_PHONE) {
            mInputView.setPhoneKeyboard(keyboard);
        }

        mCurrentId = id;
        mInputView.setKeyboard(keyboard);
        keyboard.setShifted(false);
        keyboard.setShiftLocked(keyboard.isShiftLocked());
        keyboard.setImeOptions(mInputMethodService.getResources(), mMode, imeOptions);
        keyboard.setColorOfSymbolIcons(mIsAutoCompletionActive, isBlackSym());
        // Update the settings key state because number of enabled IMEs could have been changed
        updateSettingsKeyState(PreferenceManager.getDefaultSharedPreferences(mInputMethodService));
    }

    private LatinKeyboard getKeyboard(KeyboardId id) {
        SoftReference<LatinKeyboard> ref = mKeyboards.get(id);
        LatinKeyboard keyboard = (ref == null) ? null : ref.get();
        if (keyboard == null) {
            Resources orig = mInputMethodService.getResources();
            Configuration conf = orig.getConfiguration();
            Locale saveLocale = conf.locale;
            conf.locale = mInputLocale;
            orig.updateConfiguration(conf, null);
            keyboard = new LatinKeyboard(mInputMethodService, id.mXml, id.mKeyboardMode);
            keyboard.setLanguageSwitcher(mLanguageSwitcher, mIsAutoCompletionActive, isBlackSym());

            if (id.mEnableShiftLock) {
                keyboard.enableShiftLock();
            }
            mKeyboards.put(id, new SoftReference<LatinKeyboard>(keyboard));

            conf.locale = saveLocale;
            orig.updateConfiguration(conf, null);
        }
        return keyboard;
    }

    private KeyboardId getKeyboardId(int mode, int imeOptions, boolean isSymbols) {
        int charColorId = getCharColorId();
        if (isSymbols) {
            if (mode == MODE_PHONE) {
                return new KeyboardId(KBD_PHONE_SYMBOLS[charColorId]);
            } else {
                return new KeyboardId(KBD_SYMBOLS[charColorId], mHasSettingsKey ?
                        KEYBOARDMODE_SYMBOLS_WITH_SETTINGS_KEY : KEYBOARDMODE_SYMBOLS,
                        false);
            }
        }
        // TODO: generalize for any KeyboardId
        int keyboardRowsResId = KBD_QWERTY[charColorId];

        switch (mode) {
    		case MODE_KP2A:
    			return new KeyboardId(KBD_KP2A[charColorId]);
    		case MODE_NONE:
                LatinImeLogger.logOnWarning(
                        "getKeyboardId:" + mode + "," + imeOptions + "," + isSymbols);
                /* fall through */
            case MODE_TEXT:
                return new KeyboardId(keyboardRowsResId, mHasSettingsKey ?
                        KEYBOARDMODE_NORMAL_WITH_SETTINGS_KEY : KEYBOARDMODE_NORMAL,
                        true);
            case MODE_SYMBOLS:
                return new KeyboardId(KBD_SYMBOLS[charColorId], mHasSettingsKey ?
                        KEYBOARDMODE_SYMBOLS_WITH_SETTINGS_KEY : KEYBOARDMODE_SYMBOLS,
                        false);
            case MODE_PHONE:
                return new KeyboardId(KBD_PHONE[charColorId]);
            case MODE_URL:
                return new KeyboardId(keyboardRowsResId, mHasSettingsKey ?
                        KEYBOARDMODE_URL_WITH_SETTINGS_KEY : KEYBOARDMODE_URL, true);
            case MODE_EMAIL:
                return new KeyboardId(keyboardRowsResId, mHasSettingsKey ?
                        KEYBOARDMODE_EMAIL_WITH_SETTINGS_KEY : KEYBOARDMODE_EMAIL, true);
            case MODE_IM:
                return new KeyboardId(keyboardRowsResId, mHasSettingsKey ?
                        KEYBOARDMODE_IM_WITH_SETTINGS_KEY : KEYBOARDMODE_IM, true);
            case MODE_WEB:
                return new KeyboardId(keyboardRowsResId, mHasSettingsKey ?
                        KEYBOARDMODE_WEB_WITH_SETTINGS_KEY : KEYBOARDMODE_WEB, true);
        }
        return null;
    }

    public int getKeyboardMode() {
        return mMode;
    }
    
    public boolean isAlphabetMode() {
        if (mCurrentId == null) {
            return false;
        }
        int currentMode = mCurrentId.mKeyboardMode;
        for (Integer mode : ALPHABET_MODES) {
            if (currentMode == mode) {
                return true;
            }
        }
        return false;
    }

    public void setShifted(boolean shifted) {
        if (mInputView != null) {
            mInputView.setShifted(shifted);
        }
    }

    public void setShiftLocked(boolean shiftLocked) {
        if (mInputView != null) {
            mInputView.setShiftLocked(shiftLocked);
        }
    }

    public void toggleShift() {
        if (isAlphabetMode())
            return;
        if (mCurrentId.equals(mSymbolsId) || !mCurrentId.equals(mSymbolsShiftedId)) {
            LatinKeyboard symbolsShiftedKeyboard = getKeyboard(mSymbolsShiftedId);
            mCurrentId = mSymbolsShiftedId;
            mInputView.setKeyboard(symbolsShiftedKeyboard);
            // Symbol shifted keyboard has an ALT key that has a caps lock style indicator. To
            // enable the indicator, we need to call enableShiftLock() and setShiftLocked(true).
            // Thus we can keep the ALT key's Key.on value true while LatinKey.onRelease() is
            // called.
            symbolsShiftedKeyboard.enableShiftLock();
            symbolsShiftedKeyboard.setShiftLocked(true);
            symbolsShiftedKeyboard.setImeOptions(mInputMethodService.getResources(),
                    mMode, mImeOptions);
        } else {
            LatinKeyboard symbolsKeyboard = getKeyboard(mSymbolsId);
            mCurrentId = mSymbolsId;
            mInputView.setKeyboard(symbolsKeyboard);
            // Symbol keyboard has an ALT key that has a caps lock style indicator. To disable the
            // indicator, we need to call enableShiftLock() and setShiftLocked(false).
            symbolsKeyboard.enableShiftLock();
            symbolsKeyboard.setShifted(false);
            symbolsKeyboard.setImeOptions(mInputMethodService.getResources(), mMode, mImeOptions);
        }
    }

    public void onCancelInput() {
        // Snap back to the previous keyboard mode if the user cancels sliding input.
        if (mAutoModeSwitchState == AUTO_MODE_SWITCH_STATE_MOMENTARY && getPointerCount() == 1)
            mInputMethodService.changeKeyboardMode();
    }

    public void toggleSymbols() {
        setKeyboardMode(mMode, mImeOptions, !mIsSymbols);
        if (mIsSymbols && !mPreferSymbols) {
            mAutoModeSwitchState = AUTO_MODE_SWITCH_STATE_SYMBOL_BEGIN;
        } else {
            mAutoModeSwitchState = AUTO_MODE_SWITCH_STATE_ALPHA;
        }
    }

    public boolean hasDistinctMultitouch() {
        return mInputView != null && mInputView.hasDistinctMultitouch();
    }

    public void setAutoModeSwitchStateMomentary() {
        mAutoModeSwitchState = AUTO_MODE_SWITCH_STATE_MOMENTARY;
    }

    public boolean isInMomentaryAutoModeSwitchState() {
        return mAutoModeSwitchState == AUTO_MODE_SWITCH_STATE_MOMENTARY;
    }

    public boolean isInChordingAutoModeSwitchState() {
        return mAutoModeSwitchState == AUTO_MODE_SWITCH_STATE_CHORDING;
    }

    public boolean isVibrateAndSoundFeedbackRequired() {
        return mInputView != null && !mInputView.isInSlidingKeyInput();
    }

    private int getPointerCount() {
        return mInputView == null ? 0 : mInputView.getPointerCount();
    }

    /**
     * Updates state machine to figure out when to automatically snap back to the previous mode.
     */
    public void onKey(int key) {
        // Switch back to alpha mode if user types one or more non-space/enter characters
        // followed by a space/enter
        switch (mAutoModeSwitchState) {
        case AUTO_MODE_SWITCH_STATE_MOMENTARY:
            // Only distinct multi touch devices can be in this state.
            // On non-distinct multi touch devices, mode change key is handled by {@link onKey},
            // not by {@link onPress} and {@link onRelease}. So, on such devices,
            // {@link mAutoModeSwitchState} starts from {@link AUTO_MODE_SWITCH_STATE_SYMBOL_BEGIN},
            // or {@link AUTO_MODE_SWITCH_STATE_ALPHA}, not from
            // {@link AUTO_MODE_SWITCH_STATE_MOMENTARY}.
            if (key == LatinKeyboard.KEYCODE_MODE_CHANGE) {
                // Detected only the mode change key has been pressed, and then released.
                if (mIsSymbols) {
                    mAutoModeSwitchState = AUTO_MODE_SWITCH_STATE_SYMBOL_BEGIN;
                } else {
                    mAutoModeSwitchState = AUTO_MODE_SWITCH_STATE_ALPHA;
                }
            } else if (getPointerCount() == 1) {
                // Snap back to the previous keyboard mode if the user pressed the mode change key
                // and slid to other key, then released the finger.
                // If the user cancels the sliding input, snapping back to the previous keyboard
                // mode is handled by {@link #onCancelInput}.
                mInputMethodService.changeKeyboardMode();
            } else {
                // Chording input is being started. The keyboard mode will be snapped back to the
                // previous mode in {@link onReleaseSymbol} when the mode change key is released.
                mAutoModeSwitchState = AUTO_MODE_SWITCH_STATE_CHORDING;
            }
            break;
        case AUTO_MODE_SWITCH_STATE_SYMBOL_BEGIN:
            if (key != KP2AKeyboard.KEYCODE_SPACE && key != KP2AKeyboard.KEYCODE_ENTER && key >= 0) {
                mAutoModeSwitchState = AUTO_MODE_SWITCH_STATE_SYMBOL;
            }
            break;
        case AUTO_MODE_SWITCH_STATE_SYMBOL:
            // Snap back to alpha keyboard mode if user types one or more non-space/enter
            // characters followed by a space/enter.
            if (key == KP2AKeyboard.KEYCODE_ENTER || key == KP2AKeyboard.KEYCODE_SPACE) {
                mInputMethodService.changeKeyboardMode();
            }
            break;
        }
    }

    public LatinKeyboardView getInputView() {
        return mInputView;
    }

    public void recreateInputView() {
        changeLatinKeyboardView(mLayoutId, true);
    }

    private void changeLatinKeyboardView(int newLayout, boolean forceReset) {
        if (mLayoutId != newLayout || mInputView == null || forceReset) {
            if (mInputView != null) {
                mInputView.closing();
            }
            if (THEMES.length <= newLayout) {
                newLayout = Integer.valueOf(DEFAULT_LAYOUT_ID);
            }

            LatinIMEUtil.GCUtils.getInstance().reset();
            boolean tryGC = true;
            for (int i = 0; i < LatinIMEUtil.GCUtils.GC_TRY_LOOP_MAX && tryGC; ++i) {
                try {
                    mInputView = (LatinKeyboardView) mInputMethodService.getLayoutInflater(
                            ).inflate(THEMES[newLayout], null);
                    tryGC = false;
                } catch (OutOfMemoryError e) {
                    tryGC = LatinIMEUtil.GCUtils.getInstance().tryGCOrWait(
                            mLayoutId + "," + newLayout, e);
                } catch (InflateException e) {
                    tryGC = LatinIMEUtil.GCUtils.getInstance().tryGCOrWait(
                            mLayoutId + "," + newLayout, e);
                }
            }
            mInputView.setOnKeyboardActionListener(mInputMethodService);
            mLayoutId = newLayout;
        }
        mInputMethodService.mHandler.post(new Runnable() {
            public void run() {
                if (mInputView != null) {
                    mInputMethodService.setInputView(mInputView);
                }
                mInputMethodService.updateInputViewShown();
            }});
    }

    public void onSharedPreferenceChanged(SharedPreferences sharedPreferences, String key) {
        if (PREF_KEYBOARD_LAYOUT.equals(key)) {
            changeLatinKeyboardView(
                    Integer.valueOf(sharedPreferences.getString(key, DEFAULT_LAYOUT_ID)), false);
        } else if (LatinIMESettings.PREF_SETTINGS_KEY.equals(key)) {
            updateSettingsKeyState(sharedPreferences);
            recreateInputView();
        }
    }

    public boolean isBlackSym () {
        if (mInputView != null && mInputView.getSymbolColorScheme() == 1) {
            return true;
        }
        return false;
    }

    private int getCharColorId () {
        if (isBlackSym()) {
            return CHAR_THEME_COLOR_BLACK;
        } else {
            return CHAR_THEME_COLOR_WHITE;
        }
    }

    public void onAutoCompletionStateChanged(boolean isAutoCompletion) {
        if (isAutoCompletion != mIsAutoCompletionActive) {
            LatinKeyboardView keyboardView = getInputView();
            mIsAutoCompletionActive = isAutoCompletion;
            keyboardView.invalidateKey(((LatinKeyboard) keyboardView.getKeyboard())
                    .onAutoCompletionStateChanged(isAutoCompletion));
        }
    }

    private void updateSettingsKeyState(SharedPreferences prefs) {
        /*Resources resources = mInputMethodService.getResources();
        final String settingsKeyMode = prefs.getString(LatinIMESettings.PREF_SETTINGS_KEY,
                resources.getString(DEFAULT_SETTINGS_KEY_MODE));
        // We show the settings key when 1) SETTINGS_KEY_MODE_ALWAYS_SHOW or
        // 2) SETTINGS_KEY_MODE_AUTO and there are two or more enabled IMEs on the system
        if (settingsKeyMode.equals(resources.getString(SETTINGS_KEY_MODE_ALWAYS_SHOW))
                || (settingsKeyMode.equals(resources.getString(SETTINGS_KEY_MODE_AUTO))
                        && LatinIMEUtil.hasMultipleEnabledIMEs(mInputMethodService))) {
            mHasSettingsKey = true;
        } else {
            mHasSettingsKey = false;
        }*/
        mHasSettingsKey = true;
    }
}

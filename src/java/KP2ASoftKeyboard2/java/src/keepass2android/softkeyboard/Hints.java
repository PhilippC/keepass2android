/*
 * Copyright (C) 2009 Google Inc.
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

import android.content.ContentResolver;
import android.content.Context;
import android.content.SharedPreferences;
import android.preference.PreferenceManager;
import android.view.inputmethod.InputConnection;

import java.util.Calendar;
import java.util.HashMap;
import java.util.Map;

/**
 * Logic to determine when to display hints on usage to the user.
 */
public class Hints {
    public interface Display {
        public void showHint(int viewResource);
    }

    private static final String PREF_VOICE_HINT_NUM_UNIQUE_DAYS_SHOWN =
            "voice_hint_num_unique_days_shown";
    private static final String PREF_VOICE_HINT_LAST_TIME_SHOWN =
            "voice_hint_last_time_shown";
    private static final String PREF_VOICE_INPUT_LAST_TIME_USED =
            "voice_input_last_time_used";
    private static final String PREF_VOICE_PUNCTUATION_HINT_VIEW_COUNT =
            "voice_punctuation_hint_view_count";
    private static final int DEFAULT_SWIPE_HINT_MAX_DAYS_TO_SHOW = 7;
    private static final int DEFAULT_PUNCTUATION_HINT_MAX_DISPLAYS = 7;

    private Context mContext;
    private Display mDisplay;
    private boolean mVoiceResultContainedPunctuation;
    private int mSwipeHintMaxDaysToShow;
    private int mPunctuationHintMaxDisplays;

    // Only show punctuation hint if voice result did not contain punctuation.
    static final Map<CharSequence, String> SPEAKABLE_PUNCTUATION
            = new HashMap<CharSequence, String>();
    static {
        SPEAKABLE_PUNCTUATION.put(",", "comma");
        SPEAKABLE_PUNCTUATION.put(".", "period");
        SPEAKABLE_PUNCTUATION.put("?", "question mark");
    }

    public Hints(Context context, Display display) {
        mContext = context;
        mDisplay = display;

        ContentResolver cr = mContext.getContentResolver();
        
    }

    public boolean showSwipeHintIfNecessary(boolean fieldRecommended) {
        if (fieldRecommended && shouldShowSwipeHint()) {
            showHint(R.layout.voice_swipe_hint);
            return true;
        }

        return false;
    }

    public boolean showPunctuationHintIfNecessary(InputConnection ic) {
        if (!mVoiceResultContainedPunctuation
                && ic != null
                && getAndIncrementPref(PREF_VOICE_PUNCTUATION_HINT_VIEW_COUNT)
                        < mPunctuationHintMaxDisplays) {
            CharSequence charBeforeCursor = ic.getTextBeforeCursor(1, 0);
            if (SPEAKABLE_PUNCTUATION.containsKey(charBeforeCursor)) {
                showHint(R.layout.voice_punctuation_hint);
                return true;
            }
        }

        return false;
    }

    public void registerVoiceResult(String text) {
        // Update the current time as the last time voice input was used.
        SharedPreferences.Editor editor =
                PreferenceManager.getDefaultSharedPreferences(mContext).edit();
        editor.putLong(PREF_VOICE_INPUT_LAST_TIME_USED, System.currentTimeMillis());
        SharedPreferencesCompat.apply(editor);

        mVoiceResultContainedPunctuation = false;
        for (CharSequence s : SPEAKABLE_PUNCTUATION.keySet()) {
            if (text.indexOf(s.toString()) >= 0) {
                mVoiceResultContainedPunctuation = true;
                break;
            }
        }
    }

    private boolean shouldShowSwipeHint() {
        
        
        return false;
    }

    /**
     * Determines whether the provided time is from some time today (i.e., this day, month,
     * and year).
     */
    private boolean isFromToday(long timeInMillis) {
        if (timeInMillis == 0) return false;

        Calendar today = Calendar.getInstance();
        today.setTimeInMillis(System.currentTimeMillis());

        Calendar timestamp = Calendar.getInstance();
        timestamp.setTimeInMillis(timeInMillis);

        return (today.get(Calendar.YEAR) == timestamp.get(Calendar.YEAR) &&
                today.get(Calendar.DAY_OF_MONTH) == timestamp.get(Calendar.DAY_OF_MONTH) &&
                today.get(Calendar.MONTH) == timestamp.get(Calendar.MONTH));
    }

    private void showHint(int hintViewResource) {
        SharedPreferences sp = PreferenceManager.getDefaultSharedPreferences(mContext);

        int numUniqueDaysShown = sp.getInt(PREF_VOICE_HINT_NUM_UNIQUE_DAYS_SHOWN, 0);
        long lastTimeHintWasShown = sp.getLong(PREF_VOICE_HINT_LAST_TIME_SHOWN, 0);

        // If this is the first time the hint is being shown today, increase the saved values
        // to represent that. We don't need to increase the last time the hint was shown unless
        // it is a different day from the current value.
        if (!isFromToday(lastTimeHintWasShown)) {
            SharedPreferences.Editor editor = sp.edit();
            editor.putInt(PREF_VOICE_HINT_NUM_UNIQUE_DAYS_SHOWN, numUniqueDaysShown + 1);
            editor.putLong(PREF_VOICE_HINT_LAST_TIME_SHOWN, System.currentTimeMillis());
            SharedPreferencesCompat.apply(editor);
        }

        if (mDisplay != null) {
            mDisplay.showHint(hintViewResource);
        }
    }

    private int getAndIncrementPref(String pref) {
        SharedPreferences sp = PreferenceManager.getDefaultSharedPreferences(mContext);
        int value = sp.getInt(pref, 0);
        SharedPreferences.Editor editor = sp.edit();
        editor.putInt(pref, value + 1);
        SharedPreferencesCompat.apply(editor);
        return value;
    }
}

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
import android.text.AutoText;
import android.text.TextUtils;
import android.util.Log;
import android.view.View;

import java.nio.ByteBuffer;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.Locale;

/**
 * This class loads a dictionary and provides a list of suggestions for a given sequence of 
 * characters. This includes corrections and completions.
 * @hide pending API Council Approval
 */
public class Suggest implements Dictionary.WordCallback {

    public static final int APPROX_MAX_WORD_LENGTH = 32;

    public static final int CORRECTION_NONE = 0;
    public static final int CORRECTION_BASIC = 1;
    public static final int CORRECTION_FULL = 2;
    public static final int CORRECTION_FULL_BIGRAM = 3;

    /**
     * Words that appear in both bigram and unigram data gets multiplier ranging from
     * BIGRAM_MULTIPLIER_MIN to BIGRAM_MULTIPLIER_MAX depending on the frequency score from
     * bigram data.
     */
    public static final double BIGRAM_MULTIPLIER_MIN = 1.2;
    public static final double BIGRAM_MULTIPLIER_MAX = 1.5;

    /**
     * Maximum possible bigram frequency. Will depend on how many bits are being used in data
     * structure. Maximum bigram freqeuncy will get the BIGRAM_MULTIPLIER_MAX as the multiplier.
     */
    public static final int MAXIMUM_BIGRAM_FREQUENCY = 127;

    public static final int DIC_USER_TYPED = 0;
    public static final int DIC_MAIN = 1;
    public static final int DIC_USER = 2;
    public static final int DIC_AUTO = 3;
    public static final int DIC_CONTACTS = 4;
    // If you add a type of dictionary, increment DIC_TYPE_LAST_ID
    public static final int DIC_TYPE_LAST_ID = 4;

    static final int LARGE_DICTIONARY_THRESHOLD = 200 * 1000;

    private BinaryDictionary mMainDict;
/*
    private Dictionary mUserDictionary;

    private Dictionary mAutoDictionary;

    private Dictionary mContactsDictionary;

    private Dictionary mUserBigramDictionary;
*/
    private int mPrefMaxSuggestions = 12;

    private static final int PREF_MAX_BIGRAMS = 60;

    private boolean mAutoTextEnabled;

    private int[] mPriorities = new int[mPrefMaxSuggestions];
    private int[] mBigramPriorities = new int[PREF_MAX_BIGRAMS];

    // Handle predictive correction for only the first 1280 characters for performance reasons
    // If we support scripts that need latin characters beyond that, we should probably use some
    // kind of a sparse array or language specific list with a mapping lookup table.
    // 1280 is the size of the BASE_CHARS array in ExpandableDictionary, which is a basic set of
    // latin characters.
    private int[] mNextLettersFrequencies = new int[1280];
    private ArrayList<CharSequence> mSuggestions = new ArrayList<CharSequence>();
    ArrayList<CharSequence> mBigramSuggestions  = new ArrayList<CharSequence>();
    private ArrayList<CharSequence> mStringPool = new ArrayList<CharSequence>();
    private boolean mHaveCorrection;
    private CharSequence mOriginalWord;
    private String mLowerOriginalWord;

    // TODO: Remove these member variables by passing more context to addWord() callback method
    private boolean mIsFirstCharCapitalized;
    private boolean mIsAllUpperCase;

    private int mCorrectionMode = CORRECTION_BASIC;

    public Suggest(Context context, int[] dictionaryResId) {
        mMainDict = new BinaryDictionary(context, dictionaryResId, DIC_MAIN);
        
        
        Locale locale = context.getResources().getConfiguration().locale;
        Log.d("KP2AK", "locale: " + locale.getISO3Language());
        
        if (!hasMainDictionary() 
        		|| (!"eng".equals(locale.getISO3Language()))) 
        {
        	Log.d("KP2AK", "try get plug");
            BinaryDictionary plug = PluginManager.getDictionary(context, locale.getLanguage());
            if (plug != null) {
            	Log.d("KP2AK", "ok");
                mMainDict.close();
                mMainDict = plug;
            }
        }
        
        
        initPool();
    }

    

    private void initPool() {
        for (int i = 0; i < mPrefMaxSuggestions; i++) {
            StringBuilder sb = new StringBuilder(getApproxMaxWordLength());
            mStringPool.add(sb);
        }
    }

    public void setAutoTextEnabled(boolean enabled) {
        mAutoTextEnabled = enabled;
    }

    public int getCorrectionMode() {
        return mCorrectionMode;
    }

    public void setCorrectionMode(int mode) {
        mCorrectionMode = mode;
    }

    public boolean hasMainDictionary() {
        return mMainDict.getSize() > LARGE_DICTIONARY_THRESHOLD;
    }

    public int getApproxMaxWordLength() {
        return APPROX_MAX_WORD_LENGTH;
    }
/*
    *//**
     * Sets an optional user dictionary resource to be loaded. The user dictionary is consulted
     * before the main dictionary, if set.
     *//*
    public void setUserDictionary(Dictionary userDictionary) {
        mUserDictionary = userDictionary;
    }

    *//**
     * Sets an optional contacts dictionary resource to be loaded.
     *//*
    public void setContactsDictionary(Dictionary userDictionary) {
        mContactsDictionary = userDictionary;
    }
    
    public void setAutoDictionary(Dictionary autoDictionary) {
        mAutoDictionary = autoDictionary;
    }

    public void setUserBigramDictionary(Dictionary userBigramDictionary) {
        mUserBigramDictionary = userBigramDictionary;
    }
*/
    /**
     * Number of suggestions to generate from the input key sequence. This has
     * to be a number between 1 and 100 (inclusive).
     * @param maxSuggestions
     * @throws IllegalArgumentException if the number is out of range
     */
    public void setMaxSuggestions(int maxSuggestions) {
        if (maxSuggestions < 1 || maxSuggestions > 100) {
            throw new IllegalArgumentException("maxSuggestions must be between 1 and 100");
        }
        mPrefMaxSuggestions = maxSuggestions;
        mPriorities = new int[mPrefMaxSuggestions];
        mBigramPriorities = new int[PREF_MAX_BIGRAMS];
        collectGarbage(mSuggestions, mPrefMaxSuggestions);
        while (mStringPool.size() < mPrefMaxSuggestions) {
            StringBuilder sb = new StringBuilder(getApproxMaxWordLength());
            mStringPool.add(sb);
        }
    }

    private boolean haveSufficientCommonality(String original, CharSequence suggestion) {
        final int originalLength = original.length();
        final int suggestionLength = suggestion.length();
        final int minLength = Math.min(originalLength, suggestionLength);
        if (minLength <= 2) return true;
        int matching = 0;
        int lessMatching = 0; // Count matches if we skip one character
        int i;
        for (i = 0; i < minLength; i++) {
            final char origChar = ExpandableDictionary.toLowerCase(original.charAt(i));
            if (origChar == ExpandableDictionary.toLowerCase(suggestion.charAt(i))) {
                matching++;
                lessMatching++;
            } else if (i + 1 < suggestionLength
                    && origChar == ExpandableDictionary.toLowerCase(suggestion.charAt(i + 1))) {
                lessMatching++;
            }
        }
        matching = Math.max(matching, lessMatching);

        if (minLength <= 4) {
            return matching >= 2;
        } else {
            return matching > minLength / 2;
        }
    }

    /**
     * Returns a list of words that match the list of character codes passed in.
     * This list will be overwritten the next time this function is called.
     * @param view a view for retrieving the context for AutoText
     * @param wordComposer contains what is currently being typed
     * @param prevWordForBigram previous word (used only for bigram)
     * @return list of suggestions.
     */
    public List<CharSequence> getSuggestions(View view, WordComposer wordComposer, 
            boolean includeTypedWordIfValid, CharSequence prevWordForBigram) {
        LatinImeLogger.onStartSuggestion(prevWordForBigram);
        mHaveCorrection = false;
        mIsFirstCharCapitalized = wordComposer.isFirstCharCapitalized();
        mIsAllUpperCase = wordComposer.isAllUpperCase();
        collectGarbage(mSuggestions, mPrefMaxSuggestions);
        Arrays.fill(mPriorities, 0);
        Arrays.fill(mNextLettersFrequencies, 0);

        // Save a lowercase version of the original word
        mOriginalWord = wordComposer.getTypedWord();
        if (mOriginalWord != null) {
            final String mOriginalWordString = mOriginalWord.toString();
            mOriginalWord = mOriginalWordString;
            mLowerOriginalWord = mOriginalWordString.toLowerCase();
            // Treating USER_TYPED as UNIGRAM suggestion for logging now.
            LatinImeLogger.onAddSuggestedWord(mOriginalWordString, Suggest.DIC_USER_TYPED,
                    Dictionary.DataType.UNIGRAM);
        } else {
            mLowerOriginalWord = "";
        }

        if (wordComposer.size() == 1 && (mCorrectionMode == CORRECTION_FULL_BIGRAM
                || mCorrectionMode == CORRECTION_BASIC)) {
            // At first character typed, search only the bigrams
            Arrays.fill(mBigramPriorities, 0);
            collectGarbage(mBigramSuggestions, PREF_MAX_BIGRAMS);

            if (!TextUtils.isEmpty(prevWordForBigram)) {
                CharSequence lowerPrevWord = prevWordForBigram.toString().toLowerCase();
                if (mMainDict.isValidWord(lowerPrevWord)) {
                    prevWordForBigram = lowerPrevWord;
                }
                /*if (mUserBigramDictionary != null) {
                    mUserBigramDictionary.getBigrams(wordComposer, prevWordForBigram, this,
                            mNextLettersFrequencies);
                }
                if (mContactsDictionary != null) {
                    mContactsDictionary.getBigrams(wordComposer, prevWordForBigram, this,
                            mNextLettersFrequencies);
                }*/
                if (mMainDict != null) {
                    mMainDict.getBigrams(wordComposer, prevWordForBigram, this,
                            mNextLettersFrequencies);
                }
                char currentChar = wordComposer.getTypedWord().charAt(0);
                // TODO: Must pay attention to locale when changing case.
                char currentCharUpper = Character.toUpperCase(currentChar);
                int count = 0;
                int bigramSuggestionSize = mBigramSuggestions.size();
                for (int i = 0; i < bigramSuggestionSize; i++) {
                    if (mBigramSuggestions.get(i).charAt(0) == currentChar
                            || mBigramSuggestions.get(i).charAt(0) == currentCharUpper) {
                        int poolSize = mStringPool.size();
                        StringBuilder sb = poolSize > 0 ?
                                (StringBuilder) mStringPool.remove(poolSize - 1)
                                : new StringBuilder(getApproxMaxWordLength());
                        sb.setLength(0);
                        sb.append(mBigramSuggestions.get(i));
                        mSuggestions.add(count++, sb);
                        if (count > mPrefMaxSuggestions) break;
                    }
                }
            }

        } else if (wordComposer.size() > 1) {
            // At second character typed, search the unigrams (scores being affected by bigrams)
            /*if (mUserDictionary != null || mContactsDictionary != null) {
                if (mUserDictionary != null) {
                    mUserDictionary.getWords(wordComposer, this, mNextLettersFrequencies);
                }
                if (mContactsDictionary != null) {
                    mContactsDictionary.getWords(wordComposer, this, mNextLettersFrequencies);
                }

                if (mSuggestions.size() > 0 && isValidWord(mOriginalWord)
                        && (mCorrectionMode == CORRECTION_FULL
                        || mCorrectionMode == CORRECTION_FULL_BIGRAM)) {
                    mHaveCorrection = true;
                }
            }*/
            mMainDict.getWords(wordComposer, this, mNextLettersFrequencies);
            if ((mCorrectionMode == CORRECTION_FULL || mCorrectionMode == CORRECTION_FULL_BIGRAM)
                    && mSuggestions.size() > 0) {
                mHaveCorrection = true;
            }
        }
        if (mOriginalWord != null) {
            mSuggestions.add(0, mOriginalWord.toString());
        }

        // Check if the first suggestion has a minimum number of characters in common
        if (wordComposer.size() > 1 && mSuggestions.size() > 1
                && (mCorrectionMode == CORRECTION_FULL
                || mCorrectionMode == CORRECTION_FULL_BIGRAM)) {
            if (!haveSufficientCommonality(mLowerOriginalWord, mSuggestions.get(1))) {
                mHaveCorrection = false;
            }
        }
        if (mAutoTextEnabled) {
            int i = 0;
            int max = 6;
            // Don't autotext the suggestions from the dictionaries
            if (mCorrectionMode == CORRECTION_BASIC) max = 1;
            while (i < mSuggestions.size() && i < max) {
                String suggestedWord = mSuggestions.get(i).toString().toLowerCase();
                CharSequence autoText =
                        AutoText.get(suggestedWord, 0, suggestedWord.length(), view);
                // Is there an AutoText correction?
                boolean canAdd = autoText != null;
                // Is that correction already the current prediction (or original word)?
                canAdd &= !TextUtils.equals(autoText, mSuggestions.get(i));
                // Is that correction already the next predicted word?
                if (canAdd && i + 1 < mSuggestions.size() && mCorrectionMode != CORRECTION_BASIC) {
                    canAdd &= !TextUtils.equals(autoText, mSuggestions.get(i + 1));
                }
                if (canAdd) {
                    mHaveCorrection = true;
                    mSuggestions.add(i + 1, autoText);
                    i++;
                }
                i++;
            }
        }
        removeDupes();
        return mSuggestions;
    }

    public int[] getNextLettersFrequencies() {
        return mNextLettersFrequencies;
    }

    private void removeDupes() {
        final ArrayList<CharSequence> suggestions = mSuggestions;
        if (suggestions.size() < 2) return;
        int i = 1;
        // Don't cache suggestions.size(), since we may be removing items
        while (i < suggestions.size()) {
            final CharSequence cur = suggestions.get(i);
            // Compare each candidate with each previous candidate
            for (int j = 0; j < i; j++) {
                CharSequence previous = suggestions.get(j);
                if (TextUtils.equals(cur, previous)) {
                    removeFromSuggestions(i);
                    i--;
                    break;
                }
            }
            i++;
        }
    }

    private void removeFromSuggestions(int index) {
        CharSequence garbage = mSuggestions.remove(index);
        if (garbage != null && garbage instanceof StringBuilder) {
            mStringPool.add(garbage);
        }
    }

    public boolean hasMinimalCorrection() {
        return mHaveCorrection;
    }

    private boolean compareCaseInsensitive(final String mLowerOriginalWord, 
            final char[] word, final int offset, final int length) {
        final int originalLength = mLowerOriginalWord.length();
        if (originalLength == length && Character.isUpperCase(word[offset])) {
            for (int i = 0; i < originalLength; i++) {
                if (mLowerOriginalWord.charAt(i) != Character.toLowerCase(word[offset+i])) {
                    return false;
                }
            }
            return true;
        }
        return false;
    }

    public boolean addWord(final char[] word, final int offset, final int length, int freq,
            final int dicTypeId, final Dictionary.DataType dataType) {
        Dictionary.DataType dataTypeForLog = dataType;
        ArrayList<CharSequence> suggestions;
        int[] priorities;
        int prefMaxSuggestions;
        if(dataType == Dictionary.DataType.BIGRAM) {
            suggestions = mBigramSuggestions;
            priorities = mBigramPriorities;
            prefMaxSuggestions = PREF_MAX_BIGRAMS;
        } else {
            suggestions = mSuggestions;
            priorities = mPriorities;
            prefMaxSuggestions = mPrefMaxSuggestions;
        }

        int pos = 0;

        // Check if it's the same word, only caps are different
        if (compareCaseInsensitive(mLowerOriginalWord, word, offset, length)) {
            pos = 0;
        } else {
            if (dataType == Dictionary.DataType.UNIGRAM) {
                // Check if the word was already added before (by bigram data)
                int bigramSuggestion = searchBigramSuggestion(word,offset,length);
                if(bigramSuggestion >= 0) {
                    dataTypeForLog = Dictionary.DataType.BIGRAM;
                    // turn freq from bigram into multiplier specified above
                    double multiplier = (((double) mBigramPriorities[bigramSuggestion])
                            / MAXIMUM_BIGRAM_FREQUENCY)
                            * (BIGRAM_MULTIPLIER_MAX - BIGRAM_MULTIPLIER_MIN)
                            + BIGRAM_MULTIPLIER_MIN;
                    /* Log.d(TAG,"bigram num: " + bigramSuggestion
                            + "  wordB: " + mBigramSuggestions.get(bigramSuggestion).toString()
                            + "  currentPriority: " + freq + "  bigramPriority: "
                            + mBigramPriorities[bigramSuggestion]
                            + "  multiplier: " + multiplier); */
                    freq = (int)Math.round((freq * multiplier));
                }
            }

            // Check the last one's priority and bail
            if (priorities[prefMaxSuggestions - 1] >= freq) return true;
            while (pos < prefMaxSuggestions) {
                if (priorities[pos] < freq
                        || (priorities[pos] == freq && length < suggestions.get(pos).length())) {
                    break;
                }
                pos++;
            }
        }
        if (pos >= prefMaxSuggestions) {
            return true;
        }

        System.arraycopy(priorities, pos, priorities, pos + 1,
                prefMaxSuggestions - pos - 1);
        priorities[pos] = freq;
        int poolSize = mStringPool.size();
        StringBuilder sb = poolSize > 0 ? (StringBuilder) mStringPool.remove(poolSize - 1) 
                : new StringBuilder(getApproxMaxWordLength());
        sb.setLength(0);
        // TODO: Must pay attention to locale when changing case.
        if (mIsAllUpperCase) {
            sb.append(new String(word, offset, length).toUpperCase());
        } else if (mIsFirstCharCapitalized) {
            sb.append(Character.toUpperCase(word[offset]));
            if (length > 1) {
                sb.append(word, offset + 1, length - 1);
            }
        } else {
            sb.append(word, offset, length);
        }
        suggestions.add(pos, sb);
        if (suggestions.size() > prefMaxSuggestions) {
            CharSequence garbage = suggestions.remove(prefMaxSuggestions);
            if (garbage instanceof StringBuilder) {
                mStringPool.add(garbage);
            }
        } else {
            LatinImeLogger.onAddSuggestedWord(sb.toString(), dicTypeId, dataTypeForLog);
        }
        return true;
    }

    private int searchBigramSuggestion(final char[] word, final int offset, final int length) {
        // TODO This is almost O(n^2). Might need fix.
        // search whether the word appeared in bigram data
        int bigramSuggestSize = mBigramSuggestions.size();
        for(int i = 0; i < bigramSuggestSize; i++) {
            if(mBigramSuggestions.get(i).length() == length) {
                boolean chk = true;
                for(int j = 0; j < length; j++) {
                    if(mBigramSuggestions.get(i).charAt(j) != word[offset+j]) {
                        chk = false;
                        break;
                    }
                }
                if(chk) return i;
            }
        }

        return -1;
    }

    public boolean isValidWord(final CharSequence word) {
        if (word == null || word.length() == 0) {
            return false;
        }
        return mMainDict.isValidWord(word)
                /*|| (mUserDictionary != null && mUserDictionary.isValidWord(word))
                || (mAutoDictionary != null && mAutoDictionary.isValidWord(word))
                || (mContactsDictionary != null && mContactsDictionary.isValidWord(word))*/;
    }
    
    private void collectGarbage(ArrayList<CharSequence> suggestions, int prefMaxSuggestions) {
        int poolSize = mStringPool.size();
        int garbageSize = suggestions.size();
        while (poolSize < prefMaxSuggestions && garbageSize > 0) {
            CharSequence garbage = suggestions.get(garbageSize - 1);
            if (garbage != null && garbage instanceof StringBuilder) {
                mStringPool.add(garbage);
                poolSize++;
            }
            garbageSize--;
        }
        if (poolSize == prefMaxSuggestions + 1) {
            Log.w("Suggest", "String pool got too big: " + poolSize);
        }
        suggestions.clear();
    }

    public void close() {
        if (mMainDict != null) {
            mMainDict.close();
        }
    }
}

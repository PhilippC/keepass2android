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

import java.io.InputStream;
import java.io.IOException;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.nio.channels.Channels;
import java.util.Arrays;

import android.content.Context;
import android.util.Log;

/**
 * Implements a static, compacted, binary dictionary of standard words.
 */
public class BinaryDictionary extends Dictionary {

    /**
     * There is difference between what java and native code can handle.
     * This value should only be used in BinaryDictionary.java
     * It is necessary to keep it at this value because some languages e.g. German have
     * really long words.
     */
    protected static final int MAX_WORD_LENGTH = 48;

    private static final String TAG = "BinaryDictionary";
    private static final int MAX_ALTERNATIVES = 16;
    private static final int MAX_WORDS = 18;
    private static final int MAX_BIGRAMS = 60;

    private static final int TYPED_LETTER_MULTIPLIER = 2;
    private static final boolean ENABLE_MISSED_CHARACTERS = true;

    private int mDicTypeId;
    private int mNativeDict;
    private int mDictLength;
    private int[] mInputCodes = new int[MAX_WORD_LENGTH * MAX_ALTERNATIVES];
    private char[] mOutputChars = new char[MAX_WORD_LENGTH * MAX_WORDS];
    private char[] mOutputChars_bigrams = new char[MAX_WORD_LENGTH * MAX_BIGRAMS];
    private int[] mFrequencies = new int[MAX_WORDS];
    private int[] mFrequencies_bigrams = new int[MAX_BIGRAMS];
    // Keep a reference to the native dict direct buffer in Java to avoid
    // unexpected deallocation of the direct buffer.
    private ByteBuffer mNativeDictDirectBuffer;

    static {
        try {
            System.loadLibrary("jni_latinime");
        } catch (UnsatisfiedLinkError ule) {
            Log.e("BinaryDictionary", "Could not load native library jni_latinime");
        }
    }

    /**
     * Create a dictionary from a raw resource file
     * @param context application context for reading resources
     * @param resId the resource containing the raw binary dictionary
     */
    public BinaryDictionary(Context context, int[] resId, int dicTypeId) {
        if (resId != null && resId.length > 0 && resId[0] != 0) {
        	loadDictionary(context, resId);
        }
        mDicTypeId = dicTypeId;
    }
    
    /**
     * Create a dictionary from input streams
     * @param context application context for reading resources
     * @param streams the resource streams containing the raw binary dictionary
     */
    public BinaryDictionary(Context context, InputStream[] streams, int dicTypeId) {
        if (streams != null && streams.length > 0) {
            loadDictionary(context, streams);
        }
        mDicTypeId = dicTypeId;
    }
    
    
    /**
     * Create a dictionary from a byte buffer. This is used for testing.
     * @param context application context for reading resources
     * @param byteBuffer a ByteBuffer containing the binary dictionary
     */
    public BinaryDictionary(Context context, ByteBuffer byteBuffer, int dicTypeId) {
        if (byteBuffer != null) {
            if (byteBuffer.isDirect()) {
                mNativeDictDirectBuffer = byteBuffer;
            } else {
                mNativeDictDirectBuffer = ByteBuffer.allocateDirect(byteBuffer.capacity());
                byteBuffer.rewind();
                mNativeDictDirectBuffer.put(byteBuffer);
            }
            mDictLength = byteBuffer.capacity();
            mNativeDict = openNative(mNativeDictDirectBuffer,
                    TYPED_LETTER_MULTIPLIER, FULL_WORD_FREQ_MULTIPLIER);
        }
        mDicTypeId = dicTypeId;
    }

    private native int openNative(ByteBuffer bb, int typedLetterMultiplier,
            int fullWordMultiplier);
    private native void closeNative(int dict);
    private native boolean isValidWordNative(int nativeData, char[] word, int wordLength);
    private native int getSuggestionsNative(int dict, int[] inputCodes, int codesSize, 
            char[] outputChars, int[] frequencies, int maxWordLength, int maxWords,
            int maxAlternatives, int skipPos, int[] nextLettersFrequencies, int nextLettersSize);
    private native int getBigramsNative(int dict, char[] prevWord, int prevWordLength,
            int[] inputCodes, int inputCodesLength, char[] outputChars, int[] frequencies,
            int maxWordLength, int maxBigrams, int maxAlternatives);

    private final void loadDictionary(Context context, int[] resId) {
        InputStream[] is = null;
        try {
            // merging separated dictionary into one if dictionary is separated
            is = new InputStream[resId.length];
            for (int i = 0; i < resId.length; i++) {
                is[i] = context.getResources().openRawResource(resId[i]);
            }
            loadDictionary(context, is);
            
            
        } finally {
            try {
                if (is != null) {
                    for (int i = 0; i < is.length; i++) {
                        is[i].close();
                    }
                }
            } catch (IOException e) {
                Log.w(TAG, "Failed to close input stream");
            }
        }
    }


    private void loadDictionary(Context context, InputStream[] is) 
    {
    	try
    	{
	    	int total = 0;
	        for (int i = 0; i < is.length; i++)
	        	total += is[i].available();
	
	        mNativeDictDirectBuffer =
	            ByteBuffer.allocateDirect(total).order(ByteOrder.nativeOrder());
	        int got = 0;
	        for (int i = 0; i < is.length; i++) {
	             got += Channels.newChannel(is[i]).read(mNativeDictDirectBuffer);
	        }
	        if (got != total) {
	            Log.e(TAG, "Read " + got + " bytes, expected " + total);
	        } else {
	            mNativeDict = openNative(mNativeDictDirectBuffer,
	                    TYPED_LETTER_MULTIPLIER, FULL_WORD_FREQ_MULTIPLIER);
	            mDictLength = total;
	        }	
	 
    	}
    	catch (IOException e) {
            Log.w(TAG, "No available memory for binary dictionary");
        } catch (UnsatisfiedLinkError e) {
            Log.w(TAG, "Failed to load native dictionary", e);
        } finally {
            try {
                if (is != null) {
                    for (int i = 0; i < is.length; i++) {
                        is[i].close();
                    }
                }
            } catch (IOException e) {
                Log.w(TAG, "Failed to close input stream");
            }
        }
    	       
	}

	@Override
    public void getBigrams(final WordComposer codes, final CharSequence previousWord,
            final WordCallback callback, int[] nextLettersFrequencies) {

        char[] chars = previousWord.toString().toCharArray();
        Arrays.fill(mOutputChars_bigrams, (char) 0);
        Arrays.fill(mFrequencies_bigrams, 0);

        int codesSize = codes.size();
        Arrays.fill(mInputCodes, -1);
        int[] alternatives = codes.getCodesAt(0);
        System.arraycopy(alternatives, 0, mInputCodes, 0,
                Math.min(alternatives.length, MAX_ALTERNATIVES));

        int count = getBigramsNative(mNativeDict, chars, chars.length, mInputCodes, codesSize,
                mOutputChars_bigrams, mFrequencies_bigrams, MAX_WORD_LENGTH, MAX_BIGRAMS,
                MAX_ALTERNATIVES);

        for (int j = 0; j < count; j++) {
            if (mFrequencies_bigrams[j] < 1) break;
            int start = j * MAX_WORD_LENGTH;
            int len = 0;
            while (mOutputChars_bigrams[start + len] != 0) {
                len++;
            }
            if (len > 0) {
                callback.addWord(mOutputChars_bigrams, start, len, mFrequencies_bigrams[j],
                        mDicTypeId, DataType.BIGRAM);
            }
        }
    }

    @Override
    public void getWords(final WordComposer codes, final WordCallback callback,
            int[] nextLettersFrequencies) {
        final int codesSize = codes.size();
        // Won't deal with really long words.
        if (codesSize > MAX_WORD_LENGTH - 1) return;
        
        Arrays.fill(mInputCodes, -1);
        for (int i = 0; i < codesSize; i++) {
            int[] alternatives = codes.getCodesAt(i);
            System.arraycopy(alternatives, 0, mInputCodes, i * MAX_ALTERNATIVES,
                    Math.min(alternatives.length, MAX_ALTERNATIVES));
        }
        Arrays.fill(mOutputChars, (char) 0);
        Arrays.fill(mFrequencies, 0);

        int count = getSuggestionsNative(mNativeDict, mInputCodes, codesSize,
                mOutputChars, mFrequencies,
                MAX_WORD_LENGTH, MAX_WORDS, MAX_ALTERNATIVES, -1,
                nextLettersFrequencies,
                nextLettersFrequencies != null ? nextLettersFrequencies.length : 0);

        // If there aren't sufficient suggestions, search for words by allowing wild cards at
        // the different character positions. This feature is not ready for prime-time as we need
        // to figure out the best ranking for such words compared to proximity corrections and
        // completions.
        if (ENABLE_MISSED_CHARACTERS && count < 5) {
            for (int skip = 0; skip < codesSize; skip++) {
                int tempCount = getSuggestionsNative(mNativeDict, mInputCodes, codesSize,
                        mOutputChars, mFrequencies,
                        MAX_WORD_LENGTH, MAX_WORDS, MAX_ALTERNATIVES, skip,
                        null, 0);
                count = Math.max(count, tempCount);
                if (tempCount > 0) break;
            }
        }

        for (int j = 0; j < count; j++) {
            if (mFrequencies[j] < 1) break;
            int start = j * MAX_WORD_LENGTH;
            int len = 0;
            while (mOutputChars[start + len] != 0) {
                len++;
            }
            if (len > 0) {
                callback.addWord(mOutputChars, start, len, mFrequencies[j], mDicTypeId,
                        DataType.UNIGRAM);
            }
        }
    }

    @Override
    public boolean isValidWord(CharSequence word) {
        if (word == null) return false;
        char[] chars = word.toString().toCharArray();
        return isValidWordNative(mNativeDict, chars, chars.length);
    }

    public int getSize() {
        return mDictLength; // This value is initialized on the call to openNative()
    }

    @Override
    public synchronized void close() {
        if (mNativeDict != 0) {
            closeNative(mNativeDict);
            mNativeDict = 0;
        }
    }

    @Override
    protected void finalize() throws Throwable {
        close();
        super.finalize();
    }
}

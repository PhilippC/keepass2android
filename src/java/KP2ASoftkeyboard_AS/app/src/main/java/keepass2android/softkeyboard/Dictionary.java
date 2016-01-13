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

/**
 * Abstract base class for a dictionary that can do a fuzzy search for words based on a set of key
 * strokes.
 */
abstract public class Dictionary {
    /**
     * Whether or not to replicate the typed word in the suggested list, even if it's valid.
     */
    protected static final boolean INCLUDE_TYPED_WORD_IF_VALID = false;
    
    /**
     * The weight to give to a word if it's length is the same as the number of typed characters.
     */
    protected static final int FULL_WORD_FREQ_MULTIPLIER = 2;

    public static enum DataType {
        UNIGRAM, BIGRAM
    }

    /**
     * Interface to be implemented by classes requesting words to be fetched from the dictionary.
     * @see #getWords(WordComposer, WordCallback)
     */
    public interface WordCallback {
        /**
         * Adds a word to a list of suggestions. The word is expected to be ordered based on
         * the provided frequency. 
         * @param word the character array containing the word
         * @param wordOffset starting offset of the word in the character array
         * @param wordLength length of valid characters in the character array
         * @param frequency the frequency of occurence. This is normalized between 1 and 255, but
         * can exceed those limits
         * @param dicTypeId of the dictionary where word was from
         * @param dataType tells type of this data
         * @return true if the word was added, false if no more words are required
         */
        boolean addWord(char[] word, int wordOffset, int wordLength, int frequency, int dicTypeId,
                DataType dataType);
    }

    /**
     * Searches for words in the dictionary that match the characters in the composer. Matched 
     * words are added through the callback object.
     * @param composer the key sequence to match
     * @param callback the callback object to send matched words to as possible candidates
     * @param nextLettersFrequencies array of frequencies of next letters that could follow the
     *        word so far. For instance, "bracke" can be followed by "t", so array['t'] will have
     *        a non-zero value on returning from this method. 
     *        Pass in null if you don't want the dictionary to look up next letters.
     * @see WordCallback#addWord(char[], int, int)
     */
    abstract public void getWords(final WordComposer composer, final WordCallback callback,
            int[] nextLettersFrequencies);

    /**
     * Searches for pairs in the bigram dictionary that matches the previous word and all the
     * possible words following are added through the callback object.
     * @param composer the key sequence to match
     * @param callback the callback object to send possible word following previous word
     * @param nextLettersFrequencies array of frequencies of next letters that could follow the
     *        word so far. For instance, "bracke" can be followed by "t", so array['t'] will have
     *        a non-zero value on returning from this method.
     *        Pass in null if you don't want the dictionary to look up next letters.
     */
    public void getBigrams(final WordComposer composer, final CharSequence previousWord,
            final WordCallback callback, int[] nextLettersFrequencies) {
        // empty base implementation
    }

    /**
     * Checks if the given word occurs in the dictionary
     * @param word the word to search for. The search should be case-insensitive.
     * @return true if the word exists, false otherwise
     */
    abstract public boolean isValidWord(CharSequence word);
    
    /**
     * Compares the contents of the character array with the typed word and returns true if they
     * are the same.
     * @param word the array of characters that make up the word
     * @param length the number of valid characters in the character array
     * @param typedWord the word to compare with
     * @return true if they are the same, false otherwise.
     */
    protected boolean same(final char[] word, final int length, final CharSequence typedWord) {
        if (typedWord.length() != length) {
            return false;
        }
        for (int i = 0; i < length; i++) {
            if (word[i] != typedWord.charAt(i)) {
                return false;
            }
        }
        return true;
    }

    /**
     * Override to clean up any resources.
     */
    public void close() {
    }
}

/*
 * Copyright (C) 2009 The Android Open Source Project
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

package keepass2android.softkeyboard;

import android.content.ContentResolver;
import android.content.Context;
import android.database.ContentObserver;
import android.database.Cursor;
import android.os.SystemClock;
import android.provider.ContactsContract.Contacts;
import android.text.TextUtils;
import android.util.Log;

public class ContactsDictionary extends ExpandableDictionary {

    private static final String[] PROJECTION = {
        Contacts._ID,
        Contacts.DISPLAY_NAME,
    };

    private static final String TAG = "ContactsDictionary";

    /**
     * Frequency for contacts information into the dictionary
     */
    private static final int FREQUENCY_FOR_CONTACTS = 128;
    private static final int FREQUENCY_FOR_CONTACTS_BIGRAM = 90;

    private static final int INDEX_NAME = 1;

    private ContentObserver mObserver;

    private long mLastLoadedContacts;

    public ContactsDictionary(Context context, int dicTypeId) {
        super(context, dicTypeId);
        // Perform a managed query. The Activity will handle closing and requerying the cursor
        // when needed.
        ContentResolver cres = context.getContentResolver();

        cres.registerContentObserver(
                Contacts.CONTENT_URI, true,mObserver = new ContentObserver(null) {
                    @Override
                    public void onChange(boolean self) {
                        setRequiresReload(true);
                    }
                });
        loadDictionary();
    }

    @Override
    public synchronized void close() {
        if (mObserver != null) {
            getContext().getContentResolver().unregisterContentObserver(mObserver);
            mObserver = null;
        }
        super.close();
    }

    @Override
    public void startDictionaryLoadingTaskLocked() {
        long now = SystemClock.uptimeMillis();
        if (mLastLoadedContacts == 0
                || now - mLastLoadedContacts > 30 * 60 * 1000 /* 30 minutes */) {
            super.startDictionaryLoadingTaskLocked();
        }
    }

    @Override
    public void loadDictionaryAsync() {
        /*try {
            Cursor cursor = getContext().getContentResolver()
                    .query(Contacts.CONTENT_URI, PROJECTION, null, null, null);
            if (cursor != null) {
                addWords(cursor);
            }
        } catch(IllegalStateException e) {
            Log.e(TAG, "Contacts DB is having problems");
        }
        mLastLoadedContacts = SystemClock.uptimeMillis();*/
    }

    private void addWords(Cursor cursor) {
        clearDictionary();

        final int maxWordLength = getMaxWordLength();
        try {
            if (cursor.moveToFirst()) {
                while (!cursor.isAfterLast()) {
                    String name = cursor.getString(INDEX_NAME);

                    if (name != null) {
                        int len = name.length();
                        String prevWord = null;

                        // TODO: Better tokenization for non-Latin writing systems
                        for (int i = 0; i < len; i++) {
                            if (Character.isLetter(name.charAt(i))) {
                                int j;
                                for (j = i + 1; j < len; j++) {
                                    char c = name.charAt(j);

                                    if (!(c == '-' || c == '\'' ||
                                          Character.isLetter(c))) {
                                        break;
                                    }
                                }

                                String word = name.substring(i, j);
                                i = j - 1;

                                // Safeguard against adding really long words. Stack
                                // may overflow due to recursion
                                // Also don't add single letter words, possibly confuses
                                // capitalization of i.
                                final int wordLen = word.length();
                                if (wordLen < maxWordLength && wordLen > 1) {
                                    super.addWord(word, FREQUENCY_FOR_CONTACTS);
                                    if (!TextUtils.isEmpty(prevWord)) {
                                        // TODO Do not add email address
                                        // Not so critical
                                        super.setBigram(prevWord, word,
                                                FREQUENCY_FOR_CONTACTS_BIGRAM);
                                    }
                                    prevWord = word;
                                }
                            }
                        }
                    }
                    cursor.moveToNext();
                }
            }
            cursor.close();
        } catch(IllegalStateException e) {
            Log.e(TAG, "Contacts DB is having problems");
        }
    }
}

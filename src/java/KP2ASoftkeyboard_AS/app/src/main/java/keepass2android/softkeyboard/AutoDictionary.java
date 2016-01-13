/*
 * Copyright (C) 2010 Google Inc.
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

import java.util.HashMap;
import java.util.Set;
import java.util.Map.Entry;

import android.content.ContentValues;
import android.content.Context;
import android.database.Cursor;
import android.database.sqlite.SQLiteDatabase;
import android.database.sqlite.SQLiteOpenHelper;
import android.database.sqlite.SQLiteQueryBuilder;
import android.os.AsyncTask;
import android.provider.BaseColumns;
import android.util.Log;

/**
 * Stores new words temporarily until they are promoted to the user dictionary
 * for longevity. Words in the auto dictionary are used to determine if it's ok
 * to accept a word that's not in the main or user dictionary. Using a new word
 * repeatedly will promote it to the user dictionary.
 */
public class AutoDictionary extends ExpandableDictionary {
    // Weight added to a user picking a new word from the suggestion strip
    static final int FREQUENCY_FOR_PICKED = 3;
    // Weight added to a user typing a new word that doesn't get corrected (or is reverted)
    static final int FREQUENCY_FOR_TYPED = 1;
    // A word that is frequently typed and gets promoted to the user dictionary, uses this
    // frequency.
    static final int FREQUENCY_FOR_AUTO_ADD = 250;
    // If the user touches a typed word 2 times or more, it will become valid.
    private static final int VALIDITY_THRESHOLD = 2 * FREQUENCY_FOR_PICKED;
    // If the user touches a typed word 4 times or more, it will be added to the user dict.
    private static final int PROMOTION_THRESHOLD = 4 * FREQUENCY_FOR_PICKED;

    private KP2AKeyboard mIme;
    // Locale for which this auto dictionary is storing words
    private String mLocale;

    private HashMap<String,Integer> mPendingWrites = new HashMap<String,Integer>();
    private final Object mPendingWritesLock = new Object();

    private static final String DATABASE_NAME = "auto_dict.db";
    private static final int DATABASE_VERSION = 1;

    // These are the columns in the dictionary
    // TODO: Consume less space by using a unique id for locale instead of the whole
    // 2-5 character string.
    private static final String COLUMN_ID = BaseColumns._ID;
    private static final String COLUMN_WORD = "word";
    private static final String COLUMN_FREQUENCY = "freq";
    private static final String COLUMN_LOCALE = "locale";

    /** Sort by descending order of frequency. */
    public static final String DEFAULT_SORT_ORDER = COLUMN_FREQUENCY + " DESC";

    /** Name of the words table in the auto_dict.db */
    private static final String AUTODICT_TABLE_NAME = "words";

    private static HashMap<String, String> sDictProjectionMap;

    static {
        sDictProjectionMap = new HashMap<String, String>();
        sDictProjectionMap.put(COLUMN_ID, COLUMN_ID);
        sDictProjectionMap.put(COLUMN_WORD, COLUMN_WORD);
        sDictProjectionMap.put(COLUMN_FREQUENCY, COLUMN_FREQUENCY);
        sDictProjectionMap.put(COLUMN_LOCALE, COLUMN_LOCALE);
    }

    private static DatabaseHelper sOpenHelper = null;

    public AutoDictionary(Context context, KP2AKeyboard ime, String locale, int dicTypeId) {
        super(context, dicTypeId);
        mIme = ime;
        mLocale = locale;
        if (sOpenHelper == null) {
            sOpenHelper = new DatabaseHelper(getContext());
        }
        if (mLocale != null && mLocale.length() > 1) {
            loadDictionary();
        }
    }

    @Override
    public boolean isValidWord(CharSequence word) {
        final int frequency = getWordFrequency(word);
        return frequency >= VALIDITY_THRESHOLD;
    }

    @Override
    public void close() {
        flushPendingWrites();
        // Don't close the database as locale changes will require it to be reopened anyway
        // Also, the database is written to somewhat frequently, so it needs to be kept alive
        // throughout the life of the process.
        // mOpenHelper.close();
        super.close();
    }

    @Override
    public void loadDictionaryAsync() {
        // Load the words that correspond to the current input locale
        Cursor cursor = query(COLUMN_LOCALE + "=?", new String[] { mLocale });
        try {
            if (cursor.moveToFirst()) {
                int wordIndex = cursor.getColumnIndex(COLUMN_WORD);
                int frequencyIndex = cursor.getColumnIndex(COLUMN_FREQUENCY);
                while (!cursor.isAfterLast()) {
                    String word = cursor.getString(wordIndex);
                    int frequency = cursor.getInt(frequencyIndex);
                    // Safeguard against adding really long words. Stack may overflow due
                    // to recursive lookup
                    if (word.length() < getMaxWordLength()) {
                        super.addWord(word, frequency);
                    }
                    cursor.moveToNext();
                }
            }
        } finally {
            cursor.close();
        }
    }

    @Override
    public void addWord(String word, int addFrequency) {
        final int length = word.length();
        // Don't add very short or very long words.
        if (length < 2 || length > getMaxWordLength()) return;
        if (mIme.getCurrentWord().isAutoCapitalized()) {
            // Remove caps before adding
            word = Character.toLowerCase(word.charAt(0)) + word.substring(1);
        }
        int freq = getWordFrequency(word);
        freq = freq < 0 ? addFrequency : freq + addFrequency;
        super.addWord(word, freq);

        if (freq >= PROMOTION_THRESHOLD) {
            mIme.promoteToUserDictionary(word, FREQUENCY_FOR_AUTO_ADD);
            freq = 0;
        }

        synchronized (mPendingWritesLock) {
            // Write a null frequency if it is to be deleted from the db
            mPendingWrites.put(word, freq == 0 ? null : new Integer(freq));
        }
    }

    /**
     * Schedules a background thread to write any pending words to the database.
     */
    public void flushPendingWrites() {
        synchronized (mPendingWritesLock) {
            // Nothing pending? Return
            if (mPendingWrites.isEmpty()) return;
            // Create a background thread to write the pending entries
            new UpdateDbTask(getContext(), sOpenHelper, mPendingWrites, mLocale).execute();
            // Create a new map for writing new entries into while the old one is written to db
            mPendingWrites = new HashMap<String, Integer>();
        }
    }

    /**
     * This class helps open, create, and upgrade the database file.
     */
    private static class DatabaseHelper extends SQLiteOpenHelper {

        DatabaseHelper(Context context) {
            super(context, DATABASE_NAME, null, DATABASE_VERSION);
        }

        @Override
        public void onCreate(SQLiteDatabase db) {
            db.execSQL("CREATE TABLE " + AUTODICT_TABLE_NAME + " ("
                    + COLUMN_ID + " INTEGER PRIMARY KEY,"
                    + COLUMN_WORD + " TEXT,"
                    + COLUMN_FREQUENCY + " INTEGER,"
                    + COLUMN_LOCALE + " TEXT"
                    + ");");
        }

        @Override
        public void onUpgrade(SQLiteDatabase db, int oldVersion, int newVersion) {
            Log.w("AutoDictionary", "Upgrading database from version " + oldVersion + " to "
                    + newVersion + ", which will destroy all old data");
            db.execSQL("DROP TABLE IF EXISTS " + AUTODICT_TABLE_NAME);
            onCreate(db);
        }
    }

    private Cursor query(String selection, String[] selectionArgs) {
        SQLiteQueryBuilder qb = new SQLiteQueryBuilder();
        qb.setTables(AUTODICT_TABLE_NAME);
        qb.setProjectionMap(sDictProjectionMap);

        // Get the database and run the query
        SQLiteDatabase db = sOpenHelper.getReadableDatabase();
        Cursor c = qb.query(db, null, selection, selectionArgs, null, null,
                DEFAULT_SORT_ORDER);
        return c;
    }

    /**
     * Async task to write pending words to the database so that it stays in sync with
     * the in-memory trie.
     */
    private static class UpdateDbTask extends AsyncTask<Void, Void, Void> {
        private final HashMap<String, Integer> mMap;
        private final DatabaseHelper mDbHelper;
        private final String mLocale;

        public UpdateDbTask(Context context, DatabaseHelper openHelper,
                HashMap<String, Integer> pendingWrites, String locale) {
            mMap = pendingWrites;
            mLocale = locale;
            mDbHelper = openHelper;
        }

        @Override
        protected Void doInBackground(Void... v) {
            SQLiteDatabase db = mDbHelper.getWritableDatabase();
            // Write all the entries to the db
            Set<Entry<String,Integer>> mEntries = mMap.entrySet();
            for (Entry<String,Integer> entry : mEntries) {
                Integer freq = entry.getValue();
                db.delete(AUTODICT_TABLE_NAME, COLUMN_WORD + "=? AND " + COLUMN_LOCALE + "=?",
                        new String[] { entry.getKey(), mLocale });
                if (freq != null) {
                    db.insert(AUTODICT_TABLE_NAME, null,
                            getContentValues(entry.getKey(), freq, mLocale));
                }
            }
            return null;
        }

        private ContentValues getContentValues(String word, int frequency, String locale) {
            ContentValues values = new ContentValues(4);
            values.put(COLUMN_WORD, word);
            values.put(COLUMN_FREQUENCY, frequency);
            values.put(COLUMN_LOCALE, locale);
            return values;
        }
    }
}

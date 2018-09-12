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

import android.app.AlertDialog;
import android.content.ActivityNotFoundException;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.SharedPreferences;
import android.content.SharedPreferences.Editor;
import android.content.res.Configuration;
import android.content.res.Resources;
import android.content.res.XmlResourceParser;
import android.graphics.drawable.BitmapDrawable;
import android.inputmethodservice.InputMethodService;
import android.inputmethodservice.Keyboard;
import android.media.AudioManager;
import android.net.Uri;
import android.os.Build;
import android.os.Debug;
import android.os.Handler;
import android.os.Message;
import android.os.SystemClock;
import android.preference.PreferenceActivity;
import android.preference.PreferenceManager;
import android.provider.Settings;
import android.text.ClipboardManager;
import android.text.TextUtils;
import android.util.DisplayMetrics;
import android.util.Log;
import android.util.PrintWriterPrinter;
import android.util.Printer;
import android.view.Gravity;
import android.view.HapticFeedbackConstants;
import android.view.KeyEvent;
import android.view.View;
import android.view.Window;
import android.view.WindowManager;
import android.view.inputmethod.CompletionInfo;
import android.view.inputmethod.EditorInfo;
import android.view.inputmethod.ExtractedText;
import android.view.inputmethod.ExtractedTextRequest;
import android.view.inputmethod.InputConnection;
import android.view.inputmethod.InputMethodManager;
import android.widget.LinearLayout;
import android.widget.PopupWindow;

import keepass2android.kbbridge.KeyboardData;
import keepass2android.kbbridge.StringForTyping;
import keepass2android.softkeyboard.LatinIMEUtil.RingCharBuffer;

import org.xmlpull.v1.XmlPullParserException;

import java.io.FileDescriptor;
import java.io.IOException;
import java.io.PrintWriter;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Objects;

/**
 * Input method implementation for Qwerty'ish keyboard.
 */
public class KP2AKeyboard extends InputMethodService
        implements LatinKeyboardBaseView.OnKeyboardActionListener,
        IKeyboardService,
        SharedPreferences.OnSharedPreferenceChangeListener {

    public static IKeyboardService CurrentlyRunningService;

    private static String get_KEEPASS2ANDROID_KEYBOARD_CLEARED(Context ctx)
    {
    	return ctx.getPackageName()+".keyboard_cleared";
    }
	private static final String KP2A_SAVED_FIELD_HINTS = "savedFieldHints";
	private static final String PREF_KP2A_REMEMBER_AUTO_FILL = "kp2a_remember_auto_fill";
	private static final String TAG = "LatinIME";
    private static final boolean PERF_DEBUG = false;
    static final boolean DEBUG = false;
    static final boolean TRACE = false;

    private static final String PREF_VIBRATE_ON = "vibrate_on";
    private static final String PREF_SOUND_ON = "sound_on";
    private static final String PREF_POPUP_ON = "popup_on";
    private static final String PREF_AUTO_CAP = "auto_cap";
    private static final String PREF_QUICK_FIXES = "quick_fixes";
    private static final String PREF_SHOW_SUGGESTIONS = "show_suggestions";
    private static final String PREF_AUTO_COMPLETE = "auto_complete";
    //private static final String PREF_BIGRAM_SUGGESTIONS = "bigram_suggestion";


    public static final String PREF_SELECTED_LANGUAGES = "selected_languages";
    public static final String PREF_INPUT_LANGUAGE = "input_language";
    private static final String PREF_RECORRECTION_ENABLED = "recorrection_enabled";

    private static final int MSG_UPDATE_SUGGESTIONS = 0;
    private static final int MSG_UPDATE_SHIFT_STATE = 2;

    private static final int MSG_UPDATE_OLD_SUGGESTIONS = 4;

    // How many continuous deletes at which to start deleting at a higher speed.
    private static final int DELETE_ACCELERATE_AT = 20;
    // Key events coming any faster than this are long-presses.
    private static final int QUICK_PRESS = 200;

    static final int KEYCODE_ENTER = '\n';
    static final int KEYCODE_SPACE = ' ';
    static final int KEYCODE_PERIOD = '.';

    // Contextual menu positions
    private static final int POS_METHOD = 0;
    private static final int POS_SETTINGS = 1;

    //private LatinKeyboardView mInputView;
    private LinearLayout mCandidateViewContainer;
    private CandidateView mCandidateView;
    private Suggest mSuggest;
    private CompletionInfo[] mCompletions;

    private AlertDialog mOptionsDialog;
    
    /* package */ KeyboardSwitcher mKeyboardSwitcher;

    //dictionaries disabled for KP2A to reduce permissions
    //private UserDictionary mUserDictionary;
    //private UserBigramDictionary mUserBigramDictionary;
    //private ContactsDictionary mContactsDictionary;
    //private AutoDictionary mAutoDictionary;

    private Resources mResources;

    private String mInputLocale;
    private String mSystemLocale;
    private LanguageSwitcher mLanguageSwitcher;

    private StringBuilder mComposing = new StringBuilder();
    private WordComposer mWord = new WordComposer();
    private int mCommittedLength;
    private boolean mPredicting;
    private boolean mRecognizing;
    private CharSequence mBestWord;
    private boolean mPredictionOn;
    private boolean mCompletionOn;
    private boolean mHasDictionary;
    private boolean mAutoSpace;
    private boolean mJustAddedAutoSpace;
    private boolean mAutoCorrectEnabled;
    private boolean mReCorrectionEnabled;
    // Bigram Suggestion is disabled in this version.
    private final boolean mBigramSuggestionEnabled = false;
    private boolean mAutoCorrectOn;
    // TODO move this state variable outside LatinIME
    private boolean mCapsLock;
    private boolean mVibrateOn;
    private boolean mSoundOn;
    private boolean mPopupOn;
    private boolean mAutoCap;
    private boolean mQuickFixes;
    private boolean mKp2aAutoFillOn;
    private boolean mKp2aRememberAutoFill;
    private boolean mKp2aEnableSimpleKeyboard;
    private boolean mKp2aSwitchKeyboardOnSendGoDone;
    private boolean mKp2aLockOnSendGoDone;
    
    private boolean mIsSendGoDone;
    
    
    private boolean mShowSuggestions;
    private boolean mIsShowingHint;
    private int     mCorrectionMode;
    private int     mOrientation;
    private List<CharSequence> mSuggestPuncList;
    // Keep track of the last selection range to decide if we need to show word alternatives
    private int     mLastSelectionStart;
    private int     mLastSelectionEnd;

    // Input type is such that we should not auto-correct
    private boolean mInputTypeNoAutoCorrect;

    // Indicates whether the suggestion strip is to be on in landscape
    private boolean mJustAccepted;
    private CharSequence mJustRevertedSeparator;
    private int mDeleteCount;
    private long mLastKeyTime;

    // Modifier keys state
    private ModifierKeyState mShiftKeyState = new ModifierKeyState();
    private ModifierKeyState mSymbolKeyState = new ModifierKeyState();

    
    private AudioManager mAudioManager;
    // Align sound effect volume on music volume
    private final float FX_VOLUME = -1.0f;
    private boolean mSilentMode;

    /* package */ String mWordSeparators;
    private String mSentenceSeparators;
    private String mSuggestPuncs;
    private boolean mConfigurationChanging;

    // Keeps track of most recently inserted text (multi-character key) for reverting
    private CharSequence mEnteredText;
    private boolean mRefreshKeyboardRequired;
    
    //KP2A
    private boolean mShowKp2aKeyboard; //true if the user wants to see/should see the Kp2a keyboard
    private boolean mHadKp2aData; //true if we already had data available in the last session

    // For each word, a list of potential replacements, usually from voice.
    private Map<String, List<CharSequence>> mWordToSuggestions =
            new HashMap<String, List<CharSequence>>();

    private ArrayList<WordAlternatives> mWordHistory = new ArrayList<WordAlternatives>();

    
    public abstract static class WordAlternatives {
        protected CharSequence mChosenWord;

        public WordAlternatives() {
            // Nothing
        }

        public WordAlternatives(CharSequence chosenWord) {
            mChosenWord = chosenWord;
        }

        @Override
        public int hashCode() {
            return mChosenWord.hashCode();
        }

        public abstract CharSequence getOriginalWord();

        public CharSequence getChosenWord() {
            return mChosenWord;
        }

        public abstract List<CharSequence> getAlternatives();
    }

    public class TypedWordAlternatives extends WordAlternatives {
        private WordComposer word;

        public TypedWordAlternatives() {
            // Nothing
        }

        public TypedWordAlternatives(CharSequence chosenWord, WordComposer wordComposer) {
            super(chosenWord);
            word = wordComposer;
        }

        @Override
        public CharSequence getOriginalWord() {
            return word.getTypedWord();
        }

        @Override
        public List<CharSequence> getAlternatives() {
            return getTypedSuggestions(word);
        }
    }

    /* package */ Handler mHandler = new Handler() {
        @Override
        public void handleMessage(Message msg) {
            switch (msg.what) {
                case MSG_UPDATE_SUGGESTIONS:
                    updateSuggestions();
                    break;
                case MSG_UPDATE_OLD_SUGGESTIONS:
                    setOldSuggestions();
                    break;
                case MSG_UPDATE_SHIFT_STATE:
                    updateShiftKeyState(getCurrentInputEditorInfo());
                    break;
                
            }
        }
    };
	private ClearKeyboardBroadcastReceiver mClearKeyboardReceiver;
	private PluginManager mPluginManager;
    
    public class ClearKeyboardBroadcastReceiver extends BroadcastReceiver {
    	  @Override
    	  public void onReceive(Context context, Intent intent) {
    	    android.util.Log.d("KP2AK", "received clear keyboard broadcast");
    	    mShowKp2aKeyboard = false;
    	    updateKeyboardMode(getCurrentInputEditorInfo());
    	    SharedPreferences sp = PreferenceManager.getDefaultSharedPreferences(context);
    		boolean switchback = sp.getBoolean("AutoSwitchBackKeyboard_key", true);
    	    if (switchback)
    	    {
    	    	//switch back, but only "silently" (i.e. if automatic switching is enabled and available)
    	    	keepass2android.kbbridge.ImeSwitcher.switchToPreviousKeyboard(KP2AKeyboard.this, true);
    	    }
    	  }

    	} 


	

    @Override
    public void onCreate() {
        CurrentlyRunningService = this;
        LatinImeLogger.init(this);
        KeyboardSwitcher.init(this);
        super.onCreate();
        //setStatusIcon(R.drawable.ime_qwerty);
        mResources = getResources();
        final Configuration conf = mResources.getConfiguration();
        final SharedPreferences prefs = PreferenceManager.getDefaultSharedPreferences(this);
        mLanguageSwitcher = new LanguageSwitcher(this);
        mLanguageSwitcher.loadLocales(prefs);
        mKeyboardSwitcher = KeyboardSwitcher.getInstance();
        mKeyboardSwitcher.setLanguageSwitcher(mLanguageSwitcher);
        mSystemLocale = conf.locale.toString();
        mLanguageSwitcher.setSystemLocale(conf.locale);
        String inputLanguage = mLanguageSwitcher.getInputLanguage();
        if (inputLanguage == null) {
            inputLanguage = conf.locale.toString();
        }
        mReCorrectionEnabled = prefs.getBoolean(PREF_RECORRECTION_ENABLED,
                getResources().getBoolean(R.bool.default_recorrection_enabled));


        
        Log.d("KP2AK","finding plugin dicts...");
        PluginManager.getPluginDictionaries(getApplicationContext());
        mPluginManager = new PluginManager(this);
        final IntentFilter pFilter = new IntentFilter();
        pFilter.addDataScheme("package");
        pFilter.addAction("android.intent.action.PACKAGE_ADDED");
        pFilter.addAction("android.intent.action.PACKAGE_REPLACED");
        pFilter.addAction("android.intent.action.PACKAGE_REMOVED");
        registerReceiver(mPluginManager, pFilter);


        LatinIMEUtil.GCUtils.getInstance().reset();
        boolean tryGC = true;
        for (int i = 0; i < LatinIMEUtil.GCUtils.GC_TRY_LOOP_MAX && tryGC; ++i) {
            try {
                initSuggest(inputLanguage);
                tryGC = false;
            } catch (OutOfMemoryError e) {
                tryGC = LatinIMEUtil.GCUtils.getInstance().tryGCOrWait(inputLanguage, e);
            }
        }

        mOrientation = conf.orientation;
        initSuggestPuncList();

        // register to receive ringer mode changes for silent mode
        IntentFilter filter = new IntentFilter(AudioManager.RINGER_MODE_CHANGED_ACTION);
        registerReceiver(mSilentModeReceiver, filter);
       
        prefs.registerOnSharedPreferenceChangeListener(this);
        
        //check if we have KP2A data available:
        mHadKp2aData = mShowKp2aKeyboard = keepass2android.kbbridge.KeyboardData.hasData();
        
        mClearKeyboardReceiver = new ClearKeyboardBroadcastReceiver();
        registerReceiver(mClearKeyboardReceiver, new IntentFilter(get_KEEPASS2ANDROID_KEYBOARD_CLEARED(this)));
        android.util.Log.d("KP2AK", "registered receiver for clear keyboard broadcast: "+get_KEEPASS2ANDROID_KEYBOARD_CLEARED(this));
        
    }

    /**
     * Loads a dictionary or multiple separated dictionary
     * @param ctx 
     * @return returns array of dictionary resource ids
     */
    /* package */ static int[] getDictionary(Resources res, Context ctx) {
        String packageName = KP2AKeyboard.class.getPackage().getName();
        Log.d("KP2AK", "package of keyboard " + packageName);
        XmlResourceParser xrp = res.getXml(R.xml.dictionary);
        ArrayList<Integer> dictionaries = new ArrayList<Integer>();

        try {
            int current = xrp.getEventType();
            while (current != XmlResourceParser.END_DOCUMENT) {
                if (current == XmlResourceParser.START_TAG) {
                    String tag = xrp.getName();
                    if (tag != null) {
                        if (tag.equals("part")) {
                            String dictFileName = xrp.getAttributeValue(null, "name");
                            int dictId = res.getIdentifier(dictFileName, "raw", ctx.getPackageName());
                            Log.d("KP2AK", "Adding " + packageName+"/"+dictFileName+"/"+dictId);
                            dictionaries.add(dictId);
                        }
                    }
                }
                xrp.next();
                current = xrp.getEventType();
            }
        } catch (XmlPullParserException e) {
            Log.e(TAG, "Dictionary XML parsing failure");
        } catch (IOException e) {
            Log.e(TAG, "Dictionary XML IOException");
        }

        int count = dictionaries.size();
        int[] dict = new int[count];
        for (int i = 0; i < count; i++) {
            dict[i] = dictionaries.get(i);
        }
        
        Log.d("KP2AK", "num dicts: " + count);

        return dict;
    }

    private void initSuggest(String locale) {
        mInputLocale = locale;

        Resources orig = getResources();
        Configuration conf = orig.getConfiguration();
        Locale saveLocale = conf.locale;
        conf.locale = new Locale(locale);
        orig.updateConfiguration(conf, orig.getDisplayMetrics());
        if (mSuggest != null) {
            mSuggest.close();
        }
        SharedPreferences sp = PreferenceManager.getDefaultSharedPreferences(this);
        mQuickFixes = sp.getBoolean(PREF_QUICK_FIXES, true);

        int[] dictionaries = getDictionary(orig, this);
        mSuggest = new Suggest(this, dictionaries);
        updateAutoTextEnabled(saveLocale);
        //if (mUserDictionary != null) mUserDictionary.close();
        //mUserDictionary = new UserDictionary(this, mInputLocale);
        /*if (mContactsDictionary == null) {
            mContactsDictionary = new ContactsDictionary(this, Suggest.DIC_CONTACTS);
        }
        if (mAutoDictionary != null) {
            mAutoDictionary.close();
        }
        mAutoDictionary = new AutoDictionary(this, this, mInputLocale, Suggest.DIC_AUTO);*/
        //if (mUserBigramDictionary != null) {
        //    mUserBigramDictionary.close();
        //}
        //mUserBigramDictionary = new UserBigramDictionary(this, this, mInputLocale,
        //        Suggest.DIC_USER);
        /*mSuggest.setUserBigramDictionary(mUserBigramDictionary);
        mSuggest.setUserDictionary(mUserDictionary);
        mSuggest.setContactsDictionary(mContactsDictionary);
        mSuggest.setAutoDictionary(mAutoDictionary);*/
        updateCorrectionMode();
        mWordSeparators = mResources.getString(R.string.word_separators);
        mSentenceSeparators = mResources.getString(R.string.sentence_separators);

        conf.locale = saveLocale;
        orig.updateConfiguration(conf, orig.getDisplayMetrics());
    }

    @Override
    public void onDestroy() {
        /*if (mUserDictionary != null) {
            mUserDictionary.close();
        }
        if (mContactsDictionary != null) {
            mContactsDictionary.close();
        }*/
        unregisterReceiver(mSilentModeReceiver);
        unregisterReceiver(mPluginManager);        
        unregisterReceiver(mClearKeyboardReceiver);
        
        LatinImeLogger.commit();
        LatinImeLogger.onDestroy();

        CurrentlyRunningService = null;
        super.onDestroy();
    }

    @Override
    public void onConfigurationChanged(Configuration conf) {
        // If the system locale changes and is different from the saved
        // locale (mSystemLocale), then reload the input locale list from the
        // latin ime settings (shared prefs) and reset the input locale
        // to the first one.
        final String systemLocale = conf.locale.toString();
        if (!TextUtils.equals(systemLocale, mSystemLocale)) {
            mSystemLocale = systemLocale;
            if (mLanguageSwitcher != null) {
                mLanguageSwitcher.loadLocales(
                        PreferenceManager.getDefaultSharedPreferences(this));
                mLanguageSwitcher.setSystemLocale(conf.locale);
                toggleLanguage(true, true);
            } else {
                reloadKeyboards();
            }
        }
        // If orientation changed while predicting, commit the change
        if (conf.orientation != mOrientation) {
            InputConnection ic = getCurrentInputConnection();
            commitTyped(ic);
            if (ic != null) ic.finishComposingText(); // For voice input
            mOrientation = conf.orientation;
            reloadKeyboards();
        }
        updateKp2aKeyLabels();
        mConfigurationChanging = true;
        super.onConfigurationChanged(conf);
        mConfigurationChanging = false;
    }

    @Override
    public View onCreateInputView() {
        mKeyboardSwitcher.recreateInputView();
        mKeyboardSwitcher.makeKeyboards(true);
        
        loadSettings();
        
        updateShowKp2aMode();
        Log.d("KP2AK", "onCreateInputView -> setKM");
        if ((mShowKp2aKeyboard) && (mKp2aEnableSimpleKeyboard))
        {
        	mKeyboardSwitcher.setKeyboardMode(
                    KeyboardSwitcher.MODE_KP2A, 0);	
        }
        else
        {
        	mKeyboardSwitcher.setKeyboardMode(
                    KeyboardSwitcher.MODE_TEXT, 0);
        }
        updateKp2aKeyLabels();
        
        return mKeyboardSwitcher.getInputView();
    }

    @Override
    public View onCreateCandidatesView() {
        mKeyboardSwitcher.makeKeyboards(true);
        mCandidateViewContainer = (LinearLayout) getLayoutInflater().inflate(
                R.layout.candidates, null);
        mCandidateView = (CandidateView) mCandidateViewContainer.findViewById(R.id.candidates);
        mCandidateView.setService(this);
        setCandidatesViewShown(true);
        return mCandidateViewContainer;
    }

    @Override
    public void onStartInputView(EditorInfo attribute, boolean restarting) {
        LatinKeyboardView inputView = mKeyboardSwitcher.getInputView();
        // In landscape mode, this method gets called without the input view being created.
        if (inputView == null) {
            return;
        }
        
        loadSettings();

        if (mRefreshKeyboardRequired) {
            mRefreshKeyboardRequired = false;
            toggleLanguage(true, true);
        }

        mKeyboardSwitcher.makeKeyboards(false);

        TextEntryState.newSession(this);
        
        updateKeyboardMode(attribute);
        inputView.closing();
        mComposing.setLength(0);
        mPredicting = false;
        mDeleteCount = 0;
        mJustAddedAutoSpace = false;
        mIsSendGoDone = ((attribute.imeOptions&(EditorInfo.IME_MASK_ACTION|EditorInfo.IME_FLAG_NO_ENTER_ACTION)) == EditorInfo.IME_ACTION_GO)
        		|| ((attribute.imeOptions&(EditorInfo.IME_MASK_ACTION|EditorInfo.IME_FLAG_NO_ENTER_ACTION)) == EditorInfo.IME_ACTION_DONE)
        		|| ((attribute.imeOptions&(EditorInfo.IME_MASK_ACTION|EditorInfo.IME_FLAG_NO_ENTER_ACTION)) == EditorInfo.IME_ACTION_SEND);

        updateShiftKeyState(attribute);

        setCandidatesViewShownInternal(isCandidateStripVisible() || mCompletionOn,
                false /* needsInputViewShown */ );
        updateSuggestions();

        // If the dictionary is not big enough, don't auto correct
        mHasDictionary = mSuggest.hasMainDictionary();
        Log.d("KP2AK", "has main dict: " + mHasDictionary);

        updateCorrectionMode();

        inputView.setPreviewEnabled(mPopupOn);
        inputView.setProximityCorrectionEnabled(true);
        mPredictionOn = mPredictionOn && (mCorrectionMode > 0 || mShowSuggestions);
        // If we just entered a text field, maybe it has some old text that requires correction
        checkReCorrectionOnStart();
        
        tryKp2aAutoFill(attribute);
        
        if (TRACE) Debug.startMethodTracing("/data/trace/latinime");
    }

	private void updateKeyboardMode(EditorInfo attribute) {
		
        mInputTypeNoAutoCorrect = false;
        mPredictionOn = false;
        mCompletionOn = false;
        mCompletions = null;
        mCapsLock = false;
        mEnteredText = null;
		
		int variation = attribute.inputType & EditorInfo.TYPE_MASK_VARIATION;
        
		updateShowKp2aMode();
		Log.d("KP2AK", "updateKeyboardMode -> setKM");
        if ((mShowKp2aKeyboard) && (mKp2aEnableSimpleKeyboard))
        {
        	mKeyboardSwitcher.setKeyboardMode(KeyboardSwitcher.MODE_KP2A, attribute.imeOptions);
            mPredictionOn = false;
            mPredicting = false;
            mCompletionOn = false;
            mInputTypeNoAutoCorrect = true;
        }
        else
        {

	        switch (attribute.inputType & EditorInfo.TYPE_MASK_CLASS) {
	            case EditorInfo.TYPE_CLASS_NUMBER:
	            case EditorInfo.TYPE_CLASS_DATETIME:
	                // fall through
	                // NOTE: For now, we use the phone keyboard for NUMBER and DATETIME until we get
	                // a dedicated number entry keypad.
	                // TODO: Use a dedicated number entry keypad here when we get one.
	            case EditorInfo.TYPE_CLASS_PHONE:
	                mKeyboardSwitcher.setKeyboardMode(KeyboardSwitcher.MODE_PHONE,
	                        attribute.imeOptions);
	                break;
	            case EditorInfo.TYPE_CLASS_TEXT:
	                mKeyboardSwitcher.setKeyboardMode(KeyboardSwitcher.MODE_TEXT,
	                        attribute.imeOptions);
	                //startPrediction();
	                mPredictionOn = true;
	                // Make sure that passwords are not displayed in candidate view
	                if (variation == EditorInfo.TYPE_TEXT_VARIATION_PASSWORD ||
	                        variation == EditorInfo.TYPE_TEXT_VARIATION_VISIBLE_PASSWORD ) {
	                    mPredictionOn = false;
	                }
	                if (variation == EditorInfo.TYPE_TEXT_VARIATION_EMAIL_ADDRESS
	                        || variation == EditorInfo.TYPE_TEXT_VARIATION_PERSON_NAME) {
	                    mAutoSpace = false;
	                } else {
	                    mAutoSpace = true;
	                }
	                if (variation == EditorInfo.TYPE_TEXT_VARIATION_EMAIL_ADDRESS) {
	                    mPredictionOn = false;
	                    mKeyboardSwitcher.setKeyboardMode(KeyboardSwitcher.MODE_EMAIL,
	                            attribute.imeOptions);
	                } else if (variation == EditorInfo.TYPE_TEXT_VARIATION_URI) {
	                    mPredictionOn = false;
	                    mKeyboardSwitcher.setKeyboardMode(KeyboardSwitcher.MODE_URL,
	                            attribute.imeOptions);
	                } else if (variation == EditorInfo.TYPE_TEXT_VARIATION_SHORT_MESSAGE) {
	                    mKeyboardSwitcher.setKeyboardMode(KeyboardSwitcher.MODE_IM,
	                            attribute.imeOptions);
	                } else if (variation == EditorInfo.TYPE_TEXT_VARIATION_FILTER) {
	                    mPredictionOn = false;
	                } else if (variation == EditorInfo.TYPE_TEXT_VARIATION_WEB_EDIT_TEXT) {
	                    mKeyboardSwitcher.setKeyboardMode(KeyboardSwitcher.MODE_WEB,
	                            attribute.imeOptions);
	                    // If it's a browser edit field and auto correct is not ON explicitly, then
	                    // disable auto correction, but keep suggestions on.
	                    if ((attribute.inputType & EditorInfo.TYPE_TEXT_FLAG_AUTO_CORRECT) == 0) {
	                        mInputTypeNoAutoCorrect = true;
	                    }
	                }
	
	                // If NO_SUGGESTIONS is set, don't do prediction.
	                if ((attribute.inputType & EditorInfo.TYPE_TEXT_FLAG_NO_SUGGESTIONS) != 0) {
	                    mPredictionOn = false;
	                    mInputTypeNoAutoCorrect = true;
	                }
	                // If it's not multiline and the autoCorrect flag is not set, then don't correct
	                if ((attribute.inputType & EditorInfo.TYPE_TEXT_FLAG_AUTO_CORRECT) == 0 &&
	                        (attribute.inputType & EditorInfo.TYPE_TEXT_FLAG_MULTI_LINE) == 0) {
	                    mInputTypeNoAutoCorrect = true;
	                }
	                if ((attribute.inputType & EditorInfo.TYPE_TEXT_FLAG_AUTO_COMPLETE) != 0) {
	                    mPredictionOn = false;
	                    mCompletionOn = isFullscreenMode();
	                }
	                break;
	            default:
	                mKeyboardSwitcher.setKeyboardMode(KeyboardSwitcher.MODE_TEXT,
	                        attribute.imeOptions);
	        }
        }
        updateKp2aKeyLabels();
	}

	private void updateShowKp2aMode() {
		if (!keepass2android.kbbridge.KeyboardData.hasData())
		{
			//data no longer available. hide kp2a keyboard:
    		mShowKp2aKeyboard = false;
    		mHadKp2aData = false;
		}
		else
		{
		
	        if (!mHadKp2aData)
	        {
	        	if (keepass2android.kbbridge.KeyboardData.hasData())
	        	{
	        		//new data available -> show kp2a keyboard:
	        		mShowKp2aKeyboard = true;
	        	}
	        }
        	
        	mHadKp2aData = keepass2android.kbbridge.KeyboardData.hasData();
        }
        
        Log.d("KP2AK", "show: " + mShowKp2aKeyboard);
	}

    private boolean tryKp2aAutoFill(final EditorInfo editorInfo) {
    	
    	if (!mKp2aAutoFillOn)
    		return false;
    	
    	//auto fill in?		
		InputConnection ic = getCurrentInputConnection();
        if (ic == null) return false;
        ExtractedTextRequest etr = new ExtractedTextRequest();
        etr.token = 0; // anything is fine here
        ExtractedText et = ic.getExtractedText(etr, 0);

		boolean hasTextInField = (et != null) && (!TextUtils.isEmpty(et.text));
		if (!hasTextInField) //only auto-fill if target field is empty
		{
			//try to look up saved field hint:
			if (!TextUtils.isEmpty(editorInfo.hintText))
			{
				SharedPreferences prefs = getApplicationContext().getSharedPreferences(KP2A_SAVED_FIELD_HINTS, MODE_PRIVATE);
				
				String key = editorInfo.packageName+"/"+keepass2android.kbbridge.KeyboardData.entryId+"/"+editorInfo.hintText;
				Log.d("KP2AK", "looking up saved field hint for "+key);
				
				String savedKey = prefs.getString(key, "");
				
				if ("".equals(savedKey) == false)
				{
					Log.d("KP2AK","Found field "+savedKey);
					if (commitTextForKey(editorInfo, savedKey))
						return true;
				}
			}

			//try to look up by hint
			if ((editorInfo.hintText != null) && (editorInfo.hintText.length() > 0))
			{
				if (commitTextForKey(editorInfo, editorInfo.hintText.toString()))
					return true;
			}

		}		
		return false;
	}
    
	private boolean commitTextForKey(final EditorInfo attribute, String key) {
		List<StringForTyping> availableFields = keepass2android.kbbridge.KeyboardData.availableFields;
		for (StringForTyping str: availableFields)
		{
			if (str.key.equals(key))
			{
				Log.d("KP2AK", "Typing!");
				commitKp2aString(str.value, attribute);
				return true;
			}
		}
		return false;
	}

	private void commitKp2aString(String value, EditorInfo editorInfo) {
		//getCurrentInputConnection().commitText(value, 0);
		onText(value);

		if ((editorInfo.imeOptions&(EditorInfo.IME_MASK_ACTION|EditorInfo.IME_FLAG_NO_ENTER_ACTION)) == EditorInfo.IME_ACTION_NEXT)
		{
			Log.d("KP2AK", "action is NEXT ");
			getCurrentInputConnection().performEditorAction(EditorInfo.IME_ACTION_NEXT);
		}
	}


	private void checkReCorrectionOnStart() {
        if (mReCorrectionEnabled && isPredictionOn()) {
            // First get the cursor position. This is required by setOldSuggestions(), so that
            // it can pass the correct range to setComposingRegion(). At this point, we don't
            // have valid values for mLastSelectionStart/Stop because onUpdateSelection() has
            // not been called yet.
            InputConnection ic = getCurrentInputConnection();
            if (ic == null) return;
            ExtractedTextRequest etr = new ExtractedTextRequest();
            etr.token = 0; // anything is fine here
            ExtractedText et = ic.getExtractedText(etr, 0);
            if (et == null) return;

            mLastSelectionStart = et.startOffset + et.selectionStart;
            mLastSelectionEnd = et.startOffset + et.selectionEnd;

            // Then look for possible corrections in a delayed fashion
            if (!TextUtils.isEmpty(et.text) && isCursorTouchingWord()) {
                postUpdateOldSuggestions();
            }
        }
    }

    @Override
    public void onFinishInput() {
        super.onFinishInput();

        LatinImeLogger.commit();
        onAutoCompletionStateChanged(false);

        
        if (mKeyboardSwitcher.getInputView() != null) {
            mKeyboardSwitcher.getInputView().closing();
        }
        //if (mAutoDictionary != null) mAutoDictionary.flushPendingWrites();
        //if (mUserBigramDictionary != null) mUserBigramDictionary.flushPendingWrites();
    }

    @Override
    public void onFinishInputView(boolean finishingInput) {
        super.onFinishInputView(finishingInput);
        // Remove penging messages related to update suggestions
        mHandler.removeMessages(MSG_UPDATE_SUGGESTIONS);
        mHandler.removeMessages(MSG_UPDATE_OLD_SUGGESTIONS);
    }

    @Override
    public void onUpdateSelection(int oldSelStart, int oldSelEnd,
            int newSelStart, int newSelEnd,
            int candidatesStart, int candidatesEnd) {
        super.onUpdateSelection(oldSelStart, oldSelEnd, newSelStart, newSelEnd,
                candidatesStart, candidatesEnd);

        if (DEBUG) {
            Log.i(TAG, "onUpdateSelection: oss=" + oldSelStart
                    + ", ose=" + oldSelEnd
                    + ", nss=" + newSelStart
                    + ", nse=" + newSelEnd
                    + ", cs=" + candidatesStart
                    + ", ce=" + candidatesEnd);
        }

                // If the current selection in the text view changes, we should
        // clear whatever candidate text we have.
        if ((((mComposing.length() > 0 && mPredicting))
                && (newSelStart != candidatesEnd
                    || newSelEnd != candidatesEnd)
                && mLastSelectionStart != newSelStart)) {
            mComposing.setLength(0);
            mPredicting = false;
            postUpdateSuggestions();
            TextEntryState.reset();
            InputConnection ic = getCurrentInputConnection();
            if (ic != null) {
                ic.finishComposingText();
            }
        } else if (!mPredicting && !mJustAccepted) {
            switch (TextEntryState.getState()) {
                case ACCEPTED_DEFAULT:
                    TextEntryState.reset();
                    // fall through
                case SPACE_AFTER_PICKED:
                    mJustAddedAutoSpace = false;  // The user moved the cursor.
                    break;
            }
        }
        mJustAccepted = false;
        postUpdateShiftKeyState();

        // Make a note of the cursor position
        mLastSelectionStart = newSelStart;
        mLastSelectionEnd = newSelEnd;

        if (mReCorrectionEnabled) {
            // Don't look for corrections if the keyboard is not visible
            if (mKeyboardSwitcher != null && mKeyboardSwitcher.getInputView() != null
                    && mKeyboardSwitcher.getInputView().isShown()) {
                // Check if we should go in or out of correction mode.
                if (isPredictionOn()
                        && mJustRevertedSeparator == null
                        && (candidatesStart == candidatesEnd || newSelStart != oldSelStart
                                || TextEntryState.isCorrecting())
                                && (newSelStart < newSelEnd - 1 || (!mPredicting))
                                ) {
                    if (isCursorTouchingWord() || mLastSelectionStart < mLastSelectionEnd) {
                        postUpdateOldSuggestions();
                    } else {
                        abortCorrection(false);
                        // Show the punctuation suggestions list if the current one is not
                        // and if not showing "Touch again to save".
                        if (mCandidateView != null
                                && !mSuggestPuncList.equals(mCandidateView.getSuggestions())
                                        && !mCandidateView.isShowingAddToDictionaryHint()) {
                            setNextSuggestions();
                        }
                    }
                }
            }
        }
    }

    /**
     * This is called when the user has clicked on the extracted text view,
     * when running in fullscreen mode.  The default implementation hides
     * the candidates view when this happens, but only if the extracted text
     * editor has a vertical scroll bar because its text doesn't fit.
     * Here we override the behavior due to the possibility that a re-correction could
     * cause the candidate strip to disappear and re-appear.
     */
    @Override
    public void onExtractedTextClicked() {
        if (mReCorrectionEnabled && isPredictionOn()) return;

        super.onExtractedTextClicked();
    }

    /**
     * This is called when the user has performed a cursor movement in the
     * extracted text view, when it is running in fullscreen mode.  The default
     * implementation hides the candidates view when a vertical movement
     * happens, but only if the extracted text editor has a vertical scroll bar
     * because its text doesn't fit.
     * Here we override the behavior due to the possibility that a re-correction could
     * cause the candidate strip to disappear and re-appear.
     */
    @Override
    public void onExtractedCursorMovement(int dx, int dy) {
        if (mReCorrectionEnabled && isPredictionOn()) return;

        super.onExtractedCursorMovement(dx, dy);
    }

    @Override
    public void hideWindow() {
        LatinImeLogger.commit();
        onAutoCompletionStateChanged(false);

        if (TRACE) Debug.stopMethodTracing();
        if (mOptionsDialog != null && mOptionsDialog.isShowing()) {
            mOptionsDialog.dismiss();
            mOptionsDialog = null;
        }
        mWordToSuggestions.clear();
        mWordHistory.clear();
        super.hideWindow();
        TextEntryState.endSession();
    }

    @Override
    public void onDisplayCompletions(CompletionInfo[] completions) {
        if (DEBUG) {
            Log.i("foo", "Received completions:");
            for (int i=0; i<(completions != null ? completions.length : 0); i++) {
                Log.i("foo", "  #" + i + ": " + completions[i]);
            }
        }
        if (mCompletionOn) {
            mCompletions = completions;
            if (completions == null) {
                clearSuggestions();
                return;
            }

            List<CharSequence> stringList = new ArrayList<CharSequence>();
            for (int i=0; i<(completions != null ? completions.length : 0); i++) {
                CompletionInfo ci = completions[i];
                if (ci != null) stringList.add(ci.getText());
            }
            // When in fullscreen mode, show completions generated by the application
            setSuggestions(stringList, true, true, true);
            mBestWord = null;
            setCandidatesViewShown(true);
        }
    }

    private void setCandidatesViewShownInternal(boolean shown, boolean needsInputViewShown) {
        // TODO: Remove this if we support candidates with hard keyboard
        if (onEvaluateInputViewShown()) {
            super.setCandidatesViewShown(shown && mKeyboardSwitcher.getInputView() != null
                    && (needsInputViewShown ? mKeyboardSwitcher.getInputView().isShown() : true));
        }
    }

    @Override
    public void setCandidatesViewShown(boolean shown) {
        setCandidatesViewShownInternal(shown, true /* needsInputViewShown */);
    }

    @Override
    public void onComputeInsets(InputMethodService.Insets outInsets) {
        super.onComputeInsets(outInsets);
        if (!isFullscreenMode()) {
            outInsets.contentTopInsets = outInsets.visibleTopInsets;
        }
    }

    @Override
    public boolean onEvaluateFullscreenMode() {
    	
    	//no full screen mode if only simple Kp2aKeyboard is shown
    	if (mShowKp2aKeyboard)
    		return false;
    	
        DisplayMetrics dm = getResources().getDisplayMetrics();
        float displayHeight = dm.heightPixels;
        // If the display is more than X inches high, don't go to fullscreen mode
        float dimen = getResources().getDimension(R.dimen.max_height_for_fullscreen);
        if (displayHeight > dimen) {
            return false;
        } else {
            return super.onEvaluateFullscreenMode();
        }
    }

    @Override
    public boolean onKeyDown(int keyCode, KeyEvent event) {
        switch (keyCode) {
            case KeyEvent.KEYCODE_BACK:
                if (event.getRepeatCount() == 0 && mKeyboardSwitcher.getInputView() != null) {
                    if (mKeyboardSwitcher.getInputView().handleBack()) {
                        return true;
                    } 
                }
                break;
            case KeyEvent.KEYCODE_DPAD_DOWN:
            case KeyEvent.KEYCODE_DPAD_UP:
            case KeyEvent.KEYCODE_DPAD_LEFT:
            case KeyEvent.KEYCODE_DPAD_RIGHT:
                break;
        }
        return super.onKeyDown(keyCode, event);
    }

    @Override
    public boolean onKeyUp(int keyCode, KeyEvent event) {
        switch (keyCode) {
            case KeyEvent.KEYCODE_DPAD_DOWN:
            case KeyEvent.KEYCODE_DPAD_UP:
            case KeyEvent.KEYCODE_DPAD_LEFT:
            case KeyEvent.KEYCODE_DPAD_RIGHT:
                LatinKeyboardView inputView = mKeyboardSwitcher.getInputView();
                // Enable shift key and DPAD to do selections
                if (inputView != null && inputView.isShown()
                        && inputView.isShifted()) {
                    event = new KeyEvent(event.getDownTime(), event.getEventTime(),
                            event.getAction(), event.getKeyCode(), event.getRepeatCount(),
                            event.getDeviceId(), event.getScanCode(),
                            KeyEvent.META_SHIFT_LEFT_ON | KeyEvent.META_SHIFT_ON);
                    InputConnection ic = getCurrentInputConnection();
                    if (ic != null) ic.sendKeyEvent(event);
                    return true;
                }
                break;
        }
        return super.onKeyUp(keyCode, event);
    }


    private void reloadKeyboards() {
        mKeyboardSwitcher.setLanguageSwitcher(mLanguageSwitcher);
        mKeyboardSwitcher.makeKeyboards(true);
        updateKp2aKeyLabels();
    }

    private void commitTyped(InputConnection inputConnection) {
        if (mPredicting) {
            mPredicting = false;
            if (mComposing.length() > 0) {
                if (inputConnection != null) {
                    inputConnection.commitText(mComposing, 1);
                }
                mCommittedLength = mComposing.length();
                TextEntryState.acceptedTyped(mComposing);
                addToDictionaries(mComposing, AutoDictionary.FREQUENCY_FOR_TYPED);
            }
            updateSuggestions();
        }
    }

    private void postUpdateShiftKeyState() {
        mHandler.removeMessages(MSG_UPDATE_SHIFT_STATE);
        // TODO: Should remove this 300ms delay?
        mHandler.sendMessageDelayed(mHandler.obtainMessage(MSG_UPDATE_SHIFT_STATE), 300);
    }

    public void updateShiftKeyState(EditorInfo attr) {
        InputConnection ic = getCurrentInputConnection();
        if (ic != null && attr != null && mKeyboardSwitcher.isAlphabetMode()) {
            mKeyboardSwitcher.setShifted(mShiftKeyState.isMomentary() || mCapsLock
                    || getCursorCapsMode(ic, attr) != 0);
        }
    }

    private int getCursorCapsMode(InputConnection ic, EditorInfo attr) {
        int caps = 0;
        EditorInfo ei = getCurrentInputEditorInfo();
        if (mAutoCap && ei != null && ei.inputType != EditorInfo.TYPE_NULL) {
            caps = ic.getCursorCapsMode(attr.inputType);
        }
        return caps;
    }

    private void swapPunctuationAndSpace() {
        final InputConnection ic = getCurrentInputConnection();
        if (ic == null) return;
        CharSequence lastTwo = ic.getTextBeforeCursor(2, 0);
        if (lastTwo != null && lastTwo.length() == 2
                && lastTwo.charAt(0) == KEYCODE_SPACE && isSentenceSeparator(lastTwo.charAt(1))) {
            ic.beginBatchEdit();
            ic.deleteSurroundingText(2, 0);
            ic.commitText(lastTwo.charAt(1) + " ", 1);
            ic.endBatchEdit();
            updateShiftKeyState(getCurrentInputEditorInfo());
            mJustAddedAutoSpace = true;
        }
    }

    private void reswapPeriodAndSpace() {
        final InputConnection ic = getCurrentInputConnection();
        if (ic == null) return;
        CharSequence lastThree = ic.getTextBeforeCursor(3, 0);
        if (lastThree != null && lastThree.length() == 3
                && lastThree.charAt(0) == KEYCODE_PERIOD
                && lastThree.charAt(1) == KEYCODE_SPACE
                && lastThree.charAt(2) == KEYCODE_PERIOD) {
            ic.beginBatchEdit();
            ic.deleteSurroundingText(3, 0);
            ic.commitText(" ..", 1);
            ic.endBatchEdit();
            updateShiftKeyState(getCurrentInputEditorInfo());
        }
    }

    private void doubleSpace() {
        //if (!mAutoPunctuate) return;
        if (mCorrectionMode == Suggest.CORRECTION_NONE) return;
        final InputConnection ic = getCurrentInputConnection();
        if (ic == null) return;
        CharSequence lastThree = ic.getTextBeforeCursor(3, 0);
        if (lastThree != null && lastThree.length() == 3
                && Character.isLetterOrDigit(lastThree.charAt(0))
                && lastThree.charAt(1) == KEYCODE_SPACE && lastThree.charAt(2) == KEYCODE_SPACE) {
            ic.beginBatchEdit();
            ic.deleteSurroundingText(2, 0);
            ic.commitText(". ", 1);
            ic.endBatchEdit();
            updateShiftKeyState(getCurrentInputEditorInfo());
            mJustAddedAutoSpace = true;
        }
    }

    private void maybeRemovePreviousPeriod(CharSequence text) {
        final InputConnection ic = getCurrentInputConnection();
        if (ic == null) return;

        // When the text's first character is '.', remove the previous period
        // if there is one.
        CharSequence lastOne = ic.getTextBeforeCursor(1, 0);
        if (lastOne != null && lastOne.length() == 1
                && lastOne.charAt(0) == KEYCODE_PERIOD
                && text.charAt(0) == KEYCODE_PERIOD) {
            ic.deleteSurroundingText(1, 0);
        }
    }

    private void removeTrailingSpace() {
        final InputConnection ic = getCurrentInputConnection();
        if (ic == null) return;

        CharSequence lastOne = ic.getTextBeforeCursor(1, 0);
        if (lastOne != null && lastOne.length() == 1
                && lastOne.charAt(0) == KEYCODE_SPACE) {
            ic.deleteSurroundingText(1, 0);
        }
    }

    public boolean addWordToDictionary(String word) {
        //mUserDictionary.addWord(word, 128);
        // Suggestion strip should be updated after the operation of adding word to the
        // user dictionary
        postUpdateSuggestions();
        return true;
    }

    private boolean isAlphabet(int code) {
        if (Character.isLetter(code)) {
            return true;
        } else {
            return false;
        }
    }

    private void showInputMethodPicker() {
        ((InputMethodManager) getSystemService(INPUT_METHOD_SERVICE))
                .showInputMethodPicker();
    }

    private void onOptionKeyPressed() {
        if (!isShowingOptionDialog()) {
            launchSettings();
        }
    }

    private void onOptionKeyLongPressed() {
        if (!isShowingOptionDialog()) {
            if (LatinIMEUtil.hasMultipleEnabledIMEs(this)) {
                showInputMethodPicker();
            } else {
                launchSettings();
            }
        }
    }

    private boolean isShowingOptionDialog() {
        return mOptionsDialog != null && mOptionsDialog.isShowing();
    }

    // Implementation of KeyboardViewListener

    public void onKey(int primaryCode, int[] keyCodes, int x, int y) {
        long when = SystemClock.uptimeMillis();
        if (primaryCode != Keyboard.KEYCODE_DELETE ||
                when > mLastKeyTime + QUICK_PRESS) {
            mDeleteCount = 0;
        }
        mLastKeyTime = when;
        final boolean distinctMultiTouch = mKeyboardSwitcher.hasDistinctMultitouch();
        switch (primaryCode) {
            case Keyboard.KEYCODE_DELETE:
                handleBackspace();
                mDeleteCount++;
                LatinImeLogger.logOnDelete();
                break;
            case Keyboard.KEYCODE_SHIFT:
                // Shift key is handled in onPress() when device has distinct multi-touch panel.
                if (!distinctMultiTouch)
                    handleShift();
                break;
            case Keyboard.KEYCODE_MODE_CHANGE:
                // Symbol key is handled in onPress() when device has distinct multi-touch panel.
                if (!distinctMultiTouch)
                    changeKeyboardMode();
                break;
            case Keyboard.KEYCODE_CANCEL:
                if (!isShowingOptionDialog()) {
                    handleClose();
                }
                break;
            case LatinKeyboardView.KEYCODE_OPTIONS:
                onOptionKeyPressed();
                break;
            case LatinKeyboardView.KEYCODE_KP2A_NEXTFIELDS:
                onKp2aNextFieldsPressed();
                break;
            case LatinKeyboardView.KEYCODE_KP2A:
                onKp2aKeyPressed();
                break;
            case LatinKeyboardView.KEYCODE_KP2A_USER:
                onKp2aUserKeyPressed();
                break;
            case LatinKeyboardView.KEYCODE_KP2A_PASSWORD:
                onKp2aPasswordKeyPressed();
                break;
            case LatinKeyboardView.KEYCODE_KP2A_ALPHA:
                onKp2aAlphaKeyPressed();
                break;
            case LatinKeyboardView.KEYCODE_KP2A_SWITCH:
                onKp2aSwitchKeyboardPressed();
                break;
            case LatinKeyboardView.KEYCODE_KP2A_LOCK:
                onKp2aLockKeyPressed();
                break;
            case LatinKeyboardView.KEYCODE_OPTIONS_LONGPRESS:
                onOptionKeyLongPressed();
                break;
            case LatinKeyboardView.KEYCODE_NEXT_LANGUAGE:
                toggleLanguage(false, true);
                break;
            case LatinKeyboardView.KEYCODE_PREV_LANGUAGE:
                toggleLanguage(false, false);
                break;
            case 9 /*Tab*/:
                sendDownUpKeyEvents(KeyEvent.KEYCODE_TAB);
                break;
            case KEYCODE_ENTER:
            	onEnterKey();
            	//fall through
            default:
                if (primaryCode != KEYCODE_ENTER) {
                    mJustAddedAutoSpace = false;
                }
                RingCharBuffer.getInstance().push((char)primaryCode, x, y);
                LatinImeLogger.logOnInputChar();
                if (isWordSeparator(primaryCode)) {
                    handleSeparator(primaryCode);
                } else {
                    handleCharacter(primaryCode, keyCodes);
                }
                // Cancel the just reverted state
                mJustRevertedSeparator = null;
        }
        mKeyboardSwitcher.onKey(primaryCode);
        // Reset after any single keystroke
        mEnteredText = null;
    }

    private void onEnterKey() {
		if ((mIsSendGoDone) && (mKeyboardSwitcher.getKeyboardMode() == KeyboardSwitcher.MODE_KP2A))
		{
			if (mKp2aSwitchKeyboardOnSendGoDone)
			{
				keepass2android.kbbridge.ImeSwitcher.switchToPreviousKeyboard(this, false);
			}
			if (mKp2aLockOnSendGoDone)
			{
				onKp2aLockKeyPressed();
			}
		}
		
	}

	private void onKp2aLockKeyPressed() {
    	
		String action = getPackageName()+".lock_database";
		android.util.Log.i("KP2A", "sending broadcast with action "+action);
		Intent intent = new Intent(action);
		intent.setPackage(getPackageName());
		sendBroadcast(intent);
		
	}

	private void onKp2aSwitchKeyboardPressed() {
    	showInputMethodPicker();
		
	}

	private void onKp2aAlphaKeyPressed() {
		mShowKp2aKeyboard = false;		
		updateKeyboardMode(getCurrentInputEditorInfo());
	}

	private void onKp2aPasswordKeyPressed() {
        if (KeyboardData.availableFields.size() > KeyboardData.kp2aFieldIndex+1) //some entries may have only title, then there are no two buttons available
		    commitStringForTyping(KeyboardData.availableFields.get(KeyboardData.kp2aFieldIndex+1));
	}


	private void onKp2aUserKeyPressed() {
        commitStringForTyping(KeyboardData.availableFields.get(KeyboardData.kp2aFieldIndex));
		
	}



    private void onKp2aNextFieldsPressed()
    {

        List<StringForTyping> availableFields = keepass2android.kbbridge.KeyboardData.availableFields;
        if (KeyboardData.kp2aFieldIndex >= availableFields.size()-2)
        {
            KeyboardData.kp2aFieldIndex = 0;
        }
        else if (KeyboardData.kp2aFieldIndex == availableFields.size()-3)
        {
            KeyboardData.kp2aFieldIndex++;
        }
        else
            KeyboardData.kp2aFieldIndex += 2;

        updateKp2aKeyLabels();
    }

    String makeShort(String input, int lineLength)
    {
        String result = input;
        if (input.length() > lineLength)
        {
            result = input.substring(0,lineLength-1)+"…";
        }
        return result;
    }

    private void updateKp2aKeyLabels() {

        if ((mKeyboardSwitcher.getInputView() != null)
            && (mKeyboardSwitcher.getInputView().getKeyboard() != null))
        {
            for (Keyboard.Key key : mKeyboardSwitcher.getInputView().getKeyboard().getKeys()) {

                boolean isFirstKey = false;
                boolean isSecondKey = false;
                for (int code : key.codes) {
                    if (code == -201)
                        isFirstKey = true;
                    if (code == -202)
                        isSecondKey = true;
                }


                int fieldIndex = -1;
                if (isFirstKey) {
                    fieldIndex = KeyboardData.kp2aFieldIndex;
                }
                if (isSecondKey) {
                    fieldIndex = KeyboardData.kp2aFieldIndex + 1;
                }

                if (fieldIndex >= 0) {
                    key.label = "";
                    if (fieldIndex < KeyboardData.availableFields.size()) {
                        String displayName = "";
                        StringForTyping fieldData = KeyboardData.availableFields.get(fieldIndex);
                        if (fieldData != null) {
                            displayName = makeShort(fieldData.displayName, 10);

                            if ("Password".equals(fieldData.key))
                                displayName = getString(R.string.kp2a_password); //might be a shorter variant
                            if ("UserName".equals(fieldData.key))
                                displayName = getString(R.string.kp2a_user); //might be a shorter variant
                        }
                        key.label = displayName;
                    }
                }
            }
            mKeyboardSwitcher.getInputView().invalidateAllKeys();
        }

    }

    private void onKp2aKeyPressed() {


		if ((mKeyboardSwitcher.getKeyboardMode() == KeyboardSwitcher.MODE_KP2A) 
				|| (!mKp2aEnableSimpleKeyboard)
				|| (!keepass2android.kbbridge.KeyboardData.hasData()))
		{
			showKp2aDialog();
			return;
		}
		mShowKp2aKeyboard = true;
		updateKeyboardMode(getCurrentInputEditorInfo());
		setCandidatesViewShown(false);		
	}



    private void openOverlaySettings() {
        final Intent intent = new Intent(Settings.ACTION_MANAGE_OVERLAY_PERMISSION,
                Uri.parse("package:" + getPackageName()));
        try {
            startActivity(intent);
        } catch (ActivityNotFoundException e) {
            Log.e(TAG, e.getMessage());
        }
    }

	private void showKp2aDialog()
    {
        boolean androidP = android.os.Build.VERSION.SDK_INT >= 28;
        //due to a change in Android P, showing the dialog as dialog does not work (only visible
        // above the keyboard, not above the target application). Use an activity here.
        // However, this is not perfect as it has another behavior regarding which task is
        // in foreground, e.g. Chrome closes the IME when the activity is brought up which causes
        // trouble entering data. So we still use the dialog in previous android versions.
        if (androidP)
        {
            final EditorInfo attribute = getCurrentInputEditorInfo();
            final String clientPackageName = attribute.packageName;

            Intent i = new Intent(this, Kp2aDialog.class);
            i.putExtra("clientPackageName", clientPackageName);
            i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            startActivity(i);
        }
        else
        {

            AlertDialog.Builder builder = new AlertDialog.Builder(this);
            String title = "Keepass2Android";
            List<StringForTyping> availableFields = keepass2android.kbbridge.KeyboardData.availableFields;

            final EditorInfo attribute = getCurrentInputEditorInfo();
            attribute.dump(new Printer() {

                @Override
                public void println(String x) {
                    Log.d("KP2AK", x);

                }
            }, "");
            final ArrayList<StringForTyping> items = new ArrayList<StringForTyping>();
            for (StringForTyping entry : availableFields) {
                items.add(entry.clone());
            }


            StringForTyping openOrChangeEntry = new StringForTyping();
            if (keepass2android.kbbridge.KeyboardData.entryName == null) {
                openOrChangeEntry.displayName = openOrChangeEntry.key = getString(R.string.open_entry);
            } else {
                openOrChangeEntry.displayName = openOrChangeEntry.key = getString(R.string.change_entry);
            }
            openOrChangeEntry.value = "KP2ASPECIAL_SelectEntryTask";
            items.add(openOrChangeEntry);


            final String clientPackageName = attribute.packageName;

            if ((clientPackageName != null) && (clientPackageName != "")) {
                StringForTyping searchEntry = new StringForTyping();
                try {
                    searchEntry.key = searchEntry.displayName
                            = getString(R.string.open_entry_for_app, new Object[]{clientPackageName});
                } catch (java.util.FormatFlagsConversionMismatchException e) //buggy crowdin support for Arabic?
                {
                    android.util.Log.e("KP2A", "Please report this error to crocoapps@gmail.com");
                    android.util.Log.e("KP2A", e.toString());

                    searchEntry.key = searchEntry.displayName
                            = "Search entry for app";
                }

                searchEntry.value = "KP2ASPECIAL_SearchUrlTask";
                items.add(searchEntry);
            }


            builder.setTitle(title);

            CharSequence[] itemNames = new CharSequence[items.size()];
            int i = 0;
            for (StringForTyping sft : items)
                itemNames[i++] = sft.displayName;

            builder.setItems(itemNames,
                    new DialogInterface.OnClickListener() {
                        public void onClick(DialogInterface dialog, int item) {


                            if (items.get(item).value.startsWith("KP2ASPECIAL")) {
                                //change entry
                                String packageName = getApplicationContext().getPackageName();
                                Intent startKp2aIntent = getPackageManager().getLaunchIntentForPackage(packageName);
                                if (startKp2aIntent != null) {
                                    startKp2aIntent.addCategory(Intent.CATEGORY_LAUNCHER);
                                    startKp2aIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
                                    String value = items.get(item).value;
                                    String taskName = value.substring("KP2ASPECIAL_".length());
                                    startKp2aIntent.putExtra("KP2A_APPTASK", taskName);
                                    if (taskName.equals("SearchUrlTask")) {
                                        startKp2aIntent.putExtra("UrlToSearch", "androidapp://" + clientPackageName);
                                    }
                                    startActivity(startKp2aIntent);
                                } else Log.w("KP2AK", "didn't find intent for " + packageName);
                            } else {

                                StringForTyping theItem = items.get(item);

                                commitStringForTyping(theItem);

                            }
                        }

                    });

            builder.setNegativeButton(android.R.string.cancel,
                    new DialogInterface.OnClickListener() {
                        public void onClick(DialogInterface dialog, int id) {
                            // User cancelled the dialog
                        }
                    });

            // Create the AlertDialog
            AlertDialog dialog = builder.create();
            Window window = dialog.getWindow();
            WindowManager.LayoutParams lp = window.getAttributes();
            LatinKeyboardView inputView = mKeyboardSwitcher.getInputView();
            lp.token = inputView.getWindowToken();
            lp.type = WindowManager.LayoutParams.TYPE_APPLICATION_ATTACHED_DIALOG;
            window.setAttributes(lp);
            window.addFlags(WindowManager.LayoutParams.FLAG_ALT_FOCUSABLE_IM);

            dialog.show();
        }

	}
	public void commitStringForTyping(StringForTyping theItem) {
		
		if ((mKp2aRememberAutoFill) && (!TextUtils.isEmpty(getCurrentInputEditorInfo().hintText)))
		{
			EditorInfo editorInfo = getCurrentInputEditorInfo();
			String key = editorInfo.packageName+"/"+keepass2android.kbbridge.KeyboardData.entryId+"/"+editorInfo.hintText;
			SharedPreferences prefs = getApplicationContext().getSharedPreferences(KP2A_SAVED_FIELD_HINTS, MODE_PRIVATE);
			
			Editor edit = prefs.edit();
			
			edit.putString(key, theItem.key);
			edit.commit();	
		}
		
		
										

		commitKp2aString(theItem.value, getCurrentInputEditorInfo());
	}

    @Override
    public void onNewData() {
        updateKp2aKeyLabels();
    }


    public void onText(CharSequence text) {
        InputConnection ic = getCurrentInputConnection();
        if (ic == null) return;
        if (text == null)
    	{
        	Log.e("KP2AK", "text = null!");
        	return;
    	}
        abortCorrection(false);
        ic.beginBatchEdit();
        if (mPredicting) {
            commitTyped(ic);
        }
        maybeRemovePreviousPeriod(text);
        ic.commitText(text, 1);
        ic.endBatchEdit();
        updateShiftKeyState(getCurrentInputEditorInfo());
        mKeyboardSwitcher.onKey(0); // dummy key code.
        mJustRevertedSeparator = null;
        mJustAddedAutoSpace = false;
        mEnteredText = text;
    }

    public void onCancel() {
        // User released a finger outside any key
        mKeyboardSwitcher.onCancelInput();
    }

    private void handleBackspace() {
        boolean deleteChar = false;
        InputConnection ic = getCurrentInputConnection();
        if (ic == null) return;

        ic.beginBatchEdit();
       
        if (mPredicting) {
            final int length = mComposing.length();
            if (length > 0) {
                mComposing.delete(length - 1, length);
                mWord.deleteLast();
                ic.setComposingText(mComposing, 1);
                if (mComposing.length() == 0) {
                    mPredicting = false;
                }
                postUpdateSuggestions();
            } else {
                ic.deleteSurroundingText(1, 0);
            }
        } else {
            deleteChar = true;
        }
        postUpdateShiftKeyState();
        TextEntryState.backspace();
        if (TextEntryState.getState() == TextEntryState.State.UNDO_COMMIT) {
            revertLastWord(deleteChar);
            ic.endBatchEdit();
            return;
        } else if (mEnteredText != null && sameAsTextBeforeCursor(ic, mEnteredText)) {
            ic.deleteSurroundingText(mEnteredText.length(), 0);
        } else if (deleteChar) {
            if (mCandidateView != null && mCandidateView.dismissAddToDictionaryHint()) {
                // Go back to the suggestion mode if the user canceled the
                // "Touch again to save".
                // NOTE: In gerenal, we don't revert the word when backspacing
                // from a manual suggestion pick.  We deliberately chose a
                // different behavior only in the case of picking the first
                // suggestion (typed word).  It's intentional to have made this
                // inconsistent with backspacing after selecting other suggestions.
                revertLastWord(deleteChar);
            } else {
                sendDownUpKeyEvents(KeyEvent.KEYCODE_DEL);
                if (mDeleteCount > DELETE_ACCELERATE_AT) {
                    sendDownUpKeyEvents(KeyEvent.KEYCODE_DEL);
                }
            }
        }
        mJustRevertedSeparator = null;
        ic.endBatchEdit();
    }

    private void resetShift() {
        handleShiftInternal(true);
    }

    private void handleShift() {
        handleShiftInternal(false);
    }

    private void handleShiftInternal(boolean forceNormal) {
        mHandler.removeMessages(MSG_UPDATE_SHIFT_STATE);
        KeyboardSwitcher switcher = mKeyboardSwitcher;
        LatinKeyboardView inputView = switcher.getInputView();
        if (switcher.isAlphabetMode()) {
            if (mCapsLock || forceNormal) {
                mCapsLock = false;
                switcher.setShifted(false);
            } else if (inputView != null) {
                if (inputView.isShifted()) {
                    mCapsLock = true;
                    switcher.setShiftLocked(true);
                } else {
                    switcher.setShifted(true);
                }
            }
        } else {
            switcher.toggleShift();
        }
    }

    private void abortCorrection(boolean force) {
        if (force || TextEntryState.isCorrecting()) {
            getCurrentInputConnection().finishComposingText();
            clearSuggestions();
        }
    }

    private void handleCharacter(int primaryCode, int[] keyCodes) {
       
        if (mLastSelectionStart == mLastSelectionEnd && TextEntryState.isCorrecting()) {
            abortCorrection(false);
        }

        if (isAlphabet(primaryCode) && isPredictionOn() && !isCursorTouchingWord()) {
            if (!mPredicting) {
                mPredicting = true;
                mComposing.setLength(0);
                saveWordInHistory(mBestWord);
                mWord.reset();
            }
        }
        if (mKeyboardSwitcher.getInputView().isShifted()) {
            if (keyCodes == null || keyCodes[0] < Character.MIN_CODE_POINT
                    || keyCodes[0] > Character.MAX_CODE_POINT) {
                return;
            }
            primaryCode = keyCodes[0];
            if (mKeyboardSwitcher.isAlphabetMode() && Character.isLowerCase(primaryCode)) {
                // In some locales, such as Turkish, Character.toUpperCase() may return a wrong
                // character because it doesn't take care of locale.
                final String upperCaseString = new String(new int[] {primaryCode}, 0, 1)
                        .toUpperCase(mLanguageSwitcher.getInputLocale());
                if (upperCaseString.codePointCount(0, upperCaseString.length()) == 1) {
                    primaryCode = upperCaseString.codePointAt(0);
                } else {
                    // Some keys, such as [eszett], have upper case as multi-characters.
                    onText(upperCaseString);
                    return;
                }
            }
        }
        if (mPredicting) {
            if (mKeyboardSwitcher.getInputView().isShifted()
                    && mKeyboardSwitcher.isAlphabetMode()
                    && mComposing.length() == 0) {
                mWord.setFirstCharCapitalized(true);
            }
            mComposing.append((char) primaryCode);
            mWord.add(primaryCode, keyCodes);
            InputConnection ic = getCurrentInputConnection();
            if (ic != null) {
                // If it's the first letter, make note of auto-caps state
                if (mWord.size() == 1) {
                    mWord.setAutoCapitalized(
                            getCursorCapsMode(ic, getCurrentInputEditorInfo()) != 0);
                }
                ic.setComposingText(mComposing, 1);
            }
            postUpdateSuggestions();
        } else {
            sendKeyChar((char) primaryCode);
        }
        updateShiftKeyState(getCurrentInputEditorInfo());
        if (KP2AKeyboard.PERF_DEBUG) measureCps();
        TextEntryState.typedCharacter((char) primaryCode, isWordSeparator(primaryCode));
    }

    private void handleSeparator(int primaryCode) {
        // Should dismiss the "Touch again to save" message when handling separator
        if (mCandidateView != null && mCandidateView.dismissAddToDictionaryHint()) {
            postUpdateSuggestions();
        }

        boolean pickedDefault = false;
        // Handle separator
        InputConnection ic = getCurrentInputConnection();
        if (ic != null) {
            ic.beginBatchEdit();
            abortCorrection(false);
        }
        if (mPredicting) {
            // In certain languages where single quote is a separator, it's better
            // not to auto correct, but accept the typed word. For instance,
            // in Italian dov' should not be expanded to dove' because the elision
            // requires the last vowel to be removed.
            if (mAutoCorrectOn && primaryCode != '\'' &&
                    (mJustRevertedSeparator == null
                            || mJustRevertedSeparator.length() == 0
                            || mJustRevertedSeparator.charAt(0) != primaryCode)) {
                pickedDefault = pickDefaultSuggestion();
                // Picked the suggestion by the space key.  We consider this
                // as "added an auto space".
                if (primaryCode == KEYCODE_SPACE) {
                    mJustAddedAutoSpace = true;
                }
            } else {
                commitTyped(ic);
            }
        }
        if (mJustAddedAutoSpace && primaryCode == KEYCODE_ENTER) {
            removeTrailingSpace();
            mJustAddedAutoSpace = false;
        }
        sendKeyChar((char)primaryCode);

        // Handle the case of ". ." -> " .." with auto-space if necessary
        // before changing the TextEntryState.
        if (TextEntryState.getState() == TextEntryState.State.PUNCTUATION_AFTER_ACCEPTED
                && primaryCode == KEYCODE_PERIOD) {
            reswapPeriodAndSpace();
        }

        TextEntryState.typedCharacter((char) primaryCode, true);
        if (TextEntryState.getState() == TextEntryState.State.PUNCTUATION_AFTER_ACCEPTED
                && primaryCode != KEYCODE_ENTER) {
            swapPunctuationAndSpace();
        } else if (isPredictionOn() && primaryCode == KEYCODE_SPACE) {
            doubleSpace();
        }
        if (pickedDefault) {
            TextEntryState.backToAcceptedDefault(mWord.getTypedWord());
        }
        updateShiftKeyState(getCurrentInputEditorInfo());
        if (ic != null) {
            ic.endBatchEdit();
        }
    }

    private void handleClose() {
        commitTyped(getCurrentInputConnection());
        requestHideSelf(0);
        if (mKeyboardSwitcher != null) {
            LatinKeyboardView inputView = mKeyboardSwitcher.getInputView();
            if (inputView != null) {
                inputView.closing();
            }
        }
        TextEntryState.endSession();
    }

    private void saveWordInHistory(CharSequence result) {
        if (mWord.size() <= 1) {
            mWord.reset();
            return;
        }
        // Skip if result is null. It happens in some edge case.
        if (TextUtils.isEmpty(result)) {
            return;
        }

        // Make a copy of the CharSequence, since it is/could be a mutable CharSequence
        final String resultCopy = result.toString();
        TypedWordAlternatives entry = new TypedWordAlternatives(resultCopy,
                new WordComposer(mWord));
        mWordHistory.add(entry);
    }

    private void postUpdateSuggestions() {
        mHandler.removeMessages(MSG_UPDATE_SUGGESTIONS);
        mHandler.sendMessageDelayed(mHandler.obtainMessage(MSG_UPDATE_SUGGESTIONS), 100);
    }

    private void postUpdateOldSuggestions() {
        mHandler.removeMessages(MSG_UPDATE_OLD_SUGGESTIONS);
        mHandler.sendMessageDelayed(mHandler.obtainMessage(MSG_UPDATE_OLD_SUGGESTIONS), 300);
    }

    private boolean isPredictionOn() {
        return mPredictionOn;
    }

    private boolean isCandidateStripVisible() {
        return isPredictionOn() && mShowSuggestions;
    }

    public void onCancelVoice() {
        if (mRecognizing) {
            switchToKeyboardView();
        }
    }

    private void switchToKeyboardView() {
      mHandler.post(new Runnable() {
          public void run() {
              mRecognizing = false;
              if (mKeyboardSwitcher.getInputView() != null) {
                  setInputView(mKeyboardSwitcher.getInputView());
              }
              setCandidatesViewShown(true);
              updateInputViewShown();
              updateKp2aKeyLabels();
              postUpdateSuggestions();
          }
      });
    }
    
    private void clearSuggestions() {
        setSuggestions(null, false, false, false);
    }

    private void setSuggestions(
            List<CharSequence> suggestions,
            boolean completions,
            boolean typedWordValid,
            boolean haveMinimalSuggestion) {

        if (mIsShowingHint) {
             setCandidatesView(mCandidateViewContainer);
             mIsShowingHint = false;
        }

        if (mCandidateView != null) {
            mCandidateView.setSuggestions(
                    suggestions, completions, typedWordValid, haveMinimalSuggestion);
        }
    }

    private void updateSuggestions() {
        LatinKeyboardView inputView = mKeyboardSwitcher.getInputView();
        ((LatinKeyboard) inputView.getKeyboard()).setPreferredLetters(null);

        // Check if we have a suggestion engine attached.
        if ((mSuggest == null || !isPredictionOn())) {
            return;
        }

        if (!mPredicting) {
            setNextSuggestions();
            return;
        }
        showSuggestions(mWord);
    }

    private List<CharSequence> getTypedSuggestions(WordComposer word) {
        List<CharSequence> stringList = mSuggest.getSuggestions(
                mKeyboardSwitcher.getInputView(), word, false, null);
        return stringList;
    }

    private void showCorrections(WordAlternatives alternatives) {
        List<CharSequence> stringList = alternatives.getAlternatives();
        ((LatinKeyboard) mKeyboardSwitcher.getInputView().getKeyboard()).setPreferredLetters(null);
        showSuggestions(stringList, alternatives.getOriginalWord(), false, false);
    }

    private void showSuggestions(WordComposer word) {
        // long startTime = System.currentTimeMillis(); // TIME MEASUREMENT!
        // TODO Maybe need better way of retrieving previous word
        CharSequence prevWord = EditingUtil.getPreviousWord(getCurrentInputConnection(),
                mWordSeparators);
        List<CharSequence> stringList = mSuggest.getSuggestions(
                mKeyboardSwitcher.getInputView(), word, false, prevWord);
        // long stopTime = System.currentTimeMillis(); // TIME MEASUREMENT!
        // Log.d("LatinIME","Suggest Total Time - " + (stopTime - startTime));

        int[] nextLettersFrequencies = mSuggest.getNextLettersFrequencies();

        ((LatinKeyboard) mKeyboardSwitcher.getInputView().getKeyboard()).setPreferredLetters(
                nextLettersFrequencies);

        boolean correctionAvailable = !mInputTypeNoAutoCorrect && mSuggest.hasMinimalCorrection();
        //|| mCorrectionMode == mSuggest.CORRECTION_FULL;
        CharSequence typedWord = word.getTypedWord();
        // If we're in basic correct
        boolean typedWordValid = mSuggest.isValidWord(typedWord) ||
                (preferCapitalization()
                        && mSuggest.isValidWord(typedWord.toString().toLowerCase()));
        if (mCorrectionMode == Suggest.CORRECTION_FULL
                || mCorrectionMode == Suggest.CORRECTION_FULL_BIGRAM) {
            correctionAvailable |= typedWordValid;
        }
        // Don't auto-correct words with multiple capital letter
        correctionAvailable &= !word.isMostlyCaps();
        correctionAvailable &= !TextEntryState.isCorrecting();

        showSuggestions(stringList, typedWord, typedWordValid, correctionAvailable);
    }

    private void showSuggestions(List<CharSequence> stringList, CharSequence typedWord,
            boolean typedWordValid, boolean correctionAvailable) {
        setSuggestions(stringList, false, typedWordValid, correctionAvailable);
        if (stringList.size() > 0) {
            if (correctionAvailable && !typedWordValid && stringList.size() > 1) {
                mBestWord = stringList.get(1);
            } else {
                mBestWord = typedWord;
            }
        } else {
            mBestWord = null;
        }
        setCandidatesViewShown(isCandidateStripVisible() || mCompletionOn);
    }

    private boolean pickDefaultSuggestion() {
        // Complete any pending candidate query first
        if (mHandler.hasMessages(MSG_UPDATE_SUGGESTIONS)) {
            mHandler.removeMessages(MSG_UPDATE_SUGGESTIONS);
            updateSuggestions();
        }
        if (mBestWord != null && mBestWord.length() > 0) {
            TextEntryState.acceptedDefault(mWord.getTypedWord(), mBestWord);
            mJustAccepted = true;
            pickSuggestion(mBestWord, false);
            // Add the word to the auto dictionary if it's not a known word
            addToDictionaries(mBestWord, AutoDictionary.FREQUENCY_FOR_TYPED);
            return true;

        }
        return false;
    }

    public void pickSuggestionManually(int index, CharSequence suggestion) {
        List<CharSequence> suggestions = mCandidateView.getSuggestions();

        

        final boolean correcting = TextEntryState.isCorrecting();
        InputConnection ic = getCurrentInputConnection();
        if (ic != null) {
            ic.beginBatchEdit();
        }
        if (mCompletionOn && mCompletions != null && index >= 0
                && index < mCompletions.length) {
            CompletionInfo ci = mCompletions[index];
            if (ic != null) {
                ic.commitCompletion(ci);
            }
            mCommittedLength = suggestion.length();
            if (mCandidateView != null) {
                mCandidateView.clear();
            }
            updateShiftKeyState(getCurrentInputEditorInfo());
            if (ic != null) {
                ic.endBatchEdit();
            }
            return;
        }

        // If this is a punctuation, apply it through the normal key press
        if (suggestion.length() == 1 && (isWordSeparator(suggestion.charAt(0))
                || isSuggestedPunctuation(suggestion.charAt(0)))) {
            // Word separators are suggested before the user inputs something.
            // So, LatinImeLogger logs "" as a user's input.
            LatinImeLogger.logOnManualSuggestion(
                    "", suggestion.toString(), index, suggestions);
            final char primaryCode = suggestion.charAt(0);
            onKey(primaryCode, new int[]{primaryCode}, LatinKeyboardBaseView.NOT_A_TOUCH_COORDINATE,
                    LatinKeyboardBaseView.NOT_A_TOUCH_COORDINATE);
            if (ic != null) {
                ic.endBatchEdit();
            }
            return;
        }
        mJustAccepted = true;
        pickSuggestion(suggestion, correcting);
        // Add the word to the auto dictionary if it's not a known word
        if (index == 0) {
            addToDictionaries(suggestion, AutoDictionary.FREQUENCY_FOR_PICKED);
        } else {
            addToBigramDictionary(suggestion, 1);
        }
        LatinImeLogger.logOnManualSuggestion(mComposing.toString(), suggestion.toString(),
                index, suggestions);
        TextEntryState.acceptedSuggestion(mComposing.toString(), suggestion);
        // Follow it with a space
        if (mAutoSpace && !correcting) {
            sendSpace();
            mJustAddedAutoSpace = true;
        }

        final boolean showingAddToDictionaryHint = index == 0 && mCorrectionMode > 0
                && !mSuggest.isValidWord(suggestion)
                && !mSuggest.isValidWord(suggestion.toString().toLowerCase());

        if (!correcting) {
            // Fool the state watcher so that a subsequent backspace will not do a revert, unless
            // we just did a correction, in which case we need to stay in
            // TextEntryState.State.PICKED_SUGGESTION state.
            TextEntryState.typedCharacter((char) KEYCODE_SPACE, true);
            setNextSuggestions();
        } else if (!showingAddToDictionaryHint) {
            // If we're not showing the "Touch again to save", then show corrections again.
            // In case the cursor position doesn't change, make sure we show the suggestions again.
            clearSuggestions();
            postUpdateOldSuggestions();
        }
        if (showingAddToDictionaryHint) {
            mCandidateView.showAddToDictionaryHint(suggestion);
        }
        if (ic != null) {
            ic.endBatchEdit();
        }
    }

    private void rememberReplacedWord(CharSequence suggestion) {
        
    }

    /**
     * Commits the chosen word to the text field and saves it for later
     * retrieval.
     * @param suggestion the suggestion picked by the user to be committed to
     *            the text field
     * @param correcting whether this is due to a correction of an existing
     *            word.
     */
    private void pickSuggestion(CharSequence suggestion, boolean correcting) {
        final LatinKeyboardView inputView = mKeyboardSwitcher.getInputView();
        final Locale inputLocale = mLanguageSwitcher.getInputLocale();
        if (mCapsLock) {
            suggestion = suggestion.toString().toUpperCase(inputLocale);
        } else if (preferCapitalization()
                || (mKeyboardSwitcher.isAlphabetMode()
                        && inputView.isShifted())) {
            suggestion = suggestion.toString().toUpperCase(inputLocale).charAt(0)
                    + suggestion.subSequence(1, suggestion.length()).toString();
        }
        InputConnection ic = getCurrentInputConnection();
        if (ic != null) {
            rememberReplacedWord(suggestion);
            ic.commitText(suggestion, 1);
        }
        saveWordInHistory(suggestion);
        mPredicting = false;
        mCommittedLength = suggestion.length();
        ((LatinKeyboard) inputView.getKeyboard()).setPreferredLetters(null);
        // If we just corrected a word, then don't show punctuations
        if (!correcting) {
            setNextSuggestions();
        }
        updateShiftKeyState(getCurrentInputEditorInfo());
    }

    /**
     * Tries to apply any typed alternatives for the word if we have any cached alternatives,
     * otherwise tries to find new corrections and completions for the word.
     * @param touching The word that the cursor is touching, with position information
     * @return true if an alternative was found, false otherwise.
     */
    private boolean applyTypedAlternatives(EditingUtil.SelectedWord touching) {
        // If we didn't find a match, search for result in typed word history
        WordComposer foundWord = null;
        WordAlternatives alternatives = null;
        for (WordAlternatives entry : mWordHistory) {
            if (TextUtils.equals(entry.getChosenWord(), touching.word)) {
                if (entry instanceof TypedWordAlternatives) {
                    foundWord = ((TypedWordAlternatives) entry).word;
                }
                alternatives = entry;
                break;
            }
        }
        // If we didn't find a match, at least suggest completions
        if (foundWord == null
                && (mSuggest.isValidWord(touching.word)
                        || mSuggest.isValidWord(touching.word.toString().toLowerCase()))) {
            foundWord = new WordComposer();
            for (int i = 0; i < touching.word.length(); i++) {
                foundWord.add(touching.word.charAt(i), new int[] {
                    touching.word.charAt(i)
                });
            }
            foundWord.setFirstCharCapitalized(Character.isUpperCase(touching.word.charAt(0)));
        }
        // Found a match, show suggestions
        if (foundWord != null || alternatives != null) {
            if (alternatives == null) {
                alternatives = new TypedWordAlternatives(touching.word, foundWord);
            }
            showCorrections(alternatives);
            if (foundWord != null) {
                mWord = new WordComposer(foundWord);
            } else {
                mWord.reset();
            }
            return true;
        }
        return false;
    }

    private void setOldSuggestions() {
        
        if (mCandidateView != null && mCandidateView.isShowingAddToDictionaryHint()) {
            return;
        }
        InputConnection ic = getCurrentInputConnection();
        if (ic == null) return;
        if (!mPredicting) {
            // Extract the selected or touching text
            EditingUtil.SelectedWord touching = EditingUtil.getWordAtCursorOrSelection(ic,
                    mLastSelectionStart, mLastSelectionEnd, mWordSeparators);

            if (touching != null && touching.word.length() > 1) {
                ic.beginBatchEdit();

                if (!applyTypedAlternatives(touching)) {
                    abortCorrection(true);
                } else {
                    TextEntryState.selectedForCorrection();
                    EditingUtil.underlineWord(ic, touching);
                }

                ic.endBatchEdit();
            } else {
                abortCorrection(true);
                setNextSuggestions();  // Show the punctuation suggestions list
            }
        } else {
            abortCorrection(true);
        }
    }

    private void setNextSuggestions() {
        setSuggestions(mSuggestPuncList, false, false, false);
    }

    private void addToDictionaries(CharSequence suggestion, int frequencyDelta) {
        checkAddToDictionary(suggestion, frequencyDelta, false);
    }

    private void addToBigramDictionary(CharSequence suggestion, int frequencyDelta) {
        checkAddToDictionary(suggestion, frequencyDelta, true);
    }

    /**
     * Adds to the UserBigramDictionary and/or AutoDictionary
     * @param addToBigramDictionary true if it should be added to bigram dictionary if possible
     */
    private void checkAddToDictionary(CharSequence suggestion, int frequencyDelta,
            boolean addToBigramDictionary) {
        if (suggestion == null || suggestion.length() < 1) return;
        // Only auto-add to dictionary if auto-correct is ON. Otherwise we'll be
        // adding words in situations where the user or application really didn't
        // want corrections enabled or learned.
        if (!(mCorrectionMode == Suggest.CORRECTION_FULL
                || mCorrectionMode == Suggest.CORRECTION_FULL_BIGRAM)) {
            return;
        }
        if (suggestion != null) {
            /*if (!addToBigramDictionary && mAutoDictionary.isValidWord(suggestion)
                    || (!mSuggest.isValidWord(suggestion.toString())
                    && !mSuggest.isValidWord(suggestion.toString().toLowerCase()))) {
                mAutoDictionary.addWord(suggestion.toString(), frequencyDelta);
            }

            if (mUserBigramDictionary != null) {
                CharSequence prevWord = EditingUtil.getPreviousWord(getCurrentInputConnection(),
                        mSentenceSeparators);
                if (!TextUtils.isEmpty(prevWord)) {
                    mUserBigramDictionary.addBigrams(prevWord.toString(), suggestion.toString());
                }
            }*/
        }
    }

    private boolean isCursorTouchingWord() {
        InputConnection ic = getCurrentInputConnection();
        if (ic == null) return false;
        CharSequence toLeft = ic.getTextBeforeCursor(1, 0);
        CharSequence toRight = ic.getTextAfterCursor(1, 0);
        if (!TextUtils.isEmpty(toLeft)
                && !isWordSeparator(toLeft.charAt(0))
                && !isSuggestedPunctuation(toLeft.charAt(0))) {
            return true;
        }
        if (!TextUtils.isEmpty(toRight)
                && !isWordSeparator(toRight.charAt(0))
                && !isSuggestedPunctuation(toRight.charAt(0))) {
            return true;
        }
        return false;
    }

    private boolean sameAsTextBeforeCursor(InputConnection ic, CharSequence text) {
        CharSequence beforeText = ic.getTextBeforeCursor(text.length(), 0);
        return TextUtils.equals(text, beforeText);
    }

    public void revertLastWord(boolean deleteChar) {
        final int length = mComposing.length();
        if (!mPredicting && length > 0) {
            final InputConnection ic = getCurrentInputConnection();
            mPredicting = true;
            mJustRevertedSeparator = ic.getTextBeforeCursor(1, 0);
            if (deleteChar) ic.deleteSurroundingText(1, 0);
            int toDelete = mCommittedLength;
            CharSequence toTheLeft = ic.getTextBeforeCursor(mCommittedLength, 0);
            if (toTheLeft != null && toTheLeft.length() > 0
                    && isWordSeparator(toTheLeft.charAt(0))) {
                toDelete--;
            }
            ic.deleteSurroundingText(toDelete, 0);
            ic.setComposingText(mComposing, 1);
            TextEntryState.backspace();
            postUpdateSuggestions();
        } else {
            sendDownUpKeyEvents(KeyEvent.KEYCODE_DEL);
            mJustRevertedSeparator = null;
        }
    }

    protected String getWordSeparators() {
        return mWordSeparators;
    }

    public boolean isWordSeparator(int code) {
        String separators = getWordSeparators();
        return separators.contains(String.valueOf((char) code));
    }

    private boolean isSentenceSeparator(int code) {
        return mSentenceSeparators.contains(String.valueOf((char)code));
    }

    private void sendSpace() {
        sendKeyChar((char)KEYCODE_SPACE);
        updateShiftKeyState(getCurrentInputEditorInfo());
        //onKey(KEY_SPACE[0], KEY_SPACE);
    }

    public boolean preferCapitalization() {
        return mWord.isFirstCharCapitalized();
    }

    void toggleLanguage(boolean reset, boolean next) {
        if (reset) {
            mLanguageSwitcher.reset();
        } else {
            if (next) {
                mLanguageSwitcher.next();
            } else {
                mLanguageSwitcher.prev();
            }
        }
        int currentKeyboardMode = mKeyboardSwitcher.getKeyboardMode();
        reloadKeyboards();
        mKeyboardSwitcher.makeKeyboards(true);
        Log.d("KP2AK", "toggleLanguage -> setKM");
        mKeyboardSwitcher.setKeyboardMode(currentKeyboardMode, 0);
        initSuggest(mLanguageSwitcher.getInputLanguage());
        mLanguageSwitcher.persist();
        updateShiftKeyState(getCurrentInputEditorInfo());
    }

    public void onSharedPreferenceChanged(SharedPreferences sharedPreferences,
            String key) {
        if (PREF_SELECTED_LANGUAGES.equals(key)) {
            mLanguageSwitcher.loadLocales(sharedPreferences);
            mRefreshKeyboardRequired = true;
        } else if (PREF_RECORRECTION_ENABLED.equals(key)) {
            mReCorrectionEnabled = sharedPreferences.getBoolean(PREF_RECORRECTION_ENABLED,
                    getResources().getBoolean(R.bool.default_recorrection_enabled));
        }
        if (PREF_KP2A_REMEMBER_AUTO_FILL.equals(key))
        {
        	if (sharedPreferences.getBoolean(key, true) == false)
        	{
        		Log.d("KP2AK", "clearing saved field hints");
        		SharedPreferences savedHints = getApplicationContext().getSharedPreferences(KP2A_SAVED_FIELD_HINTS, MODE_PRIVATE);
        		Editor edit = savedHints.edit();
    			edit.clear();
    			edit.commit();
				
        	}
        }
    }

    public void swipeRight() {
        if (LatinKeyboardView.DEBUG_AUTO_PLAY) {
            ClipboardManager cm = ((ClipboardManager)getSystemService(CLIPBOARD_SERVICE));
            CharSequence text = cm.getText();
            if (!TextUtils.isEmpty(text)) {
                mKeyboardSwitcher.getInputView().startPlaying(text.toString());
            }
        }
    }

    public void swipeLeft() {
    }

    public void swipeDown() {
        handleClose();
    }

    public void swipeUp() {
        //launchSettings();
    }

    public void onPress(int primaryCode) {
        if (mKeyboardSwitcher.isVibrateAndSoundFeedbackRequired()) {
            vibrate();
            playKeyClick(primaryCode);
        }
        final boolean distinctMultiTouch = mKeyboardSwitcher.hasDistinctMultitouch();
        if (distinctMultiTouch && primaryCode == Keyboard.KEYCODE_SHIFT) {
            mShiftKeyState.onPress();
            handleShift();
        } else if (distinctMultiTouch && primaryCode == Keyboard.KEYCODE_MODE_CHANGE) {
            changeKeyboardMode();
            mSymbolKeyState.onPress();
            mKeyboardSwitcher.setAutoModeSwitchStateMomentary();
        } else {
            mShiftKeyState.onOtherKeyPressed();
            mSymbolKeyState.onOtherKeyPressed();
        }
    }

    public void onRelease(int primaryCode) {
        // Reset any drag flags in the keyboard
        ((LatinKeyboard) mKeyboardSwitcher.getInputView().getKeyboard()).keyReleased();
        //vibrate();
        final boolean distinctMultiTouch = mKeyboardSwitcher.hasDistinctMultitouch();
        if (distinctMultiTouch && primaryCode == Keyboard.KEYCODE_SHIFT) {
            if (mShiftKeyState.isMomentary())
                resetShift();
            mShiftKeyState.onRelease();
        } else if (distinctMultiTouch && primaryCode == Keyboard.KEYCODE_MODE_CHANGE) {
            // Snap back to the previous keyboard mode if the user chords the mode change key and
            // other key, then released the mode change key.
            if (mKeyboardSwitcher.isInChordingAutoModeSwitchState())
                changeKeyboardMode();
            mSymbolKeyState.onRelease();
        }
    }

        
    
    // receive ringer mode changes to detect silent mode
    private BroadcastReceiver mSilentModeReceiver = new BroadcastReceiver() {
        @Override
        public void onReceive(Context context, Intent intent) {
            updateRingerMode();
        }
    };


    private BroadcastReceiver mCommitForTypingReceiver = new BroadcastReceiver() {
        @Override
        public void onReceive(Context context, Intent intent) {
            StringForTyping stringForTyping = new StringForTyping();
            stringForTyping.key = intent.getStringExtra("key");
            stringForTyping.value = intent.getStringExtra("value");

            KP2AKeyboard.this.commitStringForTyping(stringForTyping);

        }
    };

    // update flags for silent mode
    private void updateRingerMode() {
        if (mAudioManager == null) {
            mAudioManager = (AudioManager) getSystemService(Context.AUDIO_SERVICE);
        }
        if (mAudioManager != null) {
            mSilentMode = (mAudioManager.getRingerMode() != AudioManager.RINGER_MODE_NORMAL);
        }
    }

    private void playKeyClick(int primaryCode) {
        // if mAudioManager is null, we don't have the ringer state yet
        // mAudioManager will be set by updateRingerMode
        if (mAudioManager == null) {
            if (mKeyboardSwitcher.getInputView() != null) {
                updateRingerMode();
            }
        }
        if (mSoundOn && !mSilentMode) {
            // FIXME: Volume and enable should come from UI settings
            // FIXME: These should be triggered after auto-repeat logic
            int sound = AudioManager.FX_KEYPRESS_STANDARD;
            switch (primaryCode) {
                case Keyboard.KEYCODE_DELETE:
                    sound = AudioManager.FX_KEYPRESS_DELETE;
                    break;
                case KEYCODE_ENTER:
                    sound = AudioManager.FX_KEYPRESS_RETURN;
                    break;
                case KEYCODE_SPACE:
                    sound = AudioManager.FX_KEYPRESS_SPACEBAR;
                    break;
            }
            mAudioManager.playSoundEffect(sound, FX_VOLUME);
        }
    }

    private void vibrate() {
        if (!mVibrateOn) {
            return;
        }
        if (mKeyboardSwitcher.getInputView() != null) {
            mKeyboardSwitcher.getInputView().performHapticFeedback(
                    HapticFeedbackConstants.KEYBOARD_TAP,
                    HapticFeedbackConstants.FLAG_IGNORE_GLOBAL_SETTING);
        }
    }

    /* package */ void promoteToUserDictionary(String word, int frequency) {
        //if (mUserDictionary.isValidWord(word)) return;
        //mUserDictionary.addWord(word, frequency);
    }

    /* package */ WordComposer getCurrentWord() {
        return mWord;
    }

    /* package */ boolean getPopupOn() {
        return mPopupOn;
    }

    private void updateCorrectionMode() {
        mHasDictionary = mSuggest != null ? mSuggest.hasMainDictionary() : false;
        mAutoCorrectOn = (mAutoCorrectEnabled || mQuickFixes)
                && !mInputTypeNoAutoCorrect && mHasDictionary;
        mCorrectionMode = (mAutoCorrectOn && mAutoCorrectEnabled)
                ? Suggest.CORRECTION_FULL
                : (mAutoCorrectOn ? Suggest.CORRECTION_BASIC : Suggest.CORRECTION_NONE);
        mCorrectionMode = (mBigramSuggestionEnabled && mAutoCorrectOn && mAutoCorrectEnabled)
                ? Suggest.CORRECTION_FULL_BIGRAM : mCorrectionMode;
        if (mSuggest != null) {
            mSuggest.setCorrectionMode(mCorrectionMode);
        }
    }

    private void updateAutoTextEnabled(Locale systemLocale) {
        if (mSuggest == null) return;
        boolean different =
                !systemLocale.getLanguage().equalsIgnoreCase(mInputLocale.substring(0, 2));
        mSuggest.setAutoTextEnabled(!different && mQuickFixes);
    }

    protected void launchSettings() {
        launchSettings(LatinIMESettings.class);
    }

    public void launchDebugSettings() {
        launchSettings(LatinIMEDebugSettings.class);
    }

    protected void launchSettings (Class<? extends PreferenceActivity> settingsClass) {
        handleClose();
        Intent intent = new Intent();
        intent.setClass(KP2AKeyboard.this, settingsClass);
        intent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        startActivity(intent);
    }

    private void loadSettings()
    {
        try {
            // Get the settings preferences
            SharedPreferences sp = PreferenceManager.getDefaultSharedPreferences(this);
            mVibrateOn = sp.getBoolean(PREF_VIBRATE_ON, false);
            mSoundOn = sp.getBoolean(PREF_SOUND_ON, false);
            mPopupOn = sp.getBoolean(PREF_POPUP_ON,
                    mResources.getBoolean(R.bool.default_popup_preview));
            mAutoCap = sp.getBoolean(PREF_AUTO_CAP, true);
            mQuickFixes = sp.getBoolean(PREF_QUICK_FIXES, true);

            mKp2aAutoFillOn = sp.getBoolean("kp2a_auto_fill", true);
            mKp2aRememberAutoFill = sp.getBoolean(PREF_KP2A_REMEMBER_AUTO_FILL, true);
            mKp2aEnableSimpleKeyboard = sp.getBoolean("kp2a_simple_keyboard", true);
            mKp2aSwitchKeyboardOnSendGoDone = sp.getBoolean("kp2a_switch_on_sendgodone", false);
            mKp2aLockOnSendGoDone = sp.getBoolean("kp2a_lock_on_sendgodone", false);


            mShowSuggestions = sp.getBoolean(PREF_SHOW_SUGGESTIONS, true);

            mAutoCorrectEnabled = sp.getBoolean(PREF_AUTO_COMPLETE,
                    mResources.getBoolean(R.bool.enable_autocorrect)) & mShowSuggestions;
            //mBigramSuggestionEnabled = sp.getBoolean(
            //        PREF_BIGRAM_SUGGESTIONS, true) & mShowSuggestions;
            updateCorrectionMode();
            updateAutoTextEnabled(mResources.getConfiguration().locale);
            mLanguageSwitcher.loadLocales(sp);
        }
        catch (Exception e)
        {
            android.util.Log.d("KP2AK", e.toString());
            //in a very special case loading the settings might fail. revert to defaults then.
            /*this case occurs on some Samsung devices when an external Bluetooth Keyboard is connected
             the code is then run from a different user id.
              */
            mQuickFixes = true;

            mKp2aAutoFillOn = true;
            mKp2aRememberAutoFill = true;
            mKp2aEnableSimpleKeyboard = true;

            mShowSuggestions = true;

            mAutoCorrectEnabled = mResources.getBoolean(R.bool.enable_autocorrect) & mShowSuggestions;
            updateCorrectionMode();
            updateAutoTextEnabled(mResources.getConfiguration().locale);
        }
    }

    private void initSuggestPuncList() {
        mSuggestPuncList = new ArrayList<CharSequence>();
        mSuggestPuncs = mResources.getString(R.string.suggested_punctuations);
        if (mSuggestPuncs != null) {
            for (int i = 0; i < mSuggestPuncs.length(); i++) {
                mSuggestPuncList.add(mSuggestPuncs.subSequence(i, i + 1));
            }
        }
    }

    private boolean isSuggestedPunctuation(int code) {
        return mSuggestPuncs.contains(String.valueOf((char)code));
    }

    private void showOptionsMenu() {
        AlertDialog.Builder builder = new AlertDialog.Builder(this);
        builder.setCancelable(true);
        builder.setIcon(R.drawable.ic_dialog_keyboard);
        builder.setNegativeButton(android.R.string.cancel, null);
        CharSequence itemSettings = getString(R.string.english_ime_settings);
        CharSequence itemInputMethod = getString(R.string.selectInputMethod);
        builder.setItems(new CharSequence[] {
                itemInputMethod, itemSettings},
                new DialogInterface.OnClickListener() {

            public void onClick(DialogInterface di, int position) {
                di.dismiss();
                switch (position) {
                    case POS_SETTINGS:
                        launchSettings();
                        break;
                    case POS_METHOD:
                        ((InputMethodManager) getSystemService(INPUT_METHOD_SERVICE))
                            .showInputMethodPicker();
                        break;
                }
            }
        });
        builder.setTitle(mResources.getString(R.string.english_ime_input_options));
        mOptionsDialog = builder.create();
        Window window = mOptionsDialog.getWindow();
        WindowManager.LayoutParams lp = window.getAttributes();
        lp.token = mKeyboardSwitcher.getInputView().getWindowToken();
        lp.type = WindowManager.LayoutParams.TYPE_APPLICATION_ATTACHED_DIALOG;
        window.setAttributes(lp);
        window.addFlags(WindowManager.LayoutParams.FLAG_ALT_FOCUSABLE_IM);
        mOptionsDialog.show();
    }

    public void changeKeyboardMode() {
        mKeyboardSwitcher.toggleSymbols();
        if (mCapsLock && mKeyboardSwitcher.isAlphabetMode()) {
            mKeyboardSwitcher.setShiftLocked(mCapsLock);
        }

        updateShiftKeyState(getCurrentInputEditorInfo());
    }

    public static <E> ArrayList<E> newArrayList(E... elements) {
        int capacity = (elements.length * 110) / 100 + 5;
        ArrayList<E> list = new ArrayList<E>(capacity);
        Collections.addAll(list, elements);
        return list;
    }

    @Override
    protected void dump(FileDescriptor fd, PrintWriter fout, String[] args) {
        super.dump(fd, fout, args);

        final Printer p = new PrintWriterPrinter(fout);
        p.println("LatinIME state :");
        p.println("  Keyboard mode = " + mKeyboardSwitcher.getKeyboardMode());
        p.println("  mCapsLock=" + mCapsLock);
        p.println("  mComposing=" + mComposing.toString());
        p.println("  mPredictionOn=" + mPredictionOn);
        p.println("  mCorrectionMode=" + mCorrectionMode);
        p.println("  mPredicting=" + mPredicting);
        p.println("  mAutoCorrectOn=" + mAutoCorrectOn);
        p.println("  mAutoSpace=" + mAutoSpace);
        p.println("  mCompletionOn=" + mCompletionOn);
        p.println("  TextEntryState.state=" + TextEntryState.getState());
        p.println("  mSoundOn=" + mSoundOn);
        p.println("  mVibrateOn=" + mVibrateOn);
        p.println("  mPopupOn=" + mPopupOn);
    }

    // Characters per second measurement

    private long mLastCpsTime;
    private static final int CPS_BUFFER_SIZE = 16;
    private long[] mCpsIntervals = new long[CPS_BUFFER_SIZE];
    private int mCpsIndex;

    private void measureCps() {
        long now = System.currentTimeMillis();
        if (mLastCpsTime == 0) mLastCpsTime = now - 100; // Initial
        mCpsIntervals[mCpsIndex] = now - mLastCpsTime;
        mLastCpsTime = now;
        mCpsIndex = (mCpsIndex + 1) % CPS_BUFFER_SIZE;
        long total = 0;
        for (int i = 0; i < CPS_BUFFER_SIZE; i++) total += mCpsIntervals[i];
        System.out.println("CPS = " + ((CPS_BUFFER_SIZE * 1000f) / total));
    }

    public void onAutoCompletionStateChanged(boolean isAutoCompletion) {
        mKeyboardSwitcher.onAutoCompletionStateChanged(isAutoCompletion);
    }
}

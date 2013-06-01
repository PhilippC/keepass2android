/*
 * Copyright (C) 2008-2009 The Android Open Source Project
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

import android.app.AlertDialog;
import android.content.ComponentName;
import android.content.DialogInterface;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.inputmethodservice.InputMethodService;
import android.inputmethodservice.Keyboard;
import android.inputmethodservice.KeyboardView;
import android.provider.Settings;
import android.text.InputType;
import android.view.KeyEvent;
import android.view.View;
import android.view.Window;
import android.view.WindowManager;
import android.view.inputmethod.CompletionInfo;
import android.view.inputmethod.EditorInfo;
import android.view.inputmethod.InputConnection;
import android.view.inputmethod.InputMethodManager;
import android.view.inputmethod.InputMethodSubtype;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;

import keepass2android.kbbridge.StringForTyping;
import keepass2android.softkeyboard.R;

/**
 * Example of writing an input method for a soft keyboard. This code is focused
 * on simplicity over completeness, so it should in no way be considered to be a
 * complete soft keyboard implementation. Its purpose is to provide a basic
 * example for how you would get started writing an input method, to be fleshed
 * out as appropriate.
 */
public class KP2AKeyboard extends InputMethodService implements
		KeyboardView.OnKeyboardActionListener {
	static final boolean DEBUG = false;

	private InputMethodManager mInputMethodManager;

	private LatinKeyboardView mInputView;
	private CandidateView mCandidateView;
	private CompletionInfo[] mCompletions;

	private StringBuilder mComposing = new StringBuilder();
	private boolean mPredictionOn;
	private boolean mCompletionOn;
	private int mLastDisplayWidth;
	private boolean mCapsLock;
	private long mLastShiftTime;
	private long mMetaState;

	private LatinKeyboard mSymbolsKeyboard;
	private LatinKeyboard mSymbolsShiftedKeyboard;
	private LatinKeyboard mQwertyKeyboard;

	private LatinKeyboard mCurKeyboard;

	private String mWordSeparators;

	private String mClientPackageName;

	/**
	 * Main initialization of the input method component. Be sure to call to
	 * super class.
	 */
	@Override
	public void onCreate() {
		super.onCreate();
		mInputMethodManager = (InputMethodManager) getSystemService(INPUT_METHOD_SERVICE);
		mWordSeparators = getResources().getString(R.string.word_separators);
	}

	/**
	 * This is the point where you can do all of your UI initialization. It is
	 * called after creation and any configuration change.
	 */
	@Override
	public void onInitializeInterface() {
		if (mQwertyKeyboard != null) {
			// Configuration changes can happen after the keyboard gets
			// recreated,
			// so we need to be able to re-build the keyboards if the available
			// space has changed.
			int displayWidth = getMaxWidth();
			if (displayWidth == mLastDisplayWidth)
				return;
			mLastDisplayWidth = displayWidth;
		}
		mQwertyKeyboard = new LatinKeyboard(this, R.xml.qwerty);
		mSymbolsKeyboard = new LatinKeyboard(this, R.xml.symbols);
		mSymbolsShiftedKeyboard = new LatinKeyboard(this, R.xml.symbols_shift);
	}

	/**
	 * Called by the framework when your view for creating input needs to be
	 * generated. This will be called the first time your input method is
	 * displayed, and every time it needs to be re-created such as due to a
	 * configuration change.
	 */
	@Override
	public View onCreateInputView() {
		mInputView = (LatinKeyboardView) getLayoutInflater().inflate(
				R.layout.input, null);
		mInputView.setOnKeyboardActionListener(this);
		mInputView.setKeyboard(mQwertyKeyboard);
		return mInputView;
	}

	/**
	 * Called by the framework when your view for showing candidates needs to be
	 * generated, like {@link #onCreateInputView}.
	 */
	@Override
	public View onCreateCandidatesView() {
		mCandidateView = new CandidateView(this);
		mCandidateView.setService(this);
		return mCandidateView;
	}

	/**
	 * This is the main point where we do our initialization of the input method
	 * to begin operating on an application. At this point we have been bound to
	 * the client, and are now receiving all of the detailed information about
	 * the target of our edits.
	 */
	@Override
	public void onStartInput(EditorInfo attribute, boolean restarting) {
		super.onStartInput(attribute, restarting);

		// Reset our state. We want to do this even if restarting, because
		// the underlying state of the text editor could have changed in any
		// way.
		mComposing.setLength(0);
		updateCandidates();

		if (!restarting) {
			// Clear shift states.
			mMetaState = 0;
		}
		
		mClientPackageName = attribute.packageName;

		mPredictionOn = false;
		mCompletionOn = false;
		mCompletions = null;

		// We are now going to initialize our state based on the type of
		// text being edited.
		switch (attribute.inputType & InputType.TYPE_MASK_CLASS) {
		case InputType.TYPE_CLASS_NUMBER:
		case InputType.TYPE_CLASS_DATETIME:
			// Numbers and dates default to the symbols keyboard, with
			// no extra features.
			mCurKeyboard = mSymbolsKeyboard;
			break;

		case InputType.TYPE_CLASS_PHONE:
			// Phones will also default to the symbols keyboard, though
			// often you will want to have a dedicated phone keyboard.
			mCurKeyboard = mSymbolsKeyboard;
			break;

		case InputType.TYPE_CLASS_TEXT:
			// This is general text editing. We will default to the
			// normal alphabetic keyboard, and assume that we should
			// be doing predictive text (showing candidates as the
			// user types).
			mCurKeyboard = mQwertyKeyboard;
			mPredictionOn = true;

			// We now look for a few special variations of text that will
			// modify our behavior.
			int variation = attribute.inputType & InputType.TYPE_MASK_VARIATION;
			if (variation == InputType.TYPE_TEXT_VARIATION_PASSWORD
					|| variation == InputType.TYPE_TEXT_VARIATION_VISIBLE_PASSWORD) {
				// Do not display predictions / what the user is typing
				// when they are entering a password.
				mPredictionOn = false;
			}

			if (variation == InputType.TYPE_TEXT_VARIATION_EMAIL_ADDRESS
					|| variation == InputType.TYPE_TEXT_VARIATION_URI
					|| variation == InputType.TYPE_TEXT_VARIATION_FILTER) {
				// Our predictions are not useful for e-mail addresses
				// or URIs.
				mPredictionOn = false;
			}

			if ((attribute.inputType & InputType.TYPE_TEXT_FLAG_AUTO_COMPLETE) != 0) {
				// If this is an auto-complete text view, then our predictions
				// will not be shown and instead we will allow the editor
				// to supply their own. We only show the editor's
				// candidates when in fullscreen mode, otherwise relying
				// own it displaying its own UI.
				mPredictionOn = false;
				mCompletionOn = isFullscreenMode();
			}

			// We also want to look at the current state of the editor
			// to decide whether our alphabetic keyboard should start out
			// shifted.
			updateShiftKeyState(attribute);
			break;

		default:
			// For all unknown input types, default to the alphabetic
			// keyboard with no special features.
			mCurKeyboard = mQwertyKeyboard;
			updateShiftKeyState(attribute);
		}

		// Update the label on the enter key, depending on what the application
		// says it will do.
		mCurKeyboard.setImeOptions(getResources(), attribute.imeOptions);
	}

	/**
	 * This is called when the user is done editing a field. We can use this to
	 * reset our state.
	 */
	@Override
	public void onFinishInput() {
		super.onFinishInput();

		// Clear current composing text and candidates.
		mComposing.setLength(0);
		updateCandidates();

		// We only hide the candidates window when finishing input on
		// a particular editor, to avoid popping the underlying application
		// up and down if the user is entering text into the bottom of
		// its window.
		setCandidatesViewShown(false);

		mCurKeyboard = mQwertyKeyboard;
		if (mInputView != null) {
			mInputView.closing();
		}
	}

	@Override
	public void onStartInputView(EditorInfo attribute, boolean restarting) {
		super.onStartInputView(attribute, restarting);
		// Apply the selected keyboard to the input view.
		mInputView.setKeyboard(mCurKeyboard);
		mInputView.closing();
		// final InputMethodSubtype subtype =
		// mInputMethodManager.getCurrentInputMethodSubtype();
		// mInputView.setSubtypeOnSpaceKey(subtype);
	}

	@Override
	public void onCurrentInputMethodSubtypeChanged(InputMethodSubtype subtype) {
		mInputView.setSubtypeOnSpaceKey(subtype);
	}

	/**
	 * Deal with the editor reporting movement of its cursor.
	 */
	@Override
	public void onUpdateSelection(int oldSelStart, int oldSelEnd,
			int newSelStart, int newSelEnd, int candidatesStart,
			int candidatesEnd) {
		super.onUpdateSelection(oldSelStart, oldSelEnd, newSelStart, newSelEnd,
				candidatesStart, candidatesEnd);

		// If the current selection in the text view changes, we should
		// clear whatever candidate text we have.
		if (mComposing.length() > 0
				&& (newSelStart != candidatesEnd || newSelEnd != candidatesEnd)) {
			mComposing.setLength(0);
			updateCandidates();
			InputConnection ic = getCurrentInputConnection();
			if (ic != null) {
				ic.finishComposingText();
			}
		}
	}

	/**
	 * This tells us about completions that the editor has determined based on
	 * the current text in it. We want to use this in fullscreen mode to show
	 * the completions ourself, since the editor can not be seen in that
	 * situation.
	 */
	@Override
	public void onDisplayCompletions(CompletionInfo[] completions) {
		if (mCompletionOn) {
			mCompletions = completions;
			if (completions == null) {
				setSuggestions(null, false, false);
				return;
			}

			List<String> stringList = new ArrayList<String>();
			for (int i = 0; i < completions.length; i++) {
				CompletionInfo ci = completions[i];
				if (ci != null)
					stringList.add(ci.getText().toString());
			}
			setSuggestions(stringList, true, true);
		}
	}

	/**
	 * Use this to monitor key events being delivered to the application. We get
	 * first crack at them, and can either resume them or let them continue to
	 * the app.
	 */
	@Override
	public boolean onKeyDown(int keyCode, KeyEvent event) {
		switch (keyCode) {
		case KeyEvent.KEYCODE_BACK:
			// The InputMethodService already takes care of the back
			// key for us, to dismiss the input method if it is shown.
			// However, our keyboard could be showing a pop-up window
			// that back should dismiss, so we first allow it to do that.
			if (event.getRepeatCount() == 0 && mInputView != null) {
				if (mInputView.handleBack()) {
					return true;
				}
			}
			break;

		case KeyEvent.KEYCODE_DEL:
			// Special handling of the delete key: if we currently are
			// composing text for the user, we want to modify that instead
			// of let the application to the delete itself.
			if (mComposing.length() > 0) {
				onKey(Keyboard.KEYCODE_DELETE, null);
				return true;
			}
			break;

		case KeyEvent.KEYCODE_ENTER:
			// Let the underlying text editor always handle these.
			return false;

		default:

		}

		return super.onKeyDown(keyCode, event);
	}

	/**
	 * Helper function to commit any text being composed in to the editor.
	 */
	private void commitTyped(InputConnection inputConnection) {
		if (mComposing.length() > 0) {
			inputConnection.commitText(mComposing, mComposing.length());
			mComposing.setLength(0);
			updateCandidates();
		}
	}

	/**
	 * Helper to update the shift state of our keyboard based on the initial
	 * editor state.
	 */
	private void updateShiftKeyState(EditorInfo attr) {
		if (attr != null && mInputView != null
				&& mQwertyKeyboard == mInputView.getKeyboard()) {
			int caps = 0;
			EditorInfo ei = getCurrentInputEditorInfo();
			if (ei != null && ei.inputType != InputType.TYPE_NULL) {
				caps = getCurrentInputConnection().getCursorCapsMode(
						attr.inputType);
			}
			mInputView.setShifted(mCapsLock || caps != 0);
		}
	}

	/**
	 * Helper to determine if a given character code is alphabetic.
	 */
	private boolean isAlphabet(int code) {
		if (Character.isLetter(code)) {
			return true;
		} else {
			return false;
		}
	}

	/**
	 * Helper to send a key down / key up pair to the current editor.
	 */
	private void keyDownUp(int keyEventCode) {
		getCurrentInputConnection().sendKeyEvent(
				new KeyEvent(KeyEvent.ACTION_DOWN, keyEventCode));
		getCurrentInputConnection().sendKeyEvent(
				new KeyEvent(KeyEvent.ACTION_UP, keyEventCode));
	}

	/**
	 * Helper to send a character to the editor as raw key events.
	 */
	private void sendKey(int keyCode) {
		switch (keyCode) {
		case '\n':
			keyDownUp(KeyEvent.KEYCODE_ENTER);
			break;
		default:
			if (keyCode >= '0' && keyCode <= '9') {
				keyDownUp(keyCode - '0' + KeyEvent.KEYCODE_0);
			} else {
				getCurrentInputConnection().commitText(
						String.valueOf((char) keyCode), 1);
			}
			break;
		}
	}

	// Implementation of KeyboardViewListener

	public void onKey(int primaryCode, int[] keyCodes) {
		if (isWordSeparator(primaryCode)) {
			// Handle separator
			if (mComposing.length() > 0) {
				commitTyped(getCurrentInputConnection());
			}
			sendKey(primaryCode);
			updateShiftKeyState(getCurrentInputEditorInfo());
		} else if (primaryCode == Keyboard.KEYCODE_DELETE) {
			android.util.Log.d("DEBUG", "DELETE");
			handleBackspace();
		} else if (primaryCode == Keyboard.KEYCODE_SHIFT) {
			android.util.Log.d("DEBUG", "SHIFT");
			handleShift();
		} else if (primaryCode == Keyboard.KEYCODE_CANCEL) {
			android.util.Log.d("DEBUG", "CANCEL");
			handleClose();
			return;
		} else if (primaryCode == LatinKeyboardView.KEYCODE_KP2A) {
			
			AlertDialog.Builder builder = new AlertDialog.Builder(this);
			String title = "Keepass2Android";
			List<StringForTyping> availableFields = keepass2android.kbbridge.KeyboardData.availableFields;
			
			final ArrayList<String> items = new ArrayList<String>();
			final ArrayList<String> values = new ArrayList<String>();
			for (StringForTyping entry : availableFields) 
			{
				String key = entry.displayName;
				String value = entry.value;
				items.add(key);
				values.add(value);
			}

			
			if (keepass2android.kbbridge.KeyboardData.entryName == null)
			{
				items.add(getString(R.string.open_entry));
				values.add("KP2ASPECIAL_SelectEntryTask");
			}
			else
			{
				title += " ("+keepass2android.kbbridge.KeyboardData.entryName+")";
				items.add(getString(R.string.change_entry));
				values.add("");
			}
			
			if ((mClientPackageName != null) && (mClientPackageName != ""))
			{
				items.add(getString(R.string.open_entry_for_app,mClientPackageName));
				values.add("KP2ASPECIAL_SearchUrlTask");
			}
			
			builder.setTitle(title);

			// builder.setMessage("What do you want to type securely?");
			builder.setItems(items.toArray(new CharSequence[items.size()]),
					new DialogInterface.OnClickListener() {
						public void onClick(DialogInterface dialog, int item) {

							if (values.get(item).startsWith("KP2ASPECIAL")) {
								//change entry
								Intent startKp2aIntent = getPackageManager().getLaunchIntentForPackage(getApplicationContext().getPackageName());
								//Intent startKp2aIntent = getPackageManager().getLaunchIntentForPackage("keepass2android.keepass2android");
								if (startKp2aIntent != null)
								{
									startKp2aIntent.addCategory(Intent.CATEGORY_LAUNCHER);
									startKp2aIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK |  Intent.FLAG_ACTIVITY_CLEAR_TASK);
									String value = values.get(item);
									String taskName = value.substring("KP2ASPECIAL_".length()); 
									startKp2aIntent.putExtra("KP2A_APPTASK", taskName);
									if (taskName.equals("SearchUrlTask"))
									{
										startKp2aIntent.putExtra("UrlToSearch", "androidapp://"+mClientPackageName);
									}
									startActivity(startKp2aIntent);
									Settings.Secure.getString(
											   getContentResolver(), 
											   Settings.Secure.DEFAULT_INPUT_METHOD
											);
								}
							} else {
								getCurrentInputConnection().commitText(
										values.get(item), 0);
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
			lp.token = mInputView.getWindowToken();
			lp.type = WindowManager.LayoutParams.TYPE_APPLICATION_ATTACHED_DIALOG;
			window.setAttributes(lp);
			window.addFlags(WindowManager.LayoutParams.FLAG_ALT_FOCUSABLE_IM);

			dialog.show();
		} else if (primaryCode == LatinKeyboardView.KEYCODE_SELECT_IME) {
			mInputMethodManager.showInputMethodPicker();
		} else if (primaryCode == Keyboard.KEYCODE_MODE_CHANGE
				&& mInputView != null) {
			Keyboard current = mInputView.getKeyboard();
			if (current == mSymbolsKeyboard
					|| current == mSymbolsShiftedKeyboard) {
				current = mQwertyKeyboard;
			} else {
				current = mSymbolsKeyboard;
			}
			mInputView.setKeyboard(current);
			if (current == mSymbolsKeyboard) {
				current.setShifted(false);
			}
		} else {
			handleCharacter(primaryCode, keyCodes);
			// mCapsLock = false;
			// mInputView.setShifted(mCapsLock);
			updateShiftKeyState(getCurrentInputEditorInfo());
		}
	}

	public void onText(CharSequence text) {
		InputConnection ic = getCurrentInputConnection();
		if (ic == null)
			return;
		ic.beginBatchEdit();
		if (mComposing.length() > 0) {
			commitTyped(ic);
		}
		ic.commitText(text, 0);
		ic.endBatchEdit();
		updateShiftKeyState(getCurrentInputEditorInfo());
	}

	/**
	 * Update the list of available candidates from the current composing text.
	 * This will need to be filled in by however you are determining candidates.
	 */
	private void updateCandidates() {
		if (!mCompletionOn) {
			if (mComposing.length() > 0) {
				ArrayList<String> list = new ArrayList<String>();
				list.add(mComposing.toString());
				setSuggestions(list, true, true);
			} else {
				setSuggestions(null, false, false);
			}
		}
	}

	public void setSuggestions(List<String> suggestions, boolean completions,
			boolean typedWordValid) {
		if (suggestions != null && suggestions.size() > 0) {
			setCandidatesViewShown(true);
		} else if (isExtractViewShown()) {
			setCandidatesViewShown(true);
		}
		if (mCandidateView != null) {
			mCandidateView.setSuggestions(suggestions, completions,
					typedWordValid);
		}
	}

	private void handleBackspace() {
		final int length = mComposing.length();
		if (length > 1) {
			mComposing.delete(length - 1, length);
			getCurrentInputConnection().setComposingText(mComposing, 1);
			updateCandidates();
		} else if (length > 0) {
			mComposing.setLength(0);
			getCurrentInputConnection().commitText("", 0);
			updateCandidates();
		} else {
			keyDownUp(KeyEvent.KEYCODE_DEL);
		}
		updateShiftKeyState(getCurrentInputEditorInfo());
	}

	private void handleShift() {
		if (mInputView == null) {
			return;
		}

		Keyboard currentKeyboard = mInputView.getKeyboard();
		if (mQwertyKeyboard == currentKeyboard) {
			// Alphabet keyboard
			checkToggleCapsLock();
			mInputView.setShifted(mCapsLock || !mInputView.isShifted());

		} else if (currentKeyboard == mSymbolsKeyboard) {
			mSymbolsKeyboard.setShifted(true);
			mInputView.setKeyboard(mSymbolsShiftedKeyboard);
			mSymbolsShiftedKeyboard.setShifted(true);
		} else if (currentKeyboard == mSymbolsShiftedKeyboard) {
			mSymbolsShiftedKeyboard.setShifted(false);
			mInputView.setKeyboard(mSymbolsKeyboard);
			mSymbolsKeyboard.setShifted(false);
		}
	}

	private void handleCharacter(int primaryCode, int[] keyCodes) {
		if (isInputViewShown()) {
			if (mInputView.isShifted()) {
				primaryCode = Character.toUpperCase(primaryCode);
			}
		}
		if (isAlphabet(primaryCode) && mPredictionOn) {
			mComposing.append((char) primaryCode);
			getCurrentInputConnection().setComposingText(mComposing, 1);
			updateShiftKeyState(getCurrentInputEditorInfo());
			updateCandidates();
		} else {
			getCurrentInputConnection().commitText(
					String.valueOf((char) primaryCode), 1);
		}
	}

	private void handleClose() {
		commitTyped(getCurrentInputConnection());
		requestHideSelf(0);
		mInputView.closing();
	}

	private void checkToggleCapsLock() {
		long now = System.currentTimeMillis();
		if (mLastShiftTime + 800 > now) {
			mCapsLock = !mCapsLock;
			mLastShiftTime = 0;
		} else {
			mLastShiftTime = now;
		}
	}

	private String getWordSeparators() {
		return mWordSeparators;
	}

	public boolean isWordSeparator(int code) {
		String separators = getWordSeparators();
		return separators.contains(String.valueOf((char) code));
	}

	public void pickDefaultCandidate() {
		pickSuggestionManually(0);
	}

	public void pickSuggestionManually(int index) {
		if (mCompletionOn && mCompletions != null && index >= 0
				&& index < mCompletions.length) {
			CompletionInfo ci = mCompletions[index];
			getCurrentInputConnection().commitCompletion(ci);
			if (mCandidateView != null) {
				mCandidateView.clear();
			}
			updateShiftKeyState(getCurrentInputEditorInfo());
		} else if (mComposing.length() > 0) {
			// If we were generating candidate suggestions for the current
			// text, we would commit one of them here. But for this sample,
			// we will just commit the current text.
			commitTyped(getCurrentInputConnection());
		}
	}

	public void swipeRight() {
		if (mCompletionOn) {
			pickDefaultCandidate();
		}
	}

	public void swipeLeft() {
		handleBackspace();
	}

	public void swipeDown() {
		handleClose();
	}

	public void swipeUp() {
	}

	public void onPress(int primaryCode) {
	}

	public void onRelease(int primaryCode) {
	}
}

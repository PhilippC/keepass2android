/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.ui.widget;

import group.pals.android.lib.ui.filechooser.BuildConfig;
import group.pals.android.lib.ui.filechooser.R;
import group.pals.android.lib.ui.filechooser.utils.Utils;
import group.pals.android.lib.ui.filechooser.utils.ui.Ui;
import android.content.Context;
import android.content.res.TypedArray;
import android.os.Handler;
import android.text.Editable;
import android.text.TextUtils;
import android.text.TextWatcher;
import android.util.AttributeSet;
import android.util.Log;
import android.view.KeyEvent;
import android.view.LayoutInflater;
import android.view.View;
import android.view.inputmethod.EditorInfo;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.TextView;

/**
 * AFC Search view.
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public class AfcSearchView extends LinearLayout {

    private static final String CLASSNAME = AfcSearchView.class.getName();

    /**
     * Callbacks for changes to the query text.
     */
    public static interface OnQueryTextListener {

        /**
         * Called when the user submits the query. This could be due to a key
         * press on the keyboard or due to pressing a submit button.
         * <p>
         * <b>Note:</b> This method is called before setting the new search
         * query to last search query (which can be obtained with
         * {@link AfcSearchView#getSearchText()}).
         * </p>
         * 
         * @param query
         *            the query text that is to be submitted.
         */
        void onQueryTextSubmit(String query);
    }// OnQueryTextListener

    public static interface OnStateChangeListener {

        /**
         * The user is attempting to open the SearchView.
         */
        void onOpen();

        /**
         * The user is attempting to close the SearchView.
         */
        void onClose();
    }// OnStateChangeListener

    /*
     * CONTROLS
     */

    private final View mButtonSearch;
    private final EditText mTextSearch;
    private final View mButtonClear;

    /*
     * FIELDS
     */

    private int mDelayTimeSubmission;
    private boolean mIconified;
    private boolean mClosable;
    private CharSequence mSearchText;

    /*
     * LISTENERS
     */

    private OnQueryTextListener mOnQueryTextListener;
    private OnStateChangeListener mOnStateChangeListener;

    /**
     * Creates new instance.
     * 
     * @param context
     *            {@link Context}.
     */
    public AfcSearchView(Context context) {
        this(context, null);
    }// AfcSearchView()

    /**
     * Creates new instance.
     * 
     * @param context
     *            {@link Context}.
     * @param attrs
     *            {@link AttributeSet}.
     */
    public AfcSearchView(Context context, AttributeSet attrs) {
        super(context, attrs);

        /*
         * LOADS LAYOUTS
         */

        LayoutInflater inflater = (LayoutInflater) context
                .getSystemService(Context.LAYOUT_INFLATER_SERVICE);
        inflater.inflate(R.layout.afc_widget_search_view, this, true);

        mButtonSearch = findViewById(R.id.afc_widget_search_view_button_search);
        mTextSearch = (EditText) findViewById(R.id.afc_widget_search_view_textview_search);
        mButtonClear = findViewById(R.id.afc_widget_search_view_button_clear);

        /*
         * ASSIGNS LISTENERS & ATTRIBUTES
         */

        mButtonSearch.setOnClickListener(mButtonSearchOnClickListener);
        mTextSearch.addTextChangedListener(mTextSearchTextWatcher);
        mTextSearch.setOnKeyListener(mTextSearchOnKeyListener);
        mTextSearch
                .setOnEditorActionListener(mTextSearchOnEditorActionListener);
        mButtonClear.setOnClickListener(mButtonClearOnClickListener);

        /*
         * LOADS ATTRIBUTES
         */

        TypedArray a = context.obtainStyledAttributes(attrs,
                R.styleable.AfcSearchView);

        setDelayTimeSubmission(a.getInt(
                R.styleable.AfcSearchView_delayTimeSubmission, 0));
        updateViewsVisibility(
                a.getBoolean(R.styleable.AfcSearchView_iconified, true), false);
        setClosable(a.getBoolean(R.styleable.AfcSearchView_closable, true));
        setEnabled(a.getBoolean(R.styleable.AfcSearchView_enabled, true));
        mTextSearch.setHint(a.getString(R.styleable.AfcSearchView_hint));

        a.recycle();
    }// AfcSearchView()

    /**
     * Gets the search text.
     * 
     * @return the search text, can be {@code null}.
     */
    public CharSequence getSearchText() {
        return mSearchText;
    }// getSearchText()

    /**
     * Gets delay time submission. This is the time that after the user entered
     * a search term and waited for, then the handler will be invoked to process
     * that search term.
     * 
     * @return the delay time, in milliseconds.
     * @see #setDelayTimeSubmission(int)
     */
    public int getDelayTimeSubmission() {
        return mDelayTimeSubmission;
    }// getDelayTimeSubmission()

    /**
     * Sets delay time submission. This is the time that after the user entered
     * a search term and waited for, then the handler will be invoked to process
     * that search term.
     * 
     * @param millis
     *            delay time, in milliseconds. If {@code <= 0}, auto-submission
     *            will be disabled.
     * @see #getDelayTimeSubmission()
     */
    public void setDelayTimeSubmission(int millis) {
        if (mDelayTimeSubmission != millis) {
            mDelayTimeSubmission = Math.max(0, millis);
            if (mDelayTimeSubmission <= 0)
                mAutoSubmissionHandler.removeCallbacksAndMessages(null);
        }
    }// setDelayTimeSubmission()

    /**
     * Checks if this search view is iconfied or not.
     * 
     * @return {@code true} or {@code false}.
     * @see #close()
     * @see #open()
     */
    public boolean isIconified() {
        return mIconified;
    }// isIconfied()

    /**
     * Updates views visibility.
     * 
     * @param collapsed
     *            {@code true} or {@code false}.
     * @param showSoftKeyboard
     *            set to {@code true} if you want to force show the soft
     *            keyboard in <i>expanded</i> state.
     * @see #isIconified()
     */
    protected void updateViewsVisibility(boolean collapsed,
            boolean showSoftKeyboard) {
        if (Utils.doLog())
            Log.d(CLASSNAME, "updateViewsVisibility() >> " + collapsed);

        mIconified = collapsed;

        /*
         * Always remove this trap first...
         */
        if (mIconified)
            mAutoSubmissionHandler.removeCallbacksAndMessages(null);

        if (getOnStateChangeListener() != null)
            if (mIconified)
                getOnStateChangeListener().onClose();
            else
                getOnStateChangeListener().onOpen();

        mTextSearch.setVisibility(mIconified ? GONE : VISIBLE);
        if (mIconified) {
            mSearchText = null;

            mTextSearch.removeTextChangedListener(mTextSearchTextWatcher);
            mTextSearch.setText(null);

            mTextSearch.setFocusable(false);
            mTextSearch.setFocusableInTouchMode(false);
            mTextSearch.clearFocus();

            setEnabled(false);
            Ui.showSoftKeyboard(mTextSearch, false);
        } else {
            mTextSearch.addTextChangedListener(mTextSearchTextWatcher);

            mTextSearch.setFocusable(true);
            mTextSearch.setFocusableInTouchMode(true);

            if (showSoftKeyboard) {
                mTextSearch.requestFocus();
                Ui.showSoftKeyboard(mTextSearch, true);
            }
            setEnabled(true);
        }
    }// updateViewsVisibility()

    /**
     * Minimizes this search view. Does nothing if this search view is not
     * closable.
     * 
     * @see #isIconified()
     * @see #isClosable()
     * @see #open()
     */
    public void close() {
        if (isClosable() && !isIconified())
            updateViewsVisibility(true, true);
    }// close()

    /**
     * Maximizes the view, lets the user to be able to enter search term.
     * 
     * @see #close()
     * @see #isIconified()
     */
    public void open() {
        if (isIconified())
            updateViewsVisibility(false, true);
    }// open()

    /**
     * Checks if this search view is closable or not.
     * 
     * @return {@code true} or {@code false}.
     */
    public boolean isClosable() {
        return mClosable;
    }

    /**
     * Sets closable.
     * 
     * @param closable
     *            {@code true} or {@code false}.
     */
    public void setClosable(boolean closable) {
        mClosable = closable;
        if (mClosable)
            mButtonClear.setVisibility(VISIBLE);
    }

    /**
     * Sets the query text listener.
     * 
     * @param listener
     *            {@link OnQueryTextListener}.
     * @see #getOnQueryTextListener()
     */
    public void setOnQueryTextListener(OnQueryTextListener listener) {
        mOnQueryTextListener = listener;
    }

    /**
     * Gets the on query text listener.
     * 
     * @return {@link OnQueryTextListener}, can be {@code null}.
     * @see #setOnQueryTextListener(OnQueryTextListener)
     */
    public OnQueryTextListener getOnQueryTextListener() {
        return mOnQueryTextListener;
    }

    /**
     * Sets on close listener.
     * 
     * @param listener
     *            {@link OnClickListener}.
     * @see #getOnStateChangeListener()
     */
    public void setOnStateChangeListener(OnStateChangeListener listener) {
        mOnStateChangeListener = listener;
    }

    /**
     * Gets on close listener.
     * 
     * @return {@link OnStateChangeListener}, can be {@code null}.
     * @see #setOnStateChangeListener(OnStateChangeListener)
     */
    public OnStateChangeListener getOnStateChangeListener() {
        return mOnStateChangeListener;
    }

    @Override
    public void setEnabled(boolean enabled) {
        if (isEnabled() == enabled)
            return;

        for (View v : new View[] { mButtonSearch, mTextSearch, mButtonClear })
            v.setEnabled(enabled);
        super.setEnabled(enabled);
    }// setEnabled()

    /*
     * LISTENERS
     */

    private final View.OnClickListener mButtonSearchOnClickListener = new View.OnClickListener() {

        @Override
        public void onClick(View v) {
            if (isIconified()) {
                updateViewsVisibility(false, false);
            } else {
                mAutoSubmissionHandler.removeCallbacksAndMessages(null);

                if (getOnQueryTextListener() != null)
                    getOnQueryTextListener().onQueryTextSubmit(
                            mTextSearch.getText().toString());
                mSearchText = mTextSearch.getText();
            }
        }// onClick()
    };// mButtonSearchOnClickListener

    private final Handler mAutoSubmissionHandler = new Handler();

    private final Runnable mAutoSubmissionRunnable = new Runnable() {

        @Override
        public void run() {
            if (Utils.doLog())
                Log.d(CLASSNAME, "mAutoSubmissionRunnable.run()");
            mButtonSearch.performClick();
        }// run()
    };// mAutoSubmissionRunnable

    private final TextWatcher mTextSearchTextWatcher = new TextWatcher() {

        @Override
        public void onTextChanged(CharSequence s, int start, int before,
                int count) {
            /*
             * Do nothing.
             */
        }// onTextChanged()

        @Override
        public void beforeTextChanged(CharSequence s, int start, int count,
                int after) {
            if (Utils.doLog())
                Log.d(CLASSNAME, "beforeTextChanged()");
            mAutoSubmissionHandler.removeCallbacksAndMessages(null);
        }// beforeTextChanged()

        @Override
        public void afterTextChanged(Editable s) {
            if (Utils.doLog())
                Log.d(CLASSNAME,
                        "afterTextChanged() >>> delayTimeSubmission = "
                                + getDelayTimeSubmission());

            if (TextUtils.isEmpty(mTextSearch.getText())) {
                if (!isClosable())
                    mButtonClear.setVisibility(GONE);
            } else
                mButtonClear.setVisibility(VISIBLE);

            if (getDelayTimeSubmission() > 0)
                mAutoSubmissionHandler.postDelayed(mAutoSubmissionRunnable,
                        getDelayTimeSubmission());
        }// afterTextChanged()
    };// mTextSearchTextWatcher

    private final View.OnKeyListener mTextSearchOnKeyListener = new View.OnKeyListener() {

        @Override
        public boolean onKey(View v, int keyCode, KeyEvent event) {
            if (event.getAction() == KeyEvent.ACTION_UP) {
                switch (keyCode) {
                case KeyEvent.KEYCODE_ENTER:
                    mButtonSearch.performClick();
                    return true;
                case KeyEvent.KEYCODE_ESCAPE:
                    mButtonClear.performClick();
                    return true;
                }
            }

            return false;
        }// onKey()
    };// mTextSearchOnKeyListener

    private final TextView.OnEditorActionListener mTextSearchOnEditorActionListener = new TextView.OnEditorActionListener() {

        @Override
        public boolean onEditorAction(TextView v, int actionId, KeyEvent event) {
            if (actionId == EditorInfo.IME_ACTION_SEARCH) {
                mButtonSearch.performClick();
                return true;
            }

            return false;
        }// onEditorAction()
    };// mTextSearchOnEditorActionListener

    private final View.OnClickListener mButtonClearOnClickListener = new View.OnClickListener() {

        @Override
        public void onClick(View v) {
            if (TextUtils.isEmpty(mTextSearch.getText()))
                close();
            else
                mTextSearch.setText(null);
        }// onClick()
    };// mButtonClearOnClickListener

}

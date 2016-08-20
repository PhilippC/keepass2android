/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.utils.ui;

import group.pals.android.lib.ui.filechooser.BuildConfig;
import group.pals.android.lib.ui.filechooser.R;
import android.app.Dialog;
import android.content.Context;
import android.content.res.Resources;
import android.graphics.Paint;
import android.util.DisplayMetrics;
import android.util.Log;
import android.util.TypedValue;
import android.view.View;
import android.view.Window;
import android.view.inputmethod.InputMethodManager;
import android.widget.TextView;

/**
 * UI utilities.
 * 
 * @author Hai Bison
 */
public class Ui {

    private static final String CLASSNAME = Ui.class.getName();

    /**
     * Shows/ hides soft input (soft keyboard).
     * 
     * @param view
     *            {@link View}.
     * @param show
     *            {@code true} or {@code false}. If {@code true}, this method
     *            will use a {@link Runnable} to show the IMM. So you don't need
     *            to use it, and consider using
     *            {@link View#removeCallbacks(Runnable)} if you want to cancel.
     */
    public static void showSoftKeyboard(final View view, final boolean show) {
        final InputMethodManager imm = (InputMethodManager) view.getContext()
                .getSystemService(Context.INPUT_METHOD_SERVICE);
        if (imm == null)
            return;

        if (show) {
            view.post(new Runnable() {

                @Override
                public void run() {
                    imm.showSoftInput(view, 0, null);
                }// run()
            });
        } else
            imm.hideSoftInputFromWindow(view.getWindowToken(), 0, null);
    }// showSoftKeyboard()

    /**
     * Strikes out text of {@code view}.
     * 
     * @param view
     *            {@link TextView}.
     * @param strikeOut
     *            {@code true} to strike out the text.
     */
    public static void strikeOutText(TextView view, boolean strikeOut) {
        if (strikeOut)
            view.setPaintFlags(view.getPaintFlags()
                    | Paint.STRIKE_THRU_TEXT_FLAG);
        else
            view.setPaintFlags(view.getPaintFlags()
                    & ~Paint.STRIKE_THRU_TEXT_FLAG);
    }// strikeOutText()

    /**
     * Convenient method for {@link Context#getTheme()} and
     * {@link Resources.Theme#resolveAttribute(int, TypedValue, boolean)}.
     * 
     * @param context
     *            the context.
     * @param resId
     *            The resource identifier of the desired theme attribute.
     * @return the resource ID that {@link TypedValue#resourceId} points to, or
     *         {@code 0} if not found.
     */
    public static int resolveAttribute(Context context, int resId) {
        TypedValue typedValue = new TypedValue();
        if (context.getTheme().resolveAttribute(resId, typedValue, true))
            return typedValue.resourceId;
        return 0;
    }// resolveAttribute()

    /**
     * Uses a fixed size for {@code dialog} in large screens.
     * 
     * @param dialog
     *            the dialog.
     */
    public static void adjustDialogSizeForLargeScreen(Dialog dialog) {
        adjustDialogSizeForLargeScreen(dialog.getWindow());
    }// adjustDialogSizeForLargeScreen()

    /**
     * Uses a fixed size for {@code window} in large screens.
     * 
     * @param dialogWindow
     *            the window <i>of the dialog</i>.
     */
    public static void adjustDialogSizeForLargeScreen(Window dialogWindow) {
        if (BuildConfig.DEBUG)
            Log.d(CLASSNAME, "adjustDialogSizeForLargeScreen()");
        if (dialogWindow.isFloating()
                && dialogWindow.getContext().getResources()
                        .getBoolean(R.bool.afc_is_large_screen)) {
            final DisplayMetrics metrics = dialogWindow.getContext()
                    .getResources().getDisplayMetrics();
            final boolean isPortrait = metrics.widthPixels < metrics.heightPixels;

            int width = metrics.widthPixels;// dialogWindow.getDecorView().getWidth();
            int height = metrics.heightPixels;// dialogWindow.getDecorView().getHeight();
            if (BuildConfig.DEBUG)
                Log.d(CLASSNAME, String.format("width = %,d | height = %,d",
                        width, height));
            width = (int) dialogWindow
                    .getContext()
                    .getResources()
                    .getFraction(
                            isPortrait ? R.dimen.aosp_dialog_fixed_width_minor
                                    : R.dimen.aosp_dialog_fixed_width_major,
                            width, width);
            height = (int) dialogWindow
                    .getContext()
                    .getResources()
                    .getFraction(
                            isPortrait ? R.dimen.aosp_dialog_fixed_height_major
                                    : R.dimen.aosp_dialog_fixed_height_minor,
                            height, height);
            if (BuildConfig.DEBUG)
                Log.d(CLASSNAME, String.format(
                        "NEW >>> width = %,d | height = %,d", width, height));
            dialogWindow.setLayout(width, height);
        }
    }// adjustDialogSizeForLargeScreen()

}

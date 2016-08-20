/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.utils;

import group.pals.android.lib.ui.filechooser.R;
import android.app.Dialog;
import android.content.Context;
import android.content.Intent;
import android.net.Uri;
import android.view.ContextThemeWrapper;
import android.view.View;
import android.view.Window;
import android.widget.TextView;

/**
 * Something funny :-)
 * 
 * @author Hai Bison
 */
public class E {

    /**
     * Shows it!
     * 
     * @param context
     *            {@link Context}
     */
    public static void show(Context context) {
        String msg = null;
        try {
            msg = String.format("Hi  :-)\n\n" + "%s v%s\n"
                    + "…by Hai Bison Apps\n\n" + "http://www.haibison.com\n\n"
                    + "Hope you enjoy this library.", Sys.LIB_NAME,
                    Sys.LIB_VERSION_NAME);
        } catch (Exception e) {
            msg = "Oops… You've found a broken Easter egg, try again later  :-(";
        }

        final Context ctw = new ContextThemeWrapper(context,
                R.style.Afc_Theme_Dialog_Dark);

        final int padding = ctw.getResources().getDimensionPixelSize(
                R.dimen.afc_10dp);
        TextView textView = new TextView(ctw);
        textView.setText(msg);
        textView.setPadding(padding, padding, padding, padding);
        textView.setOnClickListener(new View.OnClickListener() {

            @Override
            public void onClick(View v) {
                try {
                    ctw.startActivity(new Intent(Intent.ACTION_VIEW, Uri
                            .parse("http://www.haibison.com")));
                } catch (Throwable t) {
                    /*
                     * Ignore it.
                     */
                }
            }// onClick()
        });

        Dialog dialog = new Dialog(ctw, R.style.Afc_Theme_Dialog_Dark);
        dialog.requestWindowFeature(Window.FEATURE_NO_TITLE);
        dialog.setCanceledOnTouchOutside(true);
        dialog.setContentView(textView);
        dialog.show();
    }// show()

}

/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.utils.ui;

import group.pals.android.lib.ui.filechooser.R;
import android.app.Dialog;
import android.content.Context;
import android.text.TextUtils;
import android.view.LayoutInflater;
import android.view.View;
import android.view.Window;
import android.view.WindowManager;
import android.widget.AdapterView;
import android.widget.ListView;

/**
 * Utilities for context menu.
 * 
 * @author Hai Bison
 * @since v4.3 beta
 */
public class ContextMenuUtils {

    /**
     * Shows context menu.
     * 
     * @param context
     *            {@link Context}
     * @param iconId
     *            resource icon ID of the dialog.
     * @param title
     *            title of the dialog.
     * @param itemIds
     *            array of resource IDs of strings.
     * @param listener
     *            {@link OnMenuItemClickListener}
     */
    public static void showContextMenu(Context context, int iconId,
            String title, final Integer[] itemIds,
            final OnMenuItemClickListener listener) {
        final Dialog dialog = new Dialog(context, Ui.resolveAttribute(context,
                R.attr.afc_theme_dialog));
        dialog.setCanceledOnTouchOutside(true);
        if (iconId > 0)
            dialog.requestWindowFeature(Window.FEATURE_LEFT_ICON);
        if (TextUtils.isEmpty(title))
            dialog.requestWindowFeature(Window.FEATURE_NO_TITLE);
        else
            dialog.setTitle(title);

        final MenuItemAdapter _adapter = new MenuItemAdapter(
                dialog.getContext(), itemIds);

        View view = LayoutInflater.from(context).inflate(
                R.layout.afc_context_menu_view, null);
        ListView listview = (ListView) view
                .findViewById(R.id.afc_listview_menu);
        listview.setAdapter(_adapter);

        dialog.setContentView(view);
        if (iconId > 0)
            dialog.setFeatureDrawableResource(Window.FEATURE_LEFT_ICON, iconId);

        if (listener != null) {
            listview.setOnItemClickListener(new AdapterView.OnItemClickListener() {

                @Override
                public void onItemClick(AdapterView<?> parent, View view,
                        int position, long id) {
                    dialog.dismiss();
                    listener.onClick(itemIds[position]);
                }// onItemClick()
            });
        }// if listener != null

        dialog.show();

        /*
         * Hardcode width...
         */
        WindowManager.LayoutParams lp = new WindowManager.LayoutParams();
        lp.copyFrom(dialog.getWindow().getAttributes());
        lp.width = context.getResources().getDimensionPixelSize(
                R.dimen.afc_context_menu_width);
        dialog.getWindow().setAttributes(lp);
    }// showContextMenu()

    /**
     * Shows context menu.
     * 
     * @param context
     *            {@link Context}
     * @param iconId
     *            resource icon ID of the dialog.
     * @param titleId
     *            resource ID of the title of the dialog. {@code 0} will be
     *            ignored.
     * @param itemIds
     *            array of resource IDs of strings.
     * @param listener
     *            {@link OnMenuItemClickListener}
     */
    public static void showContextMenu(Context context, int iconId,
            int titleId, Integer[] itemIds, OnMenuItemClickListener listener) {
        showContextMenu(context, iconId,
                titleId > 0 ? context.getString(titleId) : null, itemIds,
                listener);
    }// showContextMenu()

    // ==========
    // INTERFACES

    /**
     * @author Hai Bison
     * @since v4.3 beta
     */
    public static interface OnMenuItemClickListener {

        /**
         * This method will be called after the menu dismissed.
         * 
         * @param resId
         *            the resource ID of the title of the menu item.
         */
        void onClick(int resId);
    }// OnMenuItemClickListener

}

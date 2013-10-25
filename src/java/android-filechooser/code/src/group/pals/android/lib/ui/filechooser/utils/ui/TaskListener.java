/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.utils.ui;

/**
 * The listener for any task you want to assign to.
 * 
 * @author Hai Bison
 * @since v1.8
 */
public interface TaskListener {

    /**
     * Will be called after the task finished.
     * 
     * @param ok
     *            {@code true} if everything is OK, {@code false} otherwise.
     * @param any
     *            the user data, can be {@code null}.
     */
    public void onFinish(boolean ok, Object any);

}

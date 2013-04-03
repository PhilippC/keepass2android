/*
 * Copyright (C) 2011 The Android Open Source Project
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
 * limitations under the License
 */

/**
 * This is a part of the inputmethod-common static Java library.
 * The original source code can be found at frameworks/opt/inputmethodcommon of Android Open Source
 * Project.
 */

package com.android.inputmethodcommon;

import android.graphics.drawable.Drawable;

/**
 * InputMethodSettingsInterface is the interface for adding IME related preferences to
 * PreferenceActivity or PreferenceFragment.
 */
public interface InputMethodSettingsInterface {
    /**
     * Sets the title for the input method settings category with a resource ID.
     * @param resId The resource ID of the title.
     */
    public void setInputMethodSettingsCategoryTitle(int resId);

    /**
     * Sets the title for the input method settings category with a CharSequence.
     * @param title The title for this preference.
     */
    public void setInputMethodSettingsCategoryTitle(CharSequence title);

    /**
     * Sets the title for the input method enabler preference for launching subtype enabler with a
     * resource ID.
     * @param resId The resource ID of the title.
     */
    public void setSubtypeEnablerTitle(int resId);

    /**
     * Sets the title for the input method enabler preference for launching subtype enabler with a
     * CharSequence.
     * @param title The title for this preference.
     */
    public void setSubtypeEnablerTitle(CharSequence title);

    /**
     * Sets the icon for the preference for launching subtype enabler with a resource ID.
     * @param resId The resource id of an optional icon for the preference.
     */
    public void setSubtypeEnablerIcon(int resId);

    /**
     * Sets the icon for the Preference for launching subtype enabler with a Drawable.
     * @param drawable The drawable of an optional icon for the preference.
     */
    public void setSubtypeEnablerIcon(Drawable drawable);
}

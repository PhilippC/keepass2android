//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

import com.microsoft.live.OAuth.DisplayType;

/**
 * The type of the device is used to determine the display query parameter for login.live.com.
 * Phones have a display parameter of android_phone.
 * Tablets have a display parameter of android_tablet.
 */
enum DeviceType {
    PHONE {
        @Override
        public DisplayType getDisplayParameter() {
            return DisplayType.ANDROID_PHONE;
        }
    },
    TABLET {
        @Override
        public DisplayType getDisplayParameter() {
            return DisplayType.ANDROID_TABLET;
        }
    };

    abstract public DisplayType getDisplayParameter();
}

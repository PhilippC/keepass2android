/*
 * This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
 *
 *   Keepass2Android is free software: you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation, either version 3 of the License, or
 *   (at your option) any later version.
 *
 *   Keepass2Android is distributed in the hope that it will be useful,
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *   GNU General Public License for more details.
 *
 *   You should have received a copy of the GNU General Public License
 *   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
 */

package keepass2android.plugin.qr;

import android.view.Window;
import android.view.WindowManager;
import org.junit.Test;
import static org.junit.Assert.*;
import static org.mockito.Mockito.*;

public class QRActivityTest {

    @Test
    public void testSecureFlagsConfigured() {
        Window mockWindow = mock(Window.class);
        QRActivity.applySecureFlags(mockWindow);
        verify(mockWindow, times(1)).addFlags(WindowManager.LayoutParams.FLAG_SECURE);
    }

    @Test
    public void testSecureFlagsNullWindowDoesNotThrow() {
        QRActivity.applySecureFlags(null);
    }
}

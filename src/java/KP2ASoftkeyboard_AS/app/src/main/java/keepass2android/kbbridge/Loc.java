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

package keepass2android.kbbridge;

import java.text.Collator;
import java.util.Locale;

/**
 * Created by Philipp on 16.01.2016.
 */

public class Loc implements Comparable<Object> {
    static Collator sCollator = Collator.getInstance();

    public String label;
    public Locale locale;

    public Loc(String label, Locale locale) {
        this.label = label;
        this.locale = locale;
    }

    @Override
    public String toString() {
        return this.label;
    }

    public int compareTo(Object o) {
        return sCollator.compare(this.label, ((Loc) o).label);
    }
}

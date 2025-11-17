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

package keepass2android.javafilestorage.webdav;

import java.text.ParseException;
import java.text.SimpleDateFormat;
import java.util.Arrays;
import java.util.Date;
import java.util.List;
import java.util.Locale;
import java.util.TimeZone;

public class WebDavUtil {
    /**
     * Date formats using for Date parsing.
     */
    private static final List<ThreadLocal<SimpleDateFormat>> DATETIME_FORMATS = Arrays.asList(
            new ThreadLocal<SimpleDateFormat>()
            {
                @Override
                protected SimpleDateFormat initialValue()
                {
                    SimpleDateFormat format = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss'Z'", Locale.US);
                    format.setTimeZone(TimeZone.getTimeZone("UTC"));
                    return format;
                }
            },
            new ThreadLocal<SimpleDateFormat>()
            {
                @Override
                protected SimpleDateFormat initialValue()
                {
                    SimpleDateFormat format = new SimpleDateFormat("EEE, dd MMM yyyy HH:mm:ss zzz", Locale.US);
                    format.setTimeZone(TimeZone.getTimeZone("UTC"));
                    return format;
                }
            },
            new ThreadLocal<SimpleDateFormat>()
            {
                @Override
                protected SimpleDateFormat initialValue()
                {
                    SimpleDateFormat format = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.sss'Z'", Locale.US);
                    format.setTimeZone(TimeZone.getTimeZone("UTC"));
                    return format;
                }
            },
            new ThreadLocal<SimpleDateFormat>()
            {
                @Override
                protected SimpleDateFormat initialValue()
                {
                    SimpleDateFormat format = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ssZ", Locale.US);
                    format.setTimeZone(TimeZone.getTimeZone("UTC"));
                    return format;
                }
            },
            new ThreadLocal<SimpleDateFormat>()
            {
                @Override
                protected SimpleDateFormat initialValue()
                {
                    SimpleDateFormat format = new SimpleDateFormat("EEE MMM dd HH:mm:ss zzz yyyy", Locale.US);
                    format.setTimeZone(TimeZone.getTimeZone("UTC"));
                    return format;
                }
            },
            new ThreadLocal<SimpleDateFormat>()
            {
                @Override
                protected SimpleDateFormat initialValue()
                {
                    SimpleDateFormat format = new SimpleDateFormat("EEEEEE, dd-MMM-yy HH:mm:ss zzz", Locale.US);
                    format.setTimeZone(TimeZone.getTimeZone("UTC"));
                    return format;
                }
            },
            new ThreadLocal<SimpleDateFormat>()
            {
                @Override
                protected SimpleDateFormat initialValue()
                {
                    SimpleDateFormat format = new SimpleDateFormat("EEE MMMM d HH:mm:ss yyyy", Locale.US);
                    format.setTimeZone(TimeZone.getTimeZone("UTC"));
                    return format;
                }
            }
    );

    /**
     * Loops over all the possible date formats and tries to find the right one.
     *
     * @param value ISO date string
     * @return Null if there is a parsing failure
     */
    public static Date parseDate(String value)
    {
        if (value == null)
        {
            return null;
        }
        Date date = null;
        for (ThreadLocal<SimpleDateFormat> format : DATETIME_FORMATS)
        {
            try
            {
                date = format.get().parse(value);
                break;
            }
            catch (ParseException e)
            {
                // We loop through this until we found a valid one.
            }
        }
        return date;
    }
}

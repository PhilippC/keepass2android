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

package keepass2android.javafilestorage;

import android.util.Log;

import com.jcraft.jsch.Logger;

import java.io.FileWriter;
import java.io.PrintWriter;
import java.util.Map;

public class Kp2aJSchLogger implements Logger {

    private static final String PREFIX = "KP2AJFS[JSch]";

    private interface ILogger {
        void log(String message);
    }

    private interface EntryToLogFactory {
        ILogger create(LogEntry e);
    }

    private static final EntryToLogFactory ANDROID_FACTORY = e -> e.logger;

    private static final class LogEntry {
        private final String levelTag;
        private final ILogger logger;

        LogEntry(String levelTag, ILogger logger) {
            this.levelTag = levelTag;
            this.logger = logger;
        }
    }
    private static final ILogger DEBUG = msg -> Log.d(PREFIX, msg);
    private static final LogEntry DEBUG_ENTRY = new LogEntry("D", DEBUG);
    private static final ILogger ERROR = msg -> Log.e(PREFIX, msg);
    private static final LogEntry DEFAULT_ENTRY = DEBUG_ENTRY;

    private static final Map<Integer, LogEntry> loggers = Map.of(
            Logger.DEBUG, DEBUG_ENTRY,
            Logger.INFO, new LogEntry("I", msg -> Log.i(PREFIX, msg)),
            Logger.WARN, new LogEntry("W", msg -> Log.w(PREFIX, msg)),
            Logger.ERROR, new LogEntry("E", ERROR),
            Logger.FATAL, new LogEntry("F", msg -> Log.wtf(PREFIX, msg))
    );


    private final EntryToLogFactory logFactory;

    static Kp2aJSchLogger createAndroidLogger() {
        return new Kp2aJSchLogger(ANDROID_FACTORY);
    }

    static Kp2aJSchLogger createFileLogger(String logFilename) {
        final String fName = logFilename;
        return new Kp2aJSchLogger(e -> createFileLogger(e, fName));
    }

    private Kp2aJSchLogger(EntryToLogFactory logFactory) {
        this.logFactory = logFactory;
    }

    @Override
    public boolean isEnabled(int level) {
        return true;
    }

    @Override
    public void log(int level, String message) {
        if (isEnabled(level))
            getLogger(level).log(message);
    }

    private ILogger getLogger(int level) {
        LogEntry entry = loggers.get(level);
        if (entry == null)
            entry = DEFAULT_ENTRY;

        return logFactory.create(entry);
    }

    private static ILogger createFileLogger(LogEntry entry, String fName) {
        try {
            final PrintWriter p = new PrintWriter(new FileWriter(fName, true));
            return msg -> {
                try {
                    String fullMsg = String.join(" ", entry.levelTag, PREFIX, msg);
                    p.println(fullMsg);
                } catch (Exception e) {
                    ERROR.log(e.getMessage());
                } finally {
                    p.close();
                }
            };
        } catch (Exception e) {
            ERROR.log(e.getMessage());
            return entry.logger;
        }
    }
}


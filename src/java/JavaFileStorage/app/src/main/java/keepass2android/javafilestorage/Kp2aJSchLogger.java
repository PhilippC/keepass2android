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


    private final String logFilename;

    public Kp2aJSchLogger(String logFilename) {
        this.logFilename = logFilename;
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

        ILogger logger;
        if (logFilename != null) {
            logger = createFileLogger(entry);
        } else {
            logger = entry.logger;
        }

        return logger;
    }

    private ILogger createFileLogger(LogEntry entry) {
        try {
            final PrintWriter p = new PrintWriter(new FileWriter(logFilename, true));
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


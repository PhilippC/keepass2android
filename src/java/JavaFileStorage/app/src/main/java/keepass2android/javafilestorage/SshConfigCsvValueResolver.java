package keepass2android.javafilestorage;

import android.util.Log;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/**
 * A class that manipulates CSV String values based on a list of CSV "spec" definitions, where each definition
 * can describe one of the following:
 *
 *   - Prepend to existing list: +something
 *   - Append to end of existing list: something+
 *   - Remove a specific value: -something
 *   - Remove values matching prefix: -something*
 *   - Remove values matching suffix: -*something
 *   - Remove values matching substring: -*something*
 *   - Remove values matching prefix and suffix: -some*thing
 *
 *  Otherwise CSV of values completely replace original config values
 *  
 *  Examples:
 *  <code>
 *    var r = new SshConfigCsvValueResolver("foo", "addToEnd+,-remove*,+addToBeginning,-*del*");
 *    r.resolve("one,removeTwo,three,removeThree,four") --> "addToBeginning,one,three,four,addToEnd"
 *    r.resolve("one,my-del,del-me,two,foodelbar,three") --> "addToBeginning,one,two,three,addToEnd"
 *
 *    r = new SshConfigCsvValueResolver("foo", "replace,the,config");
 *    r.resolve("one,two,three,four") --> "replace,the,config"
 *  </code>
 *
 */
class SshConfigCsvValueResolver {
    interface Matcher {
        boolean matches(String s);
    }
    private final String cfgKey;
    private static final String TAG = "KP2AJFS[sshcfg]";

    private static final String DELIM = ",";
    private static final char ADD = '+';
    private static final char REMOVE = '-';
    private static final char WILD = '*';
    private final List<String> prepends;
    private final List<String> appends;
    private final List<Matcher> removes;
    private final List<String> replaces;

    /**
     * Creates a new resolver.
     *
     * @param cfgKey - configuration key name (used for logging)
     * @param incomingSpec - A CSV String of "spec" definitions that will be used to
     *                     (potentially) modify incoming CSV String values.
     */
    SshConfigCsvValueResolver(String cfgKey, String incomingSpec) {
        List<String> prepends = new ArrayList<>();
        List<String> appends = new ArrayList<>();
        List<Matcher> removes = new ArrayList<>();
        List<String> replaces = new ArrayList<>();

        for (String iVal : incomingSpec.split(DELIM)) {
            if (iVal.isBlank()) {
                continue;
            }
            int evLen = iVal.length();
            if (iVal.charAt(0) == ADD && evLen > 1) {
                prepends.add(iVal.substring(1));
            } else if (iVal.charAt(iVal.length() - 1) == ADD && evLen > 1) {
                appends.add(iVal.substring(0, evLen - 1));
            } else if (iVal.charAt(iVal.length() - 1) == REMOVE && evLen > 1) {
                removes.add(createMatcher(iVal.substring(1)));
            } else {
                // This looks like a straight replace
                replaces.add(iVal);
            }
        }
        this.cfgKey = cfgKey;
        this.prepends = Collections.unmodifiableList(prepends);
        this.appends = Collections.unmodifiableList(appends);
        this.removes = Collections.unmodifiableList(removes);
        this.replaces = Collections.unmodifiableList(replaces);
    }

    /**
     * Takes a CSV String and (potentially) modifies it according to the "spec" entries of this resolver.
     *
     * @param existingValues - the original CSV String
     * @return an updated representation of <code>existingValues</code>, based on the defined "spec"
     *         entries of this resolver.
     */
    public String resolve(String existingValues) {
        List<String> newValues;
        // If there's even one replace, it wins over everything and the rest is thrown out
        if (!replaces.isEmpty()) {
            if (!(prepends.isEmpty() || appends.isEmpty() || removes.isEmpty())) {
                Log.w(TAG, "Discarded SSH cfg parts: key=" + cfgKey +
                        ", prepends=" + prepends + ", appends=" + appends +
                        ", removes=" + removes);
            }
            newValues = replaces;
        } else {
            // Otherwise we rebuild from existing and incoming values
            newValues = createResolvedValues(existingValues);
        }
        return String.join(DELIM, newValues);
    }

    private List<String> createResolvedValues(String existingValues) {
        List<String> newValues = new ArrayList<>(prepends);
        for (String a : existingValues.split(DELIM)) {
            if (!shouldRemove(a)) {
                newValues.add(a);
            }
        }
        newValues.addAll(appends);
        return newValues;
    }

    private boolean shouldRemove(String s) {
        s = normalize(s);
        for (Matcher m : removes) {
            if (m.matches(s)) {
                return true;
            }
        }
        return false;
    }

    private Matcher createMatcher(String val) {
        final String v = normalize(val);
        Matcher impl = s -> v.equals(s);

        int wildcardIdx = v.indexOf(WILD);
        if (wildcardIdx < 0) {
            return impl;
        }

        // *blah     *blah*     blah*       some*thing
        // endsWith  substring  startsWith  startsWith && endsWith
        String subStr = null;
        String suffix = null;
        String prefix = null;
        int vLen = v.length();

        if (v.charAt(0) == WILD && vLen > 1) {
            if (vLen > 2 && v.charAt(vLen - 1) == WILD) {
                //substring
                subStr = v.substring(1, vLen - 1);
            } else {
                // endsWith
                suffix = v.substring(1);
            }
        } else if (v.charAt(vLen - 1) == WILD && vLen > 1) {
            // beginsWith
            prefix = v.substring(0, v.length() - 1);
        } else if (wildcardIdx > 0) {
            // startsWith && endsWith
            prefix = v.substring(0, wildcardIdx);
            suffix = v.substring(wildcardIdx + 1);
        }

        if (subStr != null) {
            final String sub = subStr;
            impl = s -> s.contains(sub);
        } else if (prefix != null || suffix != null) {
            final String pre = prefix;
            final String suf = suffix;
            impl = s -> (pre == null || s.startsWith(pre)) && (suf == null || s.endsWith(suf));
        }
        return impl;
    }

    private static String normalize(String s) {
        return s == null ? null : s.toLowerCase();
    }
}

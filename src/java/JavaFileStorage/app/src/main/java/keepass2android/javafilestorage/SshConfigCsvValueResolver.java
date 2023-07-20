package keepass2android.javafilestorage;

import android.util.Log;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

class SshConfigCsvValueResolver {
    interface Matcher {
        boolean matches(String s);
    }
    private final String cfgKey;
    private static final String TAG = "KP2AJFS[sshcfg]";
    private final List<String> prepends;
    private final List<String> appends;
    private final List<Matcher> removes;
    private final List<String> replaces;

    SshConfigCsvValueResolver(String cfgKey, String incomingSpec) {
        List<String> prepends = new ArrayList<>();
        List<String> appends = new ArrayList<>();
        List<Matcher> removes = new ArrayList<>();
        List<String> replaces = new ArrayList<>();

        for (String iVal : incomingSpec.split(",")) {
            if (iVal.isBlank()) {
                continue;
            }
            int evLen = iVal.length();
            if (iVal.startsWith("+") && evLen > 1) {
                prepends.add(iVal.substring(1));
            } else if (iVal.endsWith("+") && evLen > 1) {
                appends.add(iVal.substring(0, evLen - 1));
            } else if (iVal.startsWith("-") && evLen > 1) {
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
        return String.join(",", newValues);
    }

    private List<String> createResolvedValues(String existingValues) {
        List<String> newValues = new ArrayList<>(prepends);
        for (String a : existingValues.split(",")) {
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

        int wildcardIdx = v.indexOf('*');
        if (wildcardIdx < 0) {
            return impl;
        }

        // *blah     *blah*     blah*       some*thing
        // endsWith  substring  startsWith  startsWith && endsWith
        String subStr = null;
        String suffix = null;
        String prefix = null;
        int vLen = v.length();

        if (v.charAt(0) == '*' && vLen > 1) {
            if (vLen > 2 && v.charAt(vLen - 1) == '*') {
                //substring
                subStr = v.substring(1, vLen - 1);
            } else {
                // endsWith
                suffix = v.substring(1);
            }
        } else if (v.charAt(vLen - 1) == '*' && vLen > 1) {
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

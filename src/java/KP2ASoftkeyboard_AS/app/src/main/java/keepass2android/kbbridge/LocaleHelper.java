package keepass2android.kbbridge;

import android.content.Context;

import java.text.Collator;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Locale;

import keepass2android.softkeyboard.LanguageSwitcher;


public class LocaleHelper
{
    public boolean x()
    {
        return true;
    }


    private static final String[] WHITELIST_LANGUAGES = {
            "cs", "da", "de", "en", "en_GB", "en_US", "es", "es_US", "fr", "it", "nb", "nl", "pl", "pt",
            "ru", "tr"
    };

    private static final String[] WEAK_WHITELIST_LANGUAGES = {
            "cs", "da", "de", "en_GB", "en_US", "es", "es_US", "fr", "it", "nb", "nl", "pl", "pt",
            "ru", "tr", "en"
    };

    private static boolean isWhitelisted(String lang, boolean strict) {
        for (String s : (strict? WHITELIST_LANGUAGES : WEAK_WHITELIST_LANGUAGES)) {
            if (s.equalsIgnoreCase(lang)) {
                return true;
            }
            if ((!strict) && (s.length()==2) && lang.toLowerCase(Locale.US).startsWith(s))
            {
                return true;
            }
        }
        return false;
    }


    public static ArrayList<Loc> getUniqueLocales(Context ctx, boolean strict) {
        String[] locales = ctx.getAssets().getLocales();
        Arrays.sort(locales);
        ArrayList<Loc> uniqueLocales = new ArrayList<Loc>();

        android.util.Log.d("KP2AK", "getUniqueLocales");
        final int origSize = locales.length;
        Loc[] preprocess = new Loc[origSize];
        int finalSize = 0;
        for (int i = 0 ; i < origSize; i++ ) {
            String s = locales[i];

            int len = s.length();
            final Locale l;
            final String language;
            if (len == 5) {
                language = s.substring(0, 2);
                String country = s.substring(3, 5);
                l = new Locale(language, country);
            } else if (len == 2) {
                language = s;
                l = new Locale(language);
            } else {
                android.util.Log.d("KP2AK", "locale "+s+" has unexpected length.");
                continue;
            }
            // Exclude languages that are not relevant to LatinIME
            if (!isWhitelisted(s, strict))
            {
                android.util.Log.d("KP2AK", "locale "+s+" is not white-listed " + (strict  ? " s " : "w"));
                continue;
            }

            android.util.Log.d("KP2AK", "adding locale "+s);
            if (finalSize == 0) {
                preprocess[finalSize++] =
                        new Loc(LanguageSwitcher.toTitleCase(l.getDisplayName(l), l), l);
            } else {
                // check previous entry:
                //  same lang and a country -> upgrade to full name and
                //    insert ours with full name
                //  diff lang -> insert ours with lang-only name
                if (preprocess[finalSize-1].locale.getLanguage().equals(
                        language)) {
                    preprocess[finalSize-1].label = LanguageSwitcher.toTitleCase(
                            preprocess[finalSize-1].locale.getDisplayName(),
                            preprocess[finalSize-1].locale);
                    preprocess[finalSize++] =
                            new Loc(LanguageSwitcher.toTitleCase(l.getDisplayName(), l), l);
                } else {
                    String displayName;
                    if (s.equals("zz_ZZ")) {
                    } else {
                        displayName = LanguageSwitcher.toTitleCase(l.getDisplayName(l), l);
                        preprocess[finalSize++] = new Loc(displayName, l);
                    }
                }
            }
        }
        for (int i = 0; i < finalSize ; i++) {
            uniqueLocales.add(preprocess[i]);
        }
        return uniqueLocales;
    }
}

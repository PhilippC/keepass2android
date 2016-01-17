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

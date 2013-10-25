/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.utils;

/**
 * Mime types for files.
 * 
 * @author Hai Bison
 * @since v4.5 beta
 */
public class MimeTypes {

    /**
     * Regular expression for plain text files.
     */
    public static final String REGEX_FILE_TYPE_PLAIN_TEXTS = "(?si)^.+\\.(txt|"
            + "html?|json|csv|java|pas|php.*|c|cpp|bas|python|js|javascript|"
            + "scala|xml|kml|css|ps|xslt?|tpl|tsv|bash|cmd|pl|pm|ps1|ps1xml|"
            + "psc1|psd1|psm1|py|pyc|pyo|r|rb|sdl|sh|tcl|vbs|xpl|ada|adb|ads|"
            + "clj|cls|cob|cbl|cxx|cs|csproj|d|e|el|go|h|hpp|hxx|l|m|url|ini|"
            + "prop|conf|properties|rc|srt|sa?mi|cmml|lrc)$";
    
    /**
     * Regular expression for files supported by Keepass2Android.
     */
    public static final String REGEX_FILE_TYPE_KEEPASS2ANDROID = "(?si)^.+\\.(kdbx|kdbp)$";
    

    /**
     * Regular expression for HTML files.
     */
    public static final String REGEX_FILE_TYPE_HTMLS = "(?si)^.+\\.(html?)$";

    /**
     * Regular expression for image files.
     * 
     * @see http://en.wikipedia.org/wiki/Image_file_formats
     */
    public static final String REGEX_FILE_TYPE_IMAGES = "(?si)^.+\\.(gif|jpe?g|"
            + "png|tiff?|wmf|emf|jfif|exif|raw|bmp|ppm|pgm|pbm|pnm|webp|riff|"
            + "tga|ilbm|img|pcx|ecw|sid|cd5|fits|pgf|xcf|svg|pns|jps|icon?|"
            + "jp2|mng|xpm|djvu)$";

    /**
     * Regular expression for audio files.
     * 
     * @see http://en.wikipedia.org/wiki/Audio_file_format
     * @see http://en.wikipedia.org/wiki/List_of_file_formats
     */
    public static final String REGEX_FILE_TYPE_AUDIOS = "(?si)^.+\\.(mp[2-3]+|"
            + "wav|aiff|au|m4a|ogg|raw|flac|mid|amr|aac|alac|atrac|awb|m4p|"
            + "mmf|mpc|ra|rm|tta|vox|wma)$";

    /**
     * Regular expression for video files.
     * 
     * @see http://en.wikipedia.org/wiki/Video_file_formats
     */
    public static final String REGEX_FILE_TYPE_VIDEOS = "(?si)^.+\\.(mp[4]+|"
            + "flv|wmv|webm|m4v|3gp|mkv|mov|mpe?g|rmv?|ogv|avi)$";

    /**
     * Regular expression for APK files.
     */
    public static final String REGEX_FILE_TYPE_APKS = "(?si)^.+\\.apk$";

    /**
     * Regular expression for compressed files.
     * 
     * @see http://en.wikipedia.org/wiki/List_of_file_formats
     */
    public static final String REGEX_FILE_TYPE_COMPRESSED = "(?si)^.+\\.(zip|"
            + "7z|lz?|[jrt]ar|gz|gzip|bzip|xz|cab|sfx|z|iso|bz?|rz|s7z|apk|"
            + "dmg)$";

}

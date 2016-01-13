package keepass2android.softkeyboard;

import java.io.IOException;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

import org.xmlpull.v1.XmlPullParserException;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageManager;
import android.content.pm.ResolveInfo;
import android.content.pm.PackageManager.NameNotFoundException;
import android.content.res.Resources;
import android.content.res.XmlResourceParser;
import android.util.Log;

public class PluginManager extends BroadcastReceiver {
    private static String TAG = "PCKeyboard";
    private static String HK_INTENT_DICT = "org.pocketworkstation.DICT";
    private static String SOFTKEYBOARD_INTENT_DICT = "com.menny.android.anysoftkeyboard.DICTIONARY";
    private KP2AKeyboard mIME;
    
    // Apparently anysoftkeyboard doesn't use ISO 639-1 language codes for its locales?
    // Add exceptions as needed.
    private static Map<String, String> SOFTKEYBOARD_LANG_MAP = new HashMap<String, String>();
    static {
        SOFTKEYBOARD_LANG_MAP.put("dk", "da");
    }
    
    PluginManager(KP2AKeyboard ime) {
    	super();
    	mIME = ime;
    }
    
    private static Map<String, DictPluginSpec> mPluginDicts =
        new HashMap<String, DictPluginSpec>();
    
    static interface DictPluginSpec {
        BinaryDictionary getDict(Context context);
    }

    static private abstract class DictPluginSpecBase
            implements DictPluginSpec {
        String mPackageName;
        
        Resources getResources(Context context) {
            PackageManager packageManager = context.getPackageManager();
            Resources res = null;
            try {
                ApplicationInfo appInfo = packageManager.getApplicationInfo(mPackageName, 0);
                res = packageManager.getResourcesForApplication(appInfo);
            } catch (NameNotFoundException e) {
                Log.i(TAG, "couldn't get resources");
            }
            return res;
        }

        abstract InputStream[] getStreams(Resources res);

        public BinaryDictionary getDict(Context context) {
            Resources res = getResources(context);
            if (res == null) return null;

            InputStream[] dicts = getStreams(res);
            if (dicts == null) return null;
            BinaryDictionary dict = new BinaryDictionary(
                    context, dicts, Suggest.DIC_MAIN);
            if (dict.getSize() == 0) return null;
            //Log.i(TAG, "dict size=" + dict.getSize());
            return dict;
        }
    }

    static private class DictPluginSpecHK
            extends DictPluginSpecBase {
        
        int[] mRawIds;

        public DictPluginSpecHK(String pkg, int[] ids) {
            mPackageName = pkg;
            mRawIds = ids;
        }

        @Override
        InputStream[] getStreams(Resources res) {
            if (mRawIds == null || mRawIds.length == 0) return null;
            InputStream[] streams = new InputStream[mRawIds.length];
            for (int i = 0; i < mRawIds.length; ++i) {
                streams[i] = res.openRawResource(mRawIds[i]);
            }
            return streams;
        }
    }
    
    static private class DictPluginSpecSoftKeyboard
            extends DictPluginSpecBase {
        
        String mAssetName;

        public DictPluginSpecSoftKeyboard(String pkg, String asset) {
            mPackageName = pkg;
            mAssetName = asset;
        }

        @Override
        InputStream[] getStreams(Resources res) {
            if (mAssetName == null) return null;
            try {
                InputStream in = res.getAssets().open(mAssetName);
                return new InputStream[] {in};
            } catch (IOException e) {
                Log.e(TAG, "Dictionary asset loading failure");
                return null;
            }
        }
    }
    
    @Override
    public void onReceive(Context context, Intent intent) {
        Log.i(TAG, "Package information changed, updating dictionaries.");
        getPluginDictionaries(context);
        Log.i(TAG, "Finished updating dictionaries.");
        mIME.toggleLanguage(true, true);
    }

    static void getSoftKeyboardDictionaries(PackageManager packageManager) {
        Intent dictIntent = new Intent(SOFTKEYBOARD_INTENT_DICT);
        List<ResolveInfo> dictPacks = packageManager.queryBroadcastReceivers(
        		dictIntent, PackageManager.GET_RECEIVERS);
        for (ResolveInfo ri : dictPacks) {
            ApplicationInfo appInfo = ri.activityInfo.applicationInfo;
            String pkgName = appInfo.packageName;
            boolean success = false;
            try {
                Resources res = packageManager.getResourcesForApplication(appInfo);
                Log.i("KP2AK", "Found dictionary plugin package: " + pkgName);
                int dictId = res.getIdentifier("dictionaries", "xml", pkgName);
                if (dictId == 0) continue;
                XmlResourceParser xrp = res.getXml(dictId);

                String assetName = null;
                String lang = null;
                try {
                    int current = xrp.getEventType();
                    while (current != XmlResourceParser.END_DOCUMENT) {
                        if (current == XmlResourceParser.START_TAG) {
                            String tag = xrp.getName();
                            if (tag != null) {
                                if (tag.equals("Dictionary")) {
                                    lang = xrp.getAttributeValue(null, "locale");
                                    String convLang = SOFTKEYBOARD_LANG_MAP.get(lang);
                                    if (convLang != null) lang = convLang;
                                    String type = xrp.getAttributeValue(null, "type");
                                    if (type == null || type.equals("raw") || type.equals("binary")) {
                                        assetName = xrp.getAttributeValue(null, "dictionaryAssertName"); // sic
                                    } else {
                                        Log.w(TAG, "Unsupported AnySoftKeyboard dict type " + type);
                                    }
                                    //Log.i(TAG, "asset=" + assetName + " lang=" + lang);
                                }
                            }
                        }
                        xrp.next();
                        current = xrp.getEventType();
                    }
                } catch (XmlPullParserException e) {
                    Log.e(TAG, "Dictionary XML parsing failure");
                } catch (IOException e) {
                    Log.e(TAG, "Dictionary XML IOException");
                }

                if (assetName == null || lang == null) continue;
                DictPluginSpec spec = new DictPluginSpecSoftKeyboard(pkgName, assetName);
                mPluginDicts.put(lang, spec);
                Log.i("KP2AK", "Found plugin dictionary: lang=" + lang + ", pkg=" + pkgName);
                success = true;
            } catch (NameNotFoundException e) {
                Log.i("KP2AK", "bad");
            } finally {
                if (!success) {
                    Log.i("KP2AK", "failed to load plugin dictionary spec from " + pkgName);
                }
            }
        }
    }

    static void getHKDictionaries(PackageManager packageManager) {
        Intent dictIntent = new Intent(HK_INTENT_DICT);
        List<ResolveInfo> dictPacks = packageManager.queryIntentActivities(dictIntent, 0);
        for (ResolveInfo ri : dictPacks) {
            ApplicationInfo appInfo = ri.activityInfo.applicationInfo;
            String pkgName = appInfo.packageName;
            boolean success = false;
            try {
                Resources res = packageManager.getResourcesForApplication(appInfo);
                Log.i("KP2AK", "Found dictionary plugin package: " + pkgName);
                int langId = res.getIdentifier("dict_language", "string", pkgName);
                if (langId == 0) continue;
                String lang = res.getString(langId);
                int[] rawIds = null;
                
                // Try single-file version first
                int rawId = res.getIdentifier("main", "raw", pkgName);
                if (rawId != 0) {
                    rawIds = new int[] { rawId };
                } else {
                    // try multi-part version
                    int parts = 0;
                    List<Integer> ids = new ArrayList<Integer>();
                    while (true) {
                        int id = res.getIdentifier("main" + parts, "raw", pkgName);
                        if (id == 0) break;
                        ids.add(id);
                        ++parts;
                    }
                    if (parts == 0) continue; // no parts found
                    rawIds = new int[parts];
                    for (int i = 0; i < parts; ++i) rawIds[i] = ids.get(i);
                }
                DictPluginSpec spec = new DictPluginSpecHK(pkgName, rawIds);
                mPluginDicts.put(lang, spec);
                Log.i("KP2AK", "Found plugin dictionary: lang=" + lang + ", pkg=" + pkgName);
                success = true;
            } catch (NameNotFoundException e) {
                Log.i("KP2AK", "bad");
            } finally {
                if (!success) {
                    Log.i("KP2AK", "failed to load plugin dictionary spec from " + pkgName);
                }
            }
        }
    }

    static void getPluginDictionaries(Context context) {
        mPluginDicts.clear();
        PackageManager packageManager = context.getPackageManager();
        getSoftKeyboardDictionaries(packageManager);
        getHKDictionaries(packageManager);
    }
    
    static BinaryDictionary getDictionary(Context context, String lang) {
        Log.i("KP2AK", "Looking for plugin dictionary for lang=" + lang);
        DictPluginSpec spec = mPluginDicts.get(lang);
        if (spec == null) spec = mPluginDicts.get(lang.substring(0, 2));
        if (spec == null) {
            Log.i("KP2AK", "No plugin found.");
            return null;
        }
        BinaryDictionary dict = spec.getDict(context);
        Log.i("KP2AK", "Found plugin dictionary for " + lang + (dict == null ? " is null" : ", size=" + dict.getSize()));
        return dict;
    }
}

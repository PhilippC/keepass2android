package keepass2android.autofill;

import android.accessibilityservice.AccessibilityService;
import android.annotation.TargetApi;
import android.app.Notification;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Intent;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageManager;
import android.os.Build;
import android.os.Bundle;
import android.view.accessibility.AccessibilityEvent;
import android.view.accessibility.AccessibilityNodeInfo;

import java.net.URI;
import java.net.URISyntaxException;
import java.util.ArrayList;
import java.util.List;
import java.util.Objects;

import keepass2android.kbbridge.KeyboardData;


/**
 * Created by Philipp on 25.01.2016.
 */
public class AutoFillService extends AccessibilityService {


    private static boolean _hasUsedData = false;
    private static String _lastSearchUrl;
    private static final String _logTag = "KP2AAF";
    private static boolean _isRunning;

    private final int autoFillNotificationId = 798810;
    private final String androidAppPrefix = "androidapp://";

    @Override
    public void onCreate() {
        super.onCreate();
        _isRunning = true;
        android.util.Log.d(_logTag, "OnCreate");
    }

    @Override
    public void onDestroy() {
        super.onDestroy();
        _isRunning = false;
    }

    interface NodeCondition
    {
        boolean check(AccessibilityNodeInfo n);
    }

    class WindowIdCondition implements NodeCondition
    {
        private int id;

        public WindowIdCondition(int id)
        {
            this.id = id;
        }

        @Override
        public boolean check(AccessibilityNodeInfo n) {
            return n.getWindowId() == id;
        }
    }

    boolean isLauncherPackage(CharSequence packageName)
    {
        return "com.android.systemui".equals(packageName)
                || "com.android.launcher3".equals(packageName);
    }

    @TargetApi(21)
    class SystemUiCondition implements NodeCondition
    {
        @Override
        public boolean check(AccessibilityNodeInfo n) {
            return (n.getViewIdResourceName() != null) && (
                    (n.getViewIdResourceName().startsWith("com.android.systemui")) || (n.getViewIdResourceName().startsWith("com.android.launcher3")));
        }
    }

    private class PasswordFieldCondition implements NodeCondition {
        @Override
        public boolean check(AccessibilityNodeInfo n) {
            return n.isPassword();
        }
    }

    private class EditTextCondition implements NodeCondition {
        @Override
        public boolean check(AccessibilityNodeInfo n) {
            //it seems like n.Editable is not a good check as this is false for some fields which are actually editable, at least in tests with Chrome.
            return (n.getClassName() != null) && (n.getClassName().toString().toLowerCase().contains("edittext"));
        }
    }


    public static boolean isAvailable()
    {
        return (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP);
    }

    public static boolean isRunning()
    {
        return _isRunning;
    }

    @Override
    public void onAccessibilityEvent(AccessibilityEvent event) {
        android.util.Log.d(_logTag, "OnAccEvent");

        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.LOLLIPOP)
        {
            android.util.Log.d(_logTag, "AndroidVersion not supported");
            return;
        }

        handleAccessibilityEvent(event);

    }

    @TargetApi(21)
    private void handleAccessibilityEvent(AccessibilityEvent event) {
        try
        {
            if (event.getEventType() ==  AccessibilityEvent.TYPE_WINDOW_CONTENT_CHANGED
                    || event.getEventType() == AccessibilityEvent.TYPE_WINDOW_STATE_CHANGED)
            {
                CharSequence packageName = event.getPackageName();
                android.util.Log.d(_logTag, "event: " + event.getEventType() + ", package = " + packageName);
                if ( isLauncherPackage(event.getPackageName()) )
                {
                    android.util.Log.d(_logTag, "return.");
                    return; //avoid that the notification is cancelled when pulling down notif drawer
                }
                else
                {
                    android.util.Log.d(_logTag, "event package is no launcher");
                }

                if ((packageName != null)
                    && (packageName.toString().startsWith("keepass2android.")))
                {
                    android.util.Log.d(_logTag, "don't autofill kp2a.");
                    return;
                }

                AccessibilityNodeInfo root = getRootInActiveWindow();

                if ( isLauncherPackage(root.getPackageName()) )
                {
                    android.util.Log.d(_logTag, "return, root is from launcher.");
                    return; //avoid that the notification is cancelled when pulling down notif drawer
                }
                else
                {
                    android.util.Log.d(_logTag, "root package is no launcher");
                }

                int eventWindowId = event.getWindowId();
                if ((ExistsNodeOrChildren(root, new WindowIdCondition(eventWindowId)) && !ExistsNodeOrChildren(root, new SystemUiCondition())))
                {
                    boolean cancelNotification = true;

                    String url = androidAppPrefix + root.getPackageName();

                    if ( "com.android.chrome".equals(root.getPackageName()) )
                    {
                        List<AccessibilityNodeInfo> urlFields = root.findAccessibilityNodeInfosByViewId("com.android.chrome:id/url_bar");
                        url = urlFromAddressFields(urlFields, url);

                    }
                    else if (packageName == "com.sec.android.app.sbrowser")
                    {
                        List<AccessibilityNodeInfo> urlFields = root.findAccessibilityNodeInfosByViewId("com.sec.android.app.sbrowser:id/location_bar_edit_text");
                        url = urlFromAddressFields(urlFields, url);
                    }
                    else if ("com.android.browser".equals(root.getPackageName()))
                    {
                        List<AccessibilityNodeInfo> urlFields =  root.findAccessibilityNodeInfosByViewId("com.android.browser:id/url");
                        url = urlFromAddressFields(urlFields, url);
                    }

                    android.util.Log.d(_logTag, "URL=" + url);

                    if (ExistsNodeOrChildren(root, new PasswordFieldCondition()))
                    {
                        if ((getLastReceivedCredentialsUser() != null) &&
                                (Objects.equals(url, _lastSearchUrl)
                                || isSame(getCredentialsField("URL"), url)))
                        {
                            android.util.Log.d(_logTag, "Filling credentials for " + url);

                            List<AccessibilityNodeInfo> emptyPasswordFields = new ArrayList<>();
                            GetNodeOrChildren(root, new PasswordFieldCondition(), emptyPasswordFields);

                            List<AccessibilityNodeInfo> allEditTexts = new ArrayList<>();
                            GetNodeOrChildren(root, new EditTextCondition(), allEditTexts);

                            AccessibilityNodeInfo usernameEdit = null;
                            for (int i=0;i<allEditTexts.size();i++)
                            {
                                if (allEditTexts.get(i).isPassword() == false)
                                {
                                    usernameEdit = allEditTexts.get(i);
                                    android.util.Log.d(_logTag, "setting usernameEdit = " + usernameEdit.getText() + " ");
                                }
                                else break;
                            }

                            FillPassword(url, usernameEdit, emptyPasswordFields);
                        }
                        else
                        {
                            android.util.Log.d (_logTag, "Notif for " + url );
                            AskFillPassword(url);
                            cancelNotification = false;
                        }

                    }
                    if (cancelNotification)
                    {
                        ((NotificationManager)getSystemService(NOTIFICATION_SERVICE)).cancel(autoFillNotificationId);
                        android.util.Log.d (_logTag,"Cancel notif");
                    }
                }

            }
        }
        catch (Exception e)
        {
            android.util.Log.e(_logTag, (e.toString() == null) ? "(null)" : e.toString() );

            /*Intent intent = new Intent(Intent.ACTION_SEND);
            intent.setType("message/rfc822");
            String to =  "crocoapps@gmail.com";
            intent.putExtra(Intent.EXTRA_EMAIL, new String[]{to});
            intent.putExtra(Intent.EXTRA_SUBJECT, "Error report 7d+");
            intent.putExtra(Intent.EXTRA_TEXT,
                    "Please send the following text as an error report to crocoapps@gmail.com. You may also add additional information about the workflow you tried to perform. This will help me improve the app. Thanks! \n"+e.toString() );


            Notification.Builder builder = new Notification.Builder(this);
            builder.setSmallIcon(keepass2android.softkeyboard.R.drawable.ic_notify_autofill)
                    .setContentText(e.toString())
                    .setContentTitle("error information")
                    .setWhen(java.lang.System.currentTimeMillis())
            .setContentIntent(PendingIntent.getActivity(this, 0, Intent.createChooser(intent, "Send error report"), PendingIntent.FLAG_CANCEL_CURRENT));

            NotificationManager notificationManager = (NotificationManager) getSystemService(NOTIFICATION_SERVICE);
            notificationManager.notify(autoFillNotificationId+1, builder.build());*/
        }
    }

    @TargetApi(21)
    private void AskFillPassword(String url)
    {

        Intent startKp2aIntent = getPackageManager().getLaunchIntentForPackage(getApplicationContext().getPackageName());
        if (startKp2aIntent != null)
        {
            startKp2aIntent.addCategory(Intent.CATEGORY_LAUNCHER);
            startKp2aIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
            String taskName = "SearchUrlTask";
            startKp2aIntent.putExtra("KP2A_APPTASK", taskName);
            startKp2aIntent.putExtra("UrlToSearch", url);
        }


        PendingIntent pending = PendingIntent.getActivity(this, 0, startKp2aIntent, PendingIntent.FLAG_UPDATE_CURRENT);
        String targetName = url;

        if (url.startsWith(androidAppPrefix))
        {
            String packageName = url.substring(androidAppPrefix.length());
            try
            {
                ApplicationInfo appInfo = getPackageManager().getApplicationInfo(packageName, 0);
                targetName = (String) (appInfo != null ? getPackageManager().getApplicationLabel(appInfo) : packageName);
            }
            catch (Exception e)
            {
                android.util.Log.d(_logTag, (e.toString() == null) ? "(null)" : e.toString());
                targetName = packageName;
            }
        }
        else
        {
            targetName = getHost(url);
        }


        Notification.Builder builder = new Notification.Builder(this);
        //TODO icon
        //TODO plugin icon
        builder.setSmallIcon(keepass2android.softkeyboard.R.drawable.ic_notify_autofill)
                .setContentText(getString(keepass2android.softkeyboard.R.string.NotificationContentText, new Object[]{targetName}))
                .setContentTitle(getString(keepass2android.softkeyboard.R.string.NotificationTitle))
                .setWhen(java.lang.System.currentTimeMillis())
                .setVisibility(Notification.VISIBILITY_SECRET)
                .setContentIntent(pending);
        NotificationManager notificationManager = (NotificationManager) getSystemService(NOTIFICATION_SERVICE);
        notificationManager.notify(autoFillNotificationId, builder.build());

    }

    @TargetApi(21)
    private void FillPassword(String url, AccessibilityNodeInfo usernameEdit, List<AccessibilityNodeInfo> passwordFields)
    {
        if ((keepass2android.kbbridge.KeyboardData.hasData()) && (_hasUsedData == false))
        {
            fillDataInTextField(usernameEdit, getLastReceivedCredentialsUser());
            for (int i=0;i<passwordFields.size();i++)
            {
                fillDataInTextField(passwordFields.get(i), getLastReceivedCredentialsPassword());
            }
            _hasUsedData = true;
        }



        //LookupCredentialsActivity.LastReceivedCredentials = null;
    }

    @TargetApi(21)
    private void fillDataInTextField(AccessibilityNodeInfo edit, String value) {
        if ((value == null) || (edit == null))
            return;
        Bundle b = new Bundle();
        b.putString(AccessibilityNodeInfo.ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE, value);
        edit.performAction(AccessibilityNodeInfo.ACTION_SET_TEXT, b);
    }


    private boolean isSame(String url1, String url2) {
        if (url1 == null)
            return (url2 == null);
        if (url2 == null)
            return (url1 == null);

        if (url1.startsWith("androidapp://"))
            return url1.equals(url2);

        return getHost(url1).equals(getHost(url2));
    }

    private String getHost(String url)
    {
        URI uri = null;
        try {
            uri = new URI(url);
            String domain = uri.getHost();
            if (domain == null)
                return url;
            return domain.startsWith("www.") ? domain.substring(4) : domain;
        } catch (URISyntaxException e) {
            android.util.Log.d(_logTag, "error parsing url: "+ url + e.toString());
            return url;
        }


    }

    private String getLastReceivedCredentialsUser() {
        return getCredentialsField("UserName");
    }
    private String getLastReceivedCredentialsPassword() {
        return getCredentialsField("Password");
    }

    private String getCredentialsField(String key) {
        for (int i=0;i<KeyboardData.availableFields.size();i++)
        {
            if (key.equals(KeyboardData.availableFields.get(i).key))
            {
                if (KeyboardData.availableFields.get(i).value != null)
                    return KeyboardData.availableFields.get(i).value;
            }
        }
        return null;
    }

    private void GetNodeOrChildren(AccessibilityNodeInfo n, NodeCondition condition, List<AccessibilityNodeInfo> result) {
        if (n != null)
        {
            if (condition.check(n))
                result.add(n);
            for (int i = 0; i < n.getChildCount(); i++)
            {
                GetNodeOrChildren(n.getChild(i), condition, result);
            }
        }
    }

    private boolean ExistsNodeOrChildren(AccessibilityNodeInfo n, NodeCondition condition) {
        if (n == null) return false;
        if (condition.check(n))
            return true;
        for (int i = 0; i < n.getChildCount(); i++)
        {
            if (ExistsNodeOrChildren(n.getChild(i), condition))
                return true;
        }
        return false;
    }

    private String urlFromAddressFields(List<AccessibilityNodeInfo> urlFields, String url) {
        if (!urlFields.isEmpty())
        {
            AccessibilityNodeInfo addressField = urlFields.get(0);
            CharSequence text = addressField.getText();
            if (text != null)
            {
                url = text.toString();
                if (!url.contains("://"))
                    url = "http://" + url;
            }
        }
        return url;
    }

    @Override
    public void onInterrupt() {

    }

    public static void NotifyNewData(String searchUrl)
    {
        _hasUsedData = false;
        _lastSearchUrl = searchUrl;
        android.util.Log.d(_logTag, "Notify new data: " + searchUrl);
    }

}

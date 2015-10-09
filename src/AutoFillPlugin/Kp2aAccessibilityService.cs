using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Views.Accessibility;
using Android.Widget;


namespace keepass2android.AutoFillPlugin
{
    //<meta-data android:name="android.accessibilityservice" android:resource="@xml/serviceconfig" />
    [Service(Enabled =true, Permission= "android.permission.BIND_ACCESSIBILITY_SERVICE")]
    [IntentFilter(new[] { "android.accessibilityservice.AccessibilityService" })]
    [MetaData("android.accessibilityservice", Resource = "@xml/accserviceconfig")]
    public class Kp2aAccessibilityService : Android.AccessibilityServices.AccessibilityService, IDialogInterfaceOnCancelListener
    {
        const string _logTag = "KP2AAS";
        private const int autoFillNotificationId = 0;
        private const string androidAppPrefix = "androidapp://";

        public override void OnCreate()
        {
            base.OnCreate();
            Android.Util.Log.Debug(_logTag, "OnCreate Service");
        }

        protected override void OnServiceConnected()
        {
            Android.Util.Log.Debug(_logTag, "service connected");
            base.OnServiceConnected();
        }

        public override void OnAccessibilityEvent(AccessibilityEvent e)
        {
            
            Android.Util.Log.Debug(_logTag, "OnAccEvent");
            if (e.EventType == EventTypes.WindowContentChanged || e.EventType == EventTypes.WindowStateChanged)
            {
                Android.Util.Log.Debug(_logTag, "event: " + e.EventType + ", package = " + e.PackageName);
				if (e.PackageName == "com.android.systemui")
					return; //avoid that the notification is cancelled when pulling down notif drawer
                var root = RootInActiveWindow;
                if ((ExistsNodeOrChildren(root, n => n.WindowId == e.WindowId) && !ExistsNodeOrChildren(root, n => (n.ViewIdResourceName != null) && (n.ViewIdResourceName.StartsWith("com.android.systemui")))))
                {
					bool cancelNotification = true;

                    var allEditTexts = GetNodeOrChildren(root, n=> { return IsEditText(n); });

                    var usernameEdit = allEditTexts.TakeWhile(edit => (edit.Password == false)).LastOrDefault();

                    string searchString = androidAppPrefix + root.PackageName;

                    string url = androidAppPrefix + root.PackageName;

                    if (root.PackageName == "com.android.chrome")
                    {
                        var addressField = root.FindAccessibilityNodeInfosByViewId("com.android.chrome:id/url_bar").FirstOrDefault();
                        UrlFromAddressField(ref url, addressField);

                    }
                    else if (root.PackageName == "com.android.browser")
                    {
                        var addressField = root.FindAccessibilityNodeInfosByViewId("com.android.browser:id/url").FirstOrDefault();
                        UrlFromAddressField(ref url, addressField);
                    }

                    var emptyPasswordFields = GetNodeOrChildren(root, n => { return IsPasswordField(n); }).ToList();
                    if (emptyPasswordFields.Any())
                    {
						if ((LookupCredentialsActivity.LastReceivedCredentials != null) && IsSame(LookupCredentialsActivity.LastReceivedCredentials.Url, url))
                        {
							Android.Util.Log.Debug ("KP2AAS", "Filling credentials for " + url);

                            FillPassword(url, usernameEdit, emptyPasswordFields);
                        }
                        else
                        {
							Android.Util.Log.Debug ("KP2AAS", "Notif for " + url );
							if (LookupCredentialsActivity.LastReceivedCredentials != null) 
							{
								Android.Util.Log.Debug ("KP2AAS", LookupCredentialsActivity.LastReceivedCredentials.Url);
								Android.Util.Log.Debug ("KP2AAS", url);
							}

                            AskFillPassword(url, usernameEdit, emptyPasswordFields);
                            cancelNotification = false;
                        }
                        
                    }
					if (cancelNotification)
					{
						((NotificationManager)GetSystemService(NotificationService)).Cancel(autoFillNotificationId);
						Android.Util.Log.Debug ("KP2AAS","Cancel notif");
					}
                }

            }
            

        }
        private static void UrlFromAddressField(ref string url, AccessibilityNodeInfo addressField)
        {
            if (addressField != null)
            {
                url = addressField.Text;
                if (!url.Contains("://"))
                    url = "http://" + url;
            }
            
        }

		private bool IsSame(string url1, string url2)
		{
			if (url1.StartsWith ("androidapp://"))
				return url1 == url2;
			return KeePassLib.Utility.UrlUtil.GetHost (url1) == KeePassLib.Utility.UrlUtil.GetHost (url2);
		}

        private static bool IsPasswordField(AccessibilityNodeInfo n)
        {
            //if (n.Password) Android.Util.Log.Debug(_logTag, "pwdx with " + (n.Text == null ? "null" : n.Text));
            var res = n.Password && string.IsNullOrEmpty(n.Text);
            // if (n.Password) Android.Util.Log.Debug(_logTag, "pwd with " + n.Text + res);
            return res;
        }
        
        private static bool IsEditText(AccessibilityNodeInfo n)
        {
            //it seems like n.Editable is not a good check as this is false for some fields which are actually editable, at least in tests with Chrome.
            return (n.ClassName != null) && (n.ClassName.Contains("EditText"));
        }

        private void AskFillPassword(string url, AccessibilityNodeInfo usernameEdit, IEnumerable<AccessibilityNodeInfo> passwordFields)
        {
            var runSearchIntent = new Intent(this, typeof(LookupCredentialsActivity));
            runSearchIntent.PutExtra("url", url);
            runSearchIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            var pending = PendingIntent.GetActivity(this, 0, runSearchIntent, PendingIntentFlags.UpdateCurrent);

            var targetName = url;

            if (url.StartsWith(androidAppPrefix))
            {
                var packageName = url.Substring(androidAppPrefix.Length);
                try
                {
					var appInfo = PackageManager.GetApplicationInfo(packageName, 0);
					targetName = (string) (appInfo != null ? PackageManager.GetApplicationLabel(appInfo) : packageName);
                }
                catch (Exception e)
                {
                    Android.Util.Log.Debug(_logTag, e.ToString());
                    targetName = packageName;
                }
            }
            else
            {
                targetName = KeePassLib.Utility.UrlUtil.GetHost(url);
            }
            

            var builder = new Notification.Builder(this);
            //TODO icon
            //TODO plugin icon
            builder.SetSmallIcon(Resource.Drawable.ic_notify_autofill)
                   .SetContentText(GetString(Resource.String.NotificationContentText, new Java.Lang.Object[] { targetName }))
                   .SetContentTitle(GetString(Resource.String.NotificationTitle))
                   .SetWhen(Java.Lang.JavaSystem.CurrentTimeMillis())
                   .SetTicker( GetString(Resource.String.NotificationTickerText, new Java.Lang.Object[] { targetName }))
                   .SetVisibility(Android.App.NotificationVisibility.Secret)
                   .SetContentIntent(pending);
            var notificationManager = (NotificationManager)GetSystemService(NotificationService);
            notificationManager.Notify(autoFillNotificationId, builder.Build());
            
        }

        private void FillPassword(string url, AccessibilityNodeInfo usernameEdit, IEnumerable<AccessibilityNodeInfo> passwordFields)
        {
            
            FillDataInTextField(usernameEdit, LookupCredentialsActivity.LastReceivedCredentials.User);
            foreach (var pwd in passwordFields)
                FillDataInTextField(pwd, LookupCredentialsActivity.LastReceivedCredentials.Password);

            LookupCredentialsActivity.LastReceivedCredentials = null;
        }

        private static void FillDataInTextField(AccessibilityNodeInfo edit, string newValue)
        {
            Bundle b = new Bundle();
            b.PutString(AccessibilityNodeInfo.ActionArgumentSetTextCharsequence, newValue);
            edit.PerformAction(Android.Views.Accessibility.Action.SetText, b);
        }

        private bool ExistsNodeOrChildren(AccessibilityNodeInfo n, Func<AccessibilityNodeInfo, bool> p)
        {
            return GetNodeOrChildren(n, p).Any();
        }

        private IEnumerable<AccessibilityNodeInfo> GetNodeOrChildren(AccessibilityNodeInfo n, Func<AccessibilityNodeInfo, bool> p)
        {
            if (n != null)
            {
                if (p(n))
                    yield return n;
                for (int i = 0; i < n.ChildCount; i++)
                {
                    foreach (var x in GetNodeOrChildren(n.GetChild(i), p))
                        yield return x;
                }
            }           
            
        }

        public override void OnInterrupt()
        {
            
        }

        public void OnCancel(IDialogInterface dialog)
        {
            
        }
    }
}
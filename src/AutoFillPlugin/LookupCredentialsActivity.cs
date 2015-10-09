using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using Keepass2android.Pluginsdk;

namespace keepass2android.AutoFillPlugin
{
    [Activity(Label = "@string/LookupTitle", LaunchMode = Android.Content.PM.LaunchMode.SingleInstance)]
    public class LookupCredentialsActivity : Activity
    {
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            var url = Intent.GetStringExtra("url");
            _lastQueriedUrl = url;
            StartActivityForResult(Kp2aControl.GetQueryEntryIntent(url), 123);
        }

        string _lastQueriedUrl;

        protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

			try
			{
					
	            var jsonOutput = new Org.Json.JSONObject(data.GetStringExtra(Strings.ExtraEntryOutputData));
	            Dictionary<string, string> output = new Dictionary<string, string>();
	            for (var iter = jsonOutput.Keys(); iter.HasNext;)
	            {
	                string key = iter.Next().ToString();
	                string value = jsonOutput.Get(key).ToString();
	                output[key] = value;
	            }


	            string user = "", password = "";
	            output.TryGetValue(KeePassLib.PwDefs.UserNameField, out user);
	            output.TryGetValue(KeePassLib.PwDefs.PasswordField, out password);
				Android.Util.Log.Debug ("KP2AAS", "Received credentials for " + _lastQueriedUrl);
	            LastReceivedCredentials = new Credentials() { User = user, Password = password, Url = _lastQueriedUrl };
			}
			catch(Exception e) {
				Android.Util.Log.Debug ("KP2AAS", "Exception while receiving credentials: " + e.ToString());
			}
			finally {
				
				Finish ();
			}
        }

        public static Credentials LastReceivedCredentials;
    }
}
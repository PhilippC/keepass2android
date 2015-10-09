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
    [BroadcastReceiver(Exported = true)]
    [IntentFilter(new[] { Strings.ActionTriggerRequestAccess, Strings.ActionReceiveAccess, Strings.ActionRevokeAccess })]
    public class AccessReceiver : PluginAccessBroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            Android.Util.Log.Debug("KP2AAS", intent.Action);
            base.OnReceive(context, intent);
        }

        public override IList<string> Scopes
        {
            get
            {
                return new List<string>
                    {
                        Strings.ScopeQueryCredentials
                    };
            }
        }
    }
}
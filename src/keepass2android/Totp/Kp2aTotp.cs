using System;
using System.Linq;
using System.Text;
using Android.App;
using KeePassLib.Utility;
using PluginTOTP;

namespace keepass2android
{
	class Kp2aTotp
	{

		readonly ITotpPluginAdapter[] _pluginAdapters = new ITotpPluginAdapter[] { new TrayTotpPluginAdapter(), new KeeOtpPluginAdapter(), new KeeWebOtpPluginAdapter() };

		public void OnOpenEntry()
        {
            if (App.Kp2a.LastOpenedEntry == null)
                return;
            foreach (ITotpPluginAdapter adapter in _pluginAdapters)
			{
				TotpData totpData = adapter.GetTotpData(App.Kp2a.LastOpenedEntry.OutputStrings.ToDictionary(pair => StrUtil.SafeXmlString(pair.Key), pair => pair.Value.ReadString()), Application.Context, false);
				if (totpData.IsTotpEnry)
				{
					new UpdateTotpTimerTask(Application.Context, adapter).Run();
				}
			}
		}
	}
}

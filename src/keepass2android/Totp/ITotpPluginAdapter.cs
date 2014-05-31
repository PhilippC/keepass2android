using System.Collections.Generic;
using Android.Content;
using KeePassLib.Collections;

namespace PluginTOTP
{

	interface ITotpPluginAdapter
	{
		TotpData GetTotpData(IDictionary<string, string> entryFields, Context ctx);
	}
}
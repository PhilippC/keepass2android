using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.Util;
using KeePassLib.Utility;
using Keepass2android.Pluginsdk;


namespace MasterKeePlugin
{
	[BroadcastReceiver(Exported = true)]
	[IntentFilter(new[] {Strings.ActionOpenEntry, Strings.ActionEntryActionSelected })]
	public class ActionReceiver: PluginActionBroadcastReceiver
	{
		public override void OnReceive(Context context, Intent intent)
		{
			Android.Util.Log.Debug("KP2A_MasterKee", intent.Action);
			base.OnReceive(context, intent);
		}

		private static byte[] HashHMAC(byte[] key, byte[] message)
		{
			var hash = new HMACSHA256(key);
			return hash.ComputeHash(message);
		}
		protected override void OpenEntry(OpenEntryAction oe)
		{
			string masterkey;
			Android.Util.Log.Debug("KP2A_MasterKee", "Opening entry...");
			if (oe.EntryFields.TryGetValue("MK_MasterKey", out masterkey))
			{
				Android.Util.Log.Debug("KP2A_MasterKee", "Entry is MK entry.");
				string type;
				if (!oe.EntryFields.TryGetValue("MK_Type", out type))
					type = "Long Password";
				int counter = 1;
				string strCounter;
				if (oe.EntryFields.TryGetValue("MK_Site_Counter", out strCounter))
				{
					int.TryParse(strCounter, out counter);
				}
				Android.Util.Log.Debug("KP2A_MasterKee", "Calculating password...");
				string calculatedKey = MasterPassword.MpAlgorithm.GenerateContent(type,
				                                                                  oe.EntryFields[KeepassDefs.TitleField],
				                                                                  MemUtil.HexStringToByteArray(masterkey),
				                                                                  counter, 
																				  HashHMAC
					);
				Android.Util.Log.Debug("KP2A_MasterKee", "ok. Returning data.");
				try
				{
					oe.SetEntryField(KeepassDefs.PasswordField, calculatedKey, true);
					oe.SetEntryField("MK_MasterKey", "", true);
					oe.SetEntryField("MK_Type", "", true);
					oe.SetEntryField("MK_Site_Counter", "", true);

				}
				catch (Exception e)
				{
					Android.Util.Log.Debug("KP2A_MasterKee", e.ToString());
				}
				
			}
			Android.Util.Log.Debug("KP2A_MasterKee", "Done.");
		}

		protected override void ActionSelected(ActionSelectedAction asa)
		{
			
		}
	}
}
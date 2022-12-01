﻿using System;
using System.Collections.Generic;
using System.Linq;
using Android.Content;
using Java.Util;
using KeePassLib.Security;
using KeePassLib.Utility;
using Keepass2android.Pluginsdk;
using keepass2android;
using KeeTrayTOTP.Libraries;

namespace PluginTOTP
{
	class UpdateTotpTimerTask: TimerTask
	{
		public const string TotpKey = "TOTP";
		private readonly Context _context;
		private readonly ITotpPluginAdapter _adapter;

		public UpdateTotpTimerTask(Context context, ITotpPluginAdapter adapter)
		{
			_context = context;
			_adapter = adapter;
		}

		public override void Run()
		{
			try
			{
				if (App.Kp2a.LastOpenedEntry == null)
					return; //DB was locked

				Dictionary<string, string> entryFields = App.Kp2a.LastOpenedEntry.OutputStrings.ToDictionary(pair => StrUtil.SafeXmlString(pair.Key), pair => pair.Value.ReadString());
				//mute warnings to avoid repeated display of the toasts
				TotpData totpData = _adapter.GetTotpData(entryFields, _context, true /*mute warnings*/);
				if (totpData.IsTotpEntry)
				{
					//generate a new totp
					TOTPProvider prov = new TOTPProvider(totpData);
					string totp = prov.GenerateByByte(totpData.TotpSecret);
					//update entry and keyboard
					UpdateEntryData(totp);
					//broadcast new field value (update EntryActivity). this might result in another keyboard 
					//update, but that's inexpensive and relatively rare
					BroadcastNewTotp(totp);
					//restart timer
					new Timer().Schedule(new UpdateTotpTimerTask(_context, _adapter), 1000 * prov.Timer);
				}
			}
			catch (Exception e)
			{
				Android.Util.Log.Debug(TotpKey, e.ToString());
			}
			
			
		}

		private void UpdateEntryData(string totp)
		{
			//update the Entry output in the App database and notify the CopyToClipboard service
			App.Kp2a.LastOpenedEntry.OutputStrings.Set(TotpKey, new ProtectedString(true, totp));
			Intent updateKeyboardIntent = new Intent(_context, typeof(CopyToClipboardService));
			updateKeyboardIntent.SetAction(Intents.UpdateKeyboard);
			updateKeyboardIntent.PutExtra(EntryActivity.KeyEntry, new ElementAndDatabaseId(App.Kp2a.FindDatabaseForElement(App.Kp2a.LastOpenedEntry.Entry), App.Kp2a.LastOpenedEntry.Entry).FullId);
			_context.StartService(updateKeyboardIntent);

		}

		private void BroadcastNewTotp(string totp)
		{
			Intent i = new Intent(Strings.ActionSetEntryField);
			i.PutExtra(Strings.ExtraAccessToken,new PluginDatabase(_context).GetInternalToken());
			i.SetPackage(_context.PackageName);
			i.PutExtra(Strings.ExtraSender, _context.PackageName);
			i.PutExtra(Strings.ExtraFieldValue, totp);
			i.PutExtra(Strings.ExtraEntryId, App.Kp2a.LastOpenedEntry.Entry.Uuid.ToHexString());
			i.PutExtra(Strings.ExtraFieldId, TotpKey);
			i.PutExtra(Strings.ExtraFieldProtected, true);
			
			_context.SendBroadcast(i);
		}
	}
}
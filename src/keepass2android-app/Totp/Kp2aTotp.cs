// This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
//
//   Keepass2Android is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   Keepass2Android is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Linq;
using System.Text;
using Android.App;
using KeePassLib;
using KeePassLib.Utility;
using PluginTOTP;

namespace keepass2android
{
  class Kp2aTotp
  {
    public const string TotpKey = "TOTP";

    readonly ITotpPluginAdapter[] _pluginAdapters = new ITotpPluginAdapter[]
    {
            new TrayTotpPluginAdapter(),
            new KeeOtpPluginAdapter(),
            new KeeWebOtpPluginAdapter(),
            new Keepass2TotpPluginAdapter(),
    };


    public TotpData TryGetTotpData(PwEntryOutput entry)
    {
      if (entry == null)
        return null;
      foreach (ITotpPluginAdapter adapter in _pluginAdapters)
      {
        TotpData totpData = adapter.GetTotpData(entry.OutputStrings.ToDictionary(pair => StrUtil.SafeXmlString(pair.Key), pair => pair.Value.ReadString()), LocaleManager.LocalizedAppContext, false);
        if (totpData.IsTotpEntry)
        {
          return totpData;
        }
      }

      return null;
    }

    public ITotpPluginAdapter TryGetAdapter(PwEntryOutput entry)
    {
      if (entry == null)
        return null;

      try
      {
        foreach (ITotpPluginAdapter adapter in _pluginAdapters)
        {
          TotpData totpData = adapter.GetTotpData(
              entry.OutputStrings.ToDictionary(pair => StrUtil.SafeXmlString(pair.Key),
                  pair => pair.Value.ReadString()), LocaleManager.LocalizedAppContext, false);
          if (totpData.IsTotpEntry)
          {
            return adapter;
          }
        }
      }
      catch (Exception e)
      {
        Kp2aLog.LogUnexpectedError(e);
      }


      return null;
    }

    public void OnOpenEntry()
    {
      var adapter = TryGetAdapter(App.Kp2a.LastOpenedEntry);
      if (adapter != null)
        new UpdateTotpTimerTask(LocaleManager.LocalizedAppContext, adapter).Run();
    }
  }
}

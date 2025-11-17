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
using System.Collections.Generic;
using System.Web;
using Android.Content;
using PluginTOTP;

namespace keepass2android
{
  internal class KeeWebOtpPluginAdapter : ITotpPluginAdapter
  {
    public TotpData GetTotpData(IDictionary<string, string> entryFields, Context ctx, bool muteWarnings)
    {
      TotpData res = new TotpData();
      string data;
      if (!entryFields.TryGetValue("otp", out data))
      {
        return res;
      }
      res.InternalFields.Add("otp");

      string otpUriStart = "otpauth://totp/";

      if (!data.StartsWith(otpUriStart))
        return res;


      try
      {
        Uri myUri = new Uri(data);
        var parsedQuery = HttpUtility.ParseQueryString(myUri.Query);
        res.TotpSeed = parsedQuery.Get("secret");
        res.Length = parsedQuery.Get("digits");
        res.Duration = parsedQuery.Get("period");
        res.Encoder = parsedQuery.Get("encoder");
        string algo = parsedQuery.Get("algorithm");
        if (algo == "SHA512")
          res.HashAlgorithm = TotpData.HashSha512;
        if (algo == "SHA256")
          res.HashAlgorithm = TotpData.HashSha256;


        //set defaults according to https://github.com/google/google-authenticator/wiki/Key-Uri-Format
        if (res.Length == null)
          res.Length = "6";
        if (res.Duration == null)
          res.Duration = "30";
        if (res.Encoder == null)
          res.Encoder = TotpData.EncoderRfc6238;
      }
      catch (Exception e)
      {
        return res;
      }

      res.IsTotpEntry = true;
      return res;
    }
  }
}
using System;
using System.Collections.Generic;
using Android.Content;
using keepass2android;
using KeePassLib;
using KeePassLib.Cryptography;
using KeePassLib.Utility;

namespace PluginTOTP
{
    class Keepass2TotpPluginAdapter : ITotpPluginAdapter
    {
        public TotpData GetTotpData(IDictionary<string, string> entryFields, Context ctx, bool muteWarnings)
        {
            TotpData res = new TotpData();
            byte[] pbSecret = (GetOtpSecret(entryFields, "TimeOtp-") ?? MemUtil.EmptyByteArray);
            if (pbSecret.Length == 0)
                return res;

            string strPeriod;
            uint uPeriod = 0;
            if (entryFields.TryGetValue("TimeOtp-Period", out strPeriod))
            {
                uint.TryParse(strPeriod, out uPeriod);
            }

            res.IsTotpEntry = true;

            if (uPeriod == 0)
                uPeriod = 30U;

            string strLength;
            uint uLength = 0;
            if (entryFields.TryGetValue("TimeOtp-Length", out strLength))
            {
                uint.TryParse(strLength, out uLength);
            }
            
            
            if (uLength == 0) uLength = 6;

            string strAlg;
            entryFields.TryGetValue("TimeOtp-Algorithm", out strAlg);

            res.HashAlgorithm = strAlg;
            res.TotpSecret = pbSecret;
            res.Length = uLength.ToString();
            res.Duration = uPeriod.ToString();

            return res;
        }


        private static byte[] GetOtpSecret(IDictionary<string, string> entryFields, string strPrefix)
        {
            try
            {
                string str;
                entryFields.TryGetValue(strPrefix + "Secret", out str);
                if (!string.IsNullOrEmpty(str))
                    return StrUtil.Utf8.GetBytes(str);

                entryFields.TryGetValue(strPrefix + "Secret-Hex", out str);
                if (!string.IsNullOrEmpty(str))
                    return MemUtil.HexStringToByteArray(str);

                entryFields.TryGetValue(strPrefix + "Secret-Base32", out str);
                if (!string.IsNullOrEmpty(str))
                    return MemUtil.ParseBase32(str);

                entryFields.TryGetValue(strPrefix + "Secret-Base64", out str);
                if (!string.IsNullOrEmpty(str))
                    return Convert.FromBase64String(str);
            }
            catch (Exception e)
            {
                Kp2aLog.LogUnexpectedError(e);
            }

            return null;
        }
    }
}
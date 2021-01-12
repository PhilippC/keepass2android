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
                    res.HashAlgorithm = "HMAC-SHA-512";
                if (algo == "SHA256")
                    res.HashAlgorithm = "HMAC-SHA-256";


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
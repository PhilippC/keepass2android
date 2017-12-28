using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using keepass2android.services.AutofillBase.model;

namespace keepass2android.services.AutofillBase
{
    class AutofillHintsHelper
    {
        private static readonly HashSet<string> validHints = new HashSet<string>()
        {
            View.AutofillHintUsername,
            View.AutofillHintPassword,
            W3cHints.USERNAME,
            W3cHints.CURRENT_PASSWORD,
            W3cHints.NEW_PASSWORD
        };

        private static readonly Dictionary<string, string> hintReplacements= new Dictionary<string, string>()
        {
            {W3cHints.EMAIL, View.AutofillHintEmailAddress},
            {W3cHints.USERNAME, View.AutofillHintUsername},
            {W3cHints.CURRENT_PASSWORD, View.AutofillHintPassword},
            {W3cHints.NEW_PASSWORD, View.AutofillHintPassword},
            {W3cHints.CC_EXPIRATION_MONTH, View.AutofillHintCreditCardExpirationMonth },
            {W3cHints.CC_EXPIRATION_YEAR, View.AutofillHintCreditCardExpirationYear },
            {W3cHints.CC_EXPIRATION, View.AutofillHintCreditCardExpirationDate },
            {W3cHints.CC_NUMBER, View.AutofillHintCreditCardNumber },
            {W3cHints.CC_CSC, View.AutofillHintCreditCardSecurityCode },
            {W3cHints.POSTAL_CODE, View.AutofillHintPostalCode },


        };

        public static bool IsValidHint(string hint)
        {
            return validHints.Contains(hint);
        }


        public static string[] FilterForSupportedHints(string[] hints)
        {
            var filteredHints = new string[hints.Length];
            int i = 0;
            foreach (var hint in hints)
            {
                if (IsValidHint(hint))
                {
                    filteredHints[i++] = hint;
                }
                else
                {
                    Log.Debug(CommonUtil.Tag, "Invalid autofill hint: " + hint);
                }
            }
            var finalFilteredHints = new string[i];
            Array.Copy(filteredHints, 0, finalFilteredHints, 0, i);
            return finalFilteredHints;
        }



        public static List<string> ConvertToStoredHints(string[] supportedHints)
        {
            List<string> result = new List<string>();
            foreach (string hint in supportedHints)
            {
                string storedHint = hint;
                if (hintReplacements.ContainsKey(hint))
                    storedHint = hintReplacements[hint];
                result.Add(storedHint);

            }
            return result;

        }
    }
}
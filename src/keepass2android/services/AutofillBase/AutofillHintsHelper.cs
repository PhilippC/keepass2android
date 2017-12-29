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
        private static readonly HashSet<string> _allSupportedHints = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            View.AutofillHintCreditCardExpirationDate,
            View.AutofillHintCreditCardExpirationDay,
            View.AutofillHintCreditCardExpirationMonth,
            View.AutofillHintCreditCardExpirationYear,
            View.AutofillHintCreditCardNumber,
            View.AutofillHintCreditCardSecurityCode,
            View.AutofillHintEmailAddress,
            View.AutofillHintPhone,
            View.AutofillHintName,
            View.AutofillHintPassword,
            View.AutofillHintPostalAddress,
            View.AutofillHintPostalCode,
            View.AutofillHintUsername,
            W3cHints.HONORIFIC_PREFIX,
            W3cHints.NAME,
            W3cHints.GIVEN_NAME,
            W3cHints.ADDITIONAL_NAME,
            W3cHints.FAMILY_NAME,
            W3cHints.HONORIFIC_SUFFIX,
            W3cHints.USERNAME,
            W3cHints.NEW_PASSWORD,
            W3cHints.CURRENT_PASSWORD,
            W3cHints.ORGANIZATION_TITLE,
            W3cHints.ORGANIZATION,
            W3cHints.STREET_ADDRESS,
            W3cHints.ADDRESS_LINE1,
            W3cHints.ADDRESS_LINE2,
            W3cHints.ADDRESS_LINE3,
            W3cHints.ADDRESS_LEVEL4,
            W3cHints.ADDRESS_LEVEL3,
            W3cHints.ADDRESS_LEVEL2,
            W3cHints.ADDRESS_LEVEL1,
            W3cHints.COUNTRY,
            W3cHints.COUNTRY_NAME,
            W3cHints.POSTAL_CODE,
            W3cHints.CC_NAME,
            W3cHints.CC_GIVEN_NAME,
            W3cHints.CC_ADDITIONAL_NAME,
            W3cHints.CC_FAMILY_NAME,
            W3cHints.CC_NUMBER,
            W3cHints.CC_EXPIRATION,
            W3cHints.CC_EXPIRATION_MONTH,
            W3cHints.CC_EXPIRATION_YEAR,
            W3cHints.CC_CSC,
            W3cHints.CC_TYPE,
            W3cHints.TRANSACTION_CURRENCY,
            W3cHints.TRANSACTION_AMOUNT,
            W3cHints.LANGUAGE,
            W3cHints.BDAY,
            W3cHints.BDAY_DAY,
            W3cHints.BDAY_MONTH,
            W3cHints.BDAY_YEAR,
            W3cHints.SEX,
            W3cHints.URL,
            W3cHints.PHOTO,
            W3cHints.TEL,
            W3cHints.TEL_COUNTRY_CODE,
            W3cHints.TEL_NATIONAL,
            W3cHints.TEL_AREA_CODE,
            W3cHints.TEL_LOCAL,
            W3cHints.TEL_LOCAL_PREFIX,
            W3cHints.TEL_LOCAL_SUFFIX,
            W3cHints.TEL_EXTENSION,
            W3cHints.EMAIL,
            W3cHints.IMPP,
        };

        private static readonly Dictionary<string, string> hintToCanonicalReplacement= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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

        public static bool IsSupportedHint(string hint)
        {
            return _allSupportedHints.Contains(hint);
        }


        public static string[] FilterForSupportedHints(string[] hints)
        {
            var filteredHints = new string[hints.Length];
            int i = 0;
            foreach (var hint in hints)
            {
                if (IsSupportedHint(hint))
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



        /// <summary>
        /// transforms hints by replacing some W3cHints by their Android counterparts and transforming everything to lowercase
        /// </summary>
        public static List<string> ConvertToCanonicalHints(string[] supportedHints)
        {
            List<string> result = new List<string>();
            foreach (string hint in supportedHints)
            {
                string canonicalHint;
                if (!hintToCanonicalReplacement.TryGetValue(hint, out canonicalHint))
                    canonicalHint = hint;
                result.Add(canonicalHint.ToLower());
            }
            return result;

        }
    }
}
using Android.Util;

namespace keepass2android.services.AutofillBase.model
{
    public class W3cHints
    {

        // Supported W3C autofill tokens (https://html.spec.whatwg.org/multipage/forms.html#autofill)
        public const string HONORIFIC_PREFIX = "honorific-prefix";
        public const string NAME = "name";
        public const string GIVEN_NAME = "given-name";
        public const string ADDITIONAL_NAME = "additional-name";
        public const string FAMILY_NAME = "family-name";
        public const string HONORIFIC_SUFFIX = "honorific-suffix";
        public const string USERNAME = "username";
        public const string NEW_PASSWORD = "new-password";
        public const string CURRENT_PASSWORD = "current-password";
        public const string ORGANIZATION_TITLE = "organization-title";
        public const string ORGANIZATION = "organization";
        public const string STREET_ADDRESS = "street-address";
        public const string ADDRESS_LINE1 = "address-line1";
        public const string ADDRESS_LINE2 = "address-line2";
        public const string ADDRESS_LINE3 = "address-line3";
        public const string ADDRESS_LEVEL4 = "address-level4";
        public const string ADDRESS_LEVEL3 = "address-level3";
        public const string ADDRESS_LEVEL2 = "address-level2";
        public const string ADDRESS_LEVEL1 = "address-level1";
        public const string COUNTRY = "country";
        public const string COUNTRY_NAME = "country-name";
        public const string POSTAL_CODE = "postal-code";
        public const string CC_NAME = "cc-name";
        public const string CC_GIVEN_NAME = "cc-given-name";
        public const string CC_ADDITIONAL_NAME = "cc-additional-name";
        public const string CC_FAMILY_NAME = "cc-family-name";
        public const string CC_NUMBER = "cc-number";
        public const string CC_EXPIRATION = "cc-exp";
        public const string CC_EXPIRATION_MONTH = "cc-exp-month";
        public const string CC_EXPIRATION_YEAR = "cc-exp-year";
        public const string CC_CSC = "cc-csc";
        public const string CC_TYPE = "cc-type";
        public const string TRANSACTION_CURRENCY = "transaction-currency";
        public const string TRANSACTION_AMOUNT = "transaction-amount";
        public const string LANGUAGE = "language";
        public const string BDAY = "bday";
        public const string BDAY_DAY = "bday-day";
        public const string BDAY_MONTH = "bday-month";
        public const string BDAY_YEAR = "bday-year";
        public const string SEX = "sex";
        public const string URL = "url";
        public const string PHOTO = "photo";
        // Optional W3C prefixes
        public const string PREFIX_SECTION = "section-";
        public const string SHIPPING = "shipping";
        public const string BILLING = "billing";
        // W3C prefixes below...
        public const string PREFIX_HOME = "home";
        public const string PREFIX_WORK = "work";
        public const string PREFIX_FAX = "fax";
        public const string PREFIX_PAGER = "pager";
        // ... require those suffix
        public const string TEL = "tel";
        public const string TEL_COUNTRY_CODE = "tel-country-code";
        public const string TEL_NATIONAL = "tel-national";
        public const string TEL_AREA_CODE = "tel-area-code";
        public const string TEL_LOCAL = "tel-local";
        public const string TEL_LOCAL_PREFIX = "tel-local-prefix";
        public const string TEL_LOCAL_SUFFIX = "tel-local-suffix";
        public const string TEL_EXTENSION = "tel_extension";
        public const string EMAIL = "email";
        public const string IMPP = "impp";

        private W3cHints()
        {
        }



        public static bool isW3cSectionPrefix(string hint)
        {
            return hint.ToLower().StartsWith(W3cHints.PREFIX_SECTION);
        }

        public static bool isW3cAddressType(string hint)
        {
            switch (hint.ToLower())
            {
                case W3cHints.SHIPPING:
                case W3cHints.BILLING:
                    return true;
            }
            return false;
        }

        public static bool isW3cTypePrefix(string hint)
        {
            switch (hint.ToLower())
            {
                case W3cHints.PREFIX_WORK:
                case W3cHints.PREFIX_FAX:
                case W3cHints.PREFIX_HOME:
                case W3cHints.PREFIX_PAGER:
                    return true;
            }
            return false;
        }

        public static bool isW3cTypeHint(string hint)
        {
            switch (hint.ToLower())
            {
                case W3cHints.TEL:
                case W3cHints.TEL_COUNTRY_CODE:
                case W3cHints.TEL_NATIONAL:
                case W3cHints.TEL_AREA_CODE:
                case W3cHints.TEL_LOCAL:
                case W3cHints.TEL_LOCAL_PREFIX:
                case W3cHints.TEL_LOCAL_SUFFIX:
                case W3cHints.TEL_EXTENSION:
                case W3cHints.EMAIL:
                case W3cHints.IMPP:
                    return true;
            }
            Log.Warn(CommonUtil.Tag, "Invalid W3C type hint: " + hint);
            return false;
        }
    }
}
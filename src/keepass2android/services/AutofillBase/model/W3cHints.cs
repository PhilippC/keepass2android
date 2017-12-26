namespace keepass2android.services.AutofillBase.model
{
    public class W3cHints
    {

        // Supported W3C autofill tokens (https://html.spec.whatwg.org/multipage/forms.html#autofill)
        public static string HONORIFIC_PREFIX = "honorific-prefix";
        public static string NAME = "name";
        public static string GIVEN_NAME = "given-name";
        public static string ADDITIONAL_NAME = "additional-name";
        public static string FAMILY_NAME = "family-name";
        public static string HONORIFIC_SUFFIX = "honorific-suffix";
        public static string USERNAME = "username";
        public static string NEW_PASSWORD = "new-password";
        public static string CURRENT_PASSWORD = "current-password";
        public static string ORGANIZATION_TITLE = "organization-title";
        public static string ORGANIZATION = "organization";
        public static string STREET_ADDRESS = "street-address";
        public static string ADDRESS_LINE1 = "address-line1";
        public static string ADDRESS_LINE2 = "address-line2";
        public static string ADDRESS_LINE3 = "address-line3";
        public static string ADDRESS_LEVEL4 = "address-level4";
        public static string ADDRESS_LEVEL3 = "address-level3";
        public static string ADDRESS_LEVEL2 = "address-level2";
        public static string ADDRESS_LEVEL1 = "address-level1";
        public static string COUNTRY = "country";
        public static string COUNTRY_NAME = "country-name";
        public static string POSTAL_CODE = "postal-code";
        public static string CC_NAME = "cc-name";
        public static string CC_GIVEN_NAME = "cc-given-name";
        public static string CC_ADDITIONAL_NAME = "cc-additional-name";
        public static string CC_FAMILY_NAME = "cc-family-name";
        public static string CC_NUMBER = "cc-number";
        public static string CC_EXPIRATION = "cc-exp";
        public static string CC_EXPIRATION_MONTH = "cc-exp-month";
        public static string CC_EXPIRATION_YEAR = "cc-exp-year";
        public static string CC_CSC = "cc-csc";
        public static string CC_TYPE = "cc-type";
        public static string TRANSACTION_CURRENCY = "transaction-currency";
        public static string TRANSACTION_AMOUNT = "transaction-amount";
        public static string LANGUAGE = "language";
        public static string BDAY = "bday";
        public static string BDAY_DAY = "bday-day";
        public static string BDAY_MONTH = "bday-month";
        public static string BDAY_YEAR = "bday-year";
        public static string SEX = "sex";
        public static string URL = "url";
        public static string PHOTO = "photo";
        // Optional W3C prefixes
        public static string PREFIX_SECTION = "section-";
        public static string SHIPPING = "shipping";
        public static string BILLING = "billing";
        // W3C prefixes below...
        public static string PREFIX_HOME = "home";
        public static string PREFIX_WORK = "work";
        public static string PREFIX_FAX = "fax";
        public static string PREFIX_PAGER = "pager";
        // ... require those suffix
        public static string TEL = "tel";
        public static string TEL_COUNTRY_CODE = "tel-country-code";
        public static string TEL_NATIONAL = "tel-national";
        public static string TEL_AREA_CODE = "tel-area-code";
        public static string TEL_LOCAL = "tel-local";
        public static string TEL_LOCAL_PREFIX = "tel-local-prefix";
        public static string TEL_LOCAL_SUFFIX = "tel-local-suffix";
        public static string TEL_EXTENSION = "tel_extension";
        public static string EMAIL = "email";
        public static string IMPP = "impp";

        private W3cHints()
        {
        }
    }
}
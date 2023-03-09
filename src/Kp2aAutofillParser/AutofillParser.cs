using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using Formatting = System.Xml.Formatting;

namespace Kp2aAutofillParser
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
            return false;
        }
    }
    /// <summary>
    /// FilledAutofillFieldCollection is the model that holds all of the data on a client app's page,
    /// plus the dataset name associated with it.
    /// </summary>
    public class FilledAutofillFieldCollection<FieldT> where FieldT:InputField
    {
        public Dictionary<string, FilledAutofillField<FieldT>> HintMap { get; }
        public string DatasetName { get; set; }

        public FilledAutofillFieldCollection(Dictionary<string, FilledAutofillField<FieldT>> hintMap, string datasetName = "")
        {
            //recreate hint map making sure we compare case insensitive
            HintMap = BuildHintMap();
            foreach (var p in hintMap)
                HintMap.Add(p.Key, p.Value);
            DatasetName = datasetName;
        }

        public FilledAutofillFieldCollection() : this(BuildHintMap())
        { }

        private static Dictionary<string, FilledAutofillField<FieldT>> BuildHintMap()
        {
            return new Dictionary<string, FilledAutofillField<FieldT>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Adds a filledAutofillField to the collection, indexed by all of its hints.
        /// </summary>
        /// <returns>The add.</returns>
        /// <param name="filledAutofillField">Filled autofill field.</param>
        public void Add(FilledAutofillField<FieldT> filledAutofillField)
        {
            foreach (string hint in filledAutofillField.AutofillHints)
            {
                if (AutofillHintsHelper.IsSupportedHint(hint))
                {
                    HintMap.TryAdd(hint, filledAutofillField);
                }
            }

        }


        

        /// <summary>
        /// Takes in a list of autofill hints (`autofillHints`), usually associated with a View or set of
        /// Views. Returns whether any of the filled fields on the page have at least 1 of these
        /// `autofillHint`s.
        /// </summary>
        /// <returns><c>true</c>, if with hints was helpsed, <c>false</c> otherwise.</returns>
        /// <param name="autofillHints">Autofill hints.</param>
        public bool HelpsWithHints(List<string> autofillHints)
        {
            for (int i = 0; i < autofillHints.Count; i++)
            {
                var autofillHint = autofillHints[i];
                if (HintMap.ContainsKey(autofillHint) && !HintMap[autofillHint].IsNull())
                {
                    return true;
                }
            }
            return false;
        }
    }
    public class AutofillHintsHelper
    {
        public const string AutofillHint2faAppOtp = "2faAppOTPCode";
        public const string AutofillHintBirthDateDay = "birthDateDay";
        public const string AutofillHintBirthDateFull = "birthDateFull";
        public const string AutofillHintBirthDateMonth = "birthDateMonth";
        public const string AutofillHintBirthDateYear = "birthDateYear";
        public const string AutofillHintCreditCardExpirationDate = "creditCardExpirationDate";
        public const string AutofillHintCreditCardExpirationDay = "creditCardExpirationDay";
        public const string AutofillHintCreditCardExpirationMonth = "creditCardExpirationMonth";
        public const string AutofillHintCreditCardExpirationYear = "creditCardExpirationYear";
        public const string AutofillHintCreditCardNumber = "creditCardNumber";
        public const string AutofillHintCreditCardSecurityCode = "creditCardSecurityCode";
        public const string AutofillHintEmailAddress = "emailAddress";
        public const string AutofillHintEmailOtp = "emailOTPCode";
        public const string AutofillHintGender = "gender";
        public const string AutofillHintName = "name";
        public const string AutofillHintNewPassword = "newPassword";
        public const string AutofillHintNewUsername = "newUsername";
        public const string AutofillHintNotApplicable = "notApplicable";
        public const string AutofillHintPassword = "password";
        public const string AutofillHintPersonName = "personName";
        public const string AutofillHintPersonNameFAMILY = "personFamilyName";
        public const string AutofillHintPersonNameGIVEN = "personGivenName";
        public const string AutofillHintPersonNameMIDDLE = "personMiddleName";
        public const string AutofillHintPersonNameMIDDLE_INITIAL = "personMiddleInitial";
        public const string AutofillHintPersonNamePREFIX = "personNamePrefix";
        public const string AutofillHintPersonNameSUFFIX = "personNameSuffix";
        public const string AutofillHintPhone = "phone";
        public const string AutofillHintPhoneContryCode = "phoneCountryCode";
        public const string AutofillHintPostalAddressAPT_NUMBER = "aptNumber";
        public const string AutofillHintPostalAddressCOUNTRY = "addressCountry";
        public const string AutofillHintPostalAddressDEPENDENT_LOCALITY = "dependentLocality";
        public const string AutofillHintPostalAddressEXTENDED_ADDRESS = "extendedAddress";
        public const string AutofillHintPostalAddressEXTENDED_POSTAL_CODE = "extendedPostalCode";
        public const string AutofillHintPostalAddressLOCALITY = "addressLocality";
        public const string AutofillHintPostalAddressREGION = "addressRegion";
        public const string AutofillHintPostalAddressSTREET_ADDRESS = "streetAddress";
        public const string AutofillHintPostalCode = "postalCode";
        public const string AutofillHintPromoCode = "promoCode";
        public const string AutofillHintSMS_OTP = "smsOTPCode";
        public const string AutofillHintUPI_VPA = "upiVirtualPaymentAddress";
        public const string AutofillHintUsername = "username";
        public const string AutofillHintWifiPassword = "wifiPassword";
        public const string AutofillHintPhoneNational = "phoneNational";
        public const string AutofillHintPhoneNumber = "phoneNumber";
        public const string AutofillHintPhoneNumberDevice = "phoneNumberDevice";
        public const string AutofillHintPostalAddress = "postalAddress";

        private static readonly HashSet<string> _allSupportedHints = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AutofillHintCreditCardExpirationDate,
            AutofillHintCreditCardExpirationDay,
            AutofillHintCreditCardExpirationMonth,
            AutofillHintCreditCardExpirationYear,
            AutofillHintCreditCardNumber,
            AutofillHintCreditCardSecurityCode,
            AutofillHintEmailAddress,
            AutofillHintPhone,
            AutofillHintName,
            AutofillHintPassword,
            AutofillHintPostalAddress,
            AutofillHintPostalCode,
            AutofillHintUsername,
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

        private static readonly List<HashSet<string>> partitionsOfCanonicalHints = new List<HashSet<string>>()
        {

            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
            AutofillHintEmailAddress,
            AutofillHintPhone,
            AutofillHintName,
            AutofillHintPassword,
            AutofillHintUsername,
            W3cHints.HONORIFIC_PREFIX,
            W3cHints.EMAIL,
            W3cHints.NAME,
            W3cHints.GIVEN_NAME,
            W3cHints.ADDITIONAL_NAME,
            W3cHints.FAMILY_NAME,
            W3cHints.HONORIFIC_SUFFIX,
            W3cHints.ORGANIZATION_TITLE,
            W3cHints.ORGANIZATION,
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
            W3cHints.IMPP,
            },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                AutofillHintPostalAddress,
                AutofillHintPostalCode,

                W3cHints.STREET_ADDRESS,
                W3cHints.ADDRESS_LINE1,
                W3cHints.ADDRESS_LINE2,
                W3cHints.ADDRESS_LINE3,
                W3cHints.ADDRESS_LEVEL4,
                W3cHints.ADDRESS_LEVEL3,
                W3cHints.ADDRESS_LEVEL2,
                W3cHints.ADDRESS_LEVEL1,
                W3cHints.COUNTRY,
                W3cHints.COUNTRY_NAME
            },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                AutofillHintCreditCardExpirationDate,
                AutofillHintCreditCardExpirationDay,
                AutofillHintCreditCardExpirationMonth,
                AutofillHintCreditCardExpirationYear,
                AutofillHintCreditCardNumber,
                AutofillHintCreditCardSecurityCode,

                W3cHints.CC_NAME,
                W3cHints.CC_GIVEN_NAME,
                W3cHints.CC_ADDITIONAL_NAME,
                W3cHints.CC_FAMILY_NAME,
                W3cHints.CC_TYPE,
                W3cHints.TRANSACTION_CURRENCY,
                W3cHints.TRANSACTION_AMOUNT,
            },

                      };

        private static readonly Dictionary<string, string> hintToCanonicalReplacement = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {W3cHints.EMAIL, AutofillHintEmailAddress},
            {W3cHints.USERNAME, AutofillHintUsername},
            {W3cHints.CURRENT_PASSWORD, AutofillHintPassword},
            {W3cHints.NEW_PASSWORD, AutofillHintPassword},
            {W3cHints.CC_EXPIRATION_MONTH, AutofillHintCreditCardExpirationMonth },
            {W3cHints.CC_EXPIRATION_YEAR, AutofillHintCreditCardExpirationYear },
            {W3cHints.CC_EXPIRATION, AutofillHintCreditCardExpirationDate },
            {W3cHints.CC_NUMBER, AutofillHintCreditCardNumber },
            {W3cHints.CC_CSC, AutofillHintCreditCardSecurityCode },
            {W3cHints.POSTAL_CODE, AutofillHintPostalCode },


        };

        public static bool IsSupportedHint(string hint)
        {
            return _allSupportedHints.Contains(hint);
        }


        public static string[] FilterForSupportedHints(string[] hints)
        {
            if (hints == null)
                return Array.Empty<string>();
            var filteredHints = new string[hints.Length];
            int i = 0;
            foreach (var hint in hints)
            {
                if (IsSupportedHint(hint))
                {
                    filteredHints[i++] = hint;
                }
                
            }
            var finalFilteredHints = new string[i];
            Array.Copy(filteredHints, 0, finalFilteredHints, 0, i);
            return finalFilteredHints;
        }



        /// <summary>
        /// transforms hints by replacing some W3cHints by their Android counterparts and transforming everything to lowercase
        /// </summary>
        public static List<string> ConvertToCanonicalLowerCaseHints(string[] supportedHints)
        {
            List<string> result = new List<string>();
            foreach (string hint in supportedHints)
            {
                var canonicalHint = ToCanonicalHint(hint);
                result.Add(canonicalHint.ToLower());
            }
            return result;

        }

        public static string ToCanonicalHint(string hint)
        {
            string canonicalHint;
            if (!hintToCanonicalReplacement.TryGetValue(hint, out canonicalHint))
                canonicalHint = hint;
            return canonicalHint;
        }

        public static int GetPartitionIndex(string hint)
        {
            for (int i = 0; i < partitionsOfCanonicalHints.Count; i++)
            {
                if (partitionsOfCanonicalHints[i].Contains(hint))
                {
                    return i;
                }
            }
            return -1;
        }

        public static FilledAutofillFieldCollection<FieldT> FilterForPartition<FieldT>(FilledAutofillFieldCollection<FieldT> autofillFields, int partitionIndex) where FieldT: InputField
        {
            FilledAutofillFieldCollection<FieldT> filteredCollection =
                new FilledAutofillFieldCollection<FieldT> { DatasetName = autofillFields.DatasetName };

            if (partitionIndex == -1)
                return filteredCollection;

            foreach (var field in autofillFields.HintMap.Values.Distinct())
            {
                foreach (var hint in field.AutofillHints)
                {
                    if (GetPartitionIndex(hint) == partitionIndex)
                    {
                        filteredCollection.Add(field);
                        break;
                    }
                }
            }

            return filteredCollection;
        }

        public static FilledAutofillFieldCollection<FieldT> FilterForPartition<FieldT>(FilledAutofillFieldCollection<FieldT> filledAutofillFieldCollection, List<string> autofillFieldsFocusedAutofillCanonicalHints) where FieldT: InputField
        {

            //only apply partition data if we have FocusedAutofillCanonicalHints. This may be empty on buggy Firefox.
            if (autofillFieldsFocusedAutofillCanonicalHints.Any())
            {
                int partitionIndex = AutofillHintsHelper.GetPartitionIndex(autofillFieldsFocusedAutofillCanonicalHints.FirstOrDefault());
                return AutofillHintsHelper.FilterForPartition(filledAutofillFieldCollection, partitionIndex);
            }

            return filledAutofillFieldCollection;
        }
    }
    /// <summary>
    /// This enum represents the Android.Text.InputTypes values. For testability, this is duplicated here.
    /// </summary>
    public enum InputTypes
    {
        ClassDatetime = 4,
        ClassNumber = 2,
        ClassPhone = 3,
        ClassText = 1,
        DatetimeVariationDate = 16,
        DatetimeVariationNormal = 0,
        DatetimeVariationTime = 32,
        MaskClass = 15,
        MaskFlags = 16773120,
        MaskVariation = 4080,
        Null = 0,
        NumberFlagDecimal = 8192,
        NumberFlagSigned = 4096,
        NumberVariationNormal = 0,
        NumberVariationPassword = 16,
        TextFlagAutoComplete = 65536,
        TextFlagAutoCorrect = 32768,
        TextFlagCapCharacters = 4096,
        TextFlagCapSentences = 16384,
        TextFlagCapWords = 8192,
        TextFlagEnableTextConversionSuggestions = 1048576,
        TextFlagImeMultiLine = 262144,
        TextFlagMultiLine = 131072,
        TextFlagNoSuggestions = 524288,
        TextVariationEmailAddress = 32,
        TextVariationEmailSubject = 48,
        TextVariationFilter = 176,
        TextVariationLongMessage = 80,
        TextVariationNormal = 0,
        TextVariationPassword = 128,
        TextVariationPersonName = 96,
        TextVariationPhonetic = 192,
        TextVariationPostalAddress = 112,
        TextVariationShortMessage = 64,
        TextVariationUri = 16,
        TextVariationVisiblePassword = 144,
        TextVariationWebEditText = 160,
        TextVariationWebEmailAddress = 208,
        TextVariationWebPassword = 224
    }

    public interface IKp2aDigitalAssetLinksDataSource
    {
        bool IsTrustedApp(string packageName);
        bool IsTrustedLink(string domain, string targetPackage);
        bool IsEnabled();

    }

    class TimeUtil
    {
        private static DateTime? m_dtUnixRoot = null;
        public static DateTime ConvertUnixTime(double dtUnix)
        {
            try
            {
                if (!m_dtUnixRoot.HasValue)
                    m_dtUnixRoot = (new DateTime(1970, 1, 1, 0, 0, 0, 0,
                        DateTimeKind.Utc)).ToLocalTime();

                return m_dtUnixRoot.Value.AddSeconds(dtUnix);
            }
            catch (Exception) { Debug.Assert(false); }

            return DateTime.UtcNow;
        }
    }

    public class FilledAutofillField<FieldT> where FieldT : InputField
    {
        private string[] _autofillHints;
        public string TextValue { get; set; }
        public long? DateValue { get; set; }
        public bool? ToggleValue { get; set; }

        public string ValueToString()
        {
            if (DateValue != null)
            {
                return TimeUtil.ConvertUnixTime((long)DateValue / 1000.0).ToLongDateString();
            }
            if (ToggleValue != null)
                return ToggleValue.ToString();
            return TextValue;
        }

        /// <summary>
        /// returns the autofill hints for the filled field. These are always lowercased for simpler string comparison.
        /// </summary>
	    public string[] AutofillHints
        {
            get
            {
                return _autofillHints;
            }
            set
            {
                _autofillHints = value;
                for (int i = 0; i < _autofillHints.Length; i++)
                    _autofillHints[i] = _autofillHints[i].ToLower();
            }
        }


        public FilledAutofillField()
        { }

        public FilledAutofillField(FieldT inputField)
            : this(inputField, inputField.AutofillHints)
        {

        }

        public FilledAutofillField(FieldT inputField, string[] hints)
        {

            string[] rawHints = AutofillHintsHelper.FilterForSupportedHints(hints);
            List<string> hintList = new List<string>();

            string nextHint = null;
            for (int i = 0; i < rawHints.Length; i++)
            {
                string hint = rawHints[i];
                if (i < rawHints.Length - 1)
                {
                    nextHint = rawHints[i + 1];
                }
                // First convert the compound W3C autofill hints
                if (W3cHints.isW3cSectionPrefix(hint) && i < rawHints.Length - 1)
                {
                    hint = rawHints[++i];
                    
                    if (i < rawHints.Length - 1)
                    {
                        nextHint = rawHints[i + 1];
                    }
                }
                if (W3cHints.isW3cTypePrefix(hint) && nextHint != null && W3cHints.isW3cTypeHint(nextHint))
                {
                    hint = nextHint;
                    i++;
                    
                }
                if (W3cHints.isW3cAddressType(hint) && nextHint != null)
                {
                    hint = nextHint;
                    i++;
                    
                }

                // Then check if the "actual" hint is supported.
                if (AutofillHintsHelper.IsSupportedHint(hint))
                {
                    hintList.Add(hint);
                }
                else
                {
                    
                }
            }
            AutofillHints = AutofillHintsHelper.ConvertToCanonicalLowerCaseHints(hintList.ToArray()).ToArray();


        }

        public bool IsNull()
        {
            return TextValue == null && DateValue == null && ToggleValue == null;
        }

        public override bool Equals(object obj)
        {
            if (this == obj) return true;
            if (obj == null || GetType() != obj.GetType()) return false;

            FilledAutofillField<FieldT> that = (FilledAutofillField<FieldT>)obj;

            if (!TextValue?.Equals(that.TextValue) ?? that.TextValue != null)
                return false;
            if (DateValue != null ? !DateValue.Equals(that.DateValue) : that.DateValue != null)
                return false;
            return ToggleValue != null ? ToggleValue.Equals(that.ToggleValue) : that.ToggleValue == null;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var result = TextValue != null ? TextValue.GetHashCode() : 0;
                result = 31 * result + (DateValue != null ? DateValue.GetHashCode() : 0);
                result = 31 * result + (ToggleValue != null ? ToggleValue.GetHashCode() : 0);
                return result;
            }
        }
    }

    /// <summary>
    /// Base class for everything that is (or could be) an input field which might (or might not) be autofilled.
    /// For testability, this is independent from Android classes like ViewNode
    /// </summary>
    public abstract class InputField
    {
        public string? IdEntry { get; set; }
        public string? Hint { get; set; }
        public string ClassName { get; set; }
        public string[] AutofillHints { get; set; }
        public bool IsFocused { get; set; }

        public InputTypes InputType { get; set; }

        public string HtmlInfoTag { get; set; }
        public string HtmlInfoTypeAttribute { get; set; }

    }

    /// <summary>
    /// Serializable structure defining the contents of the current view (from an autofill perspective)
    /// </summary>
    /// <typeparam name="TField"></typeparam>
    public class AutofillView<TField> where TField : InputField
    {
        public List<TField> InputFields { get; set; } = new List<TField>();

        public string PackageId { get; set; } = null;
        public string WebDomain { get; set; } = null;
    }

    public interface ILogger
    {
        void Log(string x);
    }

    public class StructureParserBase<FieldT> where FieldT: InputField
    {
        private readonly ILogger _log;
        private readonly IKp2aDigitalAssetLinksDataSource _digitalAssetLinksDataSource;

        private readonly List<string> _autofillHintsForLogin = new List<string>
            {
                AutofillHintsHelper.AutofillHintPassword,
                AutofillHintsHelper.AutofillHintUsername,
                AutofillHintsHelper.AutofillHintEmailAddress
            };

        public string PackageId { get; set; }

        public Dictionary<FieldT, string[]> FieldsMappedToHints = new Dictionary<FieldT, string[]>();

        public StructureParserBase(ILogger logger, IKp2aDigitalAssetLinksDataSource digitalAssetLinksDataSource)
        {
            _log = logger;
            _digitalAssetLinksDataSource = digitalAssetLinksDataSource;
        }

        public class AutofillTargetId
        {
            public string PackageName { get; set; }

            public string PackageNameWithPseudoSchema
            {
                get { return AndroidAppScheme + PackageName; }
            }

            public const string AndroidAppScheme = "androidapp://";

            public string WebDomain { get; set; }

            /// <summary>
            /// If PackageName and WebDomain are not compatible (by DAL or because PackageName is a trusted browser in which case we treat all domains as "compatible"
            /// we need to issue a warning. If we would fill credentials for the package, a malicious website could try to get credentials for the app.
            /// If we would fill credentials for the domain, a malicious app could get credentials for the domain.
            /// </summary>
            public bool IncompatiblePackageAndDomain { get; set; }

            public string DomainOrPackage
            {
                get
                {
                    return WebDomain ?? PackageNameWithPseudoSchema;
                }
            }
        }

        public AutofillTargetId ParseForFill(bool isManual, AutofillView<FieldT> autofillView)
        {
            return Parse(true, isManual, autofillView);
        }

        public AutofillTargetId ParseForSave(AutofillView<FieldT> autofillView)
        {
            return Parse(false, true, autofillView);
        }

        /// <summary>
        /// Traverse AssistStructure and add ViewNode metadata to a flat list.
        /// </summary>
        /// <returns>The parse.</returns>
        /// <param name="forFill">If set to <c>true</c> for fill.</param>
        /// <param name="isManualRequest"></param>
        protected virtual AutofillTargetId Parse(bool forFill, bool isManualRequest, AutofillView<FieldT> autofillView)
        {
            AutofillTargetId result = new AutofillTargetId()
            {
                PackageName = autofillView.PackageId,
                WebDomain = autofillView.WebDomain
            };
            
            _log.Log("parsing autofillStructure...");

            if (LogAutofillView)
            {
                string debugInfo = JsonConvert.SerializeObject(autofillView, Newtonsoft.Json.Formatting.Indented);
                _log.Log("This is the autofillStructure: \n\n " + debugInfo);
            }


            //go through each input field and determine username/password fields.
            //Depending on the target this can require more or less heuristics.
            // * if there is a valid & supported autofill hint, we assume that all fields which should be filled do have an appropriate Autofill hint
            // * if there is no such autofill hint, we use IsPassword to 

            HashSet<string> autofillHintsOfAllFields = autofillView.InputFields.Where(f => f.AutofillHints != null)
                .SelectMany(f => f.AutofillHints).Select(AutofillHintsHelper.ToCanonicalHint).ToHashSet();
            bool hasLoginAutofillHints = autofillHintsOfAllFields.Intersect(_autofillHintsForLogin).Any();

            if (hasLoginAutofillHints)
            {
                foreach (var viewNode in autofillView.InputFields)
                {
                    string[] viewHints = viewNode.AutofillHints;
                    if (viewHints == null)
                        continue;
                    if (viewHints.Select(AutofillHintsHelper.ToCanonicalHint).Intersect(_autofillHintsForLogin).Any())
                    {
                        AddFieldToHintMap(viewNode, viewHints.Select(AutofillHintsHelper.ToCanonicalHint).ToHashSet().ToArray());
                    }

                }
            }
            else
            {
                //determine password fields, first by type, then by hint:
                List<FieldT> editTexts = autofillView.InputFields.Where(f => IsEditText(f)).ToList();
                List<FieldT> passwordFields = autofillView.InputFields.Where(f => IsEditText(f) && IsPassword(f)).ToList();
                if (!passwordFields.Any())
                {
                    passwordFields = autofillView.InputFields.Where(f => IsEditText(f) && HasPasswordHint(f)).ToList();
                }

                //determine username fields. Try by hint, if that fails use the one before the password
                List<FieldT> usernameFields = autofillView.InputFields.Where(f => IsEditText(f) && HasUsernameHint(f)).ToList();
                if (!usernameFields.Any())
                {
                    foreach (var passwordField in passwordFields)
                    {
                        
                        var lastInputBeforePassword = autofillView.InputFields.Where(IsEditText)
                            .TakeWhile(f =>  f != passwordField && !passwordFields.Contains(f)).LastOrDefault();
                        
                        if (lastInputBeforePassword != null)
                            usernameFields.Add(lastInputBeforePassword);
                    }
                    
                }

                //for "heuristic determination" we demand that one of the filled fields is focused:
                if (passwordFields.Concat(usernameFields).Any(f => f.IsFocused))
                {
                    foreach (var uf in usernameFields)
                        AddFieldToHintMap(uf, new string[] { AutofillHintsHelper.AutofillHintUsername });
                    foreach (var pf in passwordFields.Except(usernameFields))
                        AddFieldToHintMap(pf, new string[] { AutofillHintsHelper.AutofillHintPassword });
                }
            }
            

            if (!string.IsNullOrEmpty(autofillView.WebDomain) && _digitalAssetLinksDataSource.IsEnabled())
            {
                result.IncompatiblePackageAndDomain = !_digitalAssetLinksDataSource.IsTrustedLink(autofillView.WebDomain, result.PackageName);
                if (result.IncompatiblePackageAndDomain)
                {
                    _log.Log($"DAL verification failed for {result.PackageName}/{result.WebDomain}");
                }
            }
            else
            {
                result.IncompatiblePackageAndDomain = false;
            }
            return result;
        }

        private void AddFieldToHintMap(FieldT field, string[] hints)
        {
            if (FieldsMappedToHints.ContainsKey(field))
            {
                FieldsMappedToHints[field] = FieldsMappedToHints[field].Concat(hints).ToArray();
            }
            else
            {
                FieldsMappedToHints[field] = hints;
            }
        }

        public bool LogAutofillView { get; set; }

        private bool IsEditText(FieldT f)
        {
            return (f.ClassName == "android.widget.EditText"
                    || f.ClassName == "android.widget.AutoCompleteTextView"
                    || f.HtmlInfoTag == "input");
        }

        private static readonly HashSet<string> _passwordHints = new HashSet<string> { "password", "passwort", "passwordAuto", "pswd" };
        private static bool HasPasswordHint(InputField f)
        {
            return IsAny(f.IdEntry, _passwordHints) ||
                   IsAny(f.Hint, _passwordHints);
        }

        private static readonly HashSet<string> _usernameHints = new HashSet<string> { "email", "e-mail", "username", "user id" };

        private static bool HasUsernameHint(InputField f)
        {
            return IsAny(f.IdEntry?.ToLower(), _usernameHints) ||
                IsAny(f.Hint?.ToLower(), _usernameHints);
        }

        private static bool IsAny(string? value, IEnumerable<string> terms)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            var lowerValue = value.ToLowerInvariant();
            return terms.Any(t => lowerValue == t);
        }

        private static bool IsInputTypeClass(InputTypes inputType, InputTypes inputTypeClass)
        {
            if (!InputTypes.MaskClass.HasFlag(inputTypeClass))
                throw new Exception("invalid inputTypeClass");
            return (((int)inputType) & (int)InputTypes.MaskClass) == (int)(inputTypeClass);
        }
        private static bool IsInputTypeVariation(InputTypes inputType, InputTypes inputTypeVariation)
        {
            if (!InputTypes.MaskVariation.HasFlag(inputTypeVariation))
                throw new Exception("invalid inputTypeVariation");
            return (((int)inputType) & (int)InputTypes.MaskVariation) == (int)(inputTypeVariation);
        }

        private static bool IsPassword(InputField f)
        {
            InputTypes inputType = f.InputType;

            return
                (!f.IdEntry?.ToLowerInvariant().Contains("search") ?? true) &&
                (!f.Hint?.ToLowerInvariant().Contains("search") ?? true) &&
                (
                   (IsInputTypeClass(inputType, InputTypes.ClassText)
                        &&
                        (
                      IsInputTypeVariation(inputType, InputTypes.TextVariationPassword)
                      || IsInputTypeVariation(inputType, InputTypes.TextVariationVisiblePassword)
                      || IsInputTypeVariation(inputType, InputTypes.TextVariationWebPassword)
                      )
                      )
                    || (f.AutofillHints != null && f.AutofillHints.First() == "passwordAuto")
                    || (f.HtmlInfoTypeAttribute == "password")
                );
        }

        
        



    }
}

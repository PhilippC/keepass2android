using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

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
    class AutofillHintsHelper
    {
        const string AutofillHint2faAppOtp = "2faAppOTPCode";
        const string AutofillHintBirthDateDay = "birthDateDay";
        const string AutofillHintBirthDateFull = "birthDateFull";
        const string AutofillHintBirthDateMonth = "birthDateMonth";
        const string AutofillHintBirthDateYear = "birthDateYear";
        const string AutofillHintCreditCardExpirationDate = "creditCardExpirationDate";
        const string AutofillHintCreditCardExpirationDay = "creditCardExpirationDay";
        const string AutofillHintCreditCardExpirationMonth = "creditCardExpirationMonth";
        const string AutofillHintCreditCardExpirationYear = "creditCardExpirationYear";
        const string AutofillHintCreditCardNumber = "creditCardNumber";
        const string AutofillHintCreditCardSecurityCode = "creditCardSecurityCode";
        const string AutofillHintEmailAddress = "emailAddress";
        const string AutofillHintEmailOtp = "emailOTPCode";
        const string AutofillHintGender = "gender";
        const string AutofillHintName = "name";
        const string AutofillHintNewPassword = "newPassword";
        const string AutofillHintNewUsername = "newUsername";
        const string AutofillHintNotApplicable = "notApplicable";
        const string AutofillHintPassword = "password";
        const string AutofillHintPersonName = "personName";
        const string AutofillHintPersonNameFAMILY = "personFamilyName";
        const string AutofillHintPersonNameGIVEN = "personGivenName";
        const string AutofillHintPersonNameMIDDLE = "personMiddleName";
        const string AutofillHintPersonNameMIDDLE_INITIAL = "personMiddleInitial";
        const string AutofillHintPersonNamePREFIX = "personNamePrefix";
        const string AutofillHintPersonNameSUFFIX = "personNameSuffix";
        const string AutofillHintPhone = "phone";
        const string AutofillHintPhoneContryCode = "phoneCountryCode";
        const string AutofillHintPostalAddressAPT_NUMBER = "aptNumber";
        const string AutofillHintPostalAddressCOUNTRY = "addressCountry";
        const string AutofillHintPostalAddressDEPENDENT_LOCALITY = "dependentLocality";
        const string AutofillHintPostalAddressEXTENDED_ADDRESS = "extendedAddress";
        const string AutofillHintPostalAddressEXTENDED_POSTAL_CODE = "extendedPostalCode";
        const string AutofillHintPostalAddressLOCALITY = "addressLocality";
        const string AutofillHintPostalAddressREGION = "addressRegion";
        const string AutofillHintPostalAddressSTREET_ADDRESS = "streetAddress";
        const string AutofillHintPostalCode = "postalCode";
        const string AutofillHintPromoCode = "promoCode";
        const string AutofillHintSMS_OTP = "smsOTPCode";
        const string AutofillHintUPI_VPA = "upiVirtualPaymentAddress";
        const string AutofillHintUsername = "username";
        const string AutofillHintWifiPassword = "wifiPassword";
        const string AutofillHintPhoneNational = "phoneNational";
        const string AutofillHintPhoneNumber = "phoneNumber";
        const string AutofillHintPhoneNumberDevice = "phoneNumberDevice";
        const string AutofillHintPostalAddress = "postalAddress";

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
            AutofillHints = AutofillHintsHelper.ConvertToCanonicalHints(hintList.ToArray()).ToArray();


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
    /// Base class for everything that is a input field which might (or might not) be autofilled.
    /// For testability, this is independent from Android classes like ViewNode
    /// </summary>
    public abstract class InputField
    {
        public string IdEntry { get; set; }
        public string Hint { get; set; }
        public string ClassName { get; set; }
        public string[] AutofillHints { get; set; }
        public bool IsFocused { get; set; }

        public InputTypes InputType { get; set; }

        public string HtmlInfoTag { get; set; }
        public string HtmlInfoTypeAttribute { get; set; }

        public abstract void FillFilledAutofillValue(FilledAutofillField<FieldT> filledField);


    }

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

        public string PackageId { get; set; }

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
        AutofillTargetId Parse(bool forFill, bool isManualRequest, AutofillView<FieldT> autofillView)
        {
            AutofillTargetId result = new AutofillTargetId();
            
            
            _editTextsWithoutHint.Clear();

            _log.Log("parsing autofillStructure...");
            
            //TODO remove from production
            _log.Log("will log the autofillStructure...");
            string debugInfo = JsonConvert.SerializeObject(autofillView, Formatting.Indented);
            _log.Log("will log the autofillStructure... size is " + debugInfo.Length);
            _log.Log("This is the autofillStructure: \n\n " + debugInfo);

            foreach (var viewNode in autofillView.InputFields)
            {
                string[] viewHints = viewNode.AutofillHints;
                if (viewHints != null && viewHints.Length == 1 && viewHints.First() == "off" && viewNode.IsFocused &&
                    isManualRequest)
                    viewHints[0] = "on";
                /*if (viewHints != null && viewHints.Any())
                {
                    CommonUtil.logd("viewHints=" + viewHints);
                    CommonUtil.logd("class=" + viewNode.ClassName);
                    CommonUtil.logd("tag=" + (viewNode?.HtmlInfo?.Tag ?? "(null)"));
                }*/

                if (IsPassword(viewNode) || HasPasswordHint(viewNode) || (HasUsernameHint(viewNode)))
                {
                    if (forFill)
                    {
                        AutofillFields.Add(new AutofillFieldMetadata(viewNode.ViewNode));
                    }
                    else
                    {
                        FilledAutofillField filledAutofillField = new FilledAutofillField(viewNode.ViewNode);
                        ClientFormData.Add(filledAutofillField);
                    }
                }
                else if (viewNode.ClassName == "android.widget.EditText"
                    || viewNode.ClassName == "android.widget.AutoCompleteTextView"
                    || viewNode.HtmlInfoTag == "input"
                    || ((viewHints?.Length ?? 0) > 0))
                {
                    _log.Log("Found something that looks fillable " + viewNode.ClassName);

                }

                if (viewHints != null && viewHints.Length > 0 && viewHints.First() != "on" /*if hint is "on", treat as if there is no hint*/)
                {
                }
                else
                {

                    if (viewNode.ClassName == "android.widget.EditText"
                        || viewNode.ClassName == "android.widget.AutoCompleteTextView"
                        || viewNode.HtmlInfoTag == "input")
                    {
                        _editTextsWithoutHint.Add(viewNode);
                    }

                }
            }

            List<ViewNodeInputField> passwordFields = new List<ViewNodeInputField>();
            List<ViewNodeInputField> usernameFields = new List<ViewNodeInputField>();
            if (AutofillFields.Empty)
            {
                passwordFields = _editTextsWithoutHint.Where(IsPassword).ToList();
                if (!passwordFields.Any())
                {
                    passwordFields = _editTextsWithoutHint.Where(HasPasswordHint).ToList();
                }

                usernameFields = _editTextsWithoutHint.Where(HasUsernameHint).ToList();

                if (usernameFields.Any() == false)
                {

                    foreach (var passwordField in passwordFields)
                    {
                        var usernameField = _editTextsWithoutHint
                            .TakeWhile(f => f != passwordField).LastOrDefault();
                        if (usernameField != null)
                        {
                            usernameFields.Add(usernameField);
                        }
                    }
                }
                if (usernameFields.Any() == false)
                {
                    //for some pages with two-step login, we don't see a password field and don't display the autofill for non-manual requests. But if the user forces autofill, 
                    //let's assume it is a username field:
                    if (isManualRequest && !passwordFields.Any() && _editTextsWithoutHint.Count == 1)
                    {
                        usernameFields.Add(_editTextsWithoutHint.First());
                    }
                }


            }

            //force focused fields to be included in autofill fields when request was triggered manually. This allows to fill fields which are "off" or don't have a hint (in case there are hints)
            if (isManualRequest)
            {
                foreach (var editText in _editTextsWithoutHint)
                {
                    if (editText.IsFocused)
                    {
                        if (IsPassword(editText) || HasPasswordHint(editText))
                            passwordFields.Add(editText);
                        else
                            usernameFields.Add(editText);
                        break;
                    }

                }
            }

            if (forFill)
            {
                foreach (var uf in usernameFields)
                    AutofillFields.Add(new AutofillFieldMetadata(uf.ViewNode, new[] { View.AutofillHintUsername }));
                foreach (var pf in passwordFields)
                    AutofillFields.Add(new AutofillFieldMetadata(pf.ViewNode, new[] { View.AutofillHintPassword }));

            }
            else
            {
                foreach (var uf in usernameFields)
                    ClientFormData.Add(new FilledAutofillField(uf.ViewNode, new[] { View.AutofillHintUsername }));
                foreach (var pf in passwordFields)
                    ClientFormData.Add(new FilledAutofillField(pf.ViewNode, new[] { View.AutofillHintPassword }));
            }


            result.WebDomain = autofillView.WebDomain;
            result.PackageName = Structure.ActivityComponent.PackageName;
            if (!string.IsNullOrEmpty(autofillView.WebDomain) && !PreferenceManager.GetDefaultSharedPreferences(mContext).GetBoolean(mContext.GetString(Resource.String.NoDalVerification_key), false))
            {
                result.IncompatiblePackageAndDomain = !kp2aDigitalAssetLinksDataSource.IsTrustedLink(autofillView.WebDomain, result.PackageName);
                if (result.IncompatiblePackageAndDomain)
                {
                    CommonUtil.loge($"DAL verification failed for {result.PackageName}/{result.WebDomain}");
                }
            }
            else
            {
                result.IncompatiblePackageAndDomain = false;
            }
            return result;
        }
        private static readonly HashSet<string> _passwordHints = new HashSet<string> { "password", "passwort", "passwordAuto", "pswd" };
        private static bool HasPasswordHint(InputField f)
        {
            return ContainsAny(f.IdEntry, _passwordHints) ||
                   ContainsAny(f.Hint, _passwordHints);
        }

        private static readonly HashSet<string> _usernameHints = new HashSet<string> { "email", "e-mail", "username" };

        private static bool HasUsernameHint(InputField f)
        {
            return ContainsAny(f.IdEntry, _usernameHints) ||
                ContainsAny(f.Hint, _usernameHints);
        }

        private static bool ContainsAny(string value, IEnumerable<string> terms)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            var lowerValue = value.ToLowerInvariant();
            return terms.Any(t => lowerValue.Contains(t));
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
                    || (f.HtmlInfoTypeAttribute == "password")
                );
        }

        AssistStructure Structure;
        private List<ViewNodeInputField> _editTextsWithoutHint = new List<ViewNodeInputField>();



    }
}

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
using System.Linq;
using Android.App.Assist;
using Android.Service.Autofill;
using Android.Views;
using Android.Views.Autofill;
using Kp2aAutofillParser;

namespace keepass2android.services.AutofillBase
{
    /// <summary>
    /// A stripped down version of a {@link ViewNode} that contains only autofill-relevant metadata. It
    /// also contains a {@code mSaveType} flag that is calculated based on the {@link ViewNode}]'s
    /// autofill hints.
    /// </summary>
    public class AutofillFieldMetadata
    {
        public SaveDataType SaveType { get; set; }

        public string[] AutofillCanonicalHints { get; set; }

        public AutofillId AutofillId { get; }
        public AutofillType AutofillType { get; }
        string[] AutofillOptions { get; }
        public bool Focused { get; }

        public AutofillFieldMetadata(AssistStructure.ViewNode view)
            : this(view, view.GetAutofillHints())
        {

        }


        public AutofillFieldMetadata(AssistStructure.ViewNode view, string[] autofillHints)
        {
            AutofillId = view.AutofillId;
            AutofillType = view.AutofillType;
            AutofillOptions = view.GetAutofillOptions();
            Focused = view.IsFocused;
            var supportedHints = AutofillHintsHelper.FilterForSupportedHints(autofillHints);
            var canonicalHints = AutofillHintsHelper.ConvertToCanonicalLowerCaseHints(supportedHints);
            SetHints(canonicalHints.ToArray());
        }

        void SetHints(string[] value)
        {
            AutofillCanonicalHints = value;
            UpdateSaveTypeFromHints();
        }

        /// <summary>
        /// When the ViewNode is a list that the user needs to choose a string from (i.e. a
        /// spinner), this is called to return the index of a specific item in the list.
        /// </summary>
        /// <returns>The autofill option index.</returns>
        /// <param name="value">Value.</param>
        public int GetAutofillOptionIndex(String value)
        {
            for (int i = 0; i < AutofillOptions.Length; i++)
            {
                if (AutofillOptions[i].Equals(value))
                {
                    return i;
                }
            }
            return -1;
        }

        static readonly HashSet<string> _creditCardHints = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            View.AutofillHintCreditCardExpirationDate,
            View.AutofillHintCreditCardExpirationDay,
            View.AutofillHintCreditCardExpirationMonth,
            View.AutofillHintCreditCardExpirationYear,
            View.AutofillHintCreditCardNumber,
            View.AutofillHintCreditCardSecurityCode
        };

        static readonly HashSet<string> _addressHints = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            View.AutofillHintPostalAddress,
            View.AutofillHintPostalCode
        };

        void UpdateSaveTypeFromHints()
        {
            SaveType = 0;
            if (AutofillCanonicalHints == null)
            {
                return;
            }
            if (AutofillCanonicalHints.Any(h => _creditCardHints.Contains(h)))
            {
                SaveType |= SaveDataType.CreditCard;
            }
            if (AutofillCanonicalHints.Any(h => h.Equals(View.AutofillHintEmailAddress, StringComparison.OrdinalIgnoreCase)))
                SaveType |= SaveDataType.EmailAddress;
            if (AutofillCanonicalHints.Any(h => _addressHints.Contains(h)))
            {
                SaveType |= SaveDataType.Address;
            }
            if (AutofillCanonicalHints.Any(h => h.Equals(View.AutofillHintUsername, StringComparison.OrdinalIgnoreCase)))
                SaveType |= SaveDataType.Username;

            if (AutofillCanonicalHints.Any(h => h.Equals(View.AutofillHintPassword, StringComparison.OrdinalIgnoreCase)))
            {
                SaveType |= SaveDataType.Password;
                SaveType &= ~SaveDataType.EmailAddress;
                SaveType &= ~SaveDataType.Username;
            }
        }
    }
}

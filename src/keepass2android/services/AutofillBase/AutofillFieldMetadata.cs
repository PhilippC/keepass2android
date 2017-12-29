using System;
using Android.App.Assist;
using Android.Service.Autofill;
using Android.Views;
using Android.Views.Autofill;

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
		{
			AutofillId = view.AutofillId;
			AutofillType = view.AutofillType;
			AutofillOptions = view.GetAutofillOptions();
			Focused = view.IsFocused;
		    var supportedHints = AutofillHintsHelper.FilterForSupportedHints(view.GetAutofillHints());
		    var canonicalHints = AutofillHintsHelper.ConvertToCanonicalHints(supportedHints);
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

		void UpdateSaveTypeFromHints()
		{
            //TODO future add savetypes for W3cHints
			SaveType = 0;
			if (AutofillCanonicalHints == null)
			{
				return;
			}
			foreach (var hint in AutofillCanonicalHints)
			{
				switch (hint)
				{
					case View.AutofillHintCreditCardExpirationDate:
					case View.AutofillHintCreditCardExpirationDay:
					case View.AutofillHintCreditCardExpirationMonth:
					case View.AutofillHintCreditCardExpirationYear:
					case View.AutofillHintCreditCardNumber:
					case View.AutofillHintCreditCardSecurityCode:
						SaveType |= SaveDataType.CreditCard;
						break;
					case View.AutofillHintEmailAddress:
						SaveType |= SaveDataType.EmailAddress;
						break;
					case View.AutofillHintPhone:
					case View.AutofillHintName:
						SaveType |= SaveDataType.Generic;
						break;
					case View.AutofillHintPassword:
						SaveType |= SaveDataType.Password;
						SaveType &= ~SaveDataType.EmailAddress;
						SaveType &= ~SaveDataType.Username;
						break;
					case View.AutofillHintPostalAddress:
					case View.AutofillHintPostalCode:
						SaveType |= SaveDataType.Address;
						break;
					case View.AutofillHintUsername:
						SaveType |= SaveDataType.Username;
						break;
				}
			}
		}
	}
}

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

		public string[] AutofillHints { get; set; }

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
            //TODO port and use AutoFillHints
			SetHints(AutofillHelper.FilterForSupportedHints(view.GetAutofillHints()));
		}

		void SetHints(string[] value)
		{
			AutofillHints = value;
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
			SaveType = 0;
			if (AutofillHints == null)
			{
				return;
			}
			foreach (var hint in AutofillHints)
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

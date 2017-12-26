using Android.App.Assist;
using Android.Views.Autofill;

namespace keepass2android.services.AutofillBase.model
{
	/// <summary>
	/// JSON serializable data class containing the same data as an {@link AutofillValue}.
	/// </summary>
	public class FilledAutofillField
	{
		public string TextValue { get; set; }
		public long? DateValue { get; set; }
		public bool? ToggleValue { get; set; }

		/// <summary>
		/// Does not need to be serialized into persistent storage, so it's not exposed.
		/// </summary>
		/// <value>The autofill hints.</value>
		public string[] AutofillHints { get; set; }

		public FilledAutofillField()
		{}

		public FilledAutofillField(AssistStructure.ViewNode viewNode)
		{
			AutofillHints = AutofillHelper.FilterForSupportedHints(viewNode.GetAutofillHints());

            //TODO port updated FilledAutofillField?
			AutofillValue autofillValue = viewNode.AutofillValue;
			if (autofillValue != null)
			{
				if (autofillValue.IsList)
				{
					string[] autofillOptions = viewNode.GetAutofillOptions();
					int index = autofillValue.ListValue;
					if (autofillOptions != null && autofillOptions.Length > 0)
					{
						TextValue = autofillOptions[index];
					}
				}
				else if (autofillValue.IsDate)
				{
					DateValue = autofillValue.DateValue;
				}
				else if (autofillValue.IsText)
				{
					// Using toString of AutofillValue.getTextValue in order to save it to
					// SharedPreferences.
					TextValue = autofillValue.TextValue;
				}
			}
		}

		public bool IsNull()
		{
			return TextValue == null && DateValue == null && ToggleValue == null;
		}

		public override bool Equals(object obj)
		{
			if (this == obj) return true;
			if (obj == null || GetType() != obj.GetType()) return false;

			FilledAutofillField that = (FilledAutofillField)obj;

			if (TextValue != null ? !TextValue.Equals(that.TextValue) : that.TextValue != null)
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
}

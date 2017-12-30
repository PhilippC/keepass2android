using System.Collections.Generic;
using Android.App.Assist;
using Android.Views.Autofill;

namespace keepass2android.services.AutofillBase.model
{
	public class FilledAutofillField
	{
	    private string[] _autofillHints;
	    public string TextValue { get; set; }
		public long? DateValue { get; set; }
		public bool? ToggleValue { get; set; }

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

	    public bool Protected { get; set; }


	    public FilledAutofillField()
		{}
        
        public FilledAutofillField(AssistStructure.ViewNode viewNode)
        {
            
			string[] rawHints = AutofillHintsHelper.FilterForSupportedHints(viewNode.GetAutofillHints());
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
		            CommonUtil.logd($"Hint is a W3C section prefix; using {hint} instead");
		            if (i < rawHints.Length - 1)
		            {
		                nextHint = rawHints[i + 1];
		            }
		        }
		        if (W3cHints.isW3cTypePrefix(hint) && nextHint != null && W3cHints.isW3cTypeHint(nextHint))
		        {
		            hint = nextHint;
		            i++;
		            CommonUtil.logd($"Hint is a W3C type prefix; using {hint} instead");
		        }
		        if (W3cHints.isW3cAddressType(hint) && nextHint != null)
		        {
		            hint = nextHint;
		            i++;
		            CommonUtil.logd($"Hint is a W3C address prefix; using  {hint} instead");
		        }

		        // Then check if the "actual" hint is supported.
		        if (AutofillHintsHelper.IsSupportedHint(hint))
		        {
		            hintList.Add(hint);
		        }
		        else
		        {
		            CommonUtil.loge($"Invalid hint: {rawHints[i]}");
		        }
		    }
            AutofillHints = hintList.ToArray();

            //TODO port updated FilledAutofillField for saving
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
}

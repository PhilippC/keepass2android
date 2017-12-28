using System.Collections.Generic;
using Android.Service.Autofill;
using Android.Util;
using Android.Views;
using Android.Views.Autofill;

namespace keepass2android.services.AutofillBase.model
{
    /// <summary>
    /// FilledAutofillFieldCollection is the model that holds all of the data on a client app's page,
    /// plus the dataset name associated with it.
    /// </summary>
    public class FilledAutofillFieldCollection
	{
		public Dictionary<string, FilledAutofillField> HintMap { get; set; }
		public string DatasetName { get; set; }

		public FilledAutofillFieldCollection(Dictionary<string, FilledAutofillField> hintMap, string datasetName = "")
		{
			HintMap = hintMap;
			DatasetName = datasetName;
		}

		public FilledAutofillFieldCollection() : this(new Dictionary<string, FilledAutofillField>()) 
		{}

		/// <summary>
		/// Adds a filledAutofillField to the collection, indexed by all of its hints.
		/// </summary>
		/// <returns>The add.</returns>
		/// <param name="filledAutofillField">Filled autofill field.</param>
		public void Add(FilledAutofillField filledAutofillField)
		{
			string[] autofillHints = filledAutofillField.AutofillHints;
            
		    string nextHint = null;
		    for (int i = 0; i < autofillHints.Length; i++)
		    {
		        string hint = autofillHints[i];
		        if (i < autofillHints.Length - 1)
		        {
		            nextHint = autofillHints[i + 1];
		        }
		        // First convert the compound W3C autofill hints
		        if (isW3cSectionPrefix(hint) && i < autofillHints.Length - 1)
		        {
		            hint = autofillHints[++i];
		            CommonUtil.logd($"Hint is a W3C section prefix; using {hint} instead");
		            if (i < autofillHints.Length - 1)
		            {
		                nextHint = autofillHints[i + 1];
		            }
		        }
		        if (isW3cTypePrefix(hint) && nextHint != null && isW3cTypeHint(nextHint))
		        {
		            hint = nextHint;
		            i++;
		            CommonUtil.logd($"Hint is a W3C type prefix; using {hint} instead");
		        }
		        if (isW3cAddressType(hint) && nextHint != null)
		        {
		            hint = nextHint;
		            i++;
		            CommonUtil.logd($"Hint is a W3C address prefix; using  {hint} instead");
		        }

		        // Then check if the "actual" hint is supported.
		        if (AutofillHintsHelper.IsValidHint(hint))
		        {
		            HintMap.Add(hint, filledAutofillField);
		        }
		        else
		        {
		            CommonUtil.loge($"Invalid hint: {autofillHints[i]}");
		        }
		    }
            
		}


	    private static bool isW3cSectionPrefix(string hint)
	    {
	        return hint.StartsWith(W3cHints.PREFIX_SECTION);
	    }

	    private static bool isW3cAddressType(string hint)
	    {
	        switch (hint)
	        {
	            case W3cHints.SHIPPING:
	            case W3cHints.BILLING:
	                return true;
	        }
	        return false;
	    }

	    private static bool isW3cTypePrefix(string hint)
	    {
	        switch (hint)
	        {
	            case W3cHints.PREFIX_WORK:
	            case W3cHints.PREFIX_FAX:
	            case W3cHints.PREFIX_HOME:
	            case W3cHints.PREFIX_PAGER:
	                return true;
	        }
	        return false;
	    }

	    private static bool isW3cTypeHint(string hint)
	    {
	        switch (hint)
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

        /// <summary>
        /// Populates a Dataset.Builder with appropriate values for each AutofillId
        /// in a AutofillFieldMetadataCollection.
        /// 
        /// In other words, it constructs an autofill Dataset.Builder 
        /// by applying saved values (from this FilledAutofillFieldCollection)
        /// to Views specified in a AutofillFieldMetadataCollection, which represents the current
        /// page the user is on.
        /// </summary>
        /// <returns><c>true</c>, if to fields was applyed, <c>false</c> otherwise.</returns>
        /// <param name="autofillFieldMetadataCollection">Autofill field metadata collection.</param>
        /// <param name="datasetBuilder">Dataset builder.</param>
        public bool ApplyToFields(AutofillFieldMetadataCollection autofillFieldMetadataCollection, Dataset.Builder datasetBuilder)
		{
			bool setValueAtLeastOnce = false;
			List<string> allHints = autofillFieldMetadataCollection.AllAutofillHints;
			for (int hintIndex = 0; hintIndex < allHints.Count; hintIndex++)
			{
				string hint = allHints[hintIndex];
				List<AutofillFieldMetadata> fillableAutofillFields = autofillFieldMetadataCollection.GetFieldsForHint(hint);
				if (fillableAutofillFields == null)
				{
					continue;
				}
				foreach (AutofillFieldMetadata autofillFieldMetadata in fillableAutofillFields)
				{
					FilledAutofillField filledAutofillField;
					if (!HintMap.TryGetValue(hint, out filledAutofillField) || (filledAutofillField == null))
					{
						continue;
					}
				    
					var autofillId = autofillFieldMetadata.AutofillId;
					var autofillType = autofillFieldMetadata.AutofillType;
					switch (autofillType)
					{
						case AutofillType.List:
							var listValue = autofillFieldMetadata.GetAutofillOptionIndex(filledAutofillField.TextValue);
							if (listValue != -1)
							{
								datasetBuilder.SetValue(autofillId, AutofillValue.ForList(listValue));
								setValueAtLeastOnce = true;
							}
							break;
						case AutofillType.Date:
							var dateValue = filledAutofillField.DateValue;
							datasetBuilder.SetValue(autofillId, AutofillValue.ForDate((long)dateValue));
							setValueAtLeastOnce = true;
							break;
						case AutofillType.Text:
							var textValue = filledAutofillField.TextValue;
							if (textValue != null)
							{
								datasetBuilder.SetValue(autofillId, AutofillValue.ForText(textValue));
								setValueAtLeastOnce = true;
							}
							break;
						case AutofillType.Toggle:
							var toggleValue = filledAutofillField.ToggleValue;
							if (toggleValue != null)
							{
								datasetBuilder.SetValue(autofillId, AutofillValue.ForToggle(toggleValue.Value));
								setValueAtLeastOnce = true;
							}
							break;
						default:
							Log.Warn(CommonUtil.Tag, "Invalid autofill type - " + autofillType);
							break;
					}
				}
			}
			return setValueAtLeastOnce;
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
}

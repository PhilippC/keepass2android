using System;
using System.Collections.Generic;
using Android.Service.Autofill;
using Android.Views.Autofill;

namespace keepass2android.services.AutofillBase
{
	/// <summary>
	/// Data structure that stores a collection of AutofillFieldMetadatas. Contains all of the
	/// client's View hierarchy autofill-relevant metadata.
	/// </summary>
	public class AutofillFieldMetadataCollection
	{
		List<AutofillId> AutofillIds = new List<AutofillId>();
        
		Dictionary<string, List<AutofillFieldMetadata>> AutofillCanonicalHintsToFieldsMap = new Dictionary<string, List<AutofillFieldMetadata>>();
        
		public List<string> AllAutofillCanonicalHints { get; }
		public List<string> FocusedAutofillCanonicalHints { get; }
		int Size = 0;
		public SaveDataType SaveType { get; set; }

		public AutofillFieldMetadataCollection()
		{
			SaveType = 0;
			FocusedAutofillCanonicalHints = new List<string>();
			AllAutofillCanonicalHints = new List<string>();
		}

		public void Add(AutofillFieldMetadata autofillFieldMetadata)
		{
			SaveType |= autofillFieldMetadata.SaveType;
			Size++;
			AutofillIds.Add(autofillFieldMetadata.AutofillId);
			var hintsList = autofillFieldMetadata.AutofillCanonicalHints;
			AllAutofillCanonicalHints.AddRange(hintsList);
			if (autofillFieldMetadata.Focused)
			{
				FocusedAutofillCanonicalHints.AddRange(hintsList);
			}
			foreach (var hint in autofillFieldMetadata.AutofillCanonicalHints)
			{
				if (!AutofillCanonicalHintsToFieldsMap.ContainsKey(hint))
				{
					AutofillCanonicalHintsToFieldsMap.Add(hint, new List<AutofillFieldMetadata>());
				}
				AutofillCanonicalHintsToFieldsMap[hint].Add(autofillFieldMetadata);
			}
		}

		public AutofillId[] GetAutofillIds()
		{
			return AutofillIds.ToArray();
		}

        /// <summary>
        /// returns the fields for the given hint or an empty list.
        /// </summary>
		public List<AutofillFieldMetadata> GetFieldsForHint(String canonicalHint)
		{
		    List<AutofillFieldMetadata> result;
		    if (!AutofillCanonicalHintsToFieldsMap.TryGetValue(canonicalHint, out result))
		    {
		        result = new List<AutofillFieldMetadata>();
		    }
            return result;
		}


	}
}

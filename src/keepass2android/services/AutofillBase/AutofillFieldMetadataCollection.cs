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
		Dictionary<string, List<AutofillFieldMetadata>> AutofillHintsToFieldsMap = new Dictionary<string, List<AutofillFieldMetadata>>();
		public List<string> AllAutofillHints { get; }
		public List<string> FocusedAutofillHints { get; }
		int Size = 0;
		public SaveDataType SaveType { get; set; }

		public AutofillFieldMetadataCollection()
		{
			SaveType = 0;
			FocusedAutofillHints = new List<string>();
			AllAutofillHints = new List<string>();
		}

		public void Add(AutofillFieldMetadata autofillFieldMetadata)
		{
			SaveType |= autofillFieldMetadata.SaveType;
			Size++;
			AutofillIds.Add(autofillFieldMetadata.AutofillId);
			var hintsList = autofillFieldMetadata.AutofillHints;
			AllAutofillHints.AddRange(hintsList);
			if (autofillFieldMetadata.Focused)
			{
				FocusedAutofillHints.AddRange(hintsList);
			}
			foreach (var hint in autofillFieldMetadata.AutofillHints)
			{
				if (!AutofillHintsToFieldsMap.ContainsKey(hint))
				{
					AutofillHintsToFieldsMap.Add(hint, new List<AutofillFieldMetadata>());
				}
				AutofillHintsToFieldsMap[hint].Add(autofillFieldMetadata);
			}
		}

		public AutofillId[] GetAutofillIds()
		{
			return AutofillIds.ToArray();
		}

		public List<AutofillFieldMetadata> GetFieldsForHint(String hint)
		{
			return AutofillHintsToFieldsMap[hint];
		}


	}
}

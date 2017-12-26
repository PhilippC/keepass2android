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
		public Dictionary<string, keepass2android.services.AutofillBase.model.FilledAutofillField> HintMap { get; set; }
		public string DatasetName { get; set; }

		public FilledAutofillFieldCollection(Dictionary<string, keepass2android.services.AutofillBase.model.FilledAutofillField> hintMap, string datasetName = "")
		{
			HintMap = hintMap;
			DatasetName = datasetName;
		}

		public FilledAutofillFieldCollection() : this(new Dictionary<string, keepass2android.services.AutofillBase.model.FilledAutofillField>()) 
		{}

		/// <summary>
		/// Adds a filledAutofillField to the collection, indexed by all of its hints.
		/// </summary>
		/// <returns>The add.</returns>
		/// <param name="filledAutofillField">Filled autofill field.</param>
		public void Add(keepass2android.services.AutofillBase.model.FilledAutofillField filledAutofillField)
		{
			string[] autofillHints = filledAutofillField.AutofillHints;
		    //TODO apply W3C transformation
            foreach (string hint in autofillHints)
			{
				HintMap.Add(hint, filledAutofillField);
			}
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
		public bool ApplyToFields(AutofillFieldMetadataCollection autofillFieldMetadataCollection,
			Dataset.Builder datasetBuilder)
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
				for (int autofillFieldIndex = 0; autofillFieldIndex < fillableAutofillFields.Count; autofillFieldIndex++)
				{
					keepass2android.services.AutofillBase.model.FilledAutofillField filledAutofillField = HintMap[hint];
					if (filledAutofillField == null)
					{
						continue;
					}
					AutofillFieldMetadata autofillFieldMetadata = fillableAutofillFields[autofillFieldIndex];
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

using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Service.Autofill;
using Android.Util;
using Android.Views;
using Android.Views.Autofill;
using Android.Widget;
using Android.Widget.Inline;
using AndroidX.AutoFill.Inline;
using AndroidX.AutoFill.Inline.V1;
using Kp2aAutofillParser;

namespace keepass2android.services.AutofillBase
{
	/// <summary>
	/// This is a class containing helper methods for building Autofill Datasets and Responses.
	/// </summary>
	public class AutofillHelper
	{

        public static InlinePresentation BuildInlinePresentation(InlinePresentationSpec inlinePresentationSpec,
            string text, string subtext, int iconId, PendingIntent pendingIntent, Context context)
        {
            if ((int)Build.VERSION.SdkInt < 30 || inlinePresentationSpec == null)
            {
                return null;
            }
            //make sure we have a pendingIntent always not null
            pendingIntent ??= PendingIntent.GetService(context, 0, new Intent(),
                Util.AddMutabilityFlag(PendingIntentFlags.OneShot | PendingIntentFlags.UpdateCurrent, PendingIntentFlags.Mutable));
            
            var slice = CreateInlinePresentationSlice(
                inlinePresentationSpec,
                text,
                subtext,
                iconId,
                "Autofill option",
                pendingIntent,
                context);
            if (slice != null)
            {
                return new InlinePresentation(slice, inlinePresentationSpec, false);
            }
            return null;
        }


        private static Android.App.Slices.Slice CreateInlinePresentationSlice(
            InlinePresentationSpec inlinePresentationSpec,
            string text,
            string subtext,
            int iconId,
            string contentDescription,
            PendingIntent pendingIntent,
            Context context)
        {
            var imeStyle = inlinePresentationSpec.Style;

            if (!UiVersions.GetVersions(imeStyle).Contains(UiVersions.InlineUiVersion1))
            {
                return null;
            }
            var contentBuilder = InlineSuggestionUi.NewContentBuilder(pendingIntent)
                .SetContentDescription(contentDescription);
            if (!string.IsNullOrWhiteSpace(text))
            {
                contentBuilder.SetTitle(text);
            }
            if (!string.IsNullOrWhiteSpace(subtext))
            {
                contentBuilder.SetSubtitle(subtext);
            }
            if (iconId > 0)
            {
                var icon = Android.Graphics.Drawables.Icon.CreateWithResource(context, iconId);
                if (icon != null)
                {
                    if (iconId == AppNames.LauncherIcon)
                    {
                        // Don't tint our logo
                        icon.SetTintBlendMode(Android.Graphics.BlendMode.Dst);
                    }
                    contentBuilder.SetStartIcon(icon);
                }
            }
            return contentBuilder.Build().JavaCast<InlineSuggestionUi.Content>()?.Slice;
        }

        /// <summary>
        /// Wraps autofill data in a LoginCredential  Dataset object which can then be sent back to the
        /// client View.
        /// </summary>
        public static Dataset NewDataset(Context context,
				AutofillFieldMetadataCollection autofillFields, 
				FilledAutofillFieldCollection<ViewNodeInputField> filledAutofillFieldCollection,
				IAutofillIntentBuilder intentBuilder, 
				Android.Widget.Inline.InlinePresentationSpec inlinePresentationSpec)
        {
            var datasetName = filledAutofillFieldCollection.DatasetName ?? "[noname]";

            var datasetBuilder = new Dataset.Builder(NewRemoteViews(context.PackageName, datasetName, intentBuilder.AppIconResource));
            datasetBuilder.SetId(datasetName);

            var setValueAtLeastOnce = ApplyToFields(filledAutofillFieldCollection, autofillFields, datasetBuilder);
            AddInlinePresentation(context, inlinePresentationSpec, datasetName, datasetBuilder, intentBuilder.AppIconResource, null);

            if (setValueAtLeastOnce)
            {
                return datasetBuilder.Build();
            }
            /*else
            {
                Kp2aLog.Log("Failed to set at least one value. #fields=" + autofillFields.GetAutofillIds().Length + " " + autofillFields.FocusedAutofillCanonicalHints);
            }*/

            return null;
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
        /// <param name="filledAutofillFieldCollection"></param>
        /// <param name="autofillFieldMetadataCollection">Autofill field metadata collection.</param>
        /// <param name="datasetBuilder">Dataset builder.</param>
        public static bool ApplyToFields(FilledAutofillFieldCollection<ViewNodeInputField> filledAutofillFieldCollection,
            AutofillFieldMetadataCollection autofillFieldMetadataCollection, Dataset.Builder datasetBuilder)
        {
            bool setValueAtLeastOnce = false;

            foreach (string hint in autofillFieldMetadataCollection.AllAutofillCanonicalHints)
            {
                foreach (AutofillFieldMetadata autofillFieldMetadata in autofillFieldMetadataCollection.GetFieldsForHint(hint))
                {
                    FilledAutofillField<ViewNodeInputField> filledAutofillField;
                    if (!filledAutofillFieldCollection.HintMap.TryGetValue(hint, out filledAutofillField) || (filledAutofillField == null))
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
            /*
            if (!setValueAtLeastOnce)
            {
                Kp2aLog.Log("No value set. Hint keys : " + string.Join(",", HintMap.Keys));
				foreach (string hint in autofillFieldMetadataCollection.AllAutofillCanonicalHints)
                {
                    Kp2aLog.Log("No value set. Hint = " + hint);
                    foreach (AutofillFieldMetadata autofillFieldMetadata in autofillFieldMetadataCollection
                        .GetFieldsForHint(hint))
                    {
                        Kp2aLog.Log("No value set. fieldForHint = " + autofillFieldMetadata.AutofillId.ToString());
                        FilledAutofillField filledAutofillField;
                        if (!HintMap.TryGetValue(hint, out filledAutofillField) || (filledAutofillField == null))
                        {
                            Kp2aLog.Log("No value set. Hint map does not contain value, " +
                                        (filledAutofillField == null));
                            continue;
                        }

                        Kp2aLog.Log("autofill type=" + autofillFieldMetadata.AutofillType);
                    }
                }
            }*/

            return setValueAtLeastOnce;
        }

        public static void AddInlinePresentation(Context context, InlinePresentationSpec inlinePresentationSpec,
            string datasetName, Dataset.Builder datasetBuilder, int iconId, PendingIntent pendingIntent)
        {
            if (inlinePresentationSpec != null)
            {
                var inlinePresentation = BuildInlinePresentation(inlinePresentationSpec, datasetName, "", iconId, pendingIntent, context);
                if (inlinePresentation != null)
                    datasetBuilder.SetInlinePresentation(inlinePresentation);
            }
        }

        public static RemoteViews NewRemoteViews(string packageName, string remoteViewsText,int drawableId)
		{
			RemoteViews presentation = new RemoteViews(packageName, Resource.Layout.autofill_service_list_item);
			presentation.SetTextViewText(Resource.Id.text, remoteViewsText);
			presentation.SetImageViewResource(Resource.Id.icon, drawableId);
			return presentation;
		}

        internal static InlinePresentationSpec ExtractSpec(IList<InlinePresentationSpec> inlinePresentationSpecs, int index)
        {
            return inlinePresentationSpecs == null ? null : inlinePresentationSpecs[Math.Min(index, inlinePresentationSpecs.Count - 1)];
        }
    }
}

using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Service.Autofill;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Widget.Inline;
using AndroidX.AutoFill.Inline;
using AndroidX.AutoFill.Inline.V1;
using FilledAutofillFieldCollection = keepass2android.services.AutofillBase.model.FilledAutofillFieldCollection;

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
                PendingIntentFlags.OneShot | PendingIntentFlags.UpdateCurrent);
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
        /// <returns>The dataset.</returns>
        /// <param name="context">Context.</param>
        /// <param name="autofillFields">Autofill fields.</param>
        /// <param name="filledAutofillFieldCollection">Filled autofill field collection.</param>
        public static Dataset NewDataset(Context context,
				AutofillFieldMetadataCollection autofillFields, 
				FilledAutofillFieldCollection filledAutofillFieldCollection,
				IAutofillIntentBuilder intentBuilder, 
				Android.Widget.Inline.InlinePresentationSpec inlinePresentationSpec)
        {
            var datasetName = filledAutofillFieldCollection.DatasetName ?? "[noname]";

            var datasetBuilder = new Dataset.Builder(NewRemoteViews(context.PackageName, datasetName, intentBuilder.AppIconResource));
            datasetBuilder.SetId(datasetName);

            var setValueAtLeastOnce = filledAutofillFieldCollection.ApplyToFields(autofillFields, datasetBuilder);
            AddInlinePresentation(context, inlinePresentationSpec, datasetName, datasetBuilder, intentBuilder.AppIconResource);

            if (setValueAtLeastOnce)
            {
                return datasetBuilder.Build();
            }
            else
            {
                Kp2aLog.Log("Failed to set at least one value. #fields=" + autofillFields.GetAutofillIds().Length + " " + autofillFields.FocusedAutofillCanonicalHints);
            }

            return null;
        }

        public static void AddInlinePresentation(Context context, InlinePresentationSpec inlinePresentationSpec, string datasetName, Dataset.Builder datasetBuilder, int iconId)
        {
            if (inlinePresentationSpec != null)
            {
                var inlinePresentation = BuildInlinePresentation(inlinePresentationSpec, datasetName, "", iconId, null, context);
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

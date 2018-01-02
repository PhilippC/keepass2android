using System;
using Android.Content;
using Android.Service.Autofill;
using Android.Util;
using Android.Views;
using Android.Widget;
using FilledAutofillFieldCollection = keepass2android.services.AutofillBase.model.FilledAutofillFieldCollection;

namespace keepass2android.services.AutofillBase
{
	/// <summary>
	/// This is a class containing helper methods for building Autofill Datasets and Responses.
	/// </summary>
	public class AutofillHelper
	{
	    /// <summary>
	    /// Wraps autofill data in a LoginCredential  Dataset object which can then be sent back to the
	    /// client View.
	    /// </summary>
	    /// <returns>The dataset.</returns>
	    /// <param name="context">Context.</param>
	    /// <param name="autofillFields">Autofill fields.</param>
	    /// <param name="filledAutofillFieldCollection">Filled autofill field collection.</param>
	    public static Dataset NewDataset(Context context,
				AutofillFieldMetadataCollection autofillFields, FilledAutofillFieldCollection filledAutofillFieldCollection, IAutofillIntentBuilder intentBuilder) 
		{
			var datasetName = filledAutofillFieldCollection.DatasetName;
			if (datasetName != null)
			{
			    var datasetBuilder = new Dataset.Builder(NewRemoteViews(context.PackageName, datasetName, intentBuilder.AppIconResource));
				
				var setValueAtLeastOnce = filledAutofillFieldCollection.ApplyToFields(autofillFields, datasetBuilder);
				if (setValueAtLeastOnce)
				{
					return datasetBuilder.Build();
				}
			}
			return null;
		}

		public static RemoteViews NewRemoteViews(string packageName, string remoteViewsText,int drawableId)
		{
			RemoteViews presentation = new RemoteViews(packageName, Resource.Layout.autofill_service_list_item);
			presentation.SetTextViewText(Resource.Id.text, remoteViewsText);
			presentation.SetImageViewResource(Resource.Id.icon, drawableId);
			return presentation;
		}
	}
}

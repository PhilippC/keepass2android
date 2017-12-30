using System;
using System.Collections.Generic;
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
		/// <param name="datasetAuth">If set to <c>true</c> dataset auth.</param>
		public static Dataset NewDataset(Context context,
				AutofillFieldMetadataCollection autofillFields, FilledAutofillFieldCollection filledAutofillFieldCollection, bool datasetAuth, IAutofillIntentBuilder intentBuilder) 
		{
			var datasetName = filledAutofillFieldCollection.DatasetName;
			if (datasetName != null)
			{
				Dataset.Builder datasetBuilder;
				if (datasetAuth)
				{
					datasetBuilder = new Dataset.Builder
					                            (NewRemoteViews(context.PackageName, datasetName, intentBuilder.AppIconResource));
					IntentSender sender = intentBuilder.GetAuthIntentSenderForDataset(context, datasetName);
					datasetBuilder.SetAuthentication(sender);
				}
				else
				{
					datasetBuilder = new Dataset.Builder
												(NewRemoteViews(context.PackageName, datasetName, intentBuilder.AppIconResource));
				}
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

	    /// <summary>
	    /// Wraps autofill data in a Response object (essentially a series of Datasets) which can then
	    /// be sent back to the client View.
	    /// </summary>
	    /// <returns>The response.</returns>
	    /// <param name="context">Context.</param>
	    /// <param name="datasetAuth">If set to <c>true</c> dataset auth.</param>
	    /// <param name="autofillFields">Autofill fields.</param>
	    /// <param name="clientFormDataMap">Client form data map.</param>
	    /// <param name="intentBuilder"></param>
	    public static FillResponse NewResponse(Context context, bool datasetAuth, AutofillFieldMetadataCollection autofillFields, Dictionary<string, FilledAutofillFieldCollection> clientFormDataMap, IAutofillIntentBuilder intentBuilder)
		{
			var responseBuilder = new FillResponse.Builder();
			if (clientFormDataMap != null)
			{
				var datasetNames = clientFormDataMap.Keys;
				foreach (var datasetName in datasetNames)
				{
					var filledAutofillFieldCollection = clientFormDataMap[datasetName];
					if (filledAutofillFieldCollection != null)
					{
						var dataset = NewDataset(context, autofillFields, filledAutofillFieldCollection, datasetAuth, intentBuilder);
						if (dataset != null)
						{
							responseBuilder.AddDataset(dataset);
						}
					}
				}
			}
			if (autofillFields.SaveType != 0)
			{
                //TODO implement save 
				var autofillIds = autofillFields.GetAutofillIds();
				responseBuilder.SetSaveInfo
				               (new SaveInfo.Builder(autofillFields.SaveType, autofillIds).Build());
				return responseBuilder.Build();
			}
			else
			{
				Log.Debug(CommonUtil.Tag, "These fields are not meant to be saved by autofill.");
				return null;
			}
		}

	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using Android.App.Assist;
using Android.Content;
using Android.Text;
using Android.Util;
using Android.Views;
using FilledAutofillFieldCollection = keepass2android.services.AutofillBase.model.FilledAutofillFieldCollection;

namespace keepass2android.services.AutofillBase
{
	/// <summary>
	///	Parser for an AssistStructure object. This is invoked when the Autofill Service receives an
	/// AssistStructure from the client Activity, representing its View hierarchy. In this sample, it
	/// parses the hierarchy and collects autofill metadata from {@link ViewNode}s along the way.
	/// </summary>
	public sealed class StructureParser
	{
	    public Context mContext { get; }
	    public AutofillFieldMetadataCollection AutofillFields { get; set; }
		AssistStructure Structure;
	    private List<AssistStructure.ViewNode> _editTextsWithoutHint = new List<AssistStructure.ViewNode>();
	    public FilledAutofillFieldCollection ClientFormData { get; set; }

		public StructureParser(Context context, AssistStructure structure)
		{
		    mContext = context;
		    Structure = structure;
			AutofillFields = new AutofillFieldMetadataCollection();
		}

		public string ParseForFill()
		{
			return Parse(true);
		}

		public string ParseForSave()
		{
			return Parse(false);
		}

		/// <summary>
		/// Traverse AssistStructure and add ViewNode metadata to a flat list.
		/// </summary>
		/// <returns>The parse.</returns>
		/// <param name="forFill">If set to <c>true</c> for fill.</param>
		string Parse(bool forFill)
		{
			Log.Debug(CommonUtil.Tag, "Parsing structure for " + Structure.ActivityComponent);
			var nodes = Structure.WindowNodeCount;
			ClientFormData = new FilledAutofillFieldCollection();
		    String webDomain = null;
		    _editTextsWithoutHint.Clear();

            for (int i = 0; i < nodes; i++)
			{
				var node = Structure.GetWindowNodeAt(i);
				var view = node.RootViewNode;
				ParseLocked(forFill, view, ref webDomain);
			}

		    if (AutofillFields.Empty)
		    {
                var passwordFields = _editTextsWithoutHint
		            .Where(f =>
		                (!f.IdEntry?.ToLowerInvariant().Contains("search") ?? true) &&
		                (!f.Hint?.ToLowerInvariant().Contains("search") ?? true) &&
		                (
		                    f.InputType.HasFlag(InputTypes.TextVariationPassword) ||
		                    f.InputType.HasFlag(InputTypes.TextVariationVisiblePassword) ||
		                    f.InputType.HasFlag(InputTypes.TextVariationWebPassword) ||
                            (f.HtmlInfo?.Attributes.Any(p => p.First.ToString() == "type" && p.Second.ToString() == "password") ?? false)
		                )
		            ).ToList();
		        if (!_editTextsWithoutHint.Any())
		        {
		            passwordFields = _editTextsWithoutHint.Where(f =>
		                (f.IdEntry?.ToLowerInvariant().Contains("password") ?? false)
		                || (f.Hint?.ToLowerInvariant().Contains("password") ?? false)).ToList();

		            
                }
		        foreach (var passwordField in passwordFields)
		        {
		            AutofillFields.Add(new AutofillFieldMetadata(passwordField, new[] { View.AutofillHintPassword }));
                    var usernameField = _editTextsWithoutHint.TakeWhile(f => f.AutofillId != passwordField.AutofillId).LastOrDefault();
		            if (usernameField != null)
		            {
		                AutofillFields.Add(new AutofillFieldMetadata(usernameField, new[] {View.AutofillHintUsername}));
		            }
		        }




            }


		    String packageName = Structure.ActivityComponent.PackageName;
            if (!string.IsNullOrEmpty(webDomain))
		    {
		        bool valid = Kp2aDigitalAssetLinksDataSource.Instance.IsValid(mContext, webDomain, packageName);
		        if (!valid)
		        {
		            throw new Java.Lang.SecurityException(mContext.GetString(
		                Resource.String.invalid_link_association, webDomain, packageName));
		        }
                Log.Debug(CommonUtil.Tag, $"Domain {webDomain} is valid for {packageName}");
		    }
		    else
		    {
		        webDomain = "androidapp://" + packageName;
                Log.Debug(CommonUtil.Tag, "no web domain. Using package name.");
		    }
		    return webDomain;
		}

		void ParseLocked(bool forFill, AssistStructure.ViewNode viewNode, ref string validWebdomain)
		{
		    String webDomain = viewNode.WebDomain;
		    if (webDomain != null)
		    {
		        Log.Debug(CommonUtil.Tag, $"child web domain: {webDomain}");
		        if (!string.IsNullOrEmpty(validWebdomain))
		        {
		            if (webDomain == validWebdomain)
		            {
		                throw new Java.Lang.SecurityException($"Found multiple web domains: valid= {validWebdomain}, child={webDomain}");
		            }
		        }
		        else
		        {
		            validWebdomain = webDomain;
		        }
		    }

            if (viewNode.GetAutofillHints() != null && viewNode.GetAutofillHints().Length > 0)
			{
				if (forFill)
				{
					AutofillFields.Add(new AutofillFieldMetadata(viewNode));
				}
				else
				{
                    //TODO implement
                    throw new NotImplementedException("TODO: Port and use AutoFill hints");
					//ClientFormData.Add(new FilledAutofillField(viewNode));
				}
			}
            else if (viewNode.ClassName == "android.widget.EditText" || viewNode?.HtmlInfo?.Tag == "input")
            {
                _editTextsWithoutHint.Add(viewNode);
            }
			var childrenSize = viewNode.ChildCount;
			if (childrenSize > 0)
			{
				for (int i = 0; i < childrenSize; i++)
				{
					ParseLocked(forFill, viewNode.GetChildAt(i), ref validWebdomain);
				}
			}
		}

	}
}

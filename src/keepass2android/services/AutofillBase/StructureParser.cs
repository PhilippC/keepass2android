using System;
using System.Collections.Generic;
using System.Linq;
using Android.App.Assist;
using Android.Content;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Views.Autofill;
using keepass2android.services.AutofillBase.model;
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

		public string ParseForFill(bool isManual)
		{
			return Parse(true, isManual);
		}

		public string ParseForSave()
		{
			return Parse(false, true);
		}

	    /// <summary>
	    /// Traverse AssistStructure and add ViewNode metadata to a flat list.
	    /// </summary>
	    /// <returns>The parse.</returns>
	    /// <param name="forFill">If set to <c>true</c> for fill.</param>
	    /// <param name="isManualRequest"></param>
	    string Parse(bool forFill, bool isManualRequest)
		{
			CommonUtil.logd("Parsing structure for " + Structure.ActivityComponent);
			var nodes = Structure.WindowNodeCount;
			ClientFormData = new FilledAutofillFieldCollection();
		    String webDomain = null;
		    _editTextsWithoutHint.Clear();

            for (int i = 0; i < nodes; i++)
			{
				var node = Structure.GetWindowNodeAt(i);
				var view = node.RootViewNode;
				ParseLocked(forFill, isManualRequest, view, ref webDomain);
			}



		    List<AssistStructure.ViewNode> passwordFields = new List<AssistStructure.ViewNode>();
		    List<AssistStructure.ViewNode> usernameFields = new List<AssistStructure.ViewNode>();
            if (AutofillFields.Empty)
		    {
                passwordFields = _editTextsWithoutHint.Where(IsPassword).ToList();
		        if (!passwordFields.Any())
		        {
		            passwordFields = _editTextsWithoutHint.Where(HasPasswordHint).ToList();
                }
		        
		        foreach (var passwordField in passwordFields)
		        {
                    var usernameField = _editTextsWithoutHint.TakeWhile(f => f.AutofillId != passwordField.AutofillId).LastOrDefault();
		            if (usernameField != null)
		            {
		                usernameFields.Add(usernameField);
		            }
		        }
                //for some pages with two-step login, we don't see a password field and don't display the autofill for non-manual requests. But if the user forces autofill, 
                //let's assume it is a username field:
		        if (isManualRequest && !passwordFields.Any() && _editTextsWithoutHint.Count == 1)
		        {
		            usernameFields.Add(_editTextsWithoutHint.First());
                }

                
            }
		    
            //force focused fields to be included in autofill fields when request was triggered manually. This allows to fill fields which are "off" or don't have a hint (in case there are hints)
		    if (isManualRequest)
		    {
		        foreach (AssistStructure.ViewNode editText in _editTextsWithoutHint)
		        {
		            if (editText.IsFocused)
		            {
		                if (IsPassword(editText) || HasPasswordHint(editText))
		                    passwordFields.Add(editText);
		                else
		                    usernameFields.Add(editText);
		                break;
		            }

		        }
		    }

		    if (forFill)
		    {
		        foreach (var uf in usernameFields)
		            AutofillFields.Add(new AutofillFieldMetadata(uf, new[] { View.AutofillHintUsername }));
		        foreach (var pf in passwordFields)
		            AutofillFields.Add(new AutofillFieldMetadata(pf, new[] { View.AutofillHintPassword }));

            }
            else
		    {
		        foreach (var uf in usernameFields)
		            ClientFormData.Add(new FilledAutofillField(uf, new[] { View.AutofillHintUsername }));
		        foreach (var pf in passwordFields)
		            ClientFormData.Add(new FilledAutofillField(pf, new[] { View.AutofillHintPassword }));
            }



            String packageName = Structure.ActivityComponent.PackageName;
            if (!string.IsNullOrEmpty(webDomain))
		    {
		        bool valid = Kp2aDigitalAssetLinksDataSource.Instance.IsValid(mContext, webDomain, packageName);
		        if (!valid)
		        {
		            CommonUtil.loge($"DAL verification failed for {packageName}/{webDomain}");
		            webDomain = null;
		        }
		    }
		    if (string.IsNullOrEmpty(webDomain))
            {
		        webDomain = "androidapp://" + packageName;
                CommonUtil.logd("no web domain. Using package name.");
		    }
		    return webDomain;
		}

	    private static bool HasPasswordHint(AssistStructure.ViewNode f)
	    {
	        return (f.IdEntry?.ToLowerInvariant().Contains("password") ?? false)
	               || (f.Hint?.ToLowerInvariant().Contains("password") ?? false);
	    }

	    private static bool IsPassword(AssistStructure.ViewNode f)
	    {
	        return 
	            (!f.IdEntry?.ToLowerInvariant().Contains("search") ?? true) &&
	            (!f.Hint?.ToLowerInvariant().Contains("search") ?? true) &&
	            (
	                f.InputType.HasFlag(InputTypes.TextVariationPassword) ||
	                f.InputType.HasFlag(InputTypes.TextVariationVisiblePassword) ||
	                f.InputType.HasFlag(InputTypes.TextVariationWebPassword) ||
	                (f.HtmlInfo?.Attributes.Any(p => p.First.ToString() == "type" && p.Second.ToString() == "password") ?? false)
	            );
	    }

	    void ParseLocked(bool forFill, bool isManualRequest, AssistStructure.ViewNode viewNode, ref string validWebdomain)
		{
		    String webDomain = viewNode.WebDomain;
		    if (webDomain != null)
		    {
		        CommonUtil.logd($"child web domain: {webDomain}");
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

		    string[] viewHints = viewNode.GetAutofillHints();
		    if (viewHints != null && viewHints.Length == 1 && viewHints.First() == "off" && viewNode.IsFocused &&
		        isManualRequest)
		        viewHints[0] = "on";
		    CommonUtil.logd("viewHints=" + viewHints);
            CommonUtil.logd("class=" + viewNode.ClassName);
		    CommonUtil.logd("tag=" + (viewNode?.HtmlInfo?.Tag ?? "(null)"));
		    if (viewNode?.HtmlInfo?.Tag == "input")
		    {
		        foreach (var p in viewNode.HtmlInfo.Attributes)
                CommonUtil.logd("attr="+p.First + "/" + p.Second);
		    }
            if (viewHints != null && viewHints.Length > 0 && viewHints.First() != "on" /*if hint is "on", treat as if there is no hint*/)
			{
				if (forFill)
				{
					AutofillFields.Add(new AutofillFieldMetadata(viewNode));
				}
				else
				{
				    FilledAutofillField filledAutofillField = new FilledAutofillField(viewNode);
				    ClientFormData.Add(filledAutofillField);
                }
			}
            else
            {
                
                if (viewNode.ClassName == "android.widget.EditText" || viewNode?.HtmlInfo?.Tag == "input")
                {
                    _editTextsWithoutHint.Add(viewNode);
                }
                
            }
			var childrenSize = viewNode.ChildCount;
			if (childrenSize > 0)
			{
				for (int i = 0; i < childrenSize; i++)
				{
					ParseLocked(forFill, isManualRequest, viewNode.GetChildAt(i), ref validWebdomain);
				}
			}
		}

	}
}

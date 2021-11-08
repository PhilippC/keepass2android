using System;
using System.Collections.Generic;
using System.Linq;
using Android.App.Assist;
using Android.Content;
using Android.Preferences;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Views.Autofill;
using Android.Views.InputMethods;
using DomainNameParser;
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
	    private PublicSuffixRuleCache domainSuffixParserCache;
	    public FilledAutofillFieldCollection ClientFormData { get; set; }

        public string PackageId { get; set; }

		public StructureParser(Context context, AssistStructure structure)
        {
            kp2aDigitalAssetLinksDataSource = new Kp2aDigitalAssetLinksDataSource(context);
		    mContext = context;
		    Structure = structure;
			AutofillFields = new AutofillFieldMetadataCollection();
		    domainSuffixParserCache = new PublicSuffixRuleCache(context);
		}

        public class AutofillTargetId
        {
			public string PackageName { get; set; }

            public string PackageNameWithPseudoSchema
            {
                get { return KeePass.AndroidAppScheme + PackageName; }
            }

            public string WebDomain { get; set; }

			/// <summary>
			/// If PackageName and WebDomain are not compatible (by DAL or because PackageName is a trusted browser in which case we treat all domains as "compatible"
			/// we need to issue a warning. If we would fill credentials for the package, a malicious website could try to get credentials for the app.
			/// If we would fill credentials for the domain, a malicious app could get credentials for the domain.
			/// </summary>
            public bool IncompatiblePackageAndDomain { get; set; }

            public string DomainOrPackage
            {
                get
                {
                    return WebDomain ?? PackageNameWithPseudoSchema;
                }
            }
        }

		public AutofillTargetId ParseForFill(bool isManual)
		{
			return Parse(true, isManual);
		}

		public AutofillTargetId ParseForSave()
		{
			return Parse(false, true);
		}

		/// <summary>
		/// Traverse AssistStructure and add ViewNode metadata to a flat list.
		/// </summary>
		/// <returns>The parse.</returns>
		/// <param name="forFill">If set to <c>true</c> for fill.</param>
		/// <param name="isManualRequest"></param>
        AutofillTargetId Parse(bool forFill, bool isManualRequest)
        {
            AutofillTargetId result = new AutofillTargetId();
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

                usernameFields = _editTextsWithoutHint.Where(HasUsernameHint).ToList();

                if (usernameFields.Any() == false)
                {

                    foreach (var passwordField in passwordFields)
                    {
                        var usernameField = _editTextsWithoutHint
                            .TakeWhile(f => f.AutofillId != passwordField.AutofillId).LastOrDefault();
                        if (usernameField != null)
                        {
                            usernameFields.Add(usernameField);
                        }
                    }
                }
                if (usernameFields.Any() == false)
                {
                    //for some pages with two-step login, we don't see a password field and don't display the autofill for non-manual requests. But if the user forces autofill, 
                    //let's assume it is a username field:
                    if (isManualRequest && !passwordFields.Any() && _editTextsWithoutHint.Count == 1)
                    {
                        usernameFields.Add(_editTextsWithoutHint.First());
                    }
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


            result.WebDomain = webDomain;
            result.PackageName = Structure.ActivityComponent.PackageName;
            if (!string.IsNullOrEmpty(webDomain) && !PreferenceManager.GetDefaultSharedPreferences(mContext).GetBoolean(mContext.GetString(Resource.String.NoDalVerification_key), false))
		    {
                result.IncompatiblePackageAndDomain = !kp2aDigitalAssetLinksDataSource.IsTrustedLink(webDomain, result.PackageName);
		        if (result.IncompatiblePackageAndDomain)
		        {   
					CommonUtil.loge($"DAL verification failed for {result.PackageName}/{result.WebDomain}");
                }
		    }
            else
            {
                result.IncompatiblePackageAndDomain = false;
            }
            return result;
		}
        private static readonly HashSet<string> _passwordHints = new HashSet<string> { "password","passwort" };
        private static bool HasPasswordHint(AssistStructure.ViewNode f)
	    {
            return ContainsAny(f.IdEntry, _passwordHints) ||
                   ContainsAny(f.Hint, _passwordHints);
        }

        private static readonly HashSet<string> _usernameHints = new HashSet<string> { "email","e-mail","username" };
        private Kp2aDigitalAssetLinksDataSource kp2aDigitalAssetLinksDataSource;

        private static bool HasUsernameHint(AssistStructure.ViewNode f)
        {
            return ContainsAny(f.IdEntry, _usernameHints) ||
                ContainsAny(f.Hint, _usernameHints);
        }

        private static bool ContainsAny(string value, IEnumerable<string> terms)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            var lowerValue = value.ToLowerInvariant();
            return terms.Any(t => lowerValue.Contains(t));
        }

        private static bool IsInputTypeClass(InputTypes inputType, InputTypes inputTypeClass)
	    {
            if (!InputTypes.MaskClass.HasFlag(inputTypeClass))
                throw new Exception("invalid inputTypeClas");
	        return (((int)inputType) & (int)InputTypes.MaskClass) == (int) (inputTypeClass);
	    }
	    private static bool IsInputTypeVariation(InputTypes inputType, InputTypes inputTypeVariation)
	    {
	        if (!InputTypes.MaskVariation.HasFlag(inputTypeVariation))
	            throw new Exception("invalid inputTypeVariation");
	        bool result = (((int)inputType) & (int)InputTypes.MaskVariation) == (int)(inputTypeVariation);
	        if (result)
	            Kp2aLog.Log("found " + ((int)inputTypeVariation).ToString("X") + " in " + ((int)inputType).ToString("X"));
            return result;
            
	    }

        private static bool IsPassword(AssistStructure.ViewNode f)
	    {
	        InputTypes inputType = f.InputType;
            
            return 
	            (!f.IdEntry?.ToLowerInvariant().Contains("search") ?? true) &&
	            (!f.Hint?.ToLowerInvariant().Contains("search") ?? true) &&
	            (
	               (IsInputTypeClass(inputType, InputTypes.ClassText)
                        && 
                        (
                      IsInputTypeVariation(inputType, InputTypes.TextVariationPassword)
	                  || IsInputTypeVariation(inputType, InputTypes.TextVariationVisiblePassword)
	                  || IsInputTypeVariation(inputType, InputTypes.TextVariationWebPassword)
                      )
                      )
	                || (f.HtmlInfo?.Attributes.Any(p => p.First.ToString() == "type" && p.Second.ToString() == "password") ?? false)
	            );
	    }

	    

        void ParseLocked(bool forFill, bool isManualRequest, AssistStructure.ViewNode viewNode, ref string validWebdomain)
		{
		    String webDomain = viewNode.WebDomain;
            if ((PackageId == null) && (!string.IsNullOrWhiteSpace(viewNode.IdPackage)) &&
                (viewNode.IdPackage != "android"))
            {
                PackageId = viewNode.IdPackage;
            }

            DomainName outDomain;
		    if (DomainName.TryParse(webDomain, domainSuffixParserCache, out outDomain))
		    {
		        webDomain = outDomain.RawDomainName;
            }

            if (webDomain != null)
		    {
                if (!string.IsNullOrEmpty(validWebdomain))
		        {
		            if (webDomain != validWebdomain)
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
            /*if (viewHints != null && viewHints.Any())
            {
                CommonUtil.logd("viewHints=" + viewHints);
                CommonUtil.logd("class=" + viewNode.ClassName);
                CommonUtil.logd("tag=" + (viewNode?.HtmlInfo?.Tag ?? "(null)"));
            }*/
		    
		   
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
                
                if (viewNode.ClassName == "android.widget.EditText" 
                    || viewNode.ClassName == "android.widget.AutoCompleteTextView" 
                    || viewNode?.HtmlInfo?.Tag == "input")
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

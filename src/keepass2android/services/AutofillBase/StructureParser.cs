using System;
using System.Linq;

using Android.App.Assist;
using Android.Content;
using Android.Views.Autofill;
using DomainNameParser;
using Kp2aAutofillParser;
using Newtonsoft.Json;

namespace keepass2android.services.AutofillBase
{
    public class ViewNodeInputField : Kp2aAutofillParser.InputField
    {
        public ViewNodeInputField(AssistStructure.ViewNode viewNode)
        {
            ViewNode = viewNode;
            IdEntry = viewNode.IdEntry;
            Hint = viewNode.Hint;
            ClassName = viewNode.ClassName;
            AutofillHints = viewNode.GetAutofillHints();
            IsFocused = viewNode.IsFocused;
            InputType = (Kp2aAutofillParser.InputTypes) ((int)viewNode.InputType);
            HtmlInfoTag = viewNode.HtmlInfo?.Tag;
            HtmlInfoTypeAttribute = viewNode.HtmlInfo?.Attributes?.FirstOrDefault(p => p.First?.ToString() == "type")?.Second?.ToString();

        }
        [JsonIgnore]
        public AssistStructure.ViewNode ViewNode { get; set; }

        public void FillFilledAutofillValue(FilledAutofillField<ViewNodeInputField> filledField)
        {
            AutofillValue autofillValue = ViewNode.AutofillValue;
            if (autofillValue != null)
            {
                if (autofillValue.IsList)
                {
                    string[] autofillOptions = ViewNode.GetAutofillOptions();
                    int index = autofillValue.ListValue;
                    if (autofillOptions != null && autofillOptions.Length > 0)
                    {
                        filledField.TextValue = autofillOptions[index];
                    }
                }
                else if (autofillValue.IsDate)
                {
                    filledField.DateValue = autofillValue.DateValue;
                }
                else if (autofillValue.IsText)
                {
                    filledField.TextValue = autofillValue.TextValue;
                }
            }
        }
    }

    /// <summary>
    /// Converts an AssistStructure into a list of InputFields
    /// </summary>
    class AutofillViewFromAssistStructureFinder
    {
        private readonly Context _context;
        private readonly AssistStructure _structure;
        private PublicSuffixRuleCache domainSuffixParserCache;

        public AutofillViewFromAssistStructureFinder(Context context, AssistStructure structure)
        {
            _context = context;
            _structure = structure;
            domainSuffixParserCache = new PublicSuffixRuleCache(context);
        }

        public AutofillView<ViewNodeInputField> GetAutofillView(bool isManualRequest)
        {
            AutofillView<ViewNodeInputField> autofillView = new AutofillView<ViewNodeInputField>();
            
            
            int nodeCount = _structure.WindowNodeCount;
            for (int i = 0; i < nodeCount; i++)
            {
                var node = _structure.GetWindowNodeAt(i);

                var view = node.RootViewNode;
                ParseRecursive(autofillView, view, isManualRequest);
            }

            return autofillView;

        }


        void ParseRecursive(AutofillView<ViewNodeInputField> autofillView, AssistStructure.ViewNode viewNode, bool isManualRequest)
        {
            String webDomain = viewNode.WebDomain;
            if ((autofillView.PackageId == null) && (!string.IsNullOrWhiteSpace(viewNode.IdPackage)) &&
                (viewNode.IdPackage != "android"))
            {
                autofillView.PackageId = viewNode.IdPackage;
            }

            DomainName outDomain;
            if (DomainName.TryParse(webDomain, domainSuffixParserCache, out outDomain))
            {
                webDomain = outDomain.RawDomainName;
            }

            if (webDomain != null)
            {
                if (!string.IsNullOrEmpty(autofillView.WebDomain))
                {
                    if (webDomain != autofillView.WebDomain)
                    {
                        throw new Java.Lang.SecurityException($"Found multiple web domains: valid= {autofillView.WebDomain}, child={webDomain}");
                    }
                }
                else
                {
                    autofillView.WebDomain = webDomain;
                }
            }

            autofillView.InputFields.Add(new ViewNodeInputField(viewNode));
          
            var childrenSize = viewNode.ChildCount;
            if (childrenSize > 0)
            {
                for (int i = 0; i < childrenSize; i++)
                {
                    ParseRecursive(autofillView, viewNode.GetChildAt(i), isManualRequest);
                }
            }
        }
    }

	/// <summary>
	///	Parser for an AssistStructure object. This is invoked when the Autofill Service receives an
	/// AssistStructure from the client Activity, representing its View hierarchy. In this sample, it
	/// parses the hierarchy and collects autofill metadata from {@link ViewNode}s along the way.
	/// </summary>
	public sealed class StructureParser: StructureParserBase<ViewNodeInputField>
	{
        private readonly AssistStructure _structure;
        public Context _context { get; }
	    public AutofillFieldMetadataCollection AutofillFields { get; set; }
		public FilledAutofillFieldCollection<ViewNodeInputField> ClientFormData { get; set; }

        public string PackageId { get; set; }

		public StructureParser(Context context, AssistStructure structure)
        : base(new Kp2aLogger(), new Kp2aDigitalAssetLinksDataSource(context))
        {
		    _context = context;
            _structure = structure;
            AutofillFields = new AutofillFieldMetadataCollection();
            
        }

        protected override AutofillTargetId Parse(bool forFill, bool isManualRequest, AutofillView<ViewNodeInputField> autofillView)
        {
            var result = base.Parse(forFill, isManualRequest, autofillView);

            if (forFill)
            {
                foreach (var p in FieldsMappedToHints)
                    AutofillFields.Add(new AutofillFieldMetadata(p.Key.ViewNode, p.Value));
            }
            else
            {
                foreach (var p in FieldsMappedToHints)
                    ClientFormData.Add(new FilledAutofillField<ViewNodeInputField>(p.Key, p.Value));
            }
            

            return result;
        }

        public AutofillTargetId ParseForSave()
        {
            var autofillView = new AutofillViewFromAssistStructureFinder(_context, _structure).GetAutofillView(true);
            return Parse(false, true, autofillView);
        }

        public StructureParserBase<ViewNodeInputField>.AutofillTargetId ParseForFill(bool isManual)
        {
            var autofillView = new AutofillViewFromAssistStructureFinder(_context, _structure).GetAutofillView(isManual);
            return Parse(true, isManual, autofillView);
        }
        

    }

    public class Kp2aLogger : ILogger
    {
        public void Log(string x)
        {
            Kp2aLog.Log(x);
        }
    }
}

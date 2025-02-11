using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

    namespace DomainNameParser
    {
        using System.Collections.Generic;
        using System.Linq;
        using System;

        public class DomainName
        {
            public DomainName(string rawDomainName, string publicSuffix, string registerableDomainName)
            {
                this.RawDomainName = rawDomainName;
                this.PublicSuffix = publicSuffix;
                this.RegisterableDomainName = registerableDomainName;
            }

            public string RawDomainName { get; private set; }

            public string PublicSuffix { get; private set; }

            public string RegisterableDomainName { get; private set; }

            public static bool TryParse(string rawDomainName, PublicSuffixRuleCache ruleCache, out DomainName domainName)
            {
                if (string.IsNullOrEmpty(rawDomainName) || !rawDomainName.Contains('.') || rawDomainName.StartsWith("."))
                {
                    domainName = new DomainName(rawDomainName, null, null);
                    return true;
                }

                try
                {
                    rawDomainName = rawDomainName.ToLower();

                    //  Split our domain into parts (based on the '.')
                    //  We'll be checking rules from the right-most part of the domain
                    var domainLabels = rawDomainName.Trim().Split('.').ToList();
                    domainLabels.Reverse();

                    // If no rules match, the prevailing rule is "*"
                    var prevailingRule = FindMatchingRule(domainLabels, ruleCache) ?? new PublicSuffixRule("*");

                    // If the prevailing rule is an exception rule, modify it by removing the leftmost label.
                    if (prevailingRule.Type == PublicSuffixRule.RuleType.Exception)
                    {
                        var labels = prevailingRule.Labels;
                        labels.Reverse();
                        labels.RemoveAt(0);

                        prevailingRule = new PublicSuffixRule(string.Join(".", labels));
                    }

                    // The public suffix is the set of labels from the domain which directly match the labels of the prevailing rule (joined by dots).
                    var publicSuffix = Enumerable.Range(0, prevailingRule.Labels.Count).Aggregate(string.Empty, (current, i) => string.Format("{0}.{1}", domainLabels[i], current).Trim('.'));

                    // The registered or registrable domain is the public suffix plus one additional label.        
                    var registrableDomain = string.Format("{0}.{1}", domainLabels[prevailingRule.Labels.Count], publicSuffix);

                    domainName = new DomainName(rawDomainName, publicSuffix, registrableDomain);
                    return true;
                }
                catch
                {
                    domainName = null;
                    return false;
                }
            }

            private static PublicSuffixRule FindMatchingRule(List<string> domainLabels, PublicSuffixRuleCache ruleCache)
            {
                var ruleMatches = ruleCache.PublicSuffixRules.Where(r => r.AppliesTo(domainLabels)).ToList();

                // If there is only one match, return it.
                if (ruleMatches.Count() == 1)
                {
                    return ruleMatches[0];
                }

                // If more than one rule matches, the prevailing rule is the one which is an exception rule.
                var exceptionRules = ruleMatches.Where(r => r.Type == PublicSuffixRule.RuleType.Exception).ToList();
                if (exceptionRules.Count() == 1)
                {
                    return exceptionRules[0];
                }
                if (exceptionRules.Count() > 1)
                {
                    throw new ApplicationException("Unexpectedly found multiple matching exception rules.");
                }

                // If there is no matching exception rule, the prevailing rule is the one with the most labels.
                var prevailingRule = ruleMatches.OrderByDescending(r => r.Labels.Count).Take(1).SingleOrDefault();
                return prevailingRule;
            }
        }

        public class PublicSuffixRule
        {
            /// <summary>
            /// Construct a rule based on a single line from the www.publicsuffix.org list
            /// </summary>
            /// <param name="ruleLine">The rule line.</param>       
            public PublicSuffixRule(string ruleLine)
            {
                if (string.IsNullOrEmpty(ruleLine) || string.IsNullOrWhiteSpace(ruleLine))
                {
                    throw new ArgumentNullException("ruleLine");
                }

                //  Parse the rule and set properties accordingly:
                string ruleName;

                if (ruleLine.StartsWith("*", StringComparison.InvariantCultureIgnoreCase))
                {
                    this.Type = RuleType.Wildcard;
                    ruleName = ruleLine;
                }
                else if (ruleLine.StartsWith("!", StringComparison.InvariantCultureIgnoreCase))
                {
                    this.Type = RuleType.Exception;
                    ruleName = ruleLine.Substring(1);
                }
                else
                {
                    this.Type = RuleType.Normal;
                    ruleName = ruleLine;
                }

                this.Name = ruleName.Split(' ')[0];

                var labels = this.Name.Split('.').ToList();
                labels.Reverse();

                this.Labels = labels;
            }

            public string Name { get; private set; }

            public RuleType Type { get; private set; }

            public List<string> Labels { get; private set; }

            public bool AppliesTo(List<string> domainLabels)
            {
                if (this.Labels.Count > domainLabels.Count)
                {
                    return false;
                }

                foreach (var position in Enumerable.Range(0, this.Labels.Count))
                {
                    if (this.Labels[position] == "*")
                    {
                        return true;
                    }

                    if (this.Labels[position] != domainLabels[position])
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// Rule type
            /// </summary>
            public enum RuleType
            {
                /// <summary>
                /// A normal rule
                /// </summary>
                Normal,

                /// <summary>
                /// A wildcard rule, as defined by www.publicsuffix.org
                /// </summary>
                Wildcard,

                /// <summary>
                /// An exception rule, as defined by www.publicsuffix.org
                /// </summary>
                Exception
            }
        }



        public class PublicSuffixRuleCache
        {
            static IEnumerable<string> ReadLines(StreamReader reader,
                Encoding encoding)
            {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        yield return line;
                    }
                
            }

        public PublicSuffixRuleCache(Context context)
            {
                
                this.PublicSuffixRules = GetRules(context);
            }

            public PublicSuffixRuleCache(IEnumerable<string> publicSuffixRules)
            {
                this.PublicSuffixRules = GetRules(publicSuffixRules);
            }

            public List<PublicSuffixRule> PublicSuffixRules { get; private set; }

            /// <summary>
            /// Gets the list of TLD rules from the cache
            /// </summary>
            /// <returns></returns>
            private static List<PublicSuffixRule> GetRules(Context context)
            { 
                AssetManager assets = context.Assets;
                using (StreamReader sr = new StreamReader(assets.Open("publicsuffix.txt")))
                {
                    var ruleStrings = ReadLines(sr, Encoding.UTF8).ToList();
                    return GetRules(ruleStrings);
                }
            }

            private static List<PublicSuffixRule> GetRules(IEnumerable<string> publicSuffixRules)
            {
                // Strip out any lines that are a comment or blank.         
                return
                  publicSuffixRules.Where(
                    ruleString =>
                      ruleString.Trim().Length != 0
                      && !ruleString.StartsWith("//", StringComparison.InvariantCultureIgnoreCase)).Select(ruleString => new PublicSuffixRule(ruleString)).ToList();
            }
        }
    }

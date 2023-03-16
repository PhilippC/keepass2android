using Kp2aAutofillParser;
using Newtonsoft.Json;
using System.IO;
using System.Reflection;
using Xunit.Abstractions;

namespace Kp2aAutofillParserTest
{
    public class AutofillTest
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public AutofillTest(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        class TestInputField: InputField
        {
            public string[] ExpectedAssignedHints { get; set; }
            public override void FillFilledAutofillValue(FilledAutofillField filledField)
            {
            }
        }

        [Fact]
        public void TestNotFocusedPasswordAutoIsNotFilled()
        {
            var resourceName = "Kp2aAutofillParserTest.com-servicenet-mobile-no-focus.json";
            RunTestFromAutofillInput(resourceName, "com.servicenet.mobile");
        }

        [Fact]
        public void TestFocusedPasswordAutoIsFilled()
        {
            var resourceName = "Kp2aAutofillParserTest.com-servicenet-mobile-focused.json";
            RunTestFromAutofillInput(resourceName, "com.servicenet.mobile" );
        }

        [Fact]
        public void TestMulitpleUnfocusedLoginsIsFilled()
        {
            var resourceName = "Kp2aAutofillParserTest.firefox-amazon-it.json";
            RunTestFromAutofillInput(resourceName, "org.mozilla.firefox", "www.amazon.it");
        }

        [Fact]
        public void CanDetectFieldsWithoutAutofillHints()
        {
            var resourceName = "Kp2aAutofillParserTest.chrome-android10-amazon-it.json";
            RunTestFromAutofillInput(resourceName, "com.android.chrome", "www.amazon.it");
        }
        
        [Fact]
        public void DetectsUsernameFieldDespitePasswordAutoHint()
        {
            var resourceName = "Kp2aAutofillParserTest.com-ifs-banking-fiid3364-android13.json";
            RunTestFromAutofillInput(resourceName, "com.ifs.banking.fiid3364", null);
        }

        [Fact]
        public void DetectsEmailAutofillHint()
        {
            var resourceName = "Kp2aAutofillParserTest.com-expressvpn-vpn-android13.json";
            RunTestFromAutofillInput(resourceName, "com.expressvpn.vpn", null);
        }

        private void RunTestFromAutofillInput(string resourceName, string expectedPackageName = null, string expectedWebDomain = null)
        {
            var assembly = Assembly.GetExecutingAssembly();
            

            string input;
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                input = reader.ReadToEnd();
            }

            AutofillView<TestInputField>? autofillView =
                JsonConvert.DeserializeObject<AutofillView<TestInputField>>(input);

            StructureParserBase<TestInputField> parser =
                new StructureParserBase<TestInputField>(new TestLogger(), new TestDalSourceTrustAll());

            var result = parser.ParseForFill(false, autofillView);
            if (expectedPackageName != null)
                Assert.Equal(expectedPackageName, result.PackageName);
            if (expectedWebDomain != null)
                Assert.Equal(expectedWebDomain, result.WebDomain);
            foreach (var field in autofillView.InputFields)
            {
                string[] expectedHints = field.ExpectedAssignedHints;
                if (expectedHints == null)
                    expectedHints = new string[0];
                string[] actualHints;
                parser.FieldsMappedToHints.TryGetValue(field, out actualHints);
                if (actualHints == null)
                    actualHints = new string[0];
                if (actualHints.Any() || expectedHints.Any())
                {
                    _testOutputHelper.WriteLine($"field = {field.IdEntry} {field.Hint} {string.Join(",", field.AutofillHints ?? new string[]{})}");
                    _testOutputHelper.WriteLine("actual Hints = " + string.Join(", ", actualHints));
                    _testOutputHelper.WriteLine("expected Hints = " + string.Join(", ", expectedHints));
                }
                
                Assert.Equal(expectedHints.Length, actualHints.Length);
                Assert.Equal(expectedHints.OrderBy(x => x), actualHints.OrderBy(x => x));
            }
        }
    }

    public class TestDalSourceTrustAll : IKp2aDigitalAssetLinksDataSource
    {
        public bool IsTrustedApp(string packageName)
        {
            return true;
        }

        public bool IsTrustedLink(string domain, string targetPackage)
        {
            return true;
        }

        public bool IsEnabled()
        {
            return true;
        }
    }

    public class TestLogger : ILogger
    {
        public void Log(string x)
        {
            Console.WriteLine(x);
        }
    }
}
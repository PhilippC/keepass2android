// This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
//
//   Keepass2Android is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   Keepass2Android is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.

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

    class TestInputField : InputField
    {
      public string[] ExpectedAssignedHints { get; set; }
      public override void FillFilledAutofillValue(FilledAutofillField filledField)
      {
      }
    }

    [Fact]
    public void TestNotFocusedPasswordAutoIsNotFilled()
    {
      var resourceName = "com-servicenet-mobile-no-focus.json";
      RunTestFromAutofillInput(resourceName, "com.servicenet.mobile");
    }

    [Fact]
    public void TestCrashRegressionEmptySequence()
    {
      var resourceName = "imdb.json";
      RunTestFromAutofillInput(resourceName, "com.vivaldi.browser", "m.imdb.com");
    }

    [Fact]
    public void TestFocusedPasswordAutoIsFilled()
    {
      var resourceName = "com-servicenet-mobile-focused.json";
      RunTestFromAutofillInput(resourceName, "com.servicenet.mobile");
    }

    [Fact]
    public void TestMulitpleUnfocusedLoginsIsFilled()
    {
      var resourceName = "firefox-amazon-it.json";
      RunTestFromAutofillInput(resourceName, "org.mozilla.firefox", "www.amazon.it");
    }

    [Fact]
    public void CanDetectFieldsWithoutAutofillHints()
    {
      var resourceName = "chrome-android10-amazon-it.json";
      RunTestFromAutofillInput(resourceName, "com.android.chrome", "www.amazon.it");
    }

    [Fact]
    public void DetectsUsernameFieldDespitePasswordAutoHint()
    {
      var resourceName = "com-ifs-banking-fiid3364-android13.json";
      RunTestFromAutofillInput(resourceName, "com.ifs.banking.fiid3364", null);
    }

    [Fact]
    public void DetectsEmailAutofillHint()
    {
      var resourceName = "com-expressvpn-vpn-android13.json";
      RunTestFromAutofillInput(resourceName, "com.expressvpn.vpn", null);
    }
    [Fact]
    public void TestIgnoresAndroidSettings()
    {
      var resourceName = "android14-settings.json";
      RunTestFromAutofillInput(resourceName, "com.android.settings", null);
    }

    private void RunTestFromAutofillInput(string resourceName, string expectedPackageName = null, string expectedWebDomain = null)
    {
      var assembly = Assembly.GetExecutingAssembly();

      string input;
      using (Stream stream = assembly.GetManifestResourceStream(assembly.GetName().Name + "." + resourceName))
      using (StreamReader reader = new StreamReader(stream))
      {
        input = reader.ReadToEnd();
      }

      AutofillView<TestInputField>? autofillView =
          JsonConvert.DeserializeObject<AutofillView<TestInputField>>(input);

      StructureParserBase<TestInputField> parser =
          new StructureParserBase<TestInputField>(new TestLogger(), new TestDalSourceTrustAll());

      var result = parser.ParseForFill(autofillView);
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
          _testOutputHelper.WriteLine($"field = {field.IdEntry} {field.Hint} {string.Join(",", field.AutofillHints ?? new string[] { })}");
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
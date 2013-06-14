//
// Copyright (C) 2012 Maya Studios (http://mayastudios.com)
//
// This file is part of MonoDroidUnitTesting.
//
// MonoDroidUnitTesting is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// MonoDroidUnitTesting is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License
// along with MonoDroidUnitTesting. If not, see <http://www.gnu.org/licenses/>.
//
using System;
using Android.App;
using Android.Content;
using Android.Widget;


namespace MonoDroidUnitTesting {
  [Activity]
  public class TestClassResultActivity : AbstractResultListActivity<TestMethod> {
    private const string INTENT_PARAM_NAME = "MonoDroidUnitTest.TestClass";

    private TestClass m_testClass;

    public TestClassResultActivity() : base("method_") { }

    private static int CompareResults(TestMethod a, TestMethod b) {
      if (a.State == b.State) {
        return a.Method.Name.CompareTo(b.Method.Name);
      }

      return a.State.CompareToForSorting(b.State);
    }

    internal static void StartActivity(Context ctx, TestClass testClass) {
      Intent i = new Intent(ctx, typeof(TestClassResultActivity));
      i.PutExtra(INTENT_PARAM_NAME, testClass.Class.AssemblyQualifiedName);
      ctx.StartActivity(i);
    }

    protected override void OnStart() {
      base.OnStart();

      if (GuiTestRunnerActivity.TestRunner == null) {
        // Only happens during deployment.
        return;
      }

      string testClassName = this.Intent.GetCharSequenceExtra(INTENT_PARAM_NAME);
      TestClass testClass = GuiTestRunnerActivity.TestRunner.GetTestClass(testClassName);

      this.Title = "Results for " + testClass.Class.Name;
      this.ResultBar.SetColorByState(testClass.State);

      this.m_testClass = testClass;

      this.ListAdapter.Clear();
      foreach (TestMethod testMethod in testClass.GetTestMethodsSorted(CompareResults)) {
        this.ListAdapter.Add(testMethod);
      }
    }

    protected override ArrayAdapter<TestMethod> CreateListAdapter() {
      return new TestMethodsAdapter(this);
    }

    protected override void OnItemClicked(AdapterView.ItemClickEventArgs e) {
      TestMethod testMethod = this.ListAdapter.GetItem(e.Position);
      TestMethodResultActivity.StartActivity(this, testMethod);
    }

    protected override void OnResume() {
      base.OnResume();

      ISharedPreferencesEditor e = GetPreferences().Edit();
      e.PutString(ACTIVITY_PREFS_NAME, typeof(TestClassResultActivity).Name);
      e.PutString(INTENT_PARAM_NAME, this.m_testClass.Class.AssemblyQualifiedName);
      e.Commit();
    }

    internal static bool RestoreActivity(Context ctx) {
      var prefs = GetPreferences();
      if (prefs.GetString(ACTIVITY_PREFS_NAME, "") != typeof(TestClassResultActivity).Name) {
        return false;
      }

      string className = prefs.GetString(INTENT_PARAM_NAME, "");
      TestClass testClass = GuiTestRunnerActivity.TestRunner.GetTestClass(className);
      if (testClass == null) {
        // Class is not longer under test or not set
        return true;
      }

      StartActivity(ctx, testClass);
      return true;
    }

    internal static string GetHTMLDescriptionFor(MethodOutcome testMethod) {
      string text = "<b>" + testMethod.Method.Name + "()</b><br>";
      switch (testMethod.State) {
        case TestState.NotYetRun:
          text += "Skipped";
          break;

        case TestState.Passed:
          text += "<font color=green>passed</font>";
          break;

        case TestState.Failed:
          text += "<font color=red>failed</font> with " + testMethod.OutcomeError.Exception.GetType().Name;
          break;

        case TestState.Inconclusive:
          text += "<font color=red>inconclusive</font>";
          break;

        default:
          throw new Exception("Unexpected");
      }

      return text;
    }
 
    private class TestMethodsAdapter : TestResultAdapter {

      public TestMethodsAdapter(TestClassResultActivity activity) : base(activity) { }

      protected override TestState GetStateFor(TestMethod testMethod) {
        return testMethod.State;
      }

      protected override string GetHTMLDescriptionFor(TestMethod testMethod) {
        return TestClassResultActivity.GetHTMLDescriptionFor(testMethod);
      }
    }
  }
}
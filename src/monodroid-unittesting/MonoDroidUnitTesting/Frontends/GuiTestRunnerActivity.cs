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
using System.Reflection;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using MonoDroidUnitTesting.Utils;


namespace MonoDroidUnitTesting {
  public abstract class GuiTestRunnerActivity : AbstractResultListActivity<TestClass> {

    internal static TestRunner TestRunner { get; private set; }

    private ResultListItemView m_headerView;
    private MethodOutcome m_headerOutcome;

    public GuiTestRunnerActivity() : base("class_") { }

    protected override void OnCreate(Bundle bundle) {
      TestRunner = null;
      base.OnCreate(bundle);
    }

    protected override View CreateHeaderView() {
      this.m_headerView = new ResultListItemView(this);
      this.m_headerView.LayoutParameters = LayoutParams.ForLL(marginBottom: 10);
      this.m_headerView.SetIcon(this.IconOutcomeError);
      this.m_headerView.SetBackgroundColor(new Color(110, 3, 15));

      this.m_headerView.Clickable = true;
      this.m_headerView.Click += OnHeaderClick;

      this.m_headerView.Visibility = ViewStates.Gone;

      return this.m_headerView;
    }

    protected override void OnDestroy() {
      TestRunner = null;
      base.OnDestroy();
    }

    protected override void OnStart() {
      base.OnStart();

      new Handler().Post(this.RunTests);
    }

    protected virtual void OnTestRunStarted() { }

    protected virtual void OnTestRunEnded() { }


    private void RunTests() {
      this.Title = "Unit Tests for " + new AssemblyName(this.GetType().Assembly.FullName).Name;

      if (TestRunner == null) {
        try {
          OnTestRunStarted();
        }
        catch (Exception e) {
          MethodInfo method = GetType().GetMethod("OnTestRunStarted", BindingFlags.NonPublic|BindingFlags.Instance);
          this.m_headerOutcome = new MethodOutcome(method);
          this.m_headerOutcome.SetOutcome(e);

          this.m_headerView.SetHtml(TestClassResultActivity.GetHTMLDescriptionFor(this.m_headerOutcome));
          this.m_headerView.Visibility = ViewStates.Visible;

          this.ResultBar.SetColorByState(TestState.Failed);
          Toast.MakeText(this, "OnTestRunStarted() notification failed.", ToastLength.Long).Show();
          return;
        }

        this.m_headerView.Visibility = ViewStates.Gone;

        this.ResultBar.SetColorByState(TestState.Running);
        AsyncTestRunner.Run(this, this.CreateTestRunner, this.OnTestRunFinished);
      }
    }

    protected abstract TestRunner CreateTestRunner();

    private void OnHeaderClick(object sender, EventArgs e) {
      if (this.m_headerOutcome == null) {
        return;
      }

      TestMethodResultActivity.StartActivity(this, this.m_headerOutcome);
    }

    private static int CompareResults(TestClass a, TestClass b) {
      if (a.State == b.State) {
        return a.Class.Name.CompareTo(b.Class.Name);
      }

      return a.State.CompareToForSorting(b.State);
    }

    private void OnTestRunFinished(TestRunner runner) {
      if (runner == null) {
        Toast.MakeText(this, "Error", ToastLength.Long).Show();
        RunOnTestRunEnded();
        return;
      }

      bool testRunNotificationOk = RunOnTestRunEnded();

      if (runner.State == TestState.Passed && testRunNotificationOk) {
        Toast.MakeText(this, "Finished. All tests passed.", ToastLength.Long).Show();
      }
      else {
        Toast.MakeText(this, "Finished with some errors.", ToastLength.Long).Show();
      }

      if (testRunNotificationOk) {
        this.ResultBar.SetColorByState(runner.State);
      }

      TestRunner = runner;
      this.ListAdapter.Clear();
      foreach (TestClass testClass in runner.GetTestClassesSorted(CompareResults)) {
        this.ListAdapter.Add(testClass);
      }

      // Restore previous activity
      if (!TestMethodResultActivity.RestoreActivity(this)) {
        TestClassResultActivity.RestoreActivity(this);
      }
    }

    private bool RunOnTestRunEnded() {
      try {
        OnTestRunEnded();
        return true;
      }
      catch (Exception e) {
        MethodInfo method = GetType().GetMethod("OnTestRunEnded", BindingFlags.NonPublic | BindingFlags.Instance);
        this.m_headerOutcome = new MethodOutcome(method);
        this.m_headerOutcome.SetOutcome(e);

        this.m_headerView.SetHtml(TestClassResultActivity.GetHTMLDescriptionFor(this.m_headerOutcome));
        this.m_headerView.Visibility = ViewStates.Visible;

        this.ResultBar.SetColorByState(TestState.Failed);
        return false;
      }
    }

    protected override void OnResume() {
      base.OnResume();

      if (TestRunner != null) {
        // Only remember this view if the test run finished and therefore the previous activity has been restored.
        ISharedPreferencesEditor e = GetPreferences().Edit();
        e.PutString(ACTIVITY_PREFS_NAME, "");
        e.Commit();
      }
    }


    // NOTE: We need to use "int" for result value and parameter type. 
    // See: 
    //  * https://bugzilla.xamarin.com/show_bug.cgi?id=5980
    //  * https://bugzilla.xamarin.com/show_bug.cgi?id=5981
    private class AsyncTestRunner : AsyncTask<int, int, int>, ITestResultHandler {
      private readonly Func<TestRunner> m_testRunnerCreatorFunc;
      private readonly Action<TestRunner> m_finishedHandler;
      private readonly Handler m_guiHandler = new Handler();

      private readonly ProgressDialog m_dialog;
      private int m_curProgress = 0;
      private int m_curSecondaryProgress = 0;
      private TestRunner m_runner = null;

      private AsyncTestRunner(Context ctx, Func<TestRunner> testRunnerCreatorFunc, Action<TestRunner> finishedHandler) {
        this.m_testRunnerCreatorFunc = testRunnerCreatorFunc;
        this.m_finishedHandler = finishedHandler;

        this.m_dialog = new ProgressDialog(ctx);
        this.m_dialog.SetProgressStyle(ProgressDialogStyle.Horizontal);
        this.m_dialog.Indeterminate = true;
        this.m_dialog.SetCancelable(false);
        this.m_dialog.SetMessage("Running unit tests...");
      }

      public static void Run(Context ctx, Func<TestRunner> testRunnerCreatorFunc, Action<TestRunner> finishedHandler) {
        AsyncTestRunner runner = new AsyncTestRunner(ctx, testRunnerCreatorFunc, finishedHandler);
        runner.Execute();
      }

      protected override void OnPreExecute() {
        this.m_dialog.Show();
      }

      protected override int RunInBackground(params int[] @params) {
        try {
          this.m_runner = this.m_testRunnerCreatorFunc();

          this.m_guiHandler.PostAtFrontOfQueue(this.InitProgressBar);

          this.m_runner.RunTests(this);
        }
        catch (Exception e) {
          Log.Error(LOG_TAG, e.ToString());
          this.m_runner = null;
        }

        return 0;
      }

      private void InitProgressBar() {
        this.m_dialog.Indeterminate = false;
        this.m_dialog.Progress = 0;
        this.m_dialog.SecondaryProgress = 0;
        this.m_dialog.Max = this.m_runner.TestMethodCount;
      }

      protected override void OnProgressUpdate(params int[] values) {
        this.m_dialog.Progress = values[0];
        this.m_dialog.SecondaryProgress = values[1];
      }

      protected override void OnPostExecute(int result) {
        OnFinished();
      }

      protected override void OnCancelled() {
        OnFinished();
      }

      private void OnFinished() {
        this.m_dialog.Hide();
        this.m_finishedHandler(this.m_runner);
      }

      public void OnTestRunStarted(TestRunner runner) { }

      public void OnTestRunEnded(TestRunner runner) { }

      public void OnTestClassTestStarted(TestClass testClass, int testClassIndex) {
        this.m_curSecondaryProgress += testClass.TestMethodCount;
        PublishProgress(this.m_curProgress, this.m_curSecondaryProgress);
      }

      public void OnTestClassError(TestClass testClass, int testClassIndex) {
        this.m_curProgress = this.m_curSecondaryProgress;
        PublishProgress(this.m_curProgress, this.m_curSecondaryProgress);
      }

      public void OnTestClassTestEnded(TestClass testClass, int testClassIndex) {
      }

      public void OnTestMethodStarted(TestMethod testMethod, int testMethodIndex) {
      }

      public void OnTestMethodEnded(TestMethod testMethod, int testMethodIndex) {
        this.m_curProgress++;
        PublishProgress(this.m_curProgress, this.m_curSecondaryProgress);
      }
    }

    protected override ArrayAdapter<TestClass> CreateListAdapter() {
      return new TestClassesAdapter(this);
    }

    protected override void OnItemClicked(AdapterView.ItemClickEventArgs e) {
      TestClass testClass = this.ListAdapter.GetItem(e.Position);
      TestClassResultActivity.StartActivity(this, testClass);
    }

    private class TestClassesAdapter : TestResultAdapter {
      public TestClassesAdapter(GuiTestRunnerActivity activity) : base(activity) { }

      protected override TestState GetStateFor(TestClass testClass) {
        return testClass.State;
      }

      protected override string GetHTMLDescriptionFor(TestClass testClass) {
        string text = "<b>" + testClass.Class.Name + "</b><br>";

        switch (testClass.State) {
          case TestState.NotYetRun:
            text += "Not run";
            break;

          case TestState.Passed:
            text += "All tests <font color=green>passed</font> (" + testClass.TestMethodCount + ")";
            break;

          case TestState.Failed:
            text += testClass.GetStateCount(TestState.Failed) + " of " + testClass.TestMethodCount + " <font color=red>failed</font>";
            break;

          case TestState.Inconclusive:
            text += testClass.GetStateCount(TestState.Inconclusive) + " of " + testClass.TestMethodCount + " <font color=red>inconclusive</font>";
            break;

          default:
            throw new Exception("Unexpected");
        }

        return text;
      }
    }
  }
}
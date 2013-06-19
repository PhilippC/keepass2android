To use this library, create a new Mono for Android Application project, reference this library and then change the code
of the new main activity to something like this:

  [Activity(Label = "MonoDroidUnit", MainLauncher = true, Icon = "@drawable/icon")]
  public class Activity1 : GuiTestRunnerActivity {
    protected override TestRunner CreateTestRunner() {
      TestRunner runner = new TestRunner();
      // Run all tests from this assembly
      runner.AddTests(Assembly.GetExecutingAssembly());
      return runner;
    }
  }

You need to inherit from "GuiTestRunnerActivity" and implement the method "CreateTestRunner()".


IMPORTANT: Mono for Android (at least in version 4.2.3) doesn't provide any file names and line numbers in stack traces
  when running without a debugger attached. If you need line numbers in your stack traces, make sure you run your
  unit test with debugger attached.
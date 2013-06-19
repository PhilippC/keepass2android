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
using Android.Util;


namespace MonoDroidUnitTesting {
  public class LogTestRunner : ITestResultHandler {
    private const string TAG = "Unit Test Runner";

    private static string FormatException(string message, Exception e) {
      return message + "\n  " + string.Join("\n    ", e.ToString().Split('\n'));
    }

    void ITestResultHandler.OnTestRunStarted(TestRunner runner) {
      Log.Info(TAG, string.Format("Found {0} test classes...", runner.TestClassCount));
    }

    void ITestResultHandler.OnTestRunEnded(TestRunner runner) {
      Log.Info(TAG, "Test run finished");
    }

    void ITestResultHandler.OnTestClassTestStarted(TestClass testClass, int testClassIndex) {
      Log.Info(TAG, string.Format("Running test class {0} ({1} of {2})", testClass.Class.Name, testClassIndex,
                                  testClass.TestMethodCount));
    }

    void ITestResultHandler.OnTestClassError(TestClass testClass, int testClassIndex) {
      switch (testClass.ErrorType) {
        case TestClassErrorType.ConstructorError:
          Log.Error(TAG, FormatException("Could not create instance of class " + testClass.Class.Name, testClass.TestClassError));
          break;

        case TestClassErrorType.ClassInitializerError:
          Log.Error(TAG, FormatException("Error in class initializing method of class " + testClass.Class.Name, testClass.TestClassError));
          break;

        case TestClassErrorType.ClassCleanupError:
          Log.Error(TAG, FormatException("Error in class cleanup method of class " + testClass.Class.Name, testClass.TestClassError));
          break;

        default:
          Log.Error(TAG, FormatException("Error (" + testClass.ErrorType + ") in class " + testClass.Class.Name, testClass.TestClassError));
          break;
      }
    }

    void ITestResultHandler.OnTestClassTestEnded(TestClass testClass, int testClassIndex) {
      string headerMsg = string.Format("Test run completed. Results: {0}/{1} passed",
                                       testClass.GetStateCount(TestState.Passed), testClass.TestMethodCount);
      if (testClass.State == TestState.Passed) {
        Log.Info(TAG, headerMsg);
      }
      else {
        Log.Error(TAG, headerMsg);
      }
    }

    void ITestResultHandler.OnTestMethodStarted(TestMethod testMethod, int testMethodIndex) {
      // No op
    }

    void ITestResultHandler.OnTestMethodEnded(TestMethod testMethod, int testMethodIndex) {
      if (testMethod.State == TestState.Passed) {
        Log.Info(TAG, "Test passed: " + testMethod.Method.Name);
      }
      else {
        Log.Error(TAG, FormatException("Error in test method " + testMethod.Method.Name, testMethod.OutcomeError.Exception));
      }
    }
  }
}
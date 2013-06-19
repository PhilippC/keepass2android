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
using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace MonoDroidUnitTesting {
  /// <summary>
  /// Represents a test method.
  /// </summary>
  public class TestMethod : MethodOutcome {
    private static readonly object[] NO_PARAMS = new object[0];

    /// <summary>
    /// The test class this test method belongs to. Never null.
    /// </summary>
    public TestClass Class { get; private set; }

    internal TestMethod(TestClass testClass, MethodInfo method) : base(method) {
      this.Class = testClass;
    }

    /// <summary>
    /// Usually this method shouldn't be invoked directly. Use <see cref="TestClass.Run"/> instead. If no test class has
    /// been specified in the constructor, this method only executes the method itself (but no class setup or teardown 
    /// methods).
    /// </summary>
    public void Run(object testClassInstance, int testMethodIndex, ITestResultHandler resultHandler) {
      this.State = TestState.Running;

      resultHandler.OnTestMethodStarted(this, testMethodIndex);

      Exception outcomeError = null;

      if (this.Class.SetupMethod != null) {
        outcomeError = InvokeTestMethod(testClassInstance, this.Class.SetupMethod);
      }

      if (outcomeError == null) {
        // No error in the setup method
        outcomeError = InvokeTestMethod(testClassInstance, this.Method);
        if (outcomeError != null && IsExpectedException(this.Method, outcomeError)) {
          outcomeError = null;
        }

        if (this.Class.TeardownMethod != null) {
          var e = InvokeTestMethod(testClassInstance, this.Class.TeardownMethod);
          // Make sure we don't override the actual failure reson.
          if (e != null && outcomeError == null) {
            outcomeError = e;
          }
        }
      }

      SetOutcome(outcomeError);

      resultHandler.OnTestMethodEnded(this, testMethodIndex);
    }

    private static bool IsExpectedException(MethodInfo method, Exception e) {
      var attributes = method.GetCustomAttributes(typeof(ExpectedExceptionAttribute), true);
      if (attributes.Length == 0) {
        return false;
      }

      Type type = e.GetType();

      foreach (ExpectedExceptionAttribute attribute in attributes) {
        if (attribute.AllowDerivedTypes) {
          if (attribute.ExceptionType.IsAssignableFrom(type)) {
            return true;
          }
        }
        else {
          if (type == attribute.ExceptionType) {
            return true;
          }
        }
      }

      return false;
    }

    internal static Exception InvokeTestMethod(object testClassInstance, MethodInfo method) {
      try {
        method.Invoke(testClassInstance, NO_PARAMS);
        return null;
      }
      catch (Exception e) {
        if (e is TargetInvocationException && e.InnerException != null) {
          return e.InnerException;
        }
        return e;
      }
    }
  }
}

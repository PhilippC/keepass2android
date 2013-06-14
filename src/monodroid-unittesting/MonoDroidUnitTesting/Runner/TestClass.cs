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
using System.Collections.Generic;

using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace MonoDroidUnitTesting {
  /// <summary>
  /// Represents a collection of test methods.
  /// </summary>
  public class TestClass {
    public Type Class { get; private set; }

    private readonly SortedDictionary<string, TestMethod> m_testMethods = new SortedDictionary<string, TestMethod>();

    public IEnumerable<TestMethod> TestMethods {
      get {
        foreach (var item in this.m_testMethods) {
          yield return item.Value;
        }
      }
    }

    public int TestMethodCount {
      get { return this.m_testMethods.Count; }
    }

    private static readonly int OUTCOME_TYPE_COUNT = Enum.GetValues(typeof(TestState)).Length;
    private readonly Dictionary<TestState, int> m_stateCount = new Dictionary<TestState, int>();

    public MethodInfo ClassSetupMethod { get; private set; }
    public MethodInfo ClassTeardownMethod { get; private set; }
    public MethodInfo SetupMethod { get; private set; }
    public MethodInfo TeardownMethod { get; private set; }

    public TestState State { get; private set; }

    public Exception TestClassError { get; private set; }
    public TestClassErrorType? ErrorType { get; private set; }

    internal TestClass(Type @class, bool addTestMethods) {
      this.Class = @class;
      Reset();

      foreach (MethodInfo method in @class.GetMethods()) {
        if (!IsValidTestMethod(method)) {
          // Invalid method
          continue;
        }

        if (method.GetCustomAttributes(typeof(TestMethodAttribute), true).Length != 0) {
          if (addTestMethods) {
            AddTestMethod(method);
          }
        }
        else if (method.GetCustomAttributes(typeof(TestInitializeAttribute), true).Length != 0) {
          this.SetupMethod = method;
        }
        else if (method.GetCustomAttributes(typeof(TestCleanupAttribute), true).Length != 0) {
          this.TeardownMethod = method;
        }
        else if (method.GetCustomAttributes(typeof(ClassInitializeAttribute), true).Length != 0) {
          this.ClassSetupMethod = method;
        }
        else if (method.GetCustomAttributes(typeof(ClassCleanupAttribute), true).Length != 0) {
          this.ClassTeardownMethod = method;
        }
      }
    }

    private static bool IsValidTestMethod(MethodInfo method) {
      return (   !method.IsAbstract && !method.IsConstructor && !method.IsGenericMethod 
              && method.GetParameters().Length == 0 && method.IsPublic);
    }

    internal void AddAllTestMethods() {
      foreach (MethodInfo method in this.Class.GetMethods()) {
        if (!IsValidTestMethod(method)) {
          // Invalid method
          continue;
        }

        if (method.GetCustomAttributes(typeof(TestMethodAttribute), true).Length != 0) {
          AddTestMethod(method);
        }
      }
    }

    internal void AddTestMethod(MethodInfo method) {
      if (method.DeclaringType != this.Class) {
        throw new ArgumentException();
      }

      if (this.m_testMethods.ContainsKey(method.Name)) {
        return; // just ignore this
      }

      if (!IsValidTestMethod(method)) {
        throw new ArgumentException("Method " + method.Name + " is not a valid test method.");
      }

      this.m_testMethods.Add(method.Name, new TestMethod(this, method));
      this.m_stateCount[TestState.NotYetRun] = this.m_stateCount[TestState.NotYetRun] + 1;
    }

    internal void Run(ITestResultHandler resultHandler, int testClassIndex) {
      this.State = TestState.Running;

      resultHandler.OnTestClassTestStarted(this, testClassIndex);

      object testClassInstance;
      try {
        testClassInstance = Activator.CreateInstance(this.Class);
      }
      catch (Exception e) {
        if (e is TargetInvocationException && e.InnerException != null) {
          e = e.InnerException;
        }

        this.State = TestState.Failed;
        this.TestClassError = e;
        this.ErrorType = TestClassErrorType.ConstructorError;

        resultHandler.OnTestClassError(this, testClassIndex);
        return;
      }

      if (this.ClassSetupMethod != null) {
        var e = TestMethod.InvokeTestMethod(testClassInstance, this.ClassSetupMethod);
        if (e != null) {
          this.State = TestState.Failed;
          this.TestClassError = e;
          this.ErrorType = TestClassErrorType.ClassInitializerError;

          resultHandler.OnTestClassError(this, testClassIndex);
          return;
        }
      }

      int testMethodIndex = 1;
      this.m_stateCount[TestState.Running] = 1;
      foreach (TestMethod testMethod in this.TestMethods) {
        testMethod.Run(testClassInstance, testMethodIndex, resultHandler);

        this.m_stateCount[testMethod.State] = this.m_stateCount[testMethod.State] + 1;
        this.m_stateCount[TestState.NotYetRun] = this.m_stateCount[TestState.NotYetRun] - 1;

        testMethodIndex++;
      }
      this.m_stateCount[TestState.Running] = 0;

      if (this.ClassTeardownMethod != null) {
        var e = TestMethod.InvokeTestMethod(testClassInstance, this.ClassTeardownMethod);
        if (e != null) {
          this.State = TestState.Failed;
          this.TestClassError = e;
          this.ErrorType = TestClassErrorType.ClassCleanupError;

          resultHandler.OnTestClassError(this, testClassIndex);
          return;
        }
      }

      if (GetStateCount(TestState.Failed) != 0) {
        this.State = TestState.Failed;
      }
      else if (GetStateCount(TestState.Inconclusive) != 0) {
        this.State = TestState.Inconclusive;
      }
      else {
        this.State = TestState.Passed;
      }

      resultHandler.OnTestClassTestEnded(this, testClassIndex);
    }

    public int GetStateCount(TestState outcomeType) {
      return this.m_stateCount[outcomeType];
    }

    internal void Reset() {
      foreach (TestMethod method in this.TestMethods) {
        method.Reset();
      }

      for (int i = 0; i < OUTCOME_TYPE_COUNT; i++) {
        this.m_stateCount[(TestState)i] = 0;
      }
      this.m_stateCount[TestState.NotYetRun] = this.TestMethodCount;

      this.State = TestState.NotYetRun;
      this.TestClassError = null;
      this.ErrorType = null;
    }

    public TestMethod GetTestMethod(string name) {
      TestMethod testMethod;
      if (this.m_testMethods.TryGetValue(name, out testMethod)) {
        return testMethod;
      }
      return null;
    }

    public List<TestMethod> GetTestMethodsSorted(Comparison<TestMethod> comparer) {
      List<TestMethod> sorted = new List<TestMethod>(this.m_testMethods.Values);
      sorted.Sort(comparer);
      return sorted;
    }
  }

  public enum TestClassErrorType {
    ConstructorError,
    ClassInitializerError,
    ClassCleanupError
  }
}

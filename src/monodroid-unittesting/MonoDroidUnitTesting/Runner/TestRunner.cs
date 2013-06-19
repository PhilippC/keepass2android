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
  public class TestRunner {
    private readonly SortedDictionary<string, TestClass> m_testClasses = new SortedDictionary<string, TestClass>();

    /// <summary>
    /// All registered test classes.
    /// </summary>
    public IEnumerable<TestClass> TestClasses {
      get {
        foreach (var item in this.m_testClasses) {
          yield return item.Value;
        }
      }
    }

    public int TestClassCount {
      get {
        return this.m_testClasses.Count;
      }
    }

    public int TestMethodCount {
      get {
        int count = 0;
        foreach (TestClass testClass in this.TestClasses) {
          count += testClass.TestMethodCount;
        }
        return count;
      }
    }

    public TestState State {
      get {
        if (this.m_isRunning) {
          return TestState.Running;
        }

        TestState state = TestState.Passed;
        foreach (TestClass testClass in this.TestClasses) {
          switch (testClass.State) {
            case TestState.Failed:
              return TestState.Failed;

            case TestState.Inconclusive:
              // Don't return here. State may be "Failed" after all.
              state = TestState.Inconclusive;
              break;

            // Ignore all other states
          }
        }

        return state;
      }
    }

    private static List<Type> GetAllTestClasses(Assembly assembly) {
      List<Type> testClasses = new List<Type>();
      foreach (Type type in assembly.GetTypes()) {
        if (IsTestClass(type)) {
          testClasses.Add(type);
        }
      }

      return testClasses;
    }

    private static bool IsTestClass(Type type) {
      return (type.IsClass && type.GetCustomAttributes(typeof(TestClassAttribute), true).Length != 0);
    }

    private bool m_isRunning = false;

    /// <summary>
    /// Adds all test classes in the specified assembly. Test classes must have the <c>[TestClass]</c> attribute.
    /// </summary>
    public void AddTests(Assembly assembly) {
      AddTests(GetAllTestClasses(assembly));
    }

    /// <summary>
    /// Adds all test classes in the specified namespace in the specified assembly. Test classes must have the 
    /// <c>[TestClass]</c> attribute.
    /// </summary>
    public void AddTests(Assembly assembly, string @namespace) {
      List<Type> testClasses = new List<Type>();
      foreach (Type testClass in GetAllTestClasses(assembly)) {
        if (testClass.Namespace == @namespace) {
          testClasses.Add(testClass);
        }
      }

      AddTests(testClasses);
    }

    /// <summary>
    /// Adds the specified types as test classes.
    /// </summary>
    /// <param name="testClasses"></param>
    public void AddTests(params Type[] testClasses) {
      // NOTE: We don't care if the class is not a test class. If the user wants this, so be it.
      List<Type> list = new List<Type>(testClasses.Length);
      list.AddRange(testClasses);
      AddTests(list);
    }

    public void AddTests(List<Type> testClasses) {
      foreach (Type testClassType in testClasses) {
        TestClass testClass;

        if (this.m_testClasses.TryGetValue(testClassType.AssemblyQualifiedName, out testClass)) {
          testClass.AddAllTestMethods();
        }
        else {
          testClass = new TestClass(testClassType, true);
          this.m_testClasses[testClassType.AssemblyQualifiedName] = testClass;
        }
      }
    }

    public void AddTests(params MethodInfo[] testMethods) {
      foreach (MethodInfo testMethod in testMethods) {
        TestClass testClass;

        if (!this.m_testClasses.TryGetValue(testMethod.DeclaringType.AssemblyQualifiedName, out testClass)) {
          testClass = new TestClass(testMethod.DeclaringType, false);
          this.m_testClasses[testMethod.DeclaringType.AssemblyQualifiedName] = testClass;
        }

        testClass.AddTestMethod(testMethod);
      }
    }

    public void AddTests(params Action[] testMethods) {
      MethodInfo[] methodInfos = new MethodInfo[testMethods.Length];

      int x = 0;
      foreach (Action method in testMethods) {
        methodInfos[x] = method.Method;
        x++;
      }

      AddTests(methodInfos);
    }

    public void RunTests(ITestResultHandler resultHandler) {
      lock (this) {
        if (this.m_isRunning) {
          throw new InvalidOperationException("Tests are currently running.");
        }

        this.m_isRunning = true;

        // Reset all test classes
        foreach (var item in this.m_testClasses) {
          item.Value.Reset();
        }

        resultHandler.OnTestRunStarted(this);

        int index = 1;
        foreach (var item in this.m_testClasses) {
          item.Value.Run(resultHandler, index);
          index++;
        }

        resultHandler.OnTestRunEnded(this);

        this.m_isRunning = false;
      }
    }

    public List<TestClass> GetTestClassesSorted(Comparison<TestClass> comparer) {
      List<TestClass> sorted = new List<TestClass>(this.m_testClasses.Values);
      sorted.Sort(comparer);
      return sorted;
    }

    public TestClass GetTestClass(string assemblyQualifiedName) {
      TestClass testClass;
      if (this.m_testClasses.TryGetValue(assemblyQualifiedName, out testClass)) {
        return testClass;
      }
      return null;
    }
  }

  public interface ITestResultHandler {
    void OnTestRunStarted(TestRunner runner);
    void OnTestRunEnded(TestRunner runner);

    void OnTestClassTestStarted(TestClass testClass, int testClassIndex);
    /// <summary>
    /// Fired when a class error occurred. Will be fired instead of <see cref="TestClassTestEnded"/>. After being fired,
    /// no more test from the test class will be run.
    /// </summary>
    void OnTestClassError(TestClass testClass, int testClassIndex);
    void OnTestClassTestEnded(TestClass testClass, int testClassIndex);

    void OnTestMethodStarted(TestMethod testMethod, int testMethodIndex);
    void OnTestMethodEnded(TestMethod testMethod, int testMethodIndex);
  }
}
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
  public class MethodOutcome {
    /// <summary>
    /// The test method.
    /// </summary>
    public MethodInfo Method { get; private set; }

    /// <summary>
    /// The state (outcome) of the test.
    /// </summary>
    public TestState State { get; protected set; }

    /// <summary>
    /// The exception that lead to the failure of the test, if it failed. Is <c>null</c> for passed tests.
    /// </summary>
    public TestError OutcomeError { get; private set; }

    public MethodOutcome(MethodInfo method) {
      this.Method = method;
      Reset();
    }

    /// <summary>
    /// Usually this method shouldn't be invoked directly. Use <see cref="TestClass.Reset"/> instead.
    /// </summary>
    public virtual void Reset() {
      this.State = TestState.NotYetRun;
      this.OutcomeError = null;
    }

    /// <summary>
    /// Manually set the outcome of this method. If you use <see cref="Run"/>, you don't need to call this method.
    /// </summary>
    /// <param name="outcomeError">the exception resulted from calling the method. <c>null</c> if the method didn't 
    /// throw an exception.</param>
    public void SetOutcome(Exception outcomeError) {
      if (outcomeError == null) {
        this.OutcomeError = null;
        this.State = TestState.Passed;
      }
      else {
        if (outcomeError is AssertInconclusiveException) {
          this.State = TestState.Inconclusive;
        }
        else {
          this.State = TestState.Failed;
        }
        this.OutcomeError = new TestError(outcomeError);
      }
    }
  }
}
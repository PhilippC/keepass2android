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


namespace Microsoft.VisualStudio.TestTools.UnitTesting {
  [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
  public sealed class TestClassAttribute : Attribute { }

  // Summary:
  //     Used to identify test methods. This class cannot be inherited.
  [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
  public sealed class TestMethodAttribute : Attribute { }

  // Summary:
  //     Identifies the method to run before the test to allocate and configure resources
  //     needed by all tests in the test class. This class cannot be inherited.
  [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
  public sealed class TestInitializeAttribute : Attribute { }

  // Summary:
  //     Identifies a method that contains code that must be used after the test has
  //     run and to free resources obtained by all the tests in the test class. This
  //     class cannot be inherited.
  [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
  public sealed class TestCleanupAttribute : Attribute { }

  // Summary:
  //     Identifies a method that contains code that must be used before any of the
  //     tests in the test class have run and to allocate resources to be used by
  //     the test class. This class cannot be inherited.
  [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
  public sealed class ClassInitializeAttribute : Attribute { }

  // Summary:
  //     Identifies a method that contains code to be used after all the tests in
  //     the test class have run and to free resources obtained by the test class.
  //     This class cannot be inherited.
  [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
  public sealed class ClassCleanupAttribute : Attribute { }

  /// <summary>
  /// Indicates that an exception is expected during test method execution. This class cannot be inherited.
  /// </summary>
  [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
  public sealed class ExpectedExceptionAttribute : Attribute {
    /// <summary>
    /// The expected exception type.
    /// </summary>
    public Type ExceptionType { get; private set; }

    /// <summary>
    /// Whether derived exception types are allowed as well. Defaults to <c>false</c>.
    /// </summary>
    public bool AllowDerivedTypes { get; set; }

    /// <summary>
    /// Initializes a new instance of this.
    /// </summary>
    /// <param name="exceptionType">An expected type of exception to be thrown by a method.</param>
    /// <param name="noExceptionMessage">describes the exception; note that the execption message is NOT compared against this value.</param>
    public ExpectedExceptionAttribute(Type exceptionType, string noExceptionMessage = "") {
      if (exceptionType == null) {
        throw new ArgumentNullException("exceptionType");
      }
        
      if (!typeof(Exception).IsAssignableFrom(exceptionType)) {
        throw new ArgumentException("Must derive from type 'Exception'.", "exceptionType");
      }

      this.ExceptionType = exceptionType;
    }
  }
}

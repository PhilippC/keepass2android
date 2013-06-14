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

using System.Globalization;


namespace Microsoft.VisualStudio.TestTools.UnitTesting {

  // For Object.Equals(Object, Object) see:
  // http://stackoverflow.com/a/1451459/614177

  public static class Assert {

    [Obsolete("Don't use 'Assert.Equals()'. Use 'Assert.AreEqual()' instead.")]
    public static new bool Equals(object objA, object objB) {
      throw new NotSupportedException("Don't use 'Assert.Equals()'. Use 'Assert.AreEqual()' instead.");
    }

    public static void AreEqual<T>(T expected, T actual, string message = null, params object[] parameters) {
      if (!EqualityComparer<T>.Default.Equals(expected, actual)) {
        throw new NotEqualException(expected, actual, message, parameters);
      }
    }

    public static void AreNotEqual<T>(T notExpected, T actual, string message = null, params object[] parameters) {
      if (EqualityComparer<T>.Default.Equals(notExpected, actual)) {
        throw new EqualsException(notExpected, message, parameters);
      }
    }

    public static void AreEqual(double expected, double actual, double delta, 
                                string message = null, params object[] parameters) {
      if (Math.Abs(expected - actual) > delta) {
        throw new NotEqualException(expected, actual, message, parameters);
      }
    }

    public static void AreNotEqual(double notExpected, double actual, double delta,
                                   string message = null, params object[] parameters) {
      if (Math.Abs(notExpected - actual) <= delta) {
        throw new EqualsException(notExpected, message, parameters);
      }
    }


    public static void AreEqual(float expected, float actual, float delta, 
                                string message = null, params object[] parameters) {
      if (Math.Abs(expected - actual) > delta) {
        throw new NotEqualException(expected, actual, message, parameters);
      }
    }

    public static void AreNotEqual(float notExpected, float actual, float delta,
                                   string message = null, params object[] parameters) {
      if (Math.Abs(notExpected - actual) <= delta) {
        throw new EqualsException(notExpected, message, parameters);
      }
    }


    public static void AreEqual(string expected, string actual, 
                                string message = null, params object[] parameters) {
      AreEqual(expected, actual, false, message, parameters);
    }

    public static void AreNotEqual(string notExpected, string actual,
                                string message = null, params object[] parameters) {
      AreNotEqual(notExpected, actual, false, message, parameters);
    }

    public static void AreEqual(string expected, string actual, bool ignoreCase, 
                                string message = null, params object[] parameters) {
      AreEqual(expected, actual, ignoreCase, CultureInfo.InvariantCulture, message, parameters);
    }

    public static void AreNotEqual(string notExpected, string actual, bool ignoreCase,
                                   string message = null, params object[] parameters) {
      AreNotEqual(notExpected, actual, ignoreCase, CultureInfo.InvariantCulture, message, parameters);
    }

    public static void AreEqual(string expected, string actual, bool ignoreCase, CultureInfo culture,
                                string message = null, params object[] parameters) {
      if (string.Compare(expected, actual, ignoreCase, culture) != 0) {
        throw new NotEqualException(expected, actual, message, parameters);
      }
    }

    public static void AreNotEqual(string notExpected, string actual, bool ignoreCase, CultureInfo culture,
                                   string message = null, params object[] parameters) {
      if (string.Compare(notExpected, actual, ignoreCase, culture) == 0) {
        throw new EqualsException(notExpected, message, parameters);
      }
    }

    public static void AreSame(object expected, object actual, 
                               string message = null, params object[] parameters) {
      if (!object.ReferenceEquals(expected, actual)) {
        throw new AssertFailedException("'" + expected + "' and '" + actual + "' are not reference equal", message, parameters);
      }
    }

    public static void AreNotSame(object notExpected, object actual, 
                                  string message = null, params object[] parameters) {
      if (object.ReferenceEquals(notExpected, actual)) {
        throw new AssertFailedException("'" + notExpected + "' and '" + actual + "' are reference equal", message, parameters);
      }
    }

    public static void IsTrue(bool condition, 
                              string message = null, params object[] parameters) {
      if (!condition) {
        throw new NotEqualException(true, condition, message, parameters);
      }
    }

    public static void IsFalse(bool condition, 
                               string message = null, params object[] parameters) {
      if (condition) {
        throw new NotEqualException(false, condition, message, parameters);
      }
    }

    public static void IsNull(object value, 
                              string message = null, params object[] parameters) {
      if (value != null) {
        throw new NotEqualException(null, value, message, parameters);
      }
    }

    public static void IsNotNull(object value, 
                                 string message = null, params object[] parameters) {
      if (value == null) {
        throw new EqualsException(null, message, parameters);
      }
    }

    internal static bool CheckIsInstanceOfType(object value, Type expectedType) {
      return expectedType.IsAssignableFrom(value.GetType());
    }

    public static void IsInstanceOfType(object value, Type expectedType,
                                        string message = null, params object[] parameters) {
      if (expectedType == null) {
        throw new NullTestArgumentException("expectedType");
      }
      if (value == null) {
        throw new AssertFailedException("'null' is no instance of " + expectedType, message, parameters);
      }

      if (!CheckIsInstanceOfType(value, expectedType)) {
        throw new AssertFailedException("'" + value + "' (Type: " + value.GetType() + ") is no instance of " + expectedType, message, parameters);
      }
    }

    public static void IsNotInstanceOfType(object value, Type wrongType,
                                           string message = null, params object[] parameters) {
      if (value == null) {
        // "null" has no type and therefore is not an instance of "wrongType".
        return;
      }

      if (wrongType == null) {
        throw new NullTestArgumentException("wrongType");
      }

      if (wrongType.IsAssignableFrom(value.GetType())) {
        throw new AssertFailedException("'" + value + "' (Type: " + value.GetType() + ") is an instance of " + wrongType, message, parameters);
      }
    }

    public static void Fail(string message = null, params object[] parameters) {
      throw new AssertFailedException(message, parameters);
    }

    public static void Inconclusive(string message = null, params object[] parameters) {
      // Basically the same as "Fail". Used in auto generated (template) code.
      throw new AssertInconclusiveException(message, parameters);
    }

    //
    // Summary:
    //     In a string, replaces null characters ('\0') with "\\0".
    //
    // Parameters:
    //   input:
    //     The string in which to search for and replace null characters.
    //
    // Returns:
    //     The converted string with null characters replaced by "\\0".
    public static string ReplaceNullChars(string input) {
      if (input == null) {
        return null;
      }

      return input.Replace("\0", "\\0");
    }
  }
}

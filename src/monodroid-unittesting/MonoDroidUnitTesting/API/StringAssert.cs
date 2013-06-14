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
using System.Text.RegularExpressions;


namespace Microsoft.VisualStudio.TestTools.UnitTesting {
  // Summary:
  //     Verifies true/false propositions associated with strings in unit tests.
  public static class StringAssert {
    /// <summary>
    /// Verifies that the first string contains the second string.
    /// </summary>
    public static void Contains(string value, string substring, string message = null, params object[] parameters) {
      if (value == null) {
        throw new NullTestArgumentException("value");
      }
      if (substring == null) {
        throw new NullTestArgumentException("substring");
      }
      if (!value.Contains(substring)) {
        throw new AssertFailedException("'" + value + "' does not contain '" + substring + "'", message, parameters);
      }
    }

    /// <summary>
    /// Verifies that the first string begins with the second string
    /// </summary>
    public static void StartsWith(string value, string substring, string message = null, params object[] parameters) {
      if (value == null) {
        throw new NullTestArgumentException("value");
      }
      if (substring == null) {
        throw new NullTestArgumentException("substring");
      }

      if (!value.StartsWith(substring)) {
        throw new AssertFailedException("'" + value + "' does not start with '" + substring + "'", message, parameters);
      }
    }

    /// <summary>
    /// Verifies that the first string ends with the second string.
    /// </summary>
    public static void EndsWith(string value, string substring, string message = null, params object[] parameters) {
      if (value == null) {
        throw new NullTestArgumentException("value");
      }
      if (substring == null) {
        throw new NullTestArgumentException("substring");
      }
      if (!value.EndsWith(substring)) {
        throw new AssertFailedException("'" + value + "' does not end with '" + substring + "'", message, parameters);
      }
    }

    /// <summary>
    /// Verifies that the specified string (or parts of it) matches the regular expression.
    /// </summary>
    public static void Matches(string value, Regex pattern, string message = null, params object[] parameters) {
      if (value == null) {
        throw new NullTestArgumentException("value");
      }
      if (pattern == null) {
        throw new NullTestArgumentException("pattern");
      }
      if (!pattern.IsMatch(value)) {
        throw new AssertFailedException("Pattern does not match '" + value + "'", message, parameters);
      }
    }

    /// <summary>
    /// Verifies that the specified string does not match the regular expression.
    /// </summary>
    public static void DoesNotMatch(string value, Regex pattern, string message = null, params object[] parameters) {
      if (value == null) {
        throw new NullTestArgumentException("value");
      }
      if (pattern == null) {
        throw new NullTestArgumentException("pattern");
      }
      if (pattern.IsMatch(value)) {
        throw new AssertFailedException("Pattern does match '" + value + "'", message, parameters);
      }
    }

  }
}

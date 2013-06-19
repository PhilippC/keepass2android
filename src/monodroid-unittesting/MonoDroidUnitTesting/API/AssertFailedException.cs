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
  public class UnitTestAssertException : Exception {
    public UnitTestAssertException() : base() { }

    public UnitTestAssertException(string message) : base(message) { }

    public UnitTestAssertException(string message, Exception innerException) : base(message, innerException) { }

    public UnitTestAssertException(string msg, object[] parameters) 
      : base(ConstructMessage(msg, parameters)) { }

    public UnitTestAssertException(string explanation, string msg, object[] parameters)
      : base(ConstructMessage(explanation, msg, parameters)) { }

    private static string ConstructMessage(string message, object[] parameters) {
      if (message == null || message == "") {
        return "";
      }

      if (parameters != null && parameters.Length != 0) {
        message = String.Format(message, parameters);
      }
      return message;
    }

    private static string ConstructMessage(string explanation, string message, object[] parameters) {
      message = ConstructMessage(message, parameters);
      if (message != "") {
        message += "  ";
      }

      return message + explanation;
    }
  }

  public class AssertInconclusiveException : UnitTestAssertException {
    public AssertInconclusiveException(string msg, object[] parameters)
      : base(msg, parameters) { }
  }

  public class AssertFailedException : UnitTestAssertException {
    public AssertFailedException(string msg, params object[] parameters) 
      : base(msg, parameters) { }

    public AssertFailedException(string explanation, string msg, object[] parameters)
      : base(explanation, msg, parameters) { }
  }

  public class NotEqualException : AssertFailedException {
    public NotEqualException(object expected, object actual, string message, object[] parameters)
      : base("Expected '" + expected + "' but got '" + actual + "'", message, parameters) { }
  }

  public class EqualsException : AssertFailedException {
    public EqualsException(object notExpected, string message, object[] parameters)
      : base("Got unexpected '" + notExpected + "'", message, parameters) { }
  }

  public class InvalidTestArgumentException : AssertFailedException {
    public InvalidTestArgumentException(string paramName, string reason)
      : base("The parameter '{0}' is invalid. {1}", paramName, reason) { }
  }

  public class NullTestArgumentException : InvalidTestArgumentException {
    public NullTestArgumentException(string paramName)
      : base("The parameter '{0}' is invalid. The value cannot be null.", paramName) { }
  }
}
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


namespace MonoDroidUnitTesting {
  public class TestError {
    /// <summary>
    /// The exception that's representing this error.
    /// </summary>
    public Exception Exception { get; private set; }

    /// <summary>
    /// The error message of this exception. Note that the message may differ from <c>this.Exception.Message</c>, if
    /// the exception is a Java exception. In this case, this property contains the actual message (and should therefore
    /// be preferred over <c>Exception.Message</c>).
    /// </summary>
    public string Message { get; private set; }

    public TestError(Exception e) {
      while (e is TargetInvocationException && e.InnerException != null) {
        e = e.InnerException;
      }
      
      this.Exception = e;
      if (e is Java.Lang.Throwable) {
        // Throwable provides a new Message property (hiding the Message property of System.Exception), containing the 
        // actual message.
        this.Message = ((Java.Lang.Throwable)e).Message;
      }
      else {
        this.Message = e.Message;
      }
    }

    public override string ToString() {
      return this.Exception.ToString();
    }
  }
}

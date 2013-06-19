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


namespace MonoDroidUnitTesting {
  /// <summary>
  /// Represents the outcome of a test method.
  /// </summary>
  public enum TestState {
    NotYetRun,
    Running,
    Passed,
    Failed,
    Inconclusive
  }

  public static class TestStateExtensions {
    private static int GetOrderId(TestState state) {
      switch (state) {
        case TestState.Failed:
          return 0;
        case TestState.Inconclusive:
          return 1;
        case TestState.NotYetRun:
          return 2;
        case TestState.Running:
          return 3;
        case TestState.Passed:
          return 4;
        default:
          throw new Exception("Unexpected " + state);
      }
    }

    public static int CompareToForSorting(this TestState a, TestState b) {
      if (a == b) {
        return 0;
      }

      int aOrderId = GetOrderId(a);
      int bOrderId = GetOrderId(b);

      return aOrderId - bOrderId;
    }
  }
}
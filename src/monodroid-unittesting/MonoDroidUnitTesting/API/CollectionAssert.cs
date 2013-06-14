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
using System.Collections;
using System.Collections.Generic;


namespace Microsoft.VisualStudio.TestTools.UnitTesting {
  // Summary:
  //     Verifies true/false propositions associated with collections in unit tests.
  public static class CollectionAssert {
    /// <summary>
    /// Verifies that all elements in the specified collection are instances of the specified type. The assertion 
    /// fails if there exists one element in the collection for which the specified type is not found in its 
    /// inheritance hierarchy.
    /// </summary>
    public static void AllItemsAreInstancesOfType(ICollection collection, Type expectedType,
                                                  string message = null, params object[] parameters) {
      if (collection == null) {
        throw new NullTestArgumentException("collection");
      }
      if (expectedType == null) {
        throw new NullTestArgumentException("expectedType");
      }

      int index = 0;
      foreach (object item in collection) {
        if (item == null) {
          throw new AssertFailedException("Element at index {0} is (null). Expected type: <{1}>.".Format(index, expectedType.FullName), message, parameters);
        }

        if (!Assert.CheckIsInstanceOfType(item, expectedType)) {
          throw new AssertFailedException(
            "Element at index {0} is not of expecteds type. Expected type: {1}. Actual type: {2}.".FormatValues(index, expectedType.FullName, item.GetType().FullName), 
            message, parameters);
        }
        index++;
      }
    }

    /// <summary>
    /// Verifies that all items in the specified collection are not null. The assertion fails if any element is null.
    /// </summary>
    public static void AllItemsAreNotNull(ICollection collection, string message = null, params object[] parameters) {
      if (collection == null) {
        throw new NullTestArgumentException("collection");
      }

      int index = 0;
      foreach (object item in collection) {
        if (item == null) {
          throw new AssertFailedException("Element at index {0} is (null).".Format(index), message, parameters);
        }
        index++;
      }
    }

    /// <summary>
    /// Verifies that all items in the specified collection are unique. The assertion fails if any two elements in the 
    /// collection are equal.
    /// </summary>
    public static void AllItemsAreUnique(ICollection collection, string message = null, params object[] parameters) {
      if (collection == null) {
        throw new NullTestArgumentException("collection");
      }

      HashSet<object> items = new HashSet<object>();
      foreach (object item in collection) {
        if (!items.Add(item)) {
          throw new AssertFailedException("Duplicate item found: {0}".FormatValues(item), message, parameters);
        }
      }
    }

    private delegate bool ComparatorFunc(object expected, object actual);

    private static void AreEqual(ICollection expected, ICollection actual, string message, object[] parameters,
                                 ComparatorFunc comparator) {
      if (object.ReferenceEquals(expected, actual)) {
        return;
      }

      if (expected == null) {
        throw new NullTestArgumentException("expected");
      }

      if (actual == null) {
        throw new NullTestArgumentException("actual");
      }

      if (expected.Count != actual.Count) {
        throw new AssertFailedException("Expected collection with " + expected.Count + " items but got " + actual.Count + " items.", message, parameters);
      }

      IEnumerator expectedIter = expected.GetEnumerator();
      IEnumerator actualIter = actual.GetEnumerator();

      int index = 0;
      while (expectedIter.MoveNext()) {
        Assert.IsTrue(actualIter.MoveNext(), message, parameters);

        if (!comparator(expectedIter.Current, actualIter.Current)) {
          throw new AssertFailedException("Elements at index {0} do not match.".Format(index), message, parameters);
        }
        index++;
      }

      Assert.IsFalse(actualIter.MoveNext());
    }

    /// <summary>
    /// Verifies that two specified collections are equal, using <see cref="object.Equals()"/> to compare the values of 
    /// elements. The assertion fails if the collections are not equal.
    /// </summary>
    public static void AreEqual(ICollection expected, ICollection actual, string message = null, params object[] parameters) {
      AreEqual(expected, actual, message, parameters, (a, b) => { return object.Equals(a, b); });
    }

    /// <summary>
    /// Verifies that two specified collections are equal, using the specified method to compare the values of 
    /// elements. The assertion fails if the collections are not equal.
    /// </summary>
    public static void AreEqual(ICollection expected, ICollection actual, IComparer comparer, string message = null, params object[] parameters) {
      AreEqual(expected, actual, message, parameters, (a, b) => { return comparer.Compare(a, b) == 0; });
    }

    public static void AreNotEqual(ICollection notExpected, ICollection actual, string message = null, params object[] parameters) {
      bool areEqual = true;
      try {
        AreEqual(notExpected, actual);
      }
      catch (AssertFailedException) {
        areEqual = false;
      }

      if (areEqual) {
        throw new AssertFailedException("Collections are equal.", message, parameters);
      }
    }

    public static void AreNotEqual(ICollection notExpected, ICollection actual, IComparer comparer, string message = null, params object[] parameters) {
      bool areEqual = true;
      try {
        AreEqual(notExpected, actual, comparer);
      }
      catch (AssertFailedException) {
        areEqual = false;
      }

      if (areEqual) {
        throw new AssertFailedException("Collections are equal.", message, parameters);
      }
    }

    /// <summary>
    /// Verifies that two specified collections are equivalent. The assertion fails if the collections are not 
    /// equivalent. Two collections are equivalent if they have the same elements in the same quantity, but in any 
    /// order. Elements are equal if their values are equal, not if they refer to the same object.
    /// </summary>
    public static void AreEquivalent(ICollection expected, ICollection actual, string message = null, params object[] parameters) {
      if (object.ReferenceEquals(expected, actual)) {
        return;
      }

      if (expected == null) {
        throw new NullTestArgumentException("expected");
      }

      if (actual == null) {
        throw new NullTestArgumentException("actual");
      }

      if (expected.Count != actual.Count) {
        throw new AssertFailedException("Expected collection with " + expected.Count + " items but got " + actual.Count + " items.", message, parameters);
      }

      Dictionary<object, int> expectedQunatities = new Dictionary<object, int>(expected.Count);
      foreach (object obj in expected) {
        int quant;

        if (expectedQunatities.TryGetValue(obj, out quant)) {
          expectedQunatities[obj] = quant + 1;
        }
        else {
          expectedQunatities[obj] = 1;
        }
      }

      Dictionary<object, int> actualQunatities = new Dictionary<object, int>(actual.Count);
      foreach (object obj in actual) {
        int quant;

        if (actualQunatities.TryGetValue(obj, out quant)) {
          actualQunatities[obj] = quant + 1;
        }
        else {
          actualQunatities[obj] = 1;
        }
      }

      if (expectedQunatities.Count != actualQunatities.Count) {
        throw new AssertFailedException("Expected " + expectedQunatities.Count + " unique items but got " + actualQunatities.Count, message, parameters);
      }

      foreach (KeyValuePair<object, int> entry in expectedQunatities) {
        int actualQuant;

        if (!actualQunatities.TryGetValue(entry.Key, out actualQuant)) {
          throw new AssertFailedException("Expected '" + entry.Key + "' not in collection.", message, parameters);
        }

        if (entry.Value != actualQuant) {
          throw new AssertFailedException("Expected to find '" + entry.Key + "' " + entry.Value + " times but got: " + actualQuant, message, parameters);
        }
      }
    }

    /// <summary>
    /// Verifies that two specified collections are not equivalent. The assertion fails if the collections are 
    /// equivalent. Two collections are equivalent if they have the same elements in the same quantity, but in any 
    /// order. Elements are equal if their values are equal, not if they refer to the same object.
    /// </summary>
    public static void AreNotEquivalent(ICollection expected, ICollection actual, string message = null, params object[] parameters) {
      bool areEquivalent = true;
      try {
        AreEquivalent(expected, actual);
      }
      catch (AssertFailedException) {
        areEquivalent = false;
      }

      if (areEquivalent) {
        throw new AssertFailedException("Collections are equivalent.", message, parameters);
      }
    }

    /// <summary>
    /// Verifies that the specified collection contains the specified element. The assertion fails if the element is 
    /// not found in the collection.
    /// </summary>
    public static void Contains(ICollection collection, object element, string message = null, params object[] parameters) {
      if (collection == null) {
        throw new NullTestArgumentException("collection");
      }

      foreach (object obj in collection) {
        if (object.Equals(obj, element)) {
          return;
        }
      }

      throw new AssertFailedException("Collection does not contain {0}.".FormatValues(element), message, parameters);
    }

    /// <summary>
    /// Verifies that the specified collection does not contain the specified element. The assertion fails if the 
    /// element is found in the collection.
    /// </summary>
    public static void DoesNotContain(ICollection collection, object element, string message = null, params object[] parameters) {
      if (collection == null) {
        throw new NullTestArgumentException("collection");
      }

      foreach (object obj in collection) {
        if (object.Equals(obj, element)) {
          throw new AssertFailedException("'" + element + "' is part of the collection.", message, parameters);
        }
      }
    }

    /// <summary>
    /// Verifies that the first collection is a subset of the second collection. One collection is a subset of another 
    /// collection if every element in the first collection also appears in the second collection. An element that 
    /// appears in the first collection more than once must appear in the second collection as many times, or more, as 
    /// it does in the first collection. The second collection may have elements that are not in the first collection, 
    /// but that is not required.
    /// </summary>
    public static void IsSubsetOf(ICollection subset, ICollection superset, string message = null, params object[] parameters) {
      if (subset == null) {
        throw new NullTestArgumentException("subset");
      }

      if (superset == null) {
        throw new NullTestArgumentException("superset");
      }

      Dictionary<object, int> subsetQuantities = new Dictionary<object, int>(subset.Count);
      foreach (object obj in subset) {
        int quant;

        if (subsetQuantities.TryGetValue(obj, out quant)) {
          subsetQuantities[obj] = quant + 1;
        }
        else {
          subsetQuantities[obj] = 1;
        }
      }

      Dictionary<object, int> supersetQuantities = new Dictionary<object, int>(superset.Count);
      foreach (object obj in superset) {
        int quant;

        if (supersetQuantities.TryGetValue(obj, out quant)) {
          supersetQuantities[obj] = quant + 1;
        }
        else {
          supersetQuantities[obj] = 1;
        }
      }

      foreach (KeyValuePair<object, int> entry in subsetQuantities) {
        int superQuant;

        if (!supersetQuantities.TryGetValue(entry.Key, out superQuant)) {
          throw new AssertFailedException("Entry '" + entry.Key + "' not contained in super set.", message, parameters);
        }

        if (superQuant < entry.Value) {
          throw new AssertFailedException("Entry '" + entry.Key + " is only contained " + superQuant + " times in super set but " + entry.Value + " times in sub set.", message, parameters);
        }
      }
    }

    /// <summary>
    /// Verifies that the first collection is not a subset of the second collection. One collection is a subset of 
    /// another collection if every element in the first collection also appears in the second collection. An element 
    /// that appears in the first collection more than once must appear in the second collection as many times, or more,
    /// as it does in the first collection. The second collection may have elements that are not in the first 
    /// collection, but that is not required.
    /// </summary>
    public static void IsNotSubsetOf(ICollection subset, ICollection superset, string message = null, params object[] parameters) {
      if (subset == null) {
        throw new NullTestArgumentException("subset");
      }

      if (superset == null) {
        throw new NullTestArgumentException("superset");
      }

      bool isSubset = true;
      try {
        IsSubsetOf(subset, superset);
      }
      catch (AssertFailedException) {
        isSubset = false;
      }

      if (isSubset) {
        throw new AssertFailedException("Is subset of superset.", message, parameters);
      }
    }
  }
}

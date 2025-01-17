using System.Collections.Generic;
using System.Linq;

namespace IZ.Core.Utils;

public static class ListUtils {
  /// <summary>
  /// The two lists have the same items, irrespective of order
  /// </summary>
  /// <param name="list1"></param>
  /// <param name="list2"></param>
  /// <typeparam name="T"></typeparam>
  /// <returns></returns>
  public static bool IsSameSet<T>(this IList<T> list1, IList<T> list2) =>
    !list1.Except(list2).Union( list2.Except(list1) ).Any();

  /// <summary>
  /// The two lists contain the same items, irrespective of order OR duplicates
  /// </summary>
  /// <param name="list1"></param>
  /// <param name="list2"></param>
  /// <typeparam name="T"></typeparam>
  /// <returns></returns>
  public static bool IsMatchingSet<T>(this IList<T> list1, IList<T> list2) =>
    !list1.Except(list2).Union( list2.Except(list1) ).Any();
}

using System;
using System.Linq;
using System.Linq.Expressions;

namespace IZ.Data.Storage;

/// <summary>
/// A SQL predicate that a string-stored enum column holds a member this build declares. The stored-enum
/// convention (`ZDbContext.ConvertStoredEnumNames`) reads a name the build lacks as the enum's fallback;
/// a reader that loads rows and branches on them would then act on that fallback as if it were a real
/// value, and a table keyed on the column would track two different stored names as one key and update
/// by a name no row holds. Filtering with this keeps such rows out of the result entirely: the members
/// are compared by name in SQL (`IN ('A', 'B', …)`), which no foreign name matches.
/// </summary>
public static class StoredEnumFilter {
  /// <summary>
  /// <paramref name="member" /> is one of <typeparamref name="TEnum" />'s declared members, other than
  /// <paramref name="except" /> — pass the fallback when it is never a real stored value (an `Unknown = -1`
  /// that only a foreign name reads as), and nothing when it is (a legacy `Unknown = 0` rows really hold).
  /// </summary>
  public static Expression<Func<T, bool>> Declared<T, TEnum>(Expression<Func<T, TEnum>> member, params TEnum[] except)
    where TEnum : struct, Enum {
    var underlying = Enum.GetUnderlyingType(typeof(TEnum));
    // Compared through the underlying type, exactly as the compiler lowers `x.E == E.A`, which is the shape
    // EF recognizes and translates through the column's converter to the member name.
    Expression column = Expression.Convert(member.Body, underlying);
    var body = Enum.GetValues(typeof(TEnum)).Cast<TEnum>()
      .Where(v => !except.Contains(v))
      .Select(v => (Expression) Expression.Equal(column, Expression.Convert(Expression.Constant(v), underlying)))
      .Aggregate(Expression.OrElse);
    return Expression.Lambda<Func<T, bool>>(body, member.Parameters);
  }
}

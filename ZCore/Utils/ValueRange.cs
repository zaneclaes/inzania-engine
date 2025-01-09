#region

using System;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Utils;

/// <summary>The Range class.</summary>
/// <typeparam name="T">Generic parameter.</typeparam>
public abstract class ValueRange<T> : IAmInternal where T : IComparable<T> {
  /// <summary>Minimum value of the range.</summary>
  public T Minimum { get; set; } = default!;

  /// <summary>Maximum value of the range.</summary>
  public T Maximum { get; set; } = default!;

  public virtual bool InclusiveOfMaximum => false;

  public virtual bool InclusiveOfMinimum => true;

  /// <summary>Presents the Range in readable format.</summary>
  /// <returns>String representation of the Range</returns>
  public override string ToString() => $"[{Minimum} - {Maximum}]";

  /// <summary>Determines if the range is valid.</summary>
  /// <returns>True if range is valid, else false</returns>
  public bool IsValid() => Minimum.CompareTo(Maximum) <= 0;

  /// <summary>Determines if the provided value is inside the range.</summary>
  /// <param name="value">The value to test</param>
  /// <returns>True if the value is inside Range, else false</returns>
  public bool ContainsValue(T value) => (InclusiveOfMinimum ? Minimum.CompareTo(value) <= 0 : Minimum.CompareTo(value) < 0) &&
                                        (InclusiveOfMaximum ? value.CompareTo(Maximum) <= 0 : value.CompareTo(Maximum) < 0);

  public bool OverlapsWith(ValueRange<T> valueRange) => IsValid() &&
                                                        (ContainsValue(valueRange.Minimum) || ContainsValue(valueRange.Maximum) ||
                                                         valueRange.ContainsValue(Minimum) || valueRange.ContainsValue(Maximum));

  /// <summary>Determines if this Range is inside the bounds of another range.</summary>
  /// <param name="Range">The parent range to test on</param>
  /// <returns>True if range is inclusive, else false</returns>
  public bool IsInsideRange(ValueRange<T> valueRange) => IsValid() && valueRange.IsValid() && valueRange.ContainsValue(Minimum) && valueRange.ContainsValue(Maximum);

  /// <summary>Determines if another range is inside the bounds of this range.</summary>
  /// <param name="Range">The child range to test</param>
  /// <returns>True if range is inside, else false</returns>
  public bool ContainsRange(ValueRange<T> valueRange) => IsValid() && valueRange.IsValid() && ContainsValue(valueRange.Minimum) && ContainsValue(valueRange.Maximum);
}

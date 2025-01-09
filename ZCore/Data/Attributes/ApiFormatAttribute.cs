#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace IZ.Core.Data.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public class ApiFormatAttribute : Attribute {
  public HashSet<string?> FormatTags { get; }

  public ApiFormatAttribute(params string?[] formats) {
    FormatTags = new HashSet<string?>();
    foreach (string? fmt in formats) FormatTags.Add(fmt);
    if (!formats.Any()) FormatTags.Add(null);
  }
}

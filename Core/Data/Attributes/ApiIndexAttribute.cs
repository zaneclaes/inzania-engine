#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace IZ.Core.Data.Attributes;

/// <summary>
///   Specifies an index to be generated in the database.
/// </summary>
/// <remarks>
///   See <see href="https://aka.ms/efcore-docs-modeling">Modeling entity types and relationships</see> for more
///   information.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ApiIndexAttribute : Attribute {
  private bool? _isUnique;
  private string? _name;

  /// <summary>
  ///   Initializes a new instance of the <see cref="IndexAttribute" /> class.
  /// </summary>
  /// <param name="propertyNames">The properties which constitute the index, in order (there must be at least one).</param>
  public ApiIndexAttribute(params string[] propertyNames) {
    // Check.NotEmpty(propertyNames, nameof(propertyNames));
    // Check.HasNoEmptyElements(propertyNames, nameof(propertyNames));

    PropertyNames = propertyNames.ToList();
  }

  /// <summary>
  ///   The properties which constitute the index, in order.
  /// </summary>
  public IReadOnlyList<string> PropertyNames { get; }

  /// <summary>
  ///   The name of the index.
  /// </summary>
  // [DisallowNull]
  public string? Name {
    get => _name;
    set => _name = value ?? throw new NullReferenceException(nameof(Name));
  }

  /// <summary>
  ///   Whether the index is unique.
  /// </summary>
  public bool IsUnique {
    get => _isUnique ?? false;
    set => _isUnique = value;
  }

  /// <summary>
  ///   Checks whether <see cref="IsUnique" /> has been explicitly set to a value.
  /// </summary>
  public bool IsUniqueHasValue
    => _isUnique.HasValue;
}

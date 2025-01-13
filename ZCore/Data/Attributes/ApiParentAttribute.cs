using System;

namespace IZ.Core.Data.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class ApiParentAttribute : Attribute {
  public string ChildProperty { get; }

  public ApiDeleteBehavior DeleteBehavior { get; }

  public Type? ThroughModelType { get; }

  public ApiParentAttribute(string childProperty, Type? throughModelType = null, ApiDeleteBehavior deleteBehavior = ApiDeleteBehavior.Cascade) {
    ChildProperty = childProperty;
    DeleteBehavior = deleteBehavior;
    ThroughModelType = throughModelType;
  }
}

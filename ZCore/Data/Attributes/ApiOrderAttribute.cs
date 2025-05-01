using System;

namespace IZ.Core.Data.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class ApiOrderAttribute : Attribute {
  public int Order { get; set; }

  public ApiOrderAttribute(int order) {
    Order = order;
  }
}

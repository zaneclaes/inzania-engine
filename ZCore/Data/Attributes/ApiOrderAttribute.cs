using System;

namespace IZ.Core.Data.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class ApiOrderAttribute : Attribute {

  public ApiOrderAttribute(int order) {
    Order = order;
  }
  public int Order { get; set; }
}

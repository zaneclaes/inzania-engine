#region

using System;
using IZ.Core.Api;

#endregion

namespace IZ.Core.Data.Attributes;

[AttributeUsage(validOn: AttributeTargets.Method)]
public class ApiMethodAttribute : Attribute {
  public ApiExecutionType ExecutionType { get; }

  public ApiMethodAttribute(ApiExecutionType executionType = ApiExecutionType.Query) {
    ExecutionType = executionType;
  }
}

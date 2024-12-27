#region

using System.Collections.Generic;

#endregion

namespace IZ.Core.Api;

public enum ApiExecutionType {
  Query,
  Mutation
  // Subscription,
}

public static class ApiExecutionTypes {
  public static List<ApiExecutionType> All { get; } = new List<ApiExecutionType> {
    ApiExecutionType.Query,
    ApiExecutionType.Mutation
  };
}

using System;
using System.Collections.Generic;
using IZ.Core.Api.Types;
using IZ.Core.Data;

namespace IZ.Core.Api;

public interface IExecutionPlan {
  public string Id { get; }

  public string OperationName { get; }

  public ApiExecutionType OperationType { get; }

  public Dictionary<string, Tuple<ZTypeDescriptor, object?>> CoerceArgs(List<object?> args);
}

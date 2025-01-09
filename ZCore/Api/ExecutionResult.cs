#region

using System;
using System.Collections.Generic;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;
using IZ.Core.Data;

#endregion

namespace IZ.Core.Api;

public class ExecutionResult : TransientObject {
  // public Type ParentClass { get; }

  public ExecutionPlan Plan { get; }

  public Dictionary<string, Tuple<ZTypeDescriptor, object?>> Args { get; } = new Dictionary<string, Tuple<ZTypeDescriptor, object?>>();

  public ExecutionResult(
    IZContext context, ExecutionPlan plan, List<object?> args
  ) : base(context) {
    // OperationType = op;
    Plan = plan;
    Args = plan.CoerceArgs(args);
    // ParentClass = parentType;
  }
}

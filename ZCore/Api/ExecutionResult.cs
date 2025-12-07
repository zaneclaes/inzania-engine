#region

using System;
using System.Collections.Generic;
using System.Linq;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;
using IZ.Core.Data;

#endregion

namespace IZ.Core.Api;

public class ExecutionResult : TransientObject {

  public ExecutionResult(
    IZContext context, IExecutionPlan plan, List<object?> args
  ) : base(context) {
    // OperationType = op;
    Plan = plan;
    Args = plan.CoerceArgs(args);

    var keys = Args.Keys.ToList();
    keys.Sort();
    CacheId = plan.Id;
    foreach (var key in keys) {
      CacheId += "_" + Args[key].Item2;
    }
    // ParentClass = parentType;
  }
  // public Type ParentClass { get; }

  public string CacheId { get; }

  public IExecutionPlan Plan { get; }

  public Dictionary<string, Tuple<ZTypeDescriptor, object?>> Args { get; }

  public string? ResponseData { get; set; }
}

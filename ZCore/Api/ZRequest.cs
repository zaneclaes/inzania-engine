#region

using System;
using System.Threading.Tasks;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Api;

public abstract class ZRequestBase : LogicBase {
  protected ZRequestBase(IZContext context) : base(context) {
    // Request = context;
  }

  protected IZResult<TData> Result<TData>(string name, Func<ExecutionPlan, TData> action, params object?[] args) =>
    new ZResult<TData>(Context, GetType(), name, action, args);
    // Request.Result(GetType(), name, action, args);

  protected IZResult<TData> Result<TData>(string name, Func<ExecutionPlan, Task<TData>> action, params object?[] args) =>
    new ZResult<TData>(Context, GetType(), name, action, args);
  //Request.Result(GetType(), name, action, args);
}

#region

using System;
using IZ.Core.Observability.Logging;

#endregion

namespace IZ.Core.Contexts;

public class ActionContext : BaseContext, ITuneChildContext {
  public ActionContext(
    ITuneContext parent, Type? type, string? action = null, ITuneLogger? logger = null
  ) : base(parent.App, null, logger) {
    Parent = parent;
    ScopeType = type;
    Action = action;
    Init();
  }

  public Type? ScopeType { get; }

  public override string? Action { get; }

  public override string Resource => ScopeType?.Name ?? "null";

  public override IServiceProvider ServiceProvider => Parent.ServiceProvider;

  public override ITuneContext Parent { get; }
}

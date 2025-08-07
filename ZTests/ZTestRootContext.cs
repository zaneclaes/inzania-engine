#region

using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;

#endregion

namespace ZTests;

public class ZTestRootContext(ZApp app, IZLogger logger) : RootContext(app, app.CreateServices(), logger) {
  public override IZChildContext ScopeAction(Type? t, string? reason = null, IZLogger? logger = null) =>
    new ZTestActionContext(this, t, reason, logger);
}

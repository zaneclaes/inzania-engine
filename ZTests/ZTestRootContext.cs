using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;

namespace ZTests;

public class ZTestRootContext(ZApp app, IServiceProvider services) : RootContext(app, services) {
  public override IZChildContext ScopeAction(Type? t, string? reason = null, IZLogger? logger = null) =>
    new ZTestActionContext(this, t, reason, logger);
}

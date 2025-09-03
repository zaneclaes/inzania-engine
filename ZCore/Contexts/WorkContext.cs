using System;

namespace IZ.Core.Contexts;

public class WorkContext : RootContext, IZBackgroundContext {
  public override string Resource => _resource ?? base.Resource;
  private readonly string? _resource;

  public WorkContext(ZApp app, IServiceProvider? services = null) : base(app, services ?? app.CreateServices()) { }

  public WorkContext(ZApp app, string reason) : base(app, app.CreateServices()) {
    _resource = reason;
  }
}

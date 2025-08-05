using System;

namespace IZ.Core.Contexts;

public class WorkContext : RootContext, IZBackgroundContext {
  public WorkContext(ZApp app, IServiceProvider? services = null) : base(app, services ?? app.CreateServices()) { }
}

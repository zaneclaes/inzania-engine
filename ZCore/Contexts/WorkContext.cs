using System;

namespace IZ.Core.Contexts;

public class WorkContext : RootContext, IZBackgroundContext {
  public WorkContext(ZApp app, IServiceProvider services) : base(app, services) { }
}

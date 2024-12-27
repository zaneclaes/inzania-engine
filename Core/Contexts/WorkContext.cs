using System;

namespace IZ.Core.Contexts;

public class WorkContext : RootContext, ITuneBackgroundContext {
  public WorkContext(ZApp app, IServiceProvider services) : base(app, services) { }
}

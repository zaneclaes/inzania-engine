#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using IZ.Core.Api;
using IZ.Core.Auth;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;
using IZ.Core.Observability.Analytics;
using IZ.Core.Observability.Logging;
using IZ.Core.Observability.Metrics;
using IZ.Core.Utils;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Core.Contexts;

public class RootContext : BaseContext, IZRootContext {

  public RootContext(
    ZApp app, IServiceProvider services
  ) : base(app, services) {
    Init();
  }

  public override IZResolver Resolver => _resolver ??=
    ServiceProvider.GetService<IProvideRootContext>()?.GetResolver(this) ?? new ZDefaultResolver(this);

  private IZResolver? _resolver;

  public override void Dispose() {
    base.Dispose();
  }
}

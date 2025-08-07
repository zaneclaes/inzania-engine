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
    ZApp app, IServiceProvider services, IZLogger? logger = null
  ) : base(app, services, logger) {
    Init();
    // Log.Information("[ROOT] created {type}#{id}: {stack}", GetType().Name, _uuid, new ZTrace());
  }

  public override IZResolver Resolver => _resolver ??= ServiceProvider.GetRequiredService<IZResolver>();

  private IZResolver? _resolver;

  public override void Dispose() {
    base.Dispose();
  }
}

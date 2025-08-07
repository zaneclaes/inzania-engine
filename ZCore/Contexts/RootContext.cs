#region

using System;
using IZ.Core.Data;
using IZ.Core.Observability.Logging;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Core.Contexts;

public class RootContext : BaseContext, IZRootContext {

  private IZResolver? _resolver;

  public RootContext(
    ZApp app, IServiceProvider services, IZLogger? logger = null
  ) : base(app, services, logger) {
    Init();
    // Log.Information("[ROOT] created {type}#{id}: {stack}", GetType().Name, _uuid, new ZTrace());
  }

  public override IZResolver Resolver => _resolver ??= ServiceProvider.GetRequiredService<IZResolver>();

  public override void Dispose() {
    base.Dispose();
  }
}

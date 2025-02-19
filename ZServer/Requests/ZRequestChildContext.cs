#region

using System;
using IZ.Core;
using IZ.Core.Contexts;

#endregion

namespace IZ.Server.Requests;

public class ZRequestChildContext : BaseContext, IZChildContext {

  private readonly HostContext _root;

  public ZRequestChildContext(
    HostContext parent
  ) : base(parent.App) {
    _root = parent;
    Init();
  }
  public override IZContext Parent => _root;

  public override IServiceProvider ServiceProvider => _root.ServiceProvider;
}

#region

using System;
using IZ.Core;
using IZ.Core.Contexts;

#endregion

namespace IZ.Server.Requests;

public class TuneRequestChildContext : BaseContext, ITuneChildContext {

  private readonly HostContext _root;

  public TuneRequestChildContext(
    HostContext parent
  ) : base(parent.App) {
    _root = parent;
    Init();
  }
  public override ITuneContext Parent => _root;

  public override IServiceProvider ServiceProvider => _root.ServiceProvider;
}

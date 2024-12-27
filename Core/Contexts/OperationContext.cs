#region

using IZ.Core.Auth;
using IZ.Core.Data.Attributes;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Core.Contexts;

[ApiDocs("[DI] Transient: ALWAYS spawned as a child of the root")]
public class OperationContext : BaseContext, ITuneChildContext {
  public override ITuneIdentity? CurrentIdentity => _root.CurrentIdentity;

  private readonly ITuneRootContext _root;

  private readonly IServiceScope? _scope;

  public OperationContext(
    ITuneRootContext parent, IServiceScope? scope = null
  ) : base(parent.App, scope?.ServiceProvider ?? parent.ServiceProvider) {
    _root = parent;
    _scope = scope;
    Init();
  }

  public override ITuneContext Parent => _root;

  public override void Dispose() {
    base.Dispose();
    // _scope?.Dispose();
  }
}

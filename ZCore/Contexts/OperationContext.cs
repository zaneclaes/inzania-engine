#region

using IZ.Core.Auth;
using IZ.Core.Data.Attributes;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Core.Contexts;

[ApiDocs("[DI] Transient: ALWAYS spawned as a child of the root")]
public class OperationContext : BaseContext, IZChildContext {

  private readonly IZRootContext _root;

  private readonly IServiceScope? _scope;

  public OperationContext(
    IZRootContext parent, IServiceScope? scope = null
  ) : base(parent.App, scope?.ServiceProvider ?? parent.ServiceProvider) {
    _root = parent;
    _scope = scope;
    Init();
  }
  public override IZIdentity? CurrentIdentity => _root.CurrentIdentity;

  public override IZContext Parent => _root;

  public override void Dispose() {
    base.Dispose();
    // _scope?.Dispose();
  }
}

using IZ.Core.Auth;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Contexts;

[ApiDocs("[DI] Transient: ALWAYS spawned as a child of the root")]
public class ExecutionContext : BaseContext, IZChildContext {

  private readonly IZRootContext _root;

  public ExecutionContext(IZRootContext parent) : base(parent.App) {
    _root = parent;
    Init();
  }
  public override IZIdentity? CurrentIdentity => _root.CurrentIdentity;

  public override IZContext Parent => _root;
}

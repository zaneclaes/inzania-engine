using System.Diagnostics;
using IZ.Core.Auth;
using IZ.Core.Data.Attributes;
using IZ.Core.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace IZ.Core.Contexts;

[ApiDocs("[DI] Transient: ALWAYS spawned as a child of the root")]
public class ExecutionContext : BaseContext, IZChildContext {
  public override IZIdentity? CurrentIdentity => _root.CurrentIdentity;

  private readonly IZRootContext _root;

  public ExecutionContext(IZRootContext parent) : base(parent.App) {
    _root = parent;
    Init();
  }

  public override IZContext Parent => _root;
}


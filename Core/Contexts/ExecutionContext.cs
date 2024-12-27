using System.Diagnostics;
using IZ.Core.Auth;
using IZ.Core.Data.Attributes;
using IZ.Core.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace IZ.Core.Contexts;

[ApiDocs("[DI] Transient: ALWAYS spawned as a child of the root")]
public class ExecutionContext : BaseContext, ITuneChildContext {
  public override ITuneIdentity? CurrentIdentity => _root.CurrentIdentity;

  private readonly ITuneRootContext _root;

  public ExecutionContext(ITuneRootContext parent) : base(parent.App) {
    _root = parent;
    Init();
    // Log.Information("TRACE {stack}", new TuneTrace(new StackTrace().ToString()).ToString());
  }

  public override ITuneContext Parent => _root;
}


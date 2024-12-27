#region

using System;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Contexts;

[ApiDocs("[DI] Scoped: always spawned as a child of the current context")]
public class AutoContext : BaseContext, ITuneChildContext {

  public AutoContext(
    ITuneContext parent
  ) : base(parent.App, parent.ServiceProvider) {
    Parent = parent;
    Init();
  }

  public override ITuneContext Parent { get; }

  public override IServiceProvider ServiceProvider => Parent.ServiceProvider;
}

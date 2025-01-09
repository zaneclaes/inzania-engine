#region

using System;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Contexts;

[ApiDocs("[DI] Scoped: always spawned as a child of the current context")]
public class AutoContext : BaseContext, IZChildContext {

  public AutoContext(
    IZContext parent
  ) : base(parent.App, parent.ServiceProvider) {
    Parent = parent;
    Init();
  }

  public override IZContext Parent { get; }

  public override IServiceProvider ServiceProvider => Parent.ServiceProvider;
}

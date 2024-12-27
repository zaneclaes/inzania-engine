using System;
using IZ.Core.Data;

namespace IZ.Core.Contexts;

public interface IProvideRootContext {
  public ITuneRootContext? GetRootContext(IServiceProvider sp);

  public ITuneResolver? GetResolver(ITuneContext context);
}

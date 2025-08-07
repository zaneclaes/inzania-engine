using System;

namespace IZ.Core.Contexts;

public interface IProvideRootContext {
  public IZRootContext? GetRootContext(IServiceProvider sp);

  // public IZResolver? GetResolver(IZContext context);
}

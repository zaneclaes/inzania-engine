#region

using System;
using System.Threading.Tasks;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Utils;

public interface IForeverTask : IHaveContext {
  public Task RunTask(TimeSpan dt);
}

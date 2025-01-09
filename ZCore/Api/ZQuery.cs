#region

#endregion

using IZ.Core.Contexts;

namespace IZ.Core.Api;

public abstract class ZQueryBase : ZRequestBase {
  protected ZQueryBase(IZContext context) : base(context) { }
}

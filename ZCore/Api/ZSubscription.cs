using IZ.Core.Contexts;

namespace IZ.Core.Api;

public abstract class ZSubscriptionBase : ZRequestBase {
  protected ZSubscriptionBase(IZContext context) : base(context) { }
}

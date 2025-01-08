#region

using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Api;

public abstract class ZMutationBase : ZRequestBase {
  protected ZMutationBase(ITuneContext context) : base(context) { }
}

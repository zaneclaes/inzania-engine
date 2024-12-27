#region

using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Api;

public abstract class TuneMutationBase : TuneRequestBase {
  protected TuneMutationBase(ITuneContext context) : base(context) { }
}

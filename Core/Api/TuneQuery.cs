#region

#endregion

using IZ.Core.Contexts;

namespace IZ.Core.Api;

public abstract class TuneQueryBase : TuneRequestBase {
  protected TuneQueryBase(ITuneContext context) : base(context) { }
}

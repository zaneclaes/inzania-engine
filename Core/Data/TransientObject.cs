#region

using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Data;

[ApiDocs("An object which may not be in a database")]
public abstract class TransientObject : ApiObject {
  protected TransientObject(ITuneContext? context = null) : base(context) { }

  protected override string ContextualObjectGroup => "Transient";

  private static ITuneContext TransientContext => _transientContext ??= IZEnv.SpawnRootContext().ScopeAction(typeof(TransientObject), "Transient");
  private static ITuneContext? _transientContext;

  protected override ITuneContext SpawnInContext(ITuneContext? context) => context ?? TransientContext;
}

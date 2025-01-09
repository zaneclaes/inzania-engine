#region

using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Data;

[ApiDocs("An object which may not be in a database")]
public abstract class TransientObject : ApiObject {
  protected TransientObject(IZContext? context = null) : base(context) { }

  protected override string ContextualObjectGroup => "Transient";

  private static IZContext TransientContext => _transientContext ??= ZEnv.SpawnRootContext().ScopeAction(typeof(TransientObject), "Transient");
  private static IZContext? _transientContext;

  protected override IZContext SpawnInContext(IZContext? context) => context ?? TransientContext;
}

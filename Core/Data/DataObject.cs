#region

using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Data;

[ApiDocs("Allowed to be stored in a database")]
public abstract class DataObject : ApiObject {
  protected DataObject(ITuneContext? context = null) : base(context) { }

  protected override string ContextualObjectGroup => "Data";

}

using IZ.Core.Contexts;
using IZ.Core.Data;

namespace IZ.Core.Api;

public abstract class ZDataObjectManipulator<TD> : ApiObject where TD : DataObject, new() {
  protected TD DataObject { get; }

  public ZDataObjectManipulator(TD dataObject) : base(dataObject.Context) {
    DataObject = dataObject;
  }
}

public abstract class ZDataModelManipulator<TD> : ZDataObjectManipulator<TD> where TD : ModelId, new() {
  public ZDataModelManipulator(TD dataObject) : base(dataObject) {}
}

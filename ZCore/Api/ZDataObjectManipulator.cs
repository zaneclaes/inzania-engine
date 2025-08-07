using IZ.Core.Data;

namespace IZ.Core.Api;

public abstract class ZDataObjectManipulator<TD> : ApiObject where TD : DataObject, new() {

  public ZDataObjectManipulator(TD dataObject) : base(dataObject.Context) {
    DataObject = dataObject;
  }
  protected TD DataObject { get; }
}

public abstract class ZDataModelManipulator<TD> : ZDataObjectManipulator<TD> where TD : ModelId, new() {
  public ZDataModelManipulator(TD dataObject) : base(dataObject) { }
}

using System;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;

namespace IZ.Core.Data.Seeds;

public abstract class DataStub {
}

public abstract class DataStub<TD> : DataStub where TD : DataObject {
  public TD Data => _data ??= Stub(DataSeed.DataContext);
  private TD? _data;

  public DataStub(TD? data = null) {
    _data = data;
  }

  public virtual TD Stub(IZContext context) => throw new NotImplementedException(nameof(Stub));

  public virtual void Update(TD stub) {
    var desc = ZTypeDescriptor.FromType(typeof(TD));
    foreach (var p in desc.ObjectDescriptor.ScalarProperties) {
      if (!p.Value.IsSettable) continue;
      p.Value.SetValue(Data, p.Value.GetValue(stub));
    }
  }
}

using System;
using System.Collections.Generic;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;

namespace IZ.Core.Data.Seeds;

public abstract class DataStub { }

public abstract class DataStub<TD> : DataStub, IItemizable where TD : DataObject {
  private TD? _data;

  public DataStub(TD? data = null) {
    _data = data;
  }
  public abstract string DataId { get; }

  public TD Data => _data ??= Stub(DataSeed.DataContext);
  public string ItemId => DataId;

  public TD Stub(IZContext context) => _data ??= CreateStub(context);

  protected virtual TD CreateStub(IZContext context) => throw new NotImplementedException($"{GetType().Name}.{nameof(Stub)}");

  public virtual void Update(TD stub) {
    var desc = ZTypeDescriptor.FromType(typeof(TD));
    foreach (KeyValuePair<string, ZPropertyDescriptor> p in desc.ObjectDescriptor.ScalarProperties) {
      if (!p.Value.IsSettable) continue;
      p.Value.SetValue(Data, p.Value.GetValue(stub));
    }
  }
}

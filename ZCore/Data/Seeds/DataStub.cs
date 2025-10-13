using System;
using System.Collections.Generic;
using System.Linq;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;

namespace IZ.Core.Data.Seeds;

public abstract class DataStub { }

public abstract class DataStub<TD> : DataStub, IItemizable where TD : DataObject {
  public abstract string DataId { get; }

  public string ItemId => DataId;

  public TD StubData => _stubData ??= Stub(DataSeed.DataContext);
  private TD? _stubData;

  public TD? DbData { get; private set; }

  public TD Data => DbData ?? StubData;

  private ZTypeDescriptor TypeDesc => _desc ??= ZTypeDescriptor.FromType(typeof(TD));
  private ZTypeDescriptor? _desc = null;

  protected DataStub(TD? data = null) {
    _stubData = data;
  }

  public TD Stub(IZContext context) => _stubData ??= CreateStub(context);

  protected virtual TD CreateStub(IZContext context) => throw new NotImplementedException($"{GetType().Name}.{nameof(Stub)}");

  private void CopyStubDataValues(TD destData) {
    var props = TypeDesc.ObjectDescriptor.ScalarProperties.Values.ToList();
    foreach (var desc in props) {
      if (!desc.IsSettable) continue;
      desc.SetValue(destData, desc.GetValue(StubData));
    }
  }

  public virtual void Update(TD? dbData = null) {
    if (dbData != null) DbData = dbData;
    if (DbData != null) CopyStubDataValues(DbData);
  }
}

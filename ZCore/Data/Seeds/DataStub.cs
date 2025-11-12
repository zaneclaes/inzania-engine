using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;

namespace IZ.Core.Data.Seeds;

public abstract class DataStub { }

public abstract class DataStub<TD> : DataStub, IItemizable where TD : DataObject {
  public abstract string ItemId { get; }

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

  protected virtual void CopyStubDataValues(TD destData) {
    var props = TypeDesc.ObjectDescriptor.ScalarProperties.Values.ToList();
    foreach (var desc in props) {
      if (!desc.IsSettable) continue;
      desc.SetValue(destData, desc.GetValue(StubData));
    }
  }

  public TD GetInsert() => StubData;

  public virtual Task UpdateDataModel(IZContext context, TD? dbData = null) {
    if (dbData != null) DbData = dbData;
    if (DbData != null) CopyStubDataValues(DbData);
    return Task.CompletedTask;
  }
}

public abstract class DataStubId<TD> : DataStub<TD> where TD : DataObject, IStringKeyData {
  public override string ItemId => _id ?? StubData.Id;
  protected string? _id;

  protected DataStubId(TD? data = null) : base(data) { }

  protected DataStubId(string id, TD? data = null) : base(data) {
    _id = id;
  }
}

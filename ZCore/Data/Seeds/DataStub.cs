using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IZ.Core.Api;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;
using IZ.Core.Json;

namespace IZ.Core.Data.Seeds;

public abstract class DataStub { }

public abstract class DataStub<TD> : DataStub, IItemizable where TD : DataObject {
  public abstract string ItemId { get; }

  public TD StubData => _stubData ??= Stub(DataSeed.DataContext);
  private TD? _stubData;

  public TD? DbData { get; private set; }

  public TD Data => DbData ?? StubData;

  private ZTypeDescriptor TypeDesc => _desc ??= ZApi.LoadTypeDescriptor(typeof(TD));
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

  public virtual TD GetInsert(IZContext context) => StubData;

  // Called for both Insert and Update
  protected virtual Task UpdateDataModel(IZContext context, TD dbData, bool isInsert) {
    CopyStubDataValues(dbData);
    return Task.CompletedTask;
  }

  public async Task<TD> Upsert(IZContext context, TD? dbData = null, Action<TD>? onInsert = null) {
    if (dbData != null) DbData = dbData;
    bool isInsert = false;
    if (DbData == null) {
      DbData = GetInsert(context);
      await context.Data.AddAsync(DbData);
      isInsert = true;
      onInsert?.Invoke(DbData);
    }
    await UpdateDataModel(context, DbData, isInsert);
    return DbData;
  }

  protected TM LoadMetaData<TM>(IZContext context, string fpMeta, bool create = true) where TM : TransientObject, new() {
    if (File.Exists(fpMeta)) {
      try {
        return ZJson.DeserializeObject<TM>(File.ReadAllText(fpMeta))!;
      } catch (Exception) {
        context.Log.Error("Failed to load {fp}", fpMeta);
        throw;
      }
    } else {
      var meta = new TM() {Context = context};
      if (create) File.WriteAllText(fpMeta, ZJson.SerializeObject(meta));
      return meta;
    }
  }
}

public abstract class DataStubId<TD> : DataStub<TD> where TD : DataObject, IStringKeyData, new() {
  public override string ItemId => _id ?? StubData.Id;
  protected string? _id;

  protected DataStubId(TD? data = null) : base(data) { }

  protected override TD CreateStub(IZContext context) => new TD {
    Context = context,
    Id = ItemId,
  };

  protected DataStubId(string id, TD? data = null) : base(data) {
    _id = id;
  }
}

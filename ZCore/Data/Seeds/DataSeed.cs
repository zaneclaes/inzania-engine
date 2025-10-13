#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Observability;
using IZ.Core.Observability.Logging;

#endregion

namespace IZ.Core.Data.Seeds;

public abstract class DataSeed : IHaveContext {
  private static IZContext? _dataContext;
  public static IZContext DataContext => _dataContext ??= ZEnv.SpawnRootContext();

  public virtual bool ReSeed => false;

  public bool IsStubbed { get; private set; }

  public IZContext Context { get; set; } = null!;
  public IZLogger Log { get; set; } = null!;

  public async Task SeedDatabase(IZContext context) {
    _dataContext = Context = context;
    Log = context.Log.ForContext(GetType());
    try {
      var sw = Stopwatch.StartNew();
      await Exec();
      await Context.Data.SaveIfNeededAsync();
      context.IncrementMetric($"{ZMetrics.SysGroup}.seed");
      Log.Information("[SEED] {type} ran in {ms}ms", GetType().Name, sw.ElapsedMilliseconds);
    } catch (Exception e) {
      Log.Error(e, "[SEED] {type} failed", GetType().Name);
    }
  }

  public void StubLibrary(IZContext context) {
    Context = context;
    Log = context.Log.ForContext(GetType());
    // Stopwatch sw = Stopwatch.StartNew();
    // Stub();
    IsStubbed = true;
    // Log.Information("[SEED] {type} stubbed in {ms}ms", GetType(), sw.ElapsedMilliseconds);
  }

  protected abstract Task Exec();

  protected abstract void Stub();
}

public abstract class DataSeed<TD> : DataSeed where TD : ModelId {

  // The best version of a model (db if available, else stub data)
  protected Dictionary<string, TD> Models { get; set; } = new Dictionary<string, TD>();
  protected abstract List<DataStub<TD>> GetStubs();

  // protected List<TD> Models { get; set; } = new List<TD>();

  protected virtual async Task ProcessExisting(List<TD> existing) {
    if (!existing.Any()) return;

    if (ReSeed) {
      await Context.Data.RemoveAsync(existing.ToArray());
      await Context.Data.SaveAsync();
      existing.Clear();
    }
  }

  protected virtual IZQueryable<TD> FetchQuery() => Context.QueryFor<TD>();

  protected virtual void SetModel(TD model) {
    Models[model.Id] = model;
  }

  // protected TD GetOrCreateModel(string title, Func<string, TD> creator, string? subtitle = null) {
  //   string key = title + (subtitle ?? "");
  //   var ret = Models.GetValueOrDefault(key);
  //   if (ret == null) {
  //     ret = creator(title);
  //     SetModel(ret);
  //   }
  //   return ret;
  // }

  private async Task<List<TD>> SeedModelIds(List<DataStub<TD>> stubs, List<TD>? existing = null) {
    string[] seedIds = stubs.Select(p => p.DataId).ToArray();
    // ZEnv.Log.Information("SEED LOOK FOR {ids}", seedIds.ToList());
    existing ??= await FetchQuery().Filter(p => seedIds.Contains(p.Id)).LoadDataModelsAsync();
    List<TD> models = existing.ToList();

    await ProcessExisting(existing);
    foreach (DataStub<TD> stub in stubs) {
      var e = existing.FirstOrDefault(e => e.Id.Equals(stub.DataId));
      if (e == null) {
        await Context.Data.AddAsync(stub.StubData);
        models.Add(stub.StubData);
      } else {
        stub.Update(e);
      }
      SetModel(e ?? stub.StubData);
    }
    await Context.Data.SaveIfNeededAsync();
    return models;
  }

  private List<DataStub<TD>> PrepareStubs() {
    List<DataStub<TD>> stubs = GetStubs();
    HashSet<string> ids = new HashSet<string>();
    foreach (DataStub<TD> s in stubs)
      if (!ids.Add(s.DataId))
        throw new ArgumentException($"Duplicate Seed<{typeof(TD)}>: {s.StubData.Id}");
    // ZEnv.Log.Information("SEED STUBS {t}", ids);
    return stubs;
  }

  protected override async Task Exec() {
    // ZEnv.Log.Information("SEED START {t}", GetType().Name);
    await SeedModelIds(PrepareStubs());
  }

  protected override void Stub() {
    // Models = PrepareStubs();
  }
}

// public abstract class DataSeed<TData> : DataSeed where TData : DataObject { }

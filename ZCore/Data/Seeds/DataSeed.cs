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

public interface IDataSeed : IHaveContext {
  public bool StubOnClient { get; }

  public Task SeedDatabase(IZContext context);

  public void StubLibrary(IZContext context);
}

public abstract class DataSeed : IDataSeed {
  private static IZContext? _dataContext;
  public static IZContext DataContext => _dataContext ??= ZEnv.SpawnRootContext();

  public virtual bool ReSeed => false;

  public bool IsSeeded { get; protected set; }
  public bool IsStubbed { get; private set; }

  public IZContext Context { get; set; } = null!;
  public IZLogger Log { get; set; } = null!;

  public virtual bool StubOnClient => false;

  public async Task SeedDatabase(IZContext context) {
    _dataContext = Context = context;
    Log = context.Log.ForContext(GetType());
    try {
      await Exec();
      await Context.Data.SaveIfNeededAsync();
      context.IncrementMetric($"{ZMetrics.SysGroup}.seed.{GetType().Name}");
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

public abstract class DataSeed<TD, TS> : DataSeed where TD : ModelId where TS : DataStub<TD> {
  // Only valid once IsStubbed/IsSeeded
  public List<TS> Stubs { get; } = new List<TS>();

  // The best version of a model (db if available, else stub data)
  protected Dictionary<string, TD> Models { get; set; } = new Dictionary<string, TD>();
  protected abstract Task<List<TS>> GetStubs();

  public List<TD> DbModels => Models.Values.ToList();

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

  public void Clear() {
    Models.Clear();
    Stubs.Clear();
    IsSeeded = false;
  }

  public async Task ReExec(IZContext context) {
    Context = context;
    Clear();
    await Exec();
    await Context.Data.SaveAsync();
  }

  private async Task SeedModelIds(List<TS> stubs, List<TD>? existing = null) {
    string[] seedIds = stubs.Select(p => p.ItemId).ToArray();
    // ZEnv.Log.Information("SEED LOOK FOR {ids}", seedIds.ToList());
    existing ??= await FetchQuery().Filter(p => seedIds.Contains(p.Id)).LoadDataModelsAsync();

    await ProcessExisting(existing);
    foreach (TS stub in stubs) {
      var dbModel = existing.FirstOrDefault(e => e.Id.Equals(stub.ItemId));
      dbModel = await stub.Upsert(Context, dbModel);
      SetModel(dbModel);
    }
    await Context.Data.SaveIfNeededAsync();
    IsSeeded = true;
  }

  private async Task<List<TS>> PrepareStubs() {
    if (!Stubs.Any()) {
      Stubs.AddRange(await GetStubs());
      HashSet<string> ids = new HashSet<string>();
      foreach (TS s in Stubs)
        if (!ids.Add(s.ItemId))
          throw new ArgumentException($"Duplicate Seed<{typeof(TD)}>: {s.StubData.Id}");
      // ZEnv.Log.Information("SEED STUBS {t}", ids);
    }
    return Stubs;
  }

  protected override async Task Exec() {
    // ZEnv.Log.Information("SEED START {t}", GetType().Name);
    await SeedModelIds(await PrepareStubs());
  }

  protected override void Stub() {
    // Models = PrepareStubs();
  }
}


// public abstract class DataSeed<TData> : DataSeed where TData : DataObject { }

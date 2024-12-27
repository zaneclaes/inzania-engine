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
  public static ITuneContext DataContext => _dataContext ??= IZEnv.SpawnRootContext();
  private static ITuneContext? _dataContext;

  public ITuneContext Context { get; set; } = default!;
  public ITuneLogger Log { get; set; } = default!;

  public virtual bool ReSeed => false;

  public async Task SeedDatabase(ITuneContext context) {
    _dataContext = Context = context;
    Log = context.Log.ForContext(GetType());
    var sw = Stopwatch.StartNew();
    await Exec();
    await Context.Data.SaveIfNeededAsync();
    context.IncrementMetric($"{TuneMetrics.SysGroup}.seed");
    Log.Information("[SEED] {type} ran in {ms}ms", GetType(), sw.ElapsedMilliseconds);
  }

  public bool IsStubbed { get; private set; }

  public void StubLibrary(ITuneContext context) {
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
  protected abstract List<DataStub<TD>> GetStubs();

  // protected List<TD> Models { get; set; } = new List<TD>();

  private async Task ProcessExisting(List<TD> existing) {
    if (!existing.Any()) return;

    if (ReSeed) {
      await Context.Data.RemoveAsync(existing.ToArray());
      await Context.Data.SaveAsync();
      existing.Clear();
    }
  }

  protected virtual ITuneQueryable<TD> GetQuery() => Context.QueryFor<TD>();

  protected Dictionary<string, TD> Models { get; set; } = new Dictionary<string, TD>();

  protected virtual void SetModel(TD model) {
    Models[model.Id] = model;
  }

  public TD GetOrCreateModel(string title, Func<string, TD> creator, string? subtitle = null) {
    string key = title + (subtitle ?? "");
    var ret = Models.GetValueOrDefault(key);
    if (ret == null) {
      ret = creator(title);
      SetModel(ret);
    }
    return ret;
  }

  private async Task<List<TD>> SeedModelIds(List<DataStub<TD>> stubs, List<TD>? existing = null) {
    var seedIds = stubs.Select(p => p.Data.Id).ToArray();
    existing ??= await GetQuery()
      .Filter(p => seedIds.Contains(p.Id))
      .LoadDataModelsAsync();
    List<TD> models = existing.ToList();

    await ProcessExisting(existing);
    foreach (var stub in stubs) {
      var e = existing.FirstOrDefault(e => e.Id.Equals(stub.Data.Id));
      if (e == null) {
        await Context.Data.AddAsync(stub.Data);
        models.Add(stub.Data);
      } else {
        stub.Update(e);
      }
      SetModel(e ?? stub.Data);
    }
    await Context.Data.SaveIfNeededAsync();
    return models;
  }

  private List<DataStub<TD>> PrepareStubs() {
    List<DataStub<TD>> stubs = GetStubs();
    HashSet<string> ids = new HashSet<string>();
    foreach (var s in stubs)
      if (!ids.Add(s.Data.Id))
        throw new ArgumentException($"Duplicate Seed<{typeof(TD)}>: {s.Data.Id}");
    return stubs;
  }

  protected override async Task Exec() {
    await SeedModelIds(PrepareStubs());
  }

  protected override void Stub() {
    // Models = PrepareStubs();
  }
}

// public abstract class DataSeed<TData> : DataSeed where TData : DataObject { }

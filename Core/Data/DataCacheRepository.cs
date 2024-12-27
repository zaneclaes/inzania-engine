#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using IZ.Core.Api;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Data;

// TODO: replace with EFCore memory cache?
public class DataCacheRepository : DataRepositoryBase, ITuneDataRepository {
  private static readonly Dictionary<Type, DataCache> Caches = new Dictionary<Type, DataCache>();

  public DataCacheRepository(ITuneContext context) : base(context) { }

  public void Initialize() { }

  public ITuneQueryable<TData> QueryFor<TData>(ITuneContext context, ResultSet? set = null) where TData : DataObject {
    if (!Caches.ContainsKey(typeof(TData))) Caches[typeof(TData)] = new DataCache<TData>(this);
    // Log.Information("[CACHE] {type} {@cache}", typeof(TData).Name, Caches[typeof(Song)]);
    return (Caches[typeof(TData)] as ITuneQueryable<TData>)!;
  }
  public ITuneQueryProvider GetQueryProvider<TData>(ITuneContext context, IQueryable<TData> database) where TData : DataObject => throw new NotImplementedException();
  public Task<long> ExecuteSumAsync<TData>(ITuneContext context, IQueryable<TData> q, Expression<Func<TData, long>> func) => throw new NotImplementedException();
  public Task<long> ExecuteCountAsync<TData>(ITuneContext context, IQueryable<TData> q) => throw new NotImplementedException();
  public Task<TData?> ExecuteFirstOrDefaultAsync<TData>(ITuneContext context, IQueryable<TData> q) => throw new NotImplementedException();
  public Task<List<TData>> ExecuteListAsync<TData>(ITuneContext context, IQueryable<TData> q) => throw new NotImplementedException();
  public IPreFetched<TEntity, TProperty> QueryInclude<TEntity, TProperty>(ITuneQueryable<TEntity> source, Expression<Func<TEntity, TProperty>> navigationPropertyPath) where TEntity : class => throw new NotImplementedException();
  public IPreFetched<TEntity, TProperty> QueryThenInclude<TEntity, TPreviousProperty, TProperty>(IPreFetched<TEntity, TPreviousProperty> source, Expression<Func<TPreviousProperty, TProperty>> navigationPropertyPath) where TEntity : class => throw new NotImplementedException();
  public IPreFetched<TEntity, TProperty> QueryThenIncludeMany<TEntity, TPreviousProperty, TProperty>(IPreFetched<TEntity, List<TPreviousProperty>> source, Expression<Func<TPreviousProperty, TProperty>> navigationPropertyPath) where TEntity : class => throw new NotImplementedException();

  public Task SaveAsync(CancellationToken ct = new CancellationToken()) => Task.CompletedTask;

  public Task AddAsync<TData>(params TData[] data) where TData : DataObject => throw new NotImplementedException();
  public Task RemoveAsync<TData>(params TData[] data) where TData : DataObject => throw new NotImplementedException();
  public Task SanitizeAsync() => throw new NotImplementedException();
  public void SetChanged<TData>(params TData[] data) where TData : DataObject {
    throw new NotImplementedException();
  }
  public bool HasChanges => false;
/*
  public static void CacheCurrentUserScores(ITuneContext context, params Score[] scores) {
    if (!Caches.ContainsKey(typeof(Score))) Caches[typeof(Score)] = new DataCache<Score>(context.Data);
    foreach (var score in scores) Caches[typeof(Score)].Cache(score);
  }*/
}

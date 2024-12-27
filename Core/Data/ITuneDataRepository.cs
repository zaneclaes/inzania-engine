#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using IZ.Core.Api;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Data;

public interface ITuneDataRepository : IHaveContext {
  public string Uuid { get; }

  [ApiDocs("Migrate, etc.")]
  public void Initialize();

  [ApiDocs("Get a queryable object for a data type (falls back on in-memory cache)")]
  public ITuneQueryable<TData> QueryFor<TData>(ITuneContext context, ResultSet? set = null) where TData : DataObject;

  [ApiDocs("Wrap a loader in a semaphore for thread safety")]
  public Task<TData> ExecuteLocked<TData>(Func<Task<TData>> loader);

  public Task<long> ExecuteSumAsync<TData>(ITuneContext context, IQueryable<TData> q, Expression<Func<TData, long>> func);

  public Task<long> ExecuteCountAsync<TData>(ITuneContext context, IQueryable<TData> q);

  public Task<TData?> ExecuteFirstOrDefaultAsync<TData>(ITuneContext context, IQueryable<TData> q);

  public Task<List<TData>> ExecuteListAsync<TData>(ITuneContext context, IQueryable<TData> q);

  IPreFetched<TEntity, TProperty> QueryInclude<TEntity, TProperty>(
    ITuneQueryable<TEntity> source,
    Expression<Func<TEntity, TProperty>> navigationPropertyPath)
    where TEntity : class;

  public IPreFetched<TEntity, TProperty> QueryThenInclude<TEntity, TPreviousProperty, TProperty>(
    IPreFetched<TEntity, TPreviousProperty> source,
    Expression<Func<TPreviousProperty, TProperty>> navigationPropertyPath)
    where TEntity : class;

  public IPreFetched<TEntity, TProperty> QueryThenIncludeMany<TEntity, TPreviousProperty, TProperty>(
    IPreFetched<TEntity, List<TPreviousProperty>> source,
    Expression<Func<TPreviousProperty, TProperty>> navigationPropertyPath)
    where TEntity : class;

  [ApiDocs("Save any changes")]
  public Task SaveAsync(CancellationToken ct = new CancellationToken());

  public Task AddAsync<TData>(params TData[] data) where TData : DataObject;

  public Task RemoveAsync<TData>(params TData[] data) where TData : DataObject;

  // public void SetChanged<TData>(params TData[] data) where TData : DataObject;

  public bool HasChanges { get; }
}

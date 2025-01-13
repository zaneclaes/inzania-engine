#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Data;

public enum DataState {
  None,
  Created = 1,
  Updated
}

public static class DataModelLoader {
  private static IZDataRepository GetRepository(this IQueryable q, IZContext? context = null) {
    if (q is IZQueryable tq) return tq.Repository;
    bool spawnedDefault = context == null;
    context ??= ZEnv.SpawnRootContext();
    if (spawnedDefault) context.Log.Warning("[DATA] spawned context for {q}", q.GetType().GenericTypeArguments.First().Name);
    return context.Data;
  }

  private static IZContext GetContext(this IQueryable q, IZContext? context = null) {
    if (q is IZQueryable tq) return tq.Context;
    if (context == null) {
      context = ZEnv.SpawnRootContext();
      context.Log.Warning("[DATA] spawning default context for {q} ({p}); stacktrace: {stacktrace}",
        q.GetType().GenericTypeArguments.First().Name, q.Expression.GetType(), new ZTrace(new StackTrace().ToString()).ToString());
    }
    return context;
  }

  // private static async Task<List<T>> LoadListAsync<T>(
  //   this IQueryable<T> queryable, IZContext? context = null, bool executeLocked = true
  // ) {
  //   context = queryable.GetContext(context);
  //   var ret = await queryable.GetRepository(context).ExecuteListAsync(context, queryable, executeLocked);
  //   return ret;
  // }

  public static async Task<Dictionary<TK, T>> LoadDataModelDictionary<TK, T>(
    this IQueryable<T> queryable, Func<T, TK> getKey, IZContext? context = null
  ) where T : DataObject where TK : notnull {
    context = queryable.GetContext(context);
    List<T>? ret = await queryable.LoadDataModelsAsync(context);
    return ret.ToDictionary(getKey);
  }

  public static async Task<List<T>> LoadDataModelsAsync<T>(
    this IQueryable<T> queryable, IZContext? context = null) where T : DataObject {
    context = queryable.GetContext(context);
    List<T>? ret = await queryable.GetRepository(context).ExecuteListAsync(context, queryable);
    foreach (var obj in ret) obj.EnforceContext(context);
    return ret;
  }

  public static async Task<ILookup<TKey, TData>> LoadLookupAsync<TKey, TData>(
    this IQueryable<TData> queryable, Func<TData, TKey> lookup, IZContext? context = null
  ) where TData : DataObject {
    List<TData>? ret = await queryable.LoadDataModelsAsync(context);
    return ret.ToLookup(lookup);
  }

  public static async Task<Dictionary<TKey, TData>> LoadDictionaryAsync<TKey, TData>(
    this IQueryable<TData> queryable, Func<TData, TKey> lookup, IZContext? context = null
  ) where TData : DataObject where TKey : notnull {
    List<TData>? ret = await queryable.LoadDataModelsAsync(context);
    return ret.ToDictionary(lookup);
  }

  private static readonly Dictionary<Type, MethodInfo> _arrayContainsMethods = new Dictionary<Type, MethodInfo>();

  public static IZQueryable<TData> FilterKeyIn<TData, TKey>(
    this IZQueryable<TData> queryable, string key, params TKey[] vals
  ) where TData : DataObject {
    if (!_arrayContainsMethods.ContainsKey(typeof(TKey))) {
      _arrayContainsMethods[typeof(TKey)] = typeof(Enumerable).GetMethods().Where(x => x.Name == "Contains").Single(x => x.GetParameters().Length == 2).MakeGenericMethod(typeof(TKey));
    }

    var item = Expression.Parameter(typeof(TData), "item");
    var prop = Expression.Property(item, key);
    var value = Expression.Constant(vals, typeof(IEnumerable<TKey>));
    var clause = Expression.Call(_arrayContainsMethods[typeof(TKey)], value, prop);

    return queryable.Filter(Expression.Lambda<Func<TData, bool>>(clause, item));
  }

  public static Task<T?> LoadScalarAsync<T>(this IQueryable<T> q, IZContext? context = null) {
    context ??= q.GetContext(context);
    return q.GetRepository(context).ExecuteFirstOrDefaultAsync(context, q);
  }

  public static Task<List<T>> LoadScalarsAsync<T>(this IQueryable<T> q, IZContext? context = null) {
    context ??= q.GetContext(context);
    return q.GetRepository(context).ExecuteListAsync(context, q);
  }

  public static async Task<long> LoadSumAsync<T>(
    this IQueryable<T> queryable, Expression<Func<T, long>> func, IZContext? context = null
  ) {
    context = queryable.GetContext(context);
    long ret = await queryable.GetRepository(context).ExecuteSumAsync(context, queryable, func);
    // if (ret != null) ret.SetParentLogger(context.Log);
    return ret;
  }

  public static async Task<long> LoadCountAsync<T>(
    this IQueryable<T> queryable, IZContext? context = null
  ) {
    context = queryable.GetContext(context);
    long ret = await queryable.GetRepository(context).ExecuteCountAsync(context, queryable);
    return ret;
  }

  public static async Task<T?> LoadDataModelAsync<T>(
    this IQueryable<T> queryable, IZContext? context = null
  ) where T : DataObject {
    context = queryable.GetContext(context);
    var repo = queryable.GetRepository(context);
    var ret = await repo.ExecuteFirstOrDefaultAsync(context, queryable);
    if (ret != null) ret.EnforceContext(context);
    return ret;
  }

  public static Task<T> LoadRequiredDataModelAsync<T>(
    this IQueryable<T> queryable, IZContext? context = null
  ) where T : DataObject => queryable.LoadDataModelAsync($"NotFound: {typeof(T)}");

  public static Task<TModel?> LoadModelId<TModel>(
    this IZContext context, string id
  ) where TModel : ModelKey<string> => context.QueryForId<TModel>(id).LoadDataModelAsync(context);

  public static Task<TModel> LoadRequiredModelId<TModel>(
    this IZContext context, string id
  ) where TModel : ModelKey<string> => context.QueryForId<TModel>(id).LoadDataModelAsync($"NotFound: {typeof(TModel)}#{id}", context);

  public static async Task<T> LoadDataModelAsync<T>(
    this IQueryable<T> queryable, string missingException, IZContext? context = null
  ) where T : DataObject {
    context = queryable.GetContext(context);
    var ret = await queryable.LoadDataModelAsync(context);
    return ret ?? throw new KeyNotFoundException(missingException);
  }

  public static Task<TModel> LoadModelId<TModel>(
    this IZContext context, string id, string errorMessage
  ) where TModel : ModelKey<string> => context.QueryForId<TModel>(id).LoadDataModelAsync(errorMessage, context);

  // public static Task<TModel> UpsertModelId<TModel>(
  //   this IZContext context, string id, Func<TModel, DataState, Task>? creator = null
  // ) where TModel : ModelId, new() => UpsertModel(context,
  //   context.QueryFor<TModel>().Where(m => m.Id.Equals(id)), (model, state) => {
  //     if (state == DataState.Created) model.Id = id;
  //     return creator == null ? Task.CompletedTask : creator.Invoke(model, state);
  //   });

  public static IZQueryable<TModel> QueryForModelIds<TModel, TKey>(
    this IZContext context, params TKey[] id
  ) where TModel : ModelKey<TKey>, new() {
    IZQueryable<TModel> q = context.QueryFor<TModel>();
    return q.Where(m => m.Id != null && id.Contains(m.Id)).AsTuneQueryable(q.QueryProvider);
  }

  public static IZQueryable<TModel> QueryForModelId<TModel, TKey>(
    this IZContext context, TKey id
  ) where TModel : ModelKey<TKey> {
    IZQueryable<TModel> q = context.QueryFor<TModel>();
    return q.Where(m => m.Id != null && m.Id.Equals(id)).AsTuneQueryable(q.QueryProvider);
  }

  public static IZQueryable<TModel> QueryForId<TModel>(
    this IZContext context, string id
  ) where TModel : ModelKey<string> => context.QueryForModelId<TModel, string>(id);

  public static IZQueryable<TModel> AsTuneQueryable<TModel>(this IQueryable<TModel> q, IZQueryProvider provider) where TModel : DataObject {
    if (q is IZQueryable<TModel> tq) return tq;
    provider.Log.Warning("[QUERY] converting {type} to ZQueryable<{dm}> via {other}", q.GetType(), typeof(TModel), provider.GetType());
    return new ZQueryable<TModel>(provider, q);
  }

  public static IZQueryable<TSource> Filter<TSource>(this IZQueryable<TSource> q, Expression<Func<TSource, bool>> predicate)
    where TSource : DataObject => q.Where(predicate).AsTuneQueryable(q.QueryProvider);

  public static Task<TModel> Upsert<TModel>(this IZQueryable<TModel> q, Action<TModel> creator) where TModel : DataObject, new() =>
    q.QueryProvider.Context.Upsert(q, creator);

  public static Task<TModel> Upsert<TModel>(
    this IZContext context, IQueryable<TModel> query, Action<TModel> creator
  ) where TModel : DataObject, new() => context.UpsertModel(query, (m, s) => {
    if (s == DataState.Created) creator.Invoke(m);
    return Task.CompletedTask;
  });

  public static Task<TModel> UpsertKey<TModel, TKey>(
    this IZContext context, TKey id, Action<TModel>? creator = null
  ) where TModel : ModelKey<TKey>, new() => context.Upsert(context.QueryForModelId<TModel, TKey>(id), m => {
    m.Id = id;
    if (creator != null) creator.Invoke(m);
  });

  public static Task<TModel> UpsertId<TModel>(
    this IZContext context, string id, Action<TModel>? creator = null
  ) where TModel : ModelKey<string>, new() => context.UpsertKey(id, creator);

  public static async Task<TModel> UpsertModel<TModel>(
    this IZContext context, IQueryable<TModel> query, Func<TModel, DataState, Task>? creator = null
  ) where TModel : DataObject, new() {
    var ret = await query.LoadDataModelAsync(context);
    if (ret != null) {
      ret.EnforceContext(context);
      if (creator != null) await creator.Invoke(ret, DataState.Updated);
      return ret;
    }
    ret = new TModel {
      Context = context
    };
    if (creator != null) await creator.Invoke(ret, DataState.Created);
    await context.Data.AddAsync(ret);
    return ret;
  }

}

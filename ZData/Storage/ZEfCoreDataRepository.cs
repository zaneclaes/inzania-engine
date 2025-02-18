﻿#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using IZ.Core;
using IZ.Core.Api;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Exceptions;
using IZ.Core.Observability.Logging;
using IZ.Core.Utils;
using IZ.Data.Resolvers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Type = System.Type;

#endregion

namespace IZ.Data.Storage;

public class ZEfCoreDataFactory<TDb> : IZDataFactory where TDb : ZDbContext {
  public IZDataRepository GetDataRepository(IZContext context) =>
    new ZEfCoreDataRepository<TDb>(context);
}

public class ZEfCoreDataRepository<TDb> : DataRepositoryBase, IZDataRepository where TDb : ZDbContext {
  private DbContextOptions<TDb> _options;

  public ZEfCoreDataRepository(IZContext context) : base(context) {
    // Db = db;
    _options = Context.GetRequiredService<DbContextOptions<TDb>>();
    // Log.Information("[EF] CREATE {id} on {context}\n{stack}", Uuid, context);//, new ZTrace(new StackTrace().ToString()).ToString());
  }

  public TDb Db {
    get {
      try {
        return _db ??= (Activator.CreateInstance(typeof(TDb), new object[] {Context, _options }) as TDb)!;
      } catch (Exception e) {
        Log.Error(e, "[DB] failed to create {type}", typeof(TDb));
        throw;
      }
    }
  }
    // (TDb) Context.ServiceProvider.GetRequiredService(typeof(TDb));
  private TDb? _db;

  public override void Dispose() {
    // Log.Information("[EF] DISPOSE {id}\n{stack}", Uuid);//, new ZTrace(new StackTrace().ToString()).ToString());
    _db?.Dispose();
    _db = null;
    base.Dispose();
  }

  public void Initialize() {
    Log.Information("[DB] beginning migrations...");
    Db.Database.Migrate();
  }

  private static readonly ConcurrentDictionary<Type, PropertyInfo> DataProps =
    new ConcurrentDictionary<Type, PropertyInfo>();

  public void Rollback() {
    Db.RejectChanges();
  }

  public DbSet<TData> GetDbSet<TData>(IZContext context) where TData : DataObject {
    return (DbSet<TData>) DataProps.GetOrAdd(typeof(TData), (t) => {
      var retType = typeof(DbSet<>).MakeGenericType(t);
      var prop = Db.GetType().GetProperties().FirstOrDefault(p => p.PropertyType == retType) ??
                     throw new ParameterZException(context, $"No database models for {typeof(TData).Name}");
      return prop;
    }).GetValue(Db)!;
  }

  public IZQueryProvider CreateQueryProvider<TData>(IZContext context, DbSet<TData>? db = null) where TData : DataObject {
    db ??= GetDbSet<TData>(context);
    return new ZEfCoreQueryProvider(context, this, (db.AsQueryable().Provider as IAsyncQueryProvider)!);
  }

  IZQueryable<TData> IZDataRepository.QueryFor<TData>(IZContext context, ResultSet? set) {
    DbSet<TData> db = GetDbSet<TData>(context);
    return new DataModelQueryable<TData>(CreateQueryProvider(context, db), db);
  }

  // Wrapper function is excecuted "locked", so sanitization is also locked, which avoids concurrency errors
  private Task<T> ExecuteLockedSanitizedData<T>(IZContext context, Func<Task<T>> t) => ExecuteLocked(async () => {
    var ret = await t();
    Sanitize(context);
    return ret;
  });

  public Task<long> ExecuteSumAsync<TData>(
    IZContext context, IQueryable<TData> q, Expression<Func<TData, long>> func
  ) => ExecuteLocked(() => q.SumAsync(func, context.CancellationToken));
  // => ExecuteSanitizedTask(context, q.SumAsync(func, context.CancellationToken), locked);
  // {
  //   Task<long> Task() => q.SumAsync(func, context.CancellationToken);
  //   var ret = locked ? (await ExecuteLocked((Func<Task<long>>) Task)) : (await Task());
  //   return ret;
  // }

  public Task<long> ExecuteCountAsync<TData>(IZContext context, IQueryable<TData> q) =>
    ExecuteLocked(() => q.LongCountAsync(context.CancellationToken));
  // ExecuteSanitizedTask(context, q.LongCountAsync(context.CancellationToken), locked);
  // {
  //   Task<long> Task() => q.LongCountAsync(context.CancellationToken);
  //   var ret = locked ? (await ExecuteLocked(Task)) : (await Task());
  //   return ret;
  // }

  public Task<TData?> ExecuteFirstOrDefaultAsync<TData>(IZContext context, IQueryable<TData> q) =>
    ExecuteLockedSanitizedData(context, async () => await q.FirstOrDefaultAsync<TData?>(context.CancellationToken));

  //{
  //   Task<TData?> Task() => q.FirstOrDefaultAsync<TData?>(context.CancellationToken);
  //   var ret = locked ? (await ExecuteLocked((Func<Task<TData?>>) Task)) : (await Task());
  //   Sanitize(context); // ensure new models have context
  //   return ret;
  // }

  public Task<List<TData>> ExecuteListAsync<TData>(IZContext context, IQueryable<TData> q) =>
    ExecuteLockedSanitizedData(context, async () => await q.ToListAsync(context.CancellationToken));

  // {
  //   Task<List<TData>> Task() => q.ToListAsync(context.CancellationToken);
  //   var ret = locked ? (await ExecuteLocked((Func<Task<List<TData>>>) Task)) : (await Task());
  //   Sanitize(context); // ensure new models have context
  //   return ret;
  // }

  public async Task SaveAsync(CancellationToken ct = new CancellationToken()) {
    await ExecuteLocked(() => Db.SaveChangesAsync(ct));
    // await Db.SaveChangesAsync(ct);
    // _changed.Clear();
  }

  public Task AddAsync<TData>(params TData[] data) where TData : DataObject =>
    ExecuteLocked(() => {
      Db.AddRange(data.Cast<object>());
      return Task.CompletedTask;
    });

  public Task RemoveAsync<TData>(params TData[] data) where TData : DataObject =>
    ExecuteLocked(() => {
      Db.RemoveRange(data.Cast<object>());
      return Task.CompletedTask;
    });

  private void Sanitize(IZContext? context) {
    string? error = Db.Sanitize(context ?? Context);
    if (error != null) {
      Log.Warning("[DB] sanitization error {error}", error);
    }
  }

  // private readonly List<object> _changed = new List<object>();

  // public void SetChanged<TData>(params TData[] data) where TData : DataObject {
  //   _changed.AddRange(data);
  // }

  public bool HasChanges => Db?.ChangeTracker.HasChanges() ?? false; //_changed.Any();

  public Task<List<T>> GetMemoryModels<T>() where T : DataObject =>
    ExecuteLocked(() => Task.FromResult(Db.ChangeTracker.Entries<T>().Select(e => e.Entity).ToList()));

  public IPreFetched<TEntity, TProperty> QueryInclude<TEntity, TProperty>(
    IZQueryable<TEntity> source, Expression<Func<TEntity, TProperty>> navigationPropertyPath
  ) where TEntity : class => new ZEfCoreRelationshipInclude<TEntity, TProperty>(this, source.QueryProvider, source.Include(navigationPropertyPath));

  public IPreFetched<TEntity, TProperty> QueryThenInclude<TEntity, TPreviousProperty, TProperty>(
    IPreFetched<TEntity, TPreviousProperty> source, Expression<Func<TPreviousProperty, TProperty>> navigationPropertyPath
  ) where TEntity : class {
    ZEfCoreRelationshipInclude<TEntity, TPreviousProperty>? src = source as ZEfCoreRelationshipInclude<TEntity, TPreviousProperty> ??
                                                                     throw new ArgumentException($"{source.GetType().Name} is not a " +
                                                                                                 $"ZEfCoreRelationshipInclude<{typeof(TEntity).Name}, {typeof(TProperty).Name}>");
    IIncludableQueryable<TEntity, TPreviousProperty> q = src.EfQueryable;
    return new ZEfCoreRelationshipInclude<TEntity, TProperty>(this, source.QueryProvider, q.ThenInclude(navigationPropertyPath));
  }

  private static string PrintFullType(Type t) {
    string name = t.Name;
    if (t.GenericTypeArguments.Any()) {
      name += "<" + string.Join(", ", t.GenericTypeArguments.Select(PrintFullType)) + ">";
    }
    return name;
  }

  public IPreFetched<TEntity, TProperty> QueryThenIncludeMany<TEntity, TPreviousProperty, TProperty>(
    IPreFetched<TEntity, List<TPreviousProperty>> source,
    Expression<Func<TPreviousProperty, TProperty>> navigationPropertyPath
  ) where TEntity : class {
    ZEfCoreRelationshipInclude<TEntity, List<TPreviousProperty>>? src = source as ZEfCoreRelationshipInclude<TEntity, List<TPreviousProperty>> ??
                                                                           throw new ArgumentException($"{PrintFullType(source.GetType())} is not a " +
                                                                                                       $"{PrintFullType(typeof(ZEfCoreRelationshipInclude<TEntity, List<TPreviousProperty>>))}");
    IIncludableQueryable<TEntity, List<TPreviousProperty>> q = src.EfQueryable;
    return new ZEfCoreRelationshipInclude<TEntity, TProperty>(this, source.QueryProvider, q.ThenInclude(navigationPropertyPath));
  }

  public override string ToString() => $"EFCore<{Db?.GetType().Name}>";
}

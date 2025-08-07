#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using IZ.Core.Api;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Exceptions;
using IZ.Data.Resolvers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Type = System.Type;

#endregion

namespace IZ.Data.Storage;

public class ZEfCoreDataRepository<TDb> : DataRepositoryBase, IZDataRepository where TDb : ZDbContext {

  private static readonly ConcurrentDictionary<Type, PropertyInfo> DataProps =
    new ConcurrentDictionary<Type, PropertyInfo>();
  private readonly DbContextOptions<TDb> _options;

  private TDb? _db;

  public ZEfCoreDataRepository(IZContext context) : base(context) {
    // Db = db;
    _options = Context.GetRequiredService<DbContextOptions<TDb>>();
    // Log.Information("[EF] CREATE {id} on {context}\n{stack}", Uuid, context);//, new ZTrace());
  }

  public TDb Db {
    get {
      try {
        return _db ??= (Activator.CreateInstance(typeof(TDb), Context, _options) as TDb)!;
      } catch (Exception e) {
        Log.Error(e, "[DB] failed to create {type}", typeof(TDb));
        throw;
      }
    }
  }

  public override void Dispose() {
    // Log.Information("[EF] DISPOSE {id}\n{stack}", Uuid);//, new ZTrace());
    _db?.Dispose();
    _db = null;
    base.Dispose();
  }

  public void Initialize() {
    Log.Information("[DB] beginning migrations...");
    Db.Database.Migrate();
  }

  public void Rollback() {
    Db.RejectChanges();
  }

  public IZQueryable<TData> QueryFor<TData>(IZContext context, ResultSet? set, DataModelTracking tracking = DataModelTracking.Full) where TData : DataObject {
    IQueryable<TData> db = GetDbSet<TData>(context, tracking);
    return new DataModelQueryable<TData>(CreateQueryProvider(context, db), db);
  }

  public Task<long> ExecuteLongSumAsync<TData>(
    IZContext context, IQueryable<TData> q, Expression<Func<TData, long>> func
  ) => ExecuteLocked(() => q.SumAsync(func, context.CancellationToken));

  public Task<double> ExecuteDoubleSumAsync<TData>(IZContext context, IQueryable<TData> q, Expression<Func<TData, double>> func) =>
    ExecuteLocked(() => q.SumAsync(func, context.CancellationToken));

  public Task<long> ExecuteCountAsync<TData>(IZContext context, IQueryable<TData> q) =>
    ExecuteLocked(() => q.LongCountAsync(context.CancellationToken));

  public Task<TData?> ExecuteFirstOrDefaultAsync<TData>(IZContext context, IQueryable<TData> q) =>
    ExecuteLockedSanitizedData(context, async () => await q.FirstOrDefaultAsync<TData?>(context.CancellationToken));

  public Task<List<TData>> ExecuteListAsync<TData>(IZContext context, IQueryable<TData> q) =>
    ExecuteLockedSanitizedData(context, async () => await q.ToListAsync(context.CancellationToken));

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

  public bool HasChanges => Db?.ChangeTracker.HasChanges() ?? false; //_changed.Any();

  public Task<List<T>> GetMemoryModels<T>() where T : class =>
    ExecuteLocked(() => Task.FromResult(Db.GetChanges().Where(e => e.Entity is T).Select(e => (e.Entity as T)!).ToList()));

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

  private IQueryable<TData> GetDbSet<TData>(IZContext context, DataModelTracking tracking) where TData : DataObject {
    DbSet<TData> ret = (DbSet<TData>) DataProps.GetOrAdd(typeof(TData), t => {
      var retType = typeof(DbSet<>).MakeGenericType(t);
      var prop = Db.GetType().GetProperties().FirstOrDefault(p => p.PropertyType == retType) ??
                 throw new ParameterZException(context, $"No database models for {typeof(TData).Name}");
      return prop;
    }).GetValue(Db)!;
    if (tracking == DataModelTracking.IdentityResolution) return ret.AsNoTrackingWithIdentityResolution();
    if (tracking == DataModelTracking.None) return ret.AsNoTracking();
    return ret;
  }

  private IZQueryProvider CreateQueryProvider<TData>(IZContext context, IQueryable<TData> db) where TData : DataObject =>
    new ZEfCoreQueryProvider(context, this, (db.AsQueryable().Provider as IAsyncQueryProvider)!);

  // Wrapper function is excecuted "locked", so sanitization is also locked, which avoids concurrency errors
  private Task<T> ExecuteLockedSanitizedData<T>(IZContext context, Func<Task<T>> t) => ExecuteLocked(async () => {
    var ret = await t();
    Sanitize(context);
    return ret;
  });

  private void Sanitize(IZContext? context) {
    string? error = Db.Sanitize(context ?? Context);
    if (error != null) {
      Log.Warning("[DB] sanitization error {error}", error);
    }
  }

  private static string PrintFullType(Type t) {
    string name = t.Name;
    if (t.GenericTypeArguments.Any()) {
      name += "<" + string.Join(", ", t.GenericTypeArguments.Select(PrintFullType)) + ">";
    }
    return name;
  }

  public override string ToString() => $"EFCore<{Db?.GetType().Name}>";
}

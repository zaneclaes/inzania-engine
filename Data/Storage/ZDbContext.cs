using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using IZ.Core;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;
using IZ.Core.Observability.Logging;
using IZ.Data.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace IZ.Data.Storage;

public class ZDbContext : DbContext, IHaveContext {
  public virtual bool CanStore(object o) => o is DataObject;

  public string Uuid { get; }

  public ZDbContext(ITuneContext root, DbContextOptions opts) : base(opts) {
    Context = root;
    Uuid = ModelId.GenerateId();
    Log = root.Log.ForContext(GetType());
    // Log.Information("[DB] CREATE {id}\n{stack}", Uuid);//, new TuneTrace(new StackTrace().ToString()).ToString());
  }

  public ZDbContext(DbContextOptions opts) : base(opts) {
    Context = IZEnv.SpawnRootContext();
    Uuid = ModelId.GenerateId();
    Log = Context.Log.ForContext(GetType());
    // Log.Information("[DB] CREATE {id}\n{stack}", Uuid);//, new TuneTrace(new StackTrace().ToString()).ToString());
  }

  public ITuneContext Context { get; }
  public ITuneLogger Log { get; }

  public override void Dispose() {
    // Log.Information("[DB] DISPOSE {id}\n{stack}", Uuid);//, new TuneTrace(new StackTrace().ToString()).ToString());
    base.Dispose();
  }

  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
    if (!optionsBuilder.IsConfigured) {
      string? fn = $"{Context.App.Storage.UserDir}/tuneality.db";
      Log.Information("[DB] falling back on {fn}", fn);
      optionsBuilder.UseSqlite($"Data Source={fn}");
    }

    base.OnConfiguring(optionsBuilder);
  }

  private DataState DataStateFromEntityState(EntityState es) {
    if (es == EntityState.Added) return DataState.Created;
    if (es == EntityState.Modified) return DataState.Updated;
    return DataState.None;
  }

  private void UpdateChanges() {
    TimeStampData.OnModelChanging(ChangeTracker);
    string? errorId = this.Sanitize(Context);
    if (errorId != null) throw new ArgumentException($"[DB] creation error: {errorId}");

    // Prevent creation of non-database models
    foreach (var entry in ChangeTracker.Entries()) {
      var ds = DataStateFromEntityState(entry.State);
      if (entry.Entity is IAutoUpdate up && ds != DataState.None) {
        up.OnSavingData(ds);
      }
    }
  }

  public override int SaveChanges() {
    UpdateChanges();
    return base.SaveChanges();
  }

  // https://stackoverflow.com/questions/16437083/dbcontext-discard-changes-without-disposing/22098063#22098063
  public void RejectChanges() {
    foreach (var entry in ChangeTracker.Entries().ToList())
      switch (entry.State) {
        case EntityState.Modified:
        case EntityState.Deleted:
          entry.State = EntityState.Modified; //Revert changes made to deleted entity.
          entry.State = EntityState.Unchanged;
          break;
        case EntityState.Added:
          entry.State = EntityState.Detached;
          break;
      }
  }

  public override Task<int> SaveChangesAsync(
    bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = new CancellationToken()
  ) {
    UpdateChanges();
    return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
  }
  // TuneEnv.SpawnRootContext().App.GetType().Assembly;

  // private static List<Type>? _dataObjectTypes = null;
  // protected static List<Type> DataObjectTypes => _dataObjectTypes ??=
  //   AppAssembly.GetTypes().ToList();

  protected override void OnModelCreating(ModelBuilder modelBuilder) {
    base.OnModelCreating(modelBuilder);

    // foreach (Type dataType in DataObjectTypes) {
    // try {
    foreach (var entityType in modelBuilder.Model.GetEntityTypes()) {
      var dataType = entityType.ClrType;
      List<ApiIndexAttribute> indexes = dataType.GetCustomAttributes<ApiIndexAttribute>().ToList();
      foreach (var attr in indexes) {
        var idx = modelBuilder.Entity(dataType).HasIndex(attr.PropertyNames.ToArray());
        if (attr.IsUnique) idx = idx.IsUnique();
      }

      List<ApiKeyAttribute> keys = dataType.GetCustomAttributes<ApiKeyAttribute>().ToList();
      foreach (var attr in keys) {
        modelBuilder.Entity(dataType).HasKey(attr.PropertyNames.ToArray());
      }
    }

    TimeStampData.AutoIndex(modelBuilder);
  }
}

public static class TuneEfCoreData {
  public static string? Sanitize(this ZDbContext db, ITuneContext context) {
    // General auto-cleanup method, applies fixes to models, returns errorId
    List<EntityEntry>? entries = db.ChangeTracker.Entries().ToList();
    foreach (var entry in entries) {
      var t = entry.Entity.GetType();
      // DataState ds = DataStateFromEntityState(entry.State);
      if (entry.State == EntityState.Added && !db.CanStore(entry.Entity)) {
        // entry.State = EntityState.Modified;
        string? id = entry.Entity is IStringKeyData d ? d.Id : "";
        IZEnv.Log.Warning("[DB] {type}#{id} is not a DataObject", t.Name, id);
        return id;
      }

      if (entry.Entity is DataObject dataObj) {
        dataObj.ProvideContext(context);
      }
    }
    return null;
  }
}

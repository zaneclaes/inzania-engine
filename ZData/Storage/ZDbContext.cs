using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using IZ.Core;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;
using IZ.Core.Observability.Logging;
using IZ.Core.Utils;
using IZ.Data.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace IZ.Data.Storage;

public class ZDbContext : DbContext, IHaveContext {
  public virtual bool CanStore(object o) => o is DataObject;

  public string Uuid { get; }

  public ZDbContext(IZContext root, DbContextOptions opts) : base(opts) {
    Context = root;
    Uuid = ModelId.GenerateId();
    Log = root.Log.ForContext(GetType());
    // Log.Information("[DB] CREATE {id}\n{stack}", Uuid);//, new ZTrace(new StackTrace().ToString()).ToString());
  }

  public ZDbContext(DbContextOptions opts) : base(opts) {
    Context = ZEnv.SpawnRootContext();
    Uuid = ModelId.GenerateId();
    Log = Context.Log.ForContext(GetType());
    // Log.Information("[DB] CREATE {id}\n{stack}", Uuid);//, new ZTrace(new StackTrace().ToString()).ToString());
  }

  public IZContext Context { get; }
  public IZLogger Log { get; }

  public override void Dispose() {
    // Log.Information("[DB] DISPOSE {id}\n{stack}", Uuid);//, new ZTrace(new StackTrace().ToString()).ToString());
    base.Dispose();
  }

  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
    if (!optionsBuilder.IsConfigured) {
      string fn = Path.Join(Context.App.Storage.UserDir, $"{Context.App.ProductName.ToSnakeCase()}.db");
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


  protected override void OnModelCreating(ModelBuilder modelBuilder) {
    base.OnModelCreating(modelBuilder);

    // foreach (Type dataType in DataObjectTypes) {
    // try {
    var entityTypes = modelBuilder.Model.GetEntityTypes().ToList();
    foreach (var entityType in entityTypes) {
      var dataType = entityType.ClrType;
      if (!typeof(DataObject).IsAssignableFrom(dataType)) continue;

      Context.Log.Debug("[DB] start {type}", dataType);

      // Go through each property...
      var dt = ZTypeDescriptor.FromType(dataType);
      foreach (var propertyName in dt.ObjectDescriptor.ObjectProperties.Keys) {
        ConfigureModelProperty(dt, propertyName, modelBuilder);
      }

      List<ApiIndexAttribute> indexes = dataType.GetCustomAttributes<ApiIndexAttribute>().ToList();
      foreach (var attr in indexes) {
        var idx = modelBuilder.Entity(dataType).HasIndex(attr.PropertyNames.ToArray());
        if (attr.IsUnique) idx = idx.IsUnique();
      }

      List<ApiKeyAttribute> keys = dataType.GetCustomAttributes<ApiKeyAttribute>().ToList();
      foreach (var attr in keys) {
        modelBuilder.Entity(dataType).HasKey(attr.PropertyNames.ToArray());
      }

      // Manual static configure method
      var configureMethod = dataType.GetMethod("ConfigureModel", BindingFlags.Static | BindingFlags.Public);
      if (configureMethod != null) {
        configureMethod.Invoke(null, new object[] { Context, modelBuilder });
      }
    }

    TimeStampData.AutoIndex(modelBuilder);
  }

  private void ConfigureModelProperty(ZTypeDescriptor zTypeDescriptor, string propertyName, ModelBuilder modelBuilder) {
    var prop = zTypeDescriptor.ObjectDescriptor.ObjectProperties[propertyName];
    if (!prop.IsInherited && prop.ChildPropertyName != null) {
      var zForeignType = ZTypeDescriptor.FromType(prop.FieldType);
      if (prop.ThroughPropertyType == null) {
        if (zForeignType.IsList) {
          Log.Debug("[PARENT] {type}.{p} <one2many> {ft}.{child}", zTypeDescriptor.OrigType, prop.Name, zForeignType.ObjectDescriptor.ObjectType, prop.ChildPropertyName);
          modelBuilder.Entity(zTypeDescriptor.OrigType)
            .HasMany(prop.Name)
            .WithOne(prop.ChildPropertyName)
            .OnDelete((DeleteBehavior) prop.ChildDeleteBehavior);
        } else {
          Log.Debug("[PARENT] {type}.{p} <one2one> {ft}.{child}", zTypeDescriptor.OrigType, prop.Name, zForeignType.OrigType, prop.ChildPropertyName);
          modelBuilder.Entity(zTypeDescriptor.OrigType)
            .HasOne(prop.Name)
            .WithOne(prop.ChildPropertyName)
            .OnDelete((DeleteBehavior) prop.ChildDeleteBehavior);
        }
      } else {
        var zThru = ZTypeDescriptor.FromType(prop.ThroughPropertyType ?? throw new NullReferenceException(nameof(prop.ThroughPropertyType)));
        var localProps = zThru.ObjectDescriptor.ObjectProperties.Values.Where(p => p.FieldType == zForeignType.ObjectDescriptor.ObjectType).ToList();
        var foreignProps = zThru.ObjectDescriptor.ObjectProperties.Values.Where(p => p.FieldType == zTypeDescriptor.ObjectDescriptor.ObjectType).ToList();
        if (localProps.Count != 1 || foreignProps.Count != 1) throw new ArgumentException($"{zThru.ObjectDescriptor.ObjectType} has {localProps.Count}x {zForeignType.ObjectDescriptor.ObjectType} and {foreignProps.Count}x {zTypeDescriptor.ObjectDescriptor.ObjectType}");

        var localSingular = localProps.First().Name;
        var foreignSingular = foreignProps.First().Name;
        Log.Debug("[THRU] {type}.{p} => {ct}.{child} ({local} <{intermediate}> {foreign})",
          zTypeDescriptor.OrigType, prop.Name, zForeignType.ObjectDescriptor.ObjectType, prop.ChildPropertyName, localSingular, prop.ThroughPropertyType, foreignSingular);

        modelBuilder.Entity(zTypeDescriptor.OrigType)
          .HasMany(prop.Name)
          .WithMany(prop.ChildPropertyName)
          .UsingEntity(
            prop.ThroughPropertyType,
            x => x.HasOne(localSingular).WithMany().HasForeignKey(prop.Name + "Id"),
            x => x.HasOne(foreignSingular).WithMany().HasForeignKey(prop.ChildPropertyName + "Id")
          );
      }
    }
  }
}

public static class ZEfCoreData {
  public static string? Sanitize(this ZDbContext db, IZContext context) {
    // General auto-cleanup method, applies fixes to models, returns errorId
    List<EntityEntry>? entries = db.ChangeTracker.Entries().ToList();
    foreach (var entry in entries) {
      var t = entry.Entity.GetType();
      // DataState ds = DataStateFromEntityState(entry.State);
      if (entry.State == EntityState.Added && !db.CanStore(entry.Entity)) {
        // entry.State = EntityState.Modified;
        string? id = entry.Entity is IStringKeyData d ? d.Id : "";
        ZEnv.Log.Warning("[DB] {type}#{id} is not a DataObject", t.Name, id);
        return id;
      }

      if (entry.Entity is DataObject dataObj) {
        dataObj.ProvideContext(context);
      }
    }
    return null;
  }
}

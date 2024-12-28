#region

using System;
using System.Collections.Generic;
using System.Linq;
using IZ.Core;
using IZ.Core.Contexts;
using IZ.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

#endregion

namespace IZ.Data.Data;

public static class TimeStampData {

  public static TimeSpan TimeSinceCreated(this ICreatedAt ts) => IZEnv.Now - ts.CreatedAt;

  public static TimeSpan TimeSinceUpdated(this ITimeStampData ts) => IZEnv.Now - (ts.UpdatedAt ?? DateTime.UnixEpoch);

  public static IOrderedQueryable<TData> OrderByNewestCreated<TData>(this IQueryable<TData> data) where TData : ICreatedAt {
    return data.OrderByDescending(d => d.CreatedAt);
  }

  public static IOrderedQueryable<TData> OrderByOldestCreated<TData>(this IQueryable<TData> data) where TData : ICreatedAt {
    return data.OrderBy(d => d.CreatedAt);
  }

  public static IOrderedQueryable<TData> OrderByNewestUpdated<TData>(this IQueryable<TData> data) where TData : ITimeStampData {
    return data.OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt);
  }

  public static IOrderedQueryable<TData> OrderByOldestUpdated<TData>(this IQueryable<TData> data) where TData : ITimeStampData {
    return data.OrderBy(d => d.UpdatedAt ?? d.CreatedAt);
  }

  public static void OnModelChanging(ChangeTracker changes) {
    List<EntityEntry>? updates = changes.Entries()
      .Where(e => e.State is EntityState.Modified && e.Entity is IUpdatedAt).ToList();

    for (int i = 0; i < updates.Count; i++) {
      var entry = updates[i];
      var tsd = (IUpdatedAt) entry.Entity;
      tsd.UpdatedAt = IZEnv.Now;
    }

    List<EntityEntry>? creates = changes.Entries()
      .Where(e => e.Entity is ICreatedAt).ToList();

    for (int i = 0; i < creates.Count; i++) {
      var cre = creates[i];
      var c = (ICreatedAt) cre.Entity;
#pragma warning disable CS8073 // The result of the expression is always the same since a value of this type is never equal to 'null'
      if (cre.State == EntityState.Added || c.CreatedAt == null || c.CreatedAt.Year < 2000)
#pragma warning restore CS8073 // The result of the expression is always the same since a value of this type is never equal to 'null'
        c.CreatedAt = IZEnv.Now;
    }
  }

  public static void AutoIndex(ModelBuilder modelBuilder) {
    try {
      foreach (var entityType in modelBuilder.Model.GetEntityTypes()) {
        var t = entityType.ClrType;
        if (typeof(ICreatedAt).IsAssignableFrom(t)) modelBuilder.Entity(t).HasIndex(nameof(ICreatedAt.CreatedAt));
        if (typeof(IUpdatedAt).IsAssignableFrom(t)) modelBuilder.Entity(t).HasIndex(nameof(IUpdatedAt.UpdatedAt));
      }
    } catch (Exception e) {
      IZEnv.Log.Error(e, "[IDX] auto-indexing failed");
    }
  }
}

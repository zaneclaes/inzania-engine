using System.Collections.Generic;
using IZ.Core;
using IZ.Core.Contexts;
using IZ.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace IZ.Data.Storage;

public static class ZEfCoreData {
  public static string? Sanitize(this ZDbContext db, IZContext context) {
    // General auto-cleanup method, applies fixes to models, returns errorId
    List<EntityEntry> entries = db.GetChanges();
    foreach (var entry in entries) {
      var t = entry.Entity.GetType();
      // DataState ds = DataStateFromEntityState(entry.State);
      if (entry.State == EntityState.Added && !db.CanStore(entry.Entity)) {
        // entry.State = EntityState.Modified;
        string id = entry.Entity is IStringKeyData d ? d.Id : "";
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

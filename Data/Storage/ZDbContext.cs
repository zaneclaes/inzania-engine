using System.Collections.Generic;
using System.Linq;
using IZ.Core;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Observability.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;

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

  public ITuneContext Context { get; }
  public ITuneLogger Log { get; }

  public override void Dispose() {
    // Log.Information("[DB] DISPOSE {id}\n{stack}", Uuid);//, new TuneTrace(new StackTrace().ToString()).ToString());
    base.Dispose();
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

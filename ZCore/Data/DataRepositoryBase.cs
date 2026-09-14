#region

using System;
using System.Threading;
using System.Threading.Tasks;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Data;

public class DataRepositoryBase : LogicBase {
  private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

  public DataRepositoryBase(IZContext context) : base(context) { }

  public async Task ExecuteLocked(Func<Task> loader) {
    await _semaphore.WaitAsync(Context.CancellationToken);
    try {
      await loader();
    } finally {
      _semaphore.Release();
    }
  }

  public async Task<TData> ExecuteLocked<TData>(Func<Task<TData>> loader) {
    // _data ??= Services.GetRequiredService<FurDataContext>(); // ensure data ready before locking
    await _semaphore.WaitAsync(Context.CancellationToken);
    try {
      return await loader();
    } finally {
      _semaphore.Release();
    }
  }

  /// <summary>
  /// Saves, treating "another writer already inserted this exact row" as success rather than as a
  /// failure. **Only the seeds use it** (<see cref="Seeds.DataSeed{TD,TS}" />); the default here is a
  /// plain save, so a store with no notion of a duplicate key behaves exactly as before.
  ///
  /// **Why seeding needs it and ordinary saving must not have it.** Every replica runs every seed on
  /// boot, concurrently, and a seed's ids are deterministic: two pods both read "this id is absent",
  /// both insert it, and the second `SaveChanges` fails on the primary key. The row is correct — the
  /// other pod wrote identical content — but the exception aborts that seed *and every seed after
  /// it*, which is how a production rollout on 2026-09-07 left one pod without its lessons. Ordinary
  /// application code keeps the exception, because outside seeding a duplicate key means two callers
  /// disagreed about what a row should contain, and swallowing that would hide a real bug.
  /// </summary>
  public virtual Task SaveTolerantAsync(CancellationToken ct = new CancellationToken()) =>
    ((IZDataRepository) this).SaveAsync(ct);

  /// <summary>
  /// Whether a save failed because a row with that key already exists.
  ///
  /// Matched on the store's own words rather than on an exception type or an error number: the core
  /// is built against no provider in particular, and a check that named `MySqlException` 1062 would
  /// silently stop recognising the case the day the provider changed — failing open, in the direction
  /// of the outage it exists to prevent. Walks the inner exceptions, because the provider's message
  /// arrives wrapped.
  /// </summary>
  public static bool IsDuplicateKey(Exception? e) {
    for (var x = e; x != null; x = x.InnerException) {
      string m = x.Message ?? string.Empty;
      if (m.IndexOf("Duplicate entry", StringComparison.OrdinalIgnoreCase) >= 0) return true;
      if (m.IndexOf("duplicate key", StringComparison.OrdinalIgnoreCase) >= 0) return true;
    }
    return false;
  }

  private static readonly AsyncLocal<bool> ToleratingDuplicateKeys = new AsyncLocal<bool>();

  /// <summary>
  /// Whether <paramref name="e" /> is the duplicate key a tolerant save is conceding right now. EF logs
  /// every failed `SaveChanges` at Error before the exception reaches <see cref="SaveTolerantAsync" />;
  /// inside that save's first attempt a duplicate key is the replica race it recovers from and reports
  /// once as a warning, so the log pipeline drops EF's Error line for it (`SerilogZLogBuilder`). The
  /// last attempt and every ordinary save are outside the scope, and their duplicate keys stay errors.
  /// </summary>
  public static bool IsConcededDuplicateKey(Exception? e) => ToleratingDuplicateKeys.Value && IsDuplicateKey(e);

  /// <summary>Marks the current async flow as a tolerant save's attempt that may still concede, until disposed.</summary>
  protected static IDisposable TolerateDuplicateKeys() {
    bool previous = ToleratingDuplicateKeys.Value;
    ToleratingDuplicateKeys.Value = true;
    return new ToleranceScope(previous);
  }

  private sealed class ToleranceScope : IDisposable {
    private readonly bool _previous;
    public ToleranceScope(bool previous) => _previous = previous;
    public void Dispose() => ToleratingDuplicateKeys.Value = _previous;
  }
}

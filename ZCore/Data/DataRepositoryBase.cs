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
}

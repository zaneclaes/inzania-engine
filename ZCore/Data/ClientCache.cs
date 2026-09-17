#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using IZ.Core.Api;
using IZ.Core.Contexts;
using IZ.Core.Json;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Data;

public abstract class ClientCache : LogicBase, IZClientCache {
  public abstract string? DiskPath { get; }

  private readonly Dictionary<Type, Dictionary<string, object>> _memory = new Dictionary<Type, Dictionary<string, object>>();

  /// <summary>The loads this cache is in the middle of, one per key. Two callers asking for the same
  /// thing at once used to make two requests, deserialize two copies and write the same file twice, so
  /// a read in between could see a file half-written. The shape is `TuneClient`'s
  /// `PlayableScoreRefresh`: the first caller's task is recorded and everyone else awaits it.
  ///
  /// It coalesces requests; it is **not** a second cache. Only a load still running is ever handed
  /// out — see <see cref="Load{T}" /> — so how promptly an entry is retired is a question of tidiness,
  /// not of correctness.</summary>
  private readonly Dictionary<string, object> _loading = new Dictionary<string, object>();

  public ClientCache(IZContext context) : base(context) { }

  protected ZTask<T> Load<T>(string id, IZResult<T> func, string? format = null, TimeSpan? maxAge = null) where T : class {
    if (!string.IsNullOrEmpty(format)) id += "_" + format;
    string loadKey = typeof(T).Name + "/" + id;
    // Only a load still RUNNING is shared. A task that has already answered is inert: a caller
    // arriving afterwards is owed its own read, and asking the task beats trusting the bookkeeping.
    // `LoadOnce` retires its entry from its own continuation, which may resume on another thread and
    // can therefore lose the race with the line below that records it — the removal then finds
    // nothing, the record lands anyway, and the entry outlives the load. Left readable, that stale
    // entry would be served for the life of this cache, i.e. for the life of the app on a client, and
    // the key would never be read again.
    if (_loading.TryGetValue(loadKey, out var inFlight) && inFlight is ZTask<T> shared && !shared.IsCompleted)
      return shared;
    var loading = LoadOnce(loadKey, id, func, format, maxAge).Preserve();
    _loading[loadKey] = loading;
    return loading;
  }

  private async ZTask<T> LoadOnce<T>(string loadKey, string id, IZResult<T> func, string? format, TimeSpan? maxAge) where T : class {
    try {
      var data = Get<T>(id, maxAge);
      maxAge ??= IZResult.DefaultOnlineCacheAge;
      if (data == null || GetJsonFileAge<T>(id) > maxAge) {
        try {
          data = await func.Execute(format);
          SetJson(id, data, format);
        } catch (Exception e) {
          if (data == null) throw;
          Log.Warning(e, "[CACHE] failed to download", id, format);
        }
      }
      return data;
    } finally {
      // Tidy-up, so the map does not grow. Correctness does not rest on this winning any race:
      // `Load` refuses a completed entry.
      _loading.Remove(loadKey);
    }
  }

  public virtual void Delete<T>(string id) {
    if (_memory.ContainsKey(typeof(T))) _memory[typeof(T)].Remove(id);
    SetFile(typeof(T), $"{id}.json", null);
  }

  public virtual T? Get<T>(string id, TimeSpan? maxCacheAge) where T : class {
    if (GetJsonFileAge<T>(id) > maxCacheAge) return null;
    var ret = _memory.GetValueOrDefault(typeof(T))?.GetValueOrDefault(id) as T ?? null;
    if (ret != null) return ret;
    ret = GetJson<T>(id);
    if (ret != null) {
      if (!_memory.ContainsKey(typeof(T))) _memory[typeof(T)] = new Dictionary<string, object>();
      _memory[typeof(T)][id] = ret;
    }
    return ret;
  }

  public virtual void Set<T>(string id, T value, string? format = null) where T : class {
    if (!_memory.ContainsKey(typeof(T))) _memory[typeof(T)] = new Dictionary<string, object>();
    _memory[typeof(T)][id] = value;
    SetJson(id, value, format);
  }

  private string? GetFilePath(Type t, string fn) {
    if (DiskPath == null) return null;
    t = t.IsListType() ? t.GetListType()! : t;
    var folder = Path.Combine(DiskPath, t.Name);
    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
    var fp = Path.Combine(folder, fn);
    var dir = Path.GetDirectoryName(fp)!;
    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
    return fp;
  }

  protected virtual string? GetFile(Type t, string fn) {
    var fp = GetFilePath(t, fn);
    return fp != null && File.Exists(fp) ? File.ReadAllText(fp) : null;
  }

  protected virtual void SetFile(Type t, string fn, string? value) {
    var fp = GetFilePath(t, fn);
    if (fp == null) return;
    if (value == null) File.Delete(fp);
    else File.WriteAllText(fp, value);
  }

  protected TimeSpan? GetJsonFileAge<T>(string key) where T : class =>
    GetFileAge<T>($"{key}.json");

  protected TimeSpan? GetFileAge<T>(string fn) where T : class {
    var fp = GetFilePath(typeof(T), fn);
    if (File.Exists(fp)) return DateTime.UtcNow - File.GetLastWriteTimeUtc(fp);
    return null;
  }

  protected T? GetJson<T>(string key) where T : class {
    var fn = $"{key}.json";
    var json = GetFile(typeof(T), fn);
    if (string.IsNullOrWhiteSpace(json)) return null;
    try {
      return ZJson.DeserializeObject<T>(Context, json);
    } catch (Exception e) {
      Log.Error(e, "[CACHE] failed to deserialize {key}: {json}", fn, json);
      return null;
    }
  }

  protected void SetJson<T>(string key, T? val, string? format = null) where T : class {
    var fn = $"{key}.json";
    SetFile(typeof(T), fn, val == null ? null : ZJson.SerializeObject(val, new ZJsonSerializationOpts() {
      ApiFormat = format,
      PrettyPrint = true
    }));
  }
}

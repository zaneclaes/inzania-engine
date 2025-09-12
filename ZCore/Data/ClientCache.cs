#region

using System;
using System.Collections.Generic;
using System.IO;
using IZ.Core.Contexts;
using IZ.Core.Json;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Data;

public interface IClientCache {
  public T? Get<T>(string id) where T : class, IStringKeyData;

  public void Set<T>(T value) where T : class, IStringKeyData;
}

public abstract class ClientCache : LogicBase, IClientCache {
  public abstract string? DiskPath { get; }

  private readonly Dictionary<Type, Dictionary<string, object>> _memory = new Dictionary<Type, Dictionary<string, object>>();

  public ClientCache(IZContext context) : base(context) { }

  public virtual void Delete<T>(string id) {
    if (_memory.ContainsKey(typeof(T))) _memory[typeof(T)].Remove(id);
    SetFile(typeof(T), $"{id}.json", null);
  }

  public virtual T? Get<T>(string id) where T : class, IStringKeyData {
    var ret = _memory.GetValueOrDefault(typeof(T))?.GetValueOrDefault(id) as T ?? null;
    if (ret != null) return ret;
    ret = GetJson<T>(id);
    if (ret != null) {
      if (!_memory.ContainsKey(typeof(T))) _memory[typeof(T)] = new Dictionary<string, object>();
      _memory[typeof(T)][id] = ret;
    }
    return ret;
  }

  public virtual void Set<T>(T value) where T : class, IStringKeyData {
    if (!_memory.ContainsKey(typeof(T))) _memory[typeof(T)] = new Dictionary<string, object>();
    _memory[typeof(T)][value.Id] = value;
    SetJson(value.Id, value);
  }

  private string? GetFilePath(Type t, string fn) {
    if (DiskPath == null) return null;
    t = t.IsListType() ? t.GetListType()! : t;
    var folder = Path.Combine(DiskPath, t.Name);
    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
    return Path.Combine(folder, fn);
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

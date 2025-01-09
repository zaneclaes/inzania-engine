#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Data;

public interface IClientCache {
  public T? Get<T>(string id) where T : class, IStringKeyData;

  public void Set<T>(T value) where T : class, IStringKeyData;
}

public class ClientCache : LogicBase, IClientCache {
  private readonly Dictionary<Type, Dictionary<string, object>> _vals = new Dictionary<Type, Dictionary<string, object>>();

  public ClientCache(IZContext context) : base(context) { }

  public T? Get<T>(string id) where T : class, IStringKeyData => _vals.GetValueOrDefault(typeof(T))?.GetValueOrDefault(id) as T ?? null;

  public void Set<T>(T value) where T : class, IStringKeyData {
    if (!_vals.ContainsKey(typeof(T))) _vals[typeof(T)] = new Dictionary<string, object>();
    _vals[typeof(T)][value.Id] = value;
  }
}

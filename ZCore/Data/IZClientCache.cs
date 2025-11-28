using System;

namespace IZ.Core.Data;

public interface IZClientCache {
  public T? Get<T>(string id, TimeSpan? maxCacheAge) where T : class;

  public void Set<T>(string id, T value) where T : class;

  public void Set<T>(T value) where T : class, IStringKeyData => Set(value.Id, value);
}

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Exceptions;
using IZ.Core.Json;
using IZ.Core.Observability.Logging;

namespace IZ.Core.Utils.Http;

public class ZHttpClient : HttpClient, IHaveContext {
  public IZLogger Log { get; }
  public IZContext Context { get; }

  private string GetCacheFn(string href, HttpMethod? method = null, string? body = null) {
    var key = href;
    if (!string.IsNullOrWhiteSpace(body)) key += body;
    key = (method ?? HttpMethod.Get) + "-" + key.ToMd5Hash();
    return Path.Join(Context.App.Storage.TmpDir, $"{key}.txt");
  }

  public async Task<string> LoadString(
    string href, HttpMethod? method = null, string? body = null, string? mediaType = null, TimeSpan? cacheDuration = null
  ) {
    // context.Log.Information("GET");
    var res = await this.LoadUrl(href, Context, method, body, mediaType);
    // res.EnsureSuccessStatusCode();
    var str = await res.Content.ReadAsStringAsync();
    // Log.Information("STR {s}", str);
    if (!res.IsSuccessStatusCode) {
      Log.Warning("[HTTP] response body: {str}", str);
      throw new RemoteZException(Context, $"[HTTP] error {res.StatusCode} from {href}");
    }
    return str;
  }

  public async Task<string> LoadCachedUrl(
    string href, HttpMethod? method = null, string? body = null, string? mediaType = null, TimeSpan? cacheDuration = null
  ) {
    string fn = GetCacheFn(href, method, body);
    cacheDuration ??= TimeSpan.FromDays(1);
    if (File.Exists(fn) && (DateTime.UtcNow - File.GetLastWriteTimeUtc(fn)) < cacheDuration) {
      return await File.ReadAllTextAsync(fn);
    }
    var str = await LoadString(href, method, body, mediaType);
    await File.WriteAllTextAsync(fn, str);
    return str;
  }

  public async Task<T> LoadJson<T>(
    string href, HttpMethod? method = null, string? body = null, string? mediaType = null, TimeSpan? cacheDuration = null
  ) {
    var str = await LoadCachedUrl(href, method, body, mediaType, cacheDuration);
    return Deserialize<T>(str, href);
  }

  public async Task<T> LoadCachedJson<T>(
    string href, HttpMethod? method = null, string? body = null, string? mediaType = null, TimeSpan? cacheDuration = null
  ) {
    var str = await LoadCachedUrl(href, method, body, mediaType, cacheDuration);
    return Deserialize<T>(str, href);
  }

  private T Deserialize<T>(string str, string errDesc) {
    try {
      // context.Log.Information("GET {href}", href);
      var ret = ZJson.DeserializeObject<T>(Context, str) ?? throw new SystemException($"Got NULL object from {str.Length} bytes ({errDesc})");
      // context.Log.Information("GETTED");
      return ret;
    } catch (Exception e) {
      throw new SystemException($"Failed to create {typeof(T)} from '{str.Length}' bytes ({errDesc})", e);
    }
  }

  public ZHttpClient(IZContext zContext, string? baseUrl = null) {
    Context = zContext;
    Log = zContext.Log.ForContext(GetType());
    if (!string.IsNullOrWhiteSpace(baseUrl)) BaseAddress = new Uri(baseUrl);
  }
}

#region

using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Exceptions;

#endregion

namespace IZ.Core.Http;

public static class HttpClientExtensions {
  public static async Task<HttpResponseMessage> LoadUrl(this HttpClient client, string href, ITuneContext context, HttpMethod? method = null, string? body = null, string? mediaType = null) {
    string? origUrl = href;
    // context.Log.Information("[URL] {href}", href);
    method ??= HttpMethod.Get;
    using var req = new HttpRequestMessage(method, href);
    if (body != null) req.Content = mediaType == null ? new StringContent(body) : new StringContent(body, Encoding.UTF8, mediaType);

    var response = await client.SendAsync(req);
    while (response.StatusCode == HttpStatusCode.Found || response.StatusCode == HttpStatusCode.Moved || response.StatusCode == HttpStatusCode.MovedPermanently) {
      string? url = response.Headers.GetValues("x-redirect")?.FirstOrDefault();
      if (!string.IsNullOrWhiteSpace(url)) {
        // context.Log.Information("[REDIRECT] {url} -> {redirect}", href, url);
        href = url;
        using var req2 = new HttpRequestMessage(method, url);
        response = await client.SendAsync(req2);
      } else {
        context.Log.Information("[REDIRECT] fail {@headers}", response.Headers);
        throw new RemoteTuneException(context, $"{origUrl} -> {href}");
      }
    }
    if (!response.IsSuccessStatusCode) {
      context.Log.Error("[URL] {url} = {res}", href, await response.Content.ReadAsStringAsync());
      response.EnsureSuccessStatusCode();
    }
    // context.Log.Information("[URL] RES {href}", href);
    return response;
  }
}

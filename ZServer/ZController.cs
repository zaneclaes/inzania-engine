#region

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;
using IZ.Core.Utils;
using Microsoft.AspNetCore.Mvc;

#endregion

namespace IZ.Server;

public abstract class ZController : Controller, IHaveContext {

  private IZLogger? _logger;

  protected ZController(IZContext context) {
    Context = context;
  }
  public IZContext Context { get; }
  public IZLogger Log => _logger ??= Context.Log.ForContext(GetType());

  public Task<FileStreamResult> ServeFile(string fp, string? mimeType = null) {
    var fileStream = new FileStream(fp, FileMode.Open, FileAccess.Read);
    return Task.FromResult(File(fileStream, mimeType ?? MimeTypeMap.GetMimeType(fp)));
  }

  public async Task<IActionResult> ServeCachedFile(string fp, string? mimeType = null) {
    var fi = new FileInfo(fp);
    return await ServeCachedObject(fi.LastWriteTimeUtc, fi.Length, async () => await ServeFile(fp, mimeType));
  }

  public async Task<IActionResult> ServeCachedObject(DateTime lastModUtc, long contentSize, Func<Task<IActionResult>> action) {
    var etag = $"\"{lastModUtc.Ticks:x}-{contentSize:x}\"";

    HttpContext.Response.Headers.ETag = etag;
    HttpContext.Response.Headers.LastModified = lastModUtc.ToString("R", CultureInfo.InvariantCulture);

    // Conditional request handling (ETag first, then Last-Modified)
    var inm = HttpContext.Request.Headers.IfNoneMatch.ToString();
    if (!string.IsNullOrEmpty(inm) && string.Equals(inm, etag, StringComparison.Ordinal))
      return StatusCode(304); // Not Modified

    var imsRaw = HttpContext.Request.Headers.IfModifiedSince.ToString();
    if (!string.IsNullOrEmpty(imsRaw) &&
        DateTimeOffset.TryParseExact(imsRaw, "R", CultureInfo.InvariantCulture,
          DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
          out var ims) &&
        lastModUtc <= ims.UtcDateTime) {
      return StatusCode(304); // Not Modified
    }

    // Viewer (browser) revalidates every time; CDN can keep longer via s-maxage
    // Adjust s-maxage to your tolerance for staleness in CloudFront.
    HttpContext.Response.Headers.CacheControl = "public, max-age=0, s-maxage=86400, must-revalidate";
    return await action();
  }
}

public abstract class ZControllerBase : ControllerBase, IHaveContext {

  private IZLogger? _logger;

  protected ZControllerBase(IZContext context) {
    Context = context;
  }
  public IZContext Context { get; }
  public IZLogger Log => _logger ??= Context.Log.ForContext(GetType());
}

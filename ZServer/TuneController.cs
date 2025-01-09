#region

using System.IO;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;
using IZ.Core.Utils;
using Microsoft.AspNetCore.Mvc;

#endregion

namespace IZ.Server;

public abstract class TuneController : Controller, IHaveContext {

  private IZLogger? _logger;

  protected TuneController(IZContext furContext) {
    Context = furContext;
  }
  public IZContext Context { get; }
  public IZLogger Log => _logger ??= Context.Log.ForContext(GetType());

  protected Task<FileStreamResult> ServeFile(string fp, string? mimeType = null) {
    var fileStream = new FileStream(fp, FileMode.Open, FileAccess.Read);
    return Task.FromResult(File(fileStream, mimeType ?? MimeTypeMap.GetMimeType(fp)));
  }
}

public abstract class TuneControllerBase : ControllerBase, IHaveContext {

  private IZLogger? _logger;

  protected TuneControllerBase(IZContext furContext) {
    Context = furContext;
  }
  public IZContext Context { get; }
  public IZLogger Log => _logger ??= Context.Log.ForContext(GetType());
}

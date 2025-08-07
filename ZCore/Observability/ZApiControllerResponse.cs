using System.Collections.Generic;
using IZ.Core.Data;

namespace IZ.Core.Observability;

public class ZApiControllerError : TransientObject {
  public string Message { get; set; } = null!;

  public string Type { get; set; } = null!;

  public string Code { get; set; } = null!;

  // Data & Stack also exist...
}

public class ZApiControllerMeta : TransientObject {
  public string TraceId { get; set; } = null!;
}

public class ZApiControllerResponse : TransientObject {
  public List<ZApiControllerError> Errors { get; set; } = new List<ZApiControllerError>();

  public ZApiControllerMeta? Meta { get; set; }
}

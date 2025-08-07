#region

using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;

#endregion

namespace ZTests;

public class ZTestActionContext(IZContext parent, Type? type, string? action = null, IZLogger? logger = null)
  : ActionContext(parent, type, action, logger), IZChildContext { }

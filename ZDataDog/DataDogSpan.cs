#region

using System;
using Datadog.Trace;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;

#endregion

namespace IZ.Observability.DataDog;

public class DataDogSpan : ZSpan, IScope, IZSpan {

  private bool _disposed;

  public DataDogSpan(IZContext context, bool useParent = true, string? resource = null, string? action = null) : base(context) {
    var parent = useParent ? context.Parent?.Span as DataDogSpan : null;
    Scope = Tracer.Instance.StartActive(action ?? context.Action ?? "", new SpanCreationSettings {
      Parent = parent?.Span?.Context
    });
    // span.SetTag("subdomain", FurEnv.Subdomain ?? "");
    Scope.Span.ResourceName = resource ?? context.Resource;
  }

  public IScope Scope { get; }

  public ISpan Span => Scope.Span;

  public void Close() {
    Scope.Close();
  }

  public override void SetTag(string key, string value) {
    base.SetTag(key, value);
    Scope.Span.SetTag(key, value);
  }

  public override void SetException(Exception ex) {
    base.SetException(ex);
    Scope.Span.SetException(ex);
  }

  public override void SetSession(IZSession session) {
    base.SetSession(session);
    Scope.Span.SetUser(new UserDetails {
      Id = session.IZUser.Id,
      Name = session.IZUser.Username,
      Email = session.IZUser.Email,
      Role = session.IZUser.Role.ToString(),
      SessionId = session.Id
    });
  }

  public override void Dispose() {
    base.Dispose();
    if (_disposed) return;
    // Log.Information("[SP] DISPOSE {op}:{name}", _scope.Span.OperationName, _scope.Span.ResourceName);
    Scope.Close();
    Scope.Dispose();
    _disposed = true;
  }
}

#region

using System;
using Datadog.Trace;
using IZ.Core.Observability.Logging;

#endregion

namespace IZ.Observability.DataDog;

/// <summary>
/// Writes exception context onto the existing Datadog trace.
/// Caught exceptions become a child span and do not flip the caller's error flag.
/// Failed boundaries keep Datadog's error.type / error.msg / error.stack tags, redacted.
/// </summary>
public static class RuntimeExceptionSpan {
  public const string CaughtOperation = "exception.caught";

  public static SpanCreationSettings CaughtChild(ISpan? parent) => new SpanCreationSettings {
    Parent = parent == null ? SpanContext.None : parent.Context
  };

  public static void RecordOnActive(ISpan? parent, Exception ex, string? source) {
    if (parent is { Error: true }) return;
    using var scope = Tracer.Instance.StartActive(CaughtOperation, CaughtChild(parent));
    if (parent != null && !string.IsNullOrEmpty(parent.ServiceName))
      scope.Span.ServiceName = parent.ServiceName;
    ApplyCaught(scope.Span, ex, source);
    if (parent != null) parent.Error = false;
  }

  public static void ApplyCaught(ISpan span, Exception ex, string? source) {
    span.ResourceName = RuntimeExceptionReport.TypeOf(ex);
    span.SetTag("exception.kind", RuntimeExceptionReport.CaughtKind);
    span.SetTag("exception.type", RuntimeExceptionReport.TypeOf(ex));
    span.SetTag("exception.message", RuntimeExceptionReport.SafeText(ex.Message));
    span.SetTag("exception.stack", RuntimeExceptionReport.StackOf(ex));
    span.SetTag("exception.source", RuntimeExceptionReport.SourceOf(ex, source));
    span.Error = false;
  }

  public static void ApplyFailed(ISpan span, Exception ex) {
    span.Error = true;
    span.SetTag(Tags.ErrorType, RuntimeExceptionReport.TypeOf(ex));
    span.SetTag(Tags.ErrorMsg, RuntimeExceptionReport.SafeText(ex.Message));
    span.SetTag(Tags.ErrorStack, RuntimeExceptionReport.StackOf(ex));
  }
}

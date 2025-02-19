#region

using System;
using Datadog.Trace;
using IZ.Core.Contexts;
using IZ.Observability.DataDog;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Server.Requests;

public class RootScope {
  public IZRootContext Context { get; }
  public IScope Scope { get; }

  public RootScope(IZRootContext context, IScope scope) {
    Context = context;
    Scope = scope;
  }
}

public static class ApiHttp {
  public const string WebSocketOp = "WS";

  public static RootScope? RootScope(this HttpContext context) =>
    context.Items.TryGetValue("Root", out object? obj) ? obj as RootScope : null;

  public static IScope? RootSpan(this HttpContext context) =>
    context.RootScope()?.Scope;

  public static bool IsWebSocket(this HttpContext context) => context.RootSpan()?.Span.OperationName?.Equals(WebSocketOp) ?? false;

  public static bool IsBatch(this HttpContext context) => context.RootSpan()?.Span.OperationName?.Equals("BATCH") ?? false;

  // public static void ClearRootSpan(this HttpContext context) {
  //   if (context.Items.ContainsKey("Root")) context.Items.Remove("Root");
  // }

  public static RootScope EnsureRootScope(this HttpContext context, string verb, string? noun = null) {
    var rootScope = context.RootScope();
    if (rootScope == null) {
      IScope? scope = null;
      if (Tracer.Instance.ActiveScope == null) {
        scope = Tracer.Instance.StartActive(verb);
      } else {
        scope = Tracer.Instance.ActiveScope;
        scope.Span.OperationName = verb;
      }
      if (noun != null) scope.Span.ResourceName = noun;
      var rootContext = new HostContext(context.RequestServices.GetRequiredService<ZApp>(), context.RequestServices, context); // context.RequestServices.GetRequiredService<IZRootContext>()
      context.Items["Root"] = rootScope = new RootScope(rootContext, scope);
      rootScope.Context.Log.Debug("[CTXT] HTTP context created for {v} {n} {ctxt}: {context}", verb, noun, context.Request.Path, rootScope.Context);
    }
    return rootScope;
  }

  public static IScope ForceRootSpan(this HttpContext context, string verb, string? noun = null) {
    var scope = context.EnsureRootScope(verb).Scope;
    scope.Span.OperationName = verb;
    if (noun != null) scope.Span.ResourceName = noun;
    return scope;
  }

  public static IScope? RenameRootResource(this HttpContext context, string name) {
    var scope = context.RootSpan();
    if (scope == null) return null;
    if (scope.Span.OperationName?.Equals(WebSocketOp) ?? false) return scope;
    scope.Span.ResourceName = name;
    return scope;
  }

  public static IZSpan ApiSpan(this HttpContext context, string operation, string name) {
    context.EnsureRootScope(operation);
    // if (context.Items.TryGetValue("API", out var obj) && obj is FurSpan s) {
    //   scope = s.Scope;
    // }

    // context.Items["API"] = span;
    return new DataDogSpan(context.RequestServices.GetCurrentContext(), false);
  }

  public static IZSpan AddRequestSpan(this HttpContext context, Type resource, string verb, bool useParent = true) => new DataDogSpan(context.RequestServices.GetCurrentContext(), useParent,
    resource.Name.Replace("Interceptor", "").Replace("Context", ""), verb);

  // public static FurSpan AddSpan(this HttpContext context, FurSpan span) {
  //   if (!context.Items.TryGetValue("Spans", out var sp) || !(sp is List<FurSpan> spans)) {
  //     spans = new List<FurSpan>();
  //     context.Items["Spans"] = spans;
  //   }
  //
  //   spans.Add(span);
  //   return span;
  // }
}

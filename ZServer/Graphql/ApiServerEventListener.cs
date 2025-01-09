#region

using System;
using System.Collections.Generic;
using HotChocolate.AspNetCore.Instrumentation;
using HotChocolate.Execution;
using HotChocolate.Language;
using IZ.Server.Requests;
using Microsoft.AspNetCore.Http;

#endregion

namespace IZ.Server.Graphql;

internal sealed class EmptyScope : IDisposable {
  public void Dispose() { }
}

public class ApiServerEventListener : ServerDiagnosticEventListener {
  public override IDisposable ExecuteHttpRequest(HttpContext context, HttpRequestKind kind) => context.ForceRootSpan("HTTP", kind.ToString());
  // return new FurSpan("HTTP", kind.ToString());
  public override IDisposable FormatHttpResponse(HttpContext context, IOperationResult result) => context.AddRequestSpan(typeof(HttpContext), "Format", false);
  //TraceSpan.Execute(nameof(FormatHttpResponse));

  public override IDisposable ParseHttpRequest(HttpContext context) => context.AddRequestSpan(typeof(HttpContext), "Parse");

  public override IDisposable WebSocketSession(HttpContext context) => context.ForceRootSpan(ApiHttp.WebSocketOp); //
  // return new FurSpan("HTTP", nameof(WebSocketSession));// new EmptyScope(); // don't create a sub-scope... the root HTTP span has been rewritten
  // public override void StartSingleRequest(HttpContext context, GraphQLRequest request) {
  //   IScope? root = context.RootSpan();
  //   if (context.IsWebSocket()) {
  //     return;
  //   }
  //   if (!root.Span.OperationName.Equals("BATCH")) {
  //     root.Span.ResourceName = request.OperationName;
  //   } else {
  //     context.ApiSpan("GQL", nameof(StartSingleRequest));
  //   }
  // }
  //
  // //
  // public override void StartOperationBatchRequest(HttpContext context, GraphQLRequest request, IReadOnlyList<string> operations) {
  //   IScope root = context.EnsureRootSpan("API");
  //   if (root.Span.OperationName.Equals("WS")) {
  //     return;
  //   }
  //   root.Span.ResourceName = request.OperationName;
  //   root.Span.OperationName = "BATCH";
  // }
  //
  public override void StartBatchRequest(HttpContext context, IReadOnlyList<GraphQLRequest> batch) {
    context.ApiSpan("BATCH", nameof(StartBatchRequest));
  }
}

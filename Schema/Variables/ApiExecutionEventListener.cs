using System;
using System.Collections.Generic;
using System.Linq;
using HotChocolate.Execution;
using HotChocolate.Execution.Instrumentation;
using HotChocolate.Execution.Processing;
using HotChocolate.Language;
using IZ.Core.Api;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;
using IZ.Core.Utils;
using IZ.Schema.Queries;
using IZ.Schema.Types;
using Microsoft.Extensions.DependencyInjection;

namespace IZ.Schema.Variables;

public class ApiExecutionEventListener : ExecutionDiagnosticEventListener {
  internal ITuneLogger Log { get; }

  public ApiExecutionEventListener(ITuneLogger log) {
    Log = log;
  }

  private IDisposable AddRequestSpan(IRequestContext context, string name) {
    Log.Debug("[SPAN] {name}", name);
    return context.Services.GetCurrentContext().ScopeAction(typeof(ApiExecutionEventListener), name);
  }

  // 	Scope that encloses the entire GraphQL request execution. Also the first diagnostic event raised during a GraphQL request.w
  public override IDisposable ExecuteRequest(IRequestContext context) =>
    // FurRequest? req = context.Services.GetService(typeof(FurRequest)) as FurRequest;
    // return new FurSpan("GQL", nameof(ExecuteRequest), req?.RequestSpan);
    AddRequestSpan(context, "Execute");

  private void ReplaceFragmentSpreads(
    ITuneContext context, IRequestContext requestContext, ISyntaxNode node, Dictionary<string, object> map
  ) {
    List<ISyntaxNode> children = node.GetNodes().ToList();
    for (int i = 0; i < children.Count; i++) {
      var ch = children[i];
      if (!(ch is FragmentSpreadNode frag)) {
        ReplaceFragmentSpreads(context, requestContext, ch, map);
        continue;
      }
      if (map.ContainsKey(frag.Name.Value)) continue;
      Log.Debug("[FRAG] {parent}[{idx}] {name}", node.Kind, i, frag.Name);
    }
  }

  private void CoerceQueryVariables(ITuneContext context, IRequestContext reqContext, OperationDocument qd) {
    List<OperationDefinitionNode> ops = qd.Document.GetNodes()
      .Where(o => o.Kind == SyntaxKind.OperationDefinition)
      .Cast<OperationDefinitionNode>()
      .ToList();
    Dictionary<string, ApiVariableValueOrLiteral> coercedValueNodes = new Dictionary<string, ApiVariableValueOrLiteral>();
    Dictionary<string, object?> coercedValues = new Dictionary<string, object?>();

    var req = (reqContext.Request as OperationRequest) ??
              throw new ArgumentException($"{reqContext.Request.GetType()} is not a OperationRequest");

    foreach (var opNode in ops) {
      List<FieldNode> functionNodes = opNode.GetNodes()
        .Where(n => n is SelectionSetNode)
        .Cast<SelectionSetNode>()
        .SelectMany(ss => ss.Selections.Where(s => s is FieldNode).Cast<FieldNode>())
        .ToList();
      foreach (var fn in functionNodes) {
        string funcName = fn.Name.Value.ToFieldName();

        var mi = ZApi.GetMethod(Enum.Parse<ApiExecutionType>(opNode.Operation.ToString()), funcName);
        if (mi == null) continue;

        Dictionary<string, ApiVariableValueOrLiteral>? inputs = TuneInputTypes.ResolveInputVariables(context,
          s => req.VariableValues?.TryGetValue(s, out object? v) ?? false ? v as IValueNode : null,
          mi.Parameters);

        foreach (var node in opNode.GetNodes()) {
          if (!(node is VariableDefinitionNode def)) continue;
          string fieldName = def.Variable.Name.Value.ToFieldName();
          if (!(req.VariableValues?.ContainsKey(fieldName) ?? false)) {
            // Might be used in next operation/function
            Log.Debug("[VARS] {name} requested, not found amount {@names}",
              fieldName, req.VariableValues?.Select(v => v.Key));
            continue;
          }
          // object? val = reqContext.Request.VariableValues[fieldName];
          // Log.Information("[COERCE] {@node} => {@kind}",
          //   def.Variable.Name.Value.ToFieldName(), objs);
          string paramName = def.Variable.Name.Value.ToFieldName();
          if (inputs != null && inputs.TryGetValue(paramName, out var argVal)) {
            Log.Debug("[INPUT] {key} = {arg}", paramName, argVal.Value);
            coercedValueNodes[paramName] = argVal;
            coercedValues[paramName] = argVal.Value;
          } else {
            Log.Warning("[INPUT] missing {key}", paramName);
          }
        }
      }
      req.WithVariableValues(coercedValues);
      reqContext.Variables = new List<IVariableValueCollection>() {
        new ApiVariableValueCollection(coercedValueNodes)
      };
    }
  }



  public override IDisposable ParseDocument(IRequestContext context) => AddRequestSpan(context, nameof(ParseDocument));

  // 1. Ensure document is okay
  public override IDisposable ValidateDocument(IRequestContext reqContext) {
    var ret = AddRequestSpan(reqContext, nameof(ValidateDocument));
    var query = reqContext.Request.Document as OperationDocument;
    var context = reqContext.Services.GetCurrentContext();
    var docId = reqContext.Request.DocumentId.ToString();
    if (query == null && !string.IsNullOrWhiteSpace(docId)) {
      query = reqContext.Services.GetRequiredService<TuneQueryAccessor>().TryReadOperation(docId);
    }
    if (query != null) {
      CoerceQueryVariables(context, reqContext, query);
      ReplaceFragmentSpreads(context, reqContext, query.Document, new Dictionary<string, object>());
    } else {
      Log.Warning("[REQ] unknown query {id} {hash} {req}",
        docId,
        reqContext.Request.DocumentHash,
        reqContext.Request.Document?.GetType());
    }
    return ret;
  }

  // 2.
  public override IDisposable CompileOperation(IRequestContext context) => AddRequestSpan(context, nameof(CompileOperation));

  // 3. Scope that encloses the entire GraphQL request execution. Also the first diagnostic event raised during a GraphQL request.
  public override IDisposable ExecuteOperation(IRequestContext context) => AddRequestSpan(context, nameof(ExecuteOperation));

  // public override IDisposable AnalyzeOperationComplexity(IRequestContext context) => (context, nameof(AnalyzeOperationComplexity));

  public override void RequestError(IRequestContext context, Exception exception) {
    context.Services.GetCurrentContext().HandleException(exception, "API", exception.GetType().Name);
  }

  public override void StartProcessing(IRequestContext context) {
    base.StartProcessing(context);
  }

  public override void StopProcessing(IRequestContext context) {
    base.StopProcessing(context);
  }

  // 	Scope that encloses the entire GraphQL request execution. Also the first diagnostic event raised during a GraphQL request.
  // public override IDisposable ExecuteSubscription(ISubscription subscription) => TraceSpan.Execute(nameof(ExecuteSubscription));

  // public override IDisposable ExecuteDeferredTask() => TraceSpan.GraphQL(nameof(ExecuteDeferredTask));

  // public override IDisposable OnSubscriptionEvent(SubscriptionEventContext context) {
  //   string name = GetOperationName(context.Subscription);
  //   // Log.Information("[SUB] start {id}:{name}", context.Subscription.Id, name);
  //   FurSpan span = TraceSpan.Start("PUB", name);
  //
  //   // span.Span.SetTag("sub", context.Subscription.Id.ToString());
  //   return AddRequestSpan(context,;
  // }

  private string GetOperationName(ISubscription? sub) {
    if (sub?.Operation == null) return "?";
    if (sub.Operation.Name != null) return sub.Operation.Name;
    return sub.Operation.Id;
  }

  public override void SubscriptionEventResult(SubscriptionEventContext context, IOperationResult result) {
    // string name = GetOperationName(context.Subscription);
    // Log.Information("[SUB] end {id}:{name}", context.Subscription.Id, name);
    base.SubscriptionEventResult(context, result);
  }

  public override void SubscriptionEventError(SubscriptionEventContext context, Exception exception) {
    Log.Warning(exception, "[SUB] error {name}", GetOperationName(context.Subscription));
    base.SubscriptionEventError(context, exception);
  }

  public override void SubscriptionTransportError(ISubscription subscription, Exception exception) {
    Log.Warning(exception, "[SUB] transport {name}", GetOperationName(subscription));
    base.SubscriptionTransportError(subscription, exception);
  }

  public override IDisposable DispatchBatch(IRequestContext context) => AddRequestSpan(context, nameof(DispatchBatch));

  // public override IDisposable RunTask(IExecutionTask task) => TraceSpan.GraphQL(nameof(RunTask));

  // public override IDisposable ResolveFieldValue(IMiddlewareContext context) => TraceSpan.GraphQL(context.ResponseName);

  // public override IDisposable CoerceVariables(IRequestContext context) {
  //   throw new ArgumentException("CoerceVariables not shortcutted");
  //   return AddRequestSpan(context, nameof(CoerceVariables));
  // }

  // public override IDisposable ExecuteStream(IOperation operation) => TraceSpan.GraphQL(nameof(ExecuteStream));
}

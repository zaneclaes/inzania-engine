#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate.Execution;
using HotChocolate.Language;
using IZ.Core;
using IZ.Core.Api;
using IZ.Core.Api.Fragments;
using IZ.Core.Contexts;
using IZ.Core.Utils;

#endregion

namespace IZ.Schema.Queries;

public class ZQueryAccessor : LogicBase, IOperationDocumentStorage {

  private static readonly ConcurrentDictionary<string, OperationDocument> Documents =
    new ConcurrentDictionary<string, OperationDocument>();

  private readonly IFragmentProvider _provider;
  public ZQueryAccessor(ZApp app, IFragmentProvider frag) : base(new WorkContext(app, nameof(ZQueryAccessor))) { _provider = frag; }

  public ValueTask<IOperationDocument?> TryReadAsync(
    OperationDocumentId documentId, CancellationToken cancellationToken = new CancellationToken()
  ) => ValueTask.FromResult(TryReadOperation(documentId.ToString()!) as IOperationDocument);

  public ValueTask SaveAsync(
    OperationDocumentId documentId, IOperationDocument document, CancellationToken cancellationToken = new CancellationToken()
  ) => throw new NotImplementedException();

  public OperationDocument? TryReadOperation(string queryId) =>
    queryId.Contains(ExecutionPlan.QueryIdSplit) ? Documents.GetOrAdd(queryId, GenerateQuery) : null;

  private OperationDocument GenerateQuery(string queryId) {
    List<string> parts = queryId.Split(ExecutionPlan.QueryIdSplit).ToList();
    string? format = null;
    var executionType = ApiExecutionType.Query;
    if (parts.Count > 1) {
      foreach (var et in ApiExecutionTypes.All) {
        if (string.Equals(parts[0], et.ToString(), StringComparison.InvariantCultureIgnoreCase)) {
          executionType = et;
          parts.RemoveAt(0);
          break;
        }
      }
    }

    if (parts.Count > 1) {
      format = parts.Last();
      parts.RemoveAt(parts.Count - 1);
      // format = Enum.Parse<FragmentFormat>(parts.Last());
    }
    if (parts.Count > 1) Log.Warning("[QUERY] {id} has unused parts", queryId);
    string fieldName = parts.First().ToFieldName();
    Log.Debug("[QUERY] {type} {fieldName} {format}", executionType, fieldName, format);

    var set = new ResultSet {
      Format = format
    };
    var exec = ExecutionPlan.Load(Context, _provider, executionType, fieldName, set);
    string query = exec.ToGraphQLDocument();
    Log.Debug("[QUERY] {id} => {q}", queryId, query);
    var doc = new OperationDocument(Utf8GraphQLParser.Parse(query));
    return doc;
  }
}

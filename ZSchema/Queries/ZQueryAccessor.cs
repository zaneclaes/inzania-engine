#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate.Execution;
using HotChocolate.Language;
using IZ.Core;
using IZ.Core.Api;
using IZ.Core.Api.Fragments;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Utils;

#endregion

namespace IZ.Schema.Queries;

public class ZQueryAccessor : IOperationDocumentStorage {
  public ZQueryAccessor(IFragmentProvider frag) { _provider = frag; }

  private readonly IFragmentProvider _provider;

  // public Task<QueryDocument?> TryReadQueryAsync(string queryId, CancellationToken cancellationToken = new CancellationToken()) =>
  // Task.FromResult(TryReadQuery(TuneEnv.SpawnRootContext(), queryId));

  private static readonly ConcurrentDictionary<string, OperationDocument> Documents =
    new ConcurrentDictionary<string, OperationDocument>();

  public OperationDocument? TryReadOperation(string queryId) =>
    queryId.Contains(ExecutionPlan.QueryIdSplit) ? Documents.GetOrAdd(queryId, GenerateQuery) : null;

  private OperationDocument GenerateQuery(string queryId) {
    ZEnv.Log.Information("[QUERY] ID {id}", queryId);
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
    if (parts.Count > 1) ZEnv.Log.Warning("[QUERY] {id} has unused parts", queryId);
    string fieldName = parts.First().ToFieldName();
    ZEnv.Log.Debug("[QUERY] {type} {fieldName} {format}", executionType, fieldName, format);

    var set = new ResultSet {
      Format = format
    };
    var exec = ExecutionPlan.Load(_provider, executionType, fieldName, set);
    string query = exec.ToGraphQLDocument();
    ZEnv.Log.Debug("[QUERY] {id} => {q}", queryId, query);
    return new OperationDocument(Utf8GraphQLParser.Parse(query));
  }

  public ValueTask<IOperationDocument?> TryReadAsync(
    OperationDocumentId documentId, CancellationToken cancellationToken = new CancellationToken()
  ) => ValueTask.FromResult(TryReadOperation(documentId.ToString()!) as IOperationDocument);

  public ValueTask SaveAsync(
    OperationDocumentId documentId, IOperationDocument document, CancellationToken cancellationToken = new CancellationToken()
  ) => throw new NotImplementedException();
}

#region

using System;
using System.Collections.Generic;
using System.Linq;
using IZ.Core.Api;
using IZ.Core.Data;
using IZ.Core.Utils;
using StrawberryShake;

#endregion

namespace IZ.Client.Queries;

public class GraphExecutionDocument : TransientObject, IDocument {
  public string Id { get; }

  public OperationKind Kind { get; }

  public ReadOnlySpan<byte> Body => _body;

  private readonly byte[] _body = { };

  public DocumentHash Hash { get; }

  private readonly ExecutionResult _executionResult;

  public Dictionary<string, object?> Args { get; }

  public Dictionary<string, Upload?>? Files { get; } = null;

  public GraphExecutionDocument(ExecutionResult executionResult) : base(executionResult.Context) {
    _executionResult = executionResult;
    Args = executionResult.Args.ToDictionary(a => a.Key, a => a.Value.Item2);
    // if (Args.ContainsKey("file") && Args["file"] is IFileUpload upload) {
    //   // Files = new Dictionary<string, Upload?>() {
    //   //   ["variables.file"] = new Upload(upload.OpenReadStream(), upload.Name),
    //   // };
    //   Log.Information("[UPLOAD] {up}", upload.Name);
    //   Args["file"] = upload;
    // }
    Kind = executionResult.Plan.OperationType.ToOperationKind();
    Id = executionResult.Plan.Id;
    Hash = new DocumentHash("md5Hash", Id.ToSimpleHashStr());
  }

  public OperationRequest ToOperationRequest() => new OperationRequest(
    Id,
    _executionResult.Plan.OperationName,
    this,
    Args,
    Files,
    RequestStrategy.Default);
}

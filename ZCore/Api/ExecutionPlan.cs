#region

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using IZ.Core.Api.Fragments;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;

// #if !Z_UNITY
// using HotChocolate.Language;
// #endif

#endregion

namespace IZ.Core.Api;

public class ExecutionPlan : TransientObject, IExecutionPlan {
  public const char QueryIdSplit = '-';

  private static readonly ConcurrentDictionary<string, ExecutionPlan> _plans = new ConcurrentDictionary<string, ExecutionPlan>();

  private readonly ZMethodDescriptor _method;

  private ExecutionPlan(
    IZContext context, IFragmentProvider fragmentProvider, ApiExecutionType op, string operationName, ResultSet? resultSet = null
  ) : base(context) {
    _method = ZApi.GetRequiredMethodByMethodName(op, operationName);
    OperationType = op;
    FieldName = _method.FieldName;
    ReturnType = ZApi.LoadTypeDescriptor(_method.FieldType);
    Set = resultSet ?? new ResultSet();
    OperationName = _method.Name;
    Id = $"{OperationType}{QueryIdSplit}{FieldName}{QueryIdSplit}{Set}";
    try {
      Fragments = ReturnType.ObjectDescriptor.IsScalar ? null : new FragmentSet(context, fragmentProvider, ReturnType, Set);
    } catch (Exception e) {
      throw new SystemException("Failed to create fragments", e);
    }
  }

  public string Id { get; }

  [ApiDocs("The name of the method being invoked")]
  public string FieldName { get; set; }

  [ApiDocs("query/mutation/subscription")]
  public ApiExecutionType OperationType { get; set; }

  public string OperationName { get; }

  public ZTypeDescriptor ReturnType { get; }

  public ResultSet Set { get; }

  private FragmentSet? Fragments { get; }

  public static ExecutionPlan Load(IZContext context, Type parent, string operationName, ResultSet resultSet) =>
    Load(context, GetClassExecutionType(parent), operationName, resultSet);

  public static ExecutionPlan Load(IZContext context, IFragmentProvider frags, ApiExecutionType op, string operationName, ResultSet resultSet) {
    string key = $"{op} {operationName} {resultSet}";
    if (_plans.TryGetValue(key, out var plan)) return plan;
    return _plans[key] = new ExecutionPlan(context, frags, op, operationName, resultSet);
  }

  public static ExecutionPlan Load(IZContext context, ApiExecutionType op, string operationName, ResultSet resultSet) =>
    Load(context, context.GetRequiredService<IFragmentProvider>(), op, operationName, resultSet);

  public Dictionary<string, Tuple<ZTypeDescriptor, object?>> CoerceArgs(List<object?> args) {
    Dictionary<string, Tuple<ZTypeDescriptor, object?>> ret = new Dictionary<string, Tuple<ZTypeDescriptor, object?>>();

    if (args.Count > _method.Parameters.Count) throw new ArgumentException($"Too many args for {OperationName}");

    var converter = Context.GetService<IParameterConverter>();

    for (int i = 0; i < _method.Parameters.Count; i++) {
      var zType = ZApi.LoadTypeDescriptor(_method.Parameters[i].ParameterType);
      object? argVal = i >= args.Count ? null :
        (converter == null ? PrepareArgJson(args[i]) : converter.ConvertParameter(args[i]));

      if (_method.Parameters[i].ParameterType == typeof(IFileUpload) && args[i] is IFileUpload upload) {
        // ZEnv.Log.Information("COERCE {arg}",args[i]?.GetType());
        argVal = upload;
      }

      ret[_method.Parameters[i].FieldName] = new Tuple<ZTypeDescriptor, object?>(zType, argVal);
    }

    return ret;
  }

  private static ApiExecutionType GetClassExecutionType(Type parent) {
    if (parent.IsSubclassOf(typeof(ZQueryBase))) return ApiExecutionType.Query;
    if (parent.IsSubclassOf(typeof(ZMutationBase))) return ApiExecutionType.Mutation;
    if (parent.IsSubclassOf(typeof(ZSubscriptionBase))) return ApiExecutionType.Subscription;
    throw new ArgumentException($"{parent.Name} is neither query nor mutation");
  }

  private static JsonNode? PrepareArgJson(object? arg) {
    if (arg == null) return null;
    if (arg is IList list) {
      var arr = new JsonArray();
      // List<object?> ret = new List<object?>();
      for (int i = 0; i < list.Count; i++) {
        // ret.Add(PrepareArg(list[i]));
        arr.Add(PrepareArgJson(list[i]));
      }
      return arr;
    }
    var desc = ZApi.LoadTypeDescriptor(arg.GetType());
    if (desc.ObjectDescriptor.IsScalar) return JsonSerializer.SerializeToNode(arg);
    // if (!(arg is ApiObject obj)) return arg;

    var mapped = new JsonObject();
    // mapped["__typename"] = desc.TypeName;
    foreach (string inputName in desc.ObjectDescriptor.Inputs.Keys) {
      mapped[inputName] = PrepareArgJson(desc.ObjectDescriptor.Inputs[inputName].GetValue(arg));
    }
    return mapped;
  }

  public string ToGraphQLDocument() {
    string op = $"{OperationType.ToString().ToLower()} {OperationName}";
    string invoke = $"result: {FieldName}";
    if (_method.Parameters.Any()) {
      List<string> args = new List<string>();
      List<string> pars = new List<string>();
      foreach (var param in _method.Parameters) {
        string key = param.FieldName;
        args.Add($"${key}: {param.ApiType.ToGraphTypeName(true)}");
        pars.Add($"{key}: ${key}");
      }
      op += "(" + string.Join(", ", args) + ")";
      invoke += "(" + string.Join(", ", pars) + ")";
    }

    string query = Fragments == null ? $"{op} {{\n  {invoke}\n}}" :
      $"{Fragments.Headers}\n\n{op} {{\n  {invoke} {{ ...{Fragments.Root.Name} }} \n}}";

    // ZEnv.Log.Information("[OP] {query}", op);

    return query;
  }
}

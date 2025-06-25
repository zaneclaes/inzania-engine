#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using HotChocolate.Language;
using IZ.Core.Api.Fragments;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Core.Api;

public class ExecutionPlan {
  public const char QueryIdSplit = '-';

  public string Id { get; }

  [ApiDocs("The name of the method being invoked")]
  public string FieldName { get; set; }

  [ApiDocs("query/mutation/subscription")]
  public ApiExecutionType OperationType { get; set; }

  public string OperationName { get; }

  public ZTypeDescriptor ReturnType { get; }

  public ResultSet Set { get; }

  public FragmentSet Fragments { get; }

  private readonly ZMethodDescriptor _method;

  private static readonly Dictionary<string, ExecutionPlan> Plans = new Dictionary<string, ExecutionPlan>();

  public static ExecutionPlan Load(IZContext context, Type parent, string operationName, ResultSet resultSet) =>
    Load(context, GetClassExecutionType(parent), operationName, resultSet);

  public static ExecutionPlan Load(IFragmentProvider frags, ApiExecutionType op, string operationName, ResultSet resultSet) {
    string key = $"{op} {operationName} {resultSet}";
    if (Plans.TryGetValue(key, out var plan)) return plan;
    return Plans[key] = new ExecutionPlan(frags, op, operationName, resultSet);
  }

  public static ExecutionPlan Load(IZContext context, ApiExecutionType op, string operationName, ResultSet resultSet) =>
    Load(context.GetRequiredService<IFragmentProvider>(), op, operationName, resultSet);

  private ExecutionPlan(
    IFragmentProvider fragmentProvider, ApiExecutionType op, string operationName, ResultSet? resultSet = null
  ) {
    _method = ZApi.GetRequiredMethodByMethodName(op, operationName);
    OperationType = op;
    FieldName = _method.FieldName;
    ReturnType = ZTypeDescriptor.FromType(_method.FieldType);
    Set = resultSet ?? new ResultSet();
    OperationName = _method.OperationName;
    Id = $"{OperationType}{QueryIdSplit}{FieldName}{QueryIdSplit}{Set}";
    try {
      Fragments = new FragmentSet(fragmentProvider, ReturnType, Set);
    } catch (Exception e) {
      throw new SystemException("Failed to create fragments", e);
    }
  }

  public Dictionary<string, Tuple<ZTypeDescriptor, object?>> CoerceArgs(List<object?> args) {
    Dictionary<string, Tuple<ZTypeDescriptor, object?>> ret = new Dictionary<string, Tuple<ZTypeDescriptor, object?>>();

    if (args.Count > _method.Parameters.Count) throw new ArgumentException($"Too many args for {OperationName}");

    for (int i = 0; i < _method.Parameters.Count; i++) {
      var zType = ZTypeDescriptor.FromType(_method.Parameters[i].ParameterType);
      object? argVal = i >= args.Count ? null :
#if Z_UNITY
        PrepareArgJson(args[i]);
#else
        PrepareArgGql(args[i]);
#endif

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
    var desc = ZTypeDescriptor.FromType(arg.GetType());
    if (desc.ObjectDescriptor.IsScalar) return JsonSerializer.SerializeToNode(arg);
    // if (!(arg is ApiObject obj)) return arg;

    var mapped = new JsonObject();
    // mapped["__typename"] = desc.TypeName;
    foreach (string inputName in desc.ObjectDescriptor.Inputs.Keys) {
      mapped[inputName] = PrepareArgJson(desc.ObjectDescriptor.Inputs[inputName].GetValue(arg));
    }
    return mapped;
  }

  private static IValueNode PrepareArgGql(object? arg) {
    if (arg == null) return NullValueNode.Default;
    if (arg is IList list) {
      var arr = new List<IValueNode>();
      for (int i = 0; i < list.Count; i++) {
        arr.Add(PrepareArgGql(list[i]));
      }
      return new ListValueNode(arr);
    }
    var desc = ZTypeDescriptor.FromType(arg.GetType());

    if (desc.ObjectDescriptor.IsScalar) {
      if (arg is string stringVal) return new StringValueNode(stringVal);
      if (arg is sbyte sbyteVal) return new IntValueNode(sbyteVal);
      if (arg is byte byteVal) return new IntValueNode(byteVal);
      if (arg is int intVal) return new IntValueNode(intVal);
      if (arg is uint uintVal) return new IntValueNode(uintVal);
      if (arg is short shortVal) return new IntValueNode(shortVal);
      if (arg is ushort ushortVal) return new IntValueNode(ushortVal);
      if (arg is bool boolVal) return new BooleanValueNode(boolVal);
      if (arg is long longVal) return new IntValueNode(longVal);
      if (arg is ulong ulongVal) return new IntValueNode(ulongVal);
      if (arg is float floatVal) return new FloatValueNode(floatVal);
      if (arg is double doubleVal) return new FloatValueNode(doubleVal);
      if (arg is decimal decVal) return new FloatValueNode(decVal);
      if (arg is Enum e) return new EnumValueNode(e.ToString());
      throw new ArgumentException($"{arg.GetType().Name} cannot be translated into a value node");
    }
    // if (!(arg is ApiObject obj)) return arg;

    List<ObjectFieldNode> fields = new List<ObjectFieldNode>();
    foreach (var inputName in desc.ObjectDescriptor.Inputs.Keys) {
      var node = PrepareArgGql(desc.ObjectDescriptor.Inputs[inputName].GetValue(arg));
      fields.Add(new ObjectFieldNode(inputName, node));
    }
    return new ObjectValueNode(fields);
  }

  public string ToGraphQLDocument() {
    string op = $"{OperationType.ToString().ToLower()} {OperationName}";
    string invoke = $"result: {FieldName}";
    if (_method.Parameters.Any()) {
      List<string> args = new List<string>();
      List<string> pars = new List<string>();
      foreach (var param in _method.Parameters) {
        string key = param.FieldName;
        args.Add($"${key}: {param.ApiType.ToObjectTypeName(true)}");
        pars.Add($"{key}: ${key}");
      }
      op += "(" + string.Join(", ", args) + ")";
      invoke += "(" + string.Join(", ", pars) + ")";
    }

    string query = $"{Fragments.Headers}\n\n{op} {{\n  {invoke} {{ ...{Fragments.Root.Name} }} \n}}";

    // ZEnv.Log.Information("[OP] {query}", op);

    return query;
  }
}

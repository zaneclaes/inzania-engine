#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Api.Types;

public class ZMethodDescriptor : ZFieldDescriptor {

  public ZMethodDescriptor(MethodInfo methodInfo) : base(methodInfo, methodInfo.ReturnType, IsMethodReturnNullable(methodInfo)) {
    Method = methodInfo;
    OperationName = methodInfo.Name;
    Parameters = methodInfo.GetParameters()
      .Select(p => new ZParameterDescriptor(p))
      .ToList();
    // ApiMethod = methodInfo.GetCustomAttribute<ApiMethodAttribute>();

    string name = Name = methodInfo.Name;
    bool isSet = name.StartsWith("Set");
    bool isGet = name.StartsWith("Get");
    if (isSet || isGet) name = name.Substring(3);
    ExecutionType = ApiExecutionType.Query;

    // if (ApiMethod != null) {
    //   ExecutionType = isSet ? ApiExecutionType.Mutation : isGet ? ApiExecutionType.Query : ApiMethod.ExecutionType;
    //   if (ExecutionType != ApiMethod.ExecutionType) {
    //     ZEnv.Log.Warning("[METHOD] {name} was converted from {type} to {exec}", OperationName, ApiMethod.ExecutionType, ExecutionType);
    //   }
    // }
    FieldName = name.ToFieldName();
  }
  public string OperationName { get; }

  // public ApiMethodAttribute? ApiMethod { get; }

  public ApiExecutionType ExecutionType { get; }

  public List<ZParameterDescriptor> Parameters { get; }

  private MethodInfo Method { get; }

  public object? Invoke(IZContext context, object o, params object?[]? args) {
    try {
      return Method.Invoke(o, args);
    } catch (Exception e) {
      context.Log.Error(e, "Failed to invoke {method} on {type}", Method.Name, o.GetType());
      throw;
    }
  }
  protected override IEnumerable<ZTypeDescriptor> GetTypeDescriptors() {
    yield return FieldTypeDescriptor;
    foreach (var p in Parameters)
      yield return p.ApiType;
  }

  public static Type StripIgnoredOuterFunctionTypes(Type t) {
    if (t.Name == "Task`1") { // ISAssignableTo(Task<>) seems to not work
      t = t.GenericTypeArguments[0];
    }
    if (t.HasAssignableType(typeof(IZResult))) {
      // ZEnv.Log.Information("T {old} -> {new}", t.Name, t.GenericTypeArguments[0].Name);
      t = t.GenericTypeArguments[0];
    }
    return t;
  }

  private static bool IsMethodReturnNullable(MethodInfo methodInfo) {
    var context = new NullabilityInfoContext();
    var nullability = context.Create(methodInfo.ReturnParameter);
    if (nullability.ReadState == NullabilityState.Nullable) return true;
    var inner = nullability.GenericTypeArguments.FirstOrDefault();
    return inner is {ReadState: NullabilityState.Nullable};
  }

  public override string ToString() => $"<{Method.Name}: {FieldTypeDescriptor}>";
}

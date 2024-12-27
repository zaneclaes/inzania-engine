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

public class TuneMethodDescriptor : TuneFieldDescriptor {
  public string OperationName { get; }

  public override Type FieldType => Method.ReturnType;

  public ApiMethodAttribute? ApiMethod { get; }

  public ApiExecutionType ExecutionType { get; }

  public List<TuneParameterDescriptor> Parameters { get; }

  private MethodInfo Method { get; }

  public object? Invoke(object o, params object?[]? args) => Method.Invoke(o, args);

  protected override List<TuneTypeDescriptor> GetTypeDescriptors() =>
    base.GetTypeDescriptors().Union(Parameters.Select(p => p.ApiType)).ToList();

  public TuneMethodDescriptor(MethodInfo methodInfo) : base(methodInfo) {
    Method = methodInfo;
    OperationName = methodInfo.Name;
    Parameters = methodInfo.GetParameters()
      .Select(p => new TuneParameterDescriptor(p))
      .ToList();
    ApiMethod = methodInfo.GetCustomAttribute<ApiMethodAttribute>();

    string name = methodInfo.Name;
    bool isSet = name.StartsWith("Set");
    bool isGet = name.StartsWith("Get");
    if (isSet || isGet) name = name.Substring(3);

    if (ApiMethod != null) {
      ExecutionType = isSet ? ApiExecutionType.Mutation : isGet ? ApiExecutionType.Query : ApiMethod.ExecutionType;
      if (ExecutionType != ApiMethod.ExecutionType) {
        IZEnv.Log.Warning("[METHOD] {name} was converted from {type} to {exec}", OperationName, ApiMethod.ExecutionType, ExecutionType);
      }
    }
    FieldName = name.ToFieldName();
  }
}

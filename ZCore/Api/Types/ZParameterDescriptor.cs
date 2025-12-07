#region

using System;
using System.Reflection;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Api.Types;

public class ZParameterDescriptor : IAmInternal {

  public ZParameterDescriptor(ParameterInfo member) {
    FieldName = member.Name!.ToFieldName();
    ParameterType = member.ParameterType;
    ApiType = ZTypeDescriptor.FromType(ParameterType, member.IsOptional);
    IsOptional = member.IsOptional || ParameterType.IsListType() || ParameterType.IsArray;
    DefaultValue = member.DefaultValue;
    IsTopic = member.GetCustomAttribute<ApiTopicAttribute>() != null;
    IsEventMessage = member.GetCustomAttribute<ApiMessageAttribute>() != null;
  }
  public string FieldName { get; }

  public Type ParameterType { get; }

  public ZTypeDescriptor ApiType { get; }

  public bool IsOptional { get; }

  public object? DefaultValue { get; }

  public bool IsTopic { get; }

  public bool IsEventMessage { get; }
}

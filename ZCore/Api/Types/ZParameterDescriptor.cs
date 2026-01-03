#region

using System;
using System.Collections.Generic;
using System.Reflection;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;
using IZ.Core.Json;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Api.Types;

public class ZParameterDescriptor : IAmInternal {

  public ZParameterDescriptor(
    string fieldName, Type parameterType, object? defaultValue = null,
    bool isTopic = false, bool isEventMessage = false, bool isOptional = false
  ) {
    FieldName = fieldName;
    ParameterType = parameterType;
    IsOptional = isOptional;
    DefaultValue = defaultValue;
    IsTopic = isTopic;
    IsEventMessage = isEventMessage;
  }

  public ZParameterDescriptor(ParameterInfo member) {
    FieldName = member.Name!.ToFieldName();
    ParameterType = member.ParameterType;
    IsOptional = member.IsOptional || ParameterType.IsListType() || ParameterType.IsArray;
    DefaultValue = ((member.DefaultValue?.GetType() ?? typeof(DBNull)) == typeof(DBNull)) ? null : member.DefaultValue;
    IsTopic = member.GetCustomAttribute<ApiTopicAttribute>() != null;
    IsEventMessage = member.GetCustomAttribute<ApiMessageAttribute>() != null;
  }
  public string FieldName { get; }

  public Type ParameterType { get; }

  public ZTypeDescriptor ApiType => _apiType ??= ZTypeDescriptor.FromType(ParameterType, IsOptional);
  private ZTypeDescriptor? _apiType;

  public bool IsOptional { get; }

  public object? DefaultValue { get; }

  public bool IsTopic { get; }

  public bool IsEventMessage { get; }

  public static string GetDefaultValueSource(object? def, HashSet<string> usings) {
    if (def == null || def is DBNull) return "null";
    usings.Add(def.GetType().Namespace!);
    if (def.GetType().IsEnum) {
      return def.GetType().Name + "." + Enum.GetName(def.GetType(), def)!;
    }
    if (def is bool) return def.ToString()!.ToLowerInvariant();
    return def.ToString()!;
  }

  public string GetSource(HashSet<string> namespaces) {
    if (ParameterType.Namespace != null) namespaces.Add(ParameterType.Namespace);
    var pt = ZTypeDescriptor.FromType(ParameterType);
    string dv = GetDefaultValueSource(DefaultValue, namespaces);
    return $"new ZParameterDescriptor(\"{FieldName}\", typeof({pt.ToSystemTypeName()}), {dv}, " +
           $"{IsTopic.ToString().ToLowerInvariant()}, {IsEventMessage.ToString().ToLowerInvariant()}, {IsOptional.ToString().ToLowerInvariant()})";
  }
}

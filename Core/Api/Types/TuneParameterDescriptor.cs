#region

using System;
using System.Reflection;
using IZ.Core.Contexts;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Api.Types;

public class TuneParameterDescriptor : IAmInternal {
  public string FieldName { get; }

  public Type ParameterType { get; }

  public TuneTypeDescriptor ApiType { get; }

  public bool IsOptional { get; }

  public object? DefaultValue { get; }

  public TuneParameterDescriptor(ParameterInfo member) {
    FieldName = member.Name!.ToFieldName();
    ParameterType = member.ParameterType;
    ApiType = TuneTypeDescriptor.FromType(ParameterType, member.IsOptional);
    IsOptional = member.IsOptional;
    DefaultValue = member.DefaultValue;
  }
}

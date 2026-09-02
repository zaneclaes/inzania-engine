#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;
using IZ.Core.Json;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Api.Types;

public class ZParameterDescriptor : IAmInternal {
  public ZTypeDescriptor ApiType => _apiType ??= _typeMap.LoadTypeDescriptor(ParameterType, IsOptional);
  protected readonly IZTypeMap _typeMap;
  private ZTypeDescriptor? _apiType;

  public ZParameterDescriptor(
    IZTypeMap typeMap, string fieldName, Type parameterType, object? defaultValue = null,
    bool isTopic = false, bool isEventMessage = false, bool isOptional = false
  ) {
    _typeMap = typeMap;
    FieldName = fieldName;
    ParameterType = parameterType;
    IsOptional = isOptional;
    DefaultValue = defaultValue;
    IsTopic = isTopic;
    IsEventMessage = isEventMessage;
  }

  public ZParameterDescriptor(IZTypeMap typeMap, ParameterInfo member) {
    _typeMap = typeMap;
    FieldName = member.Name!.ToFieldName();
    ParameterType = member.ParameterType;
    IsOptional = member.IsOptional || ParameterType.IsListType() || ParameterType.IsArray;
    DefaultValue = ((member.DefaultValue?.GetType() ?? typeof(DBNull)) == typeof(DBNull)) ? null : member.DefaultValue;
    IsTopic = member.GetCustomAttribute<ApiTopicAttribute>() != null;
    IsEventMessage = member.GetCustomAttribute<ApiMessageAttribute>() != null;
  }
  public string FieldName { get; }

  public Type ParameterType { get; }

  public bool IsOptional { get; }

  public object? DefaultValue { get; }

  public bool IsTopic { get; }

  public bool IsEventMessage { get; }

  /// <summary>
  /// A C# literal for an optional parameter's default, for the generated descriptor sources.
  ///
  /// <para>`ToString()` is not a literal, and the two ways it is wrong both matter. A string or char
  /// came out unquoted — `position = "front"` emitted `front`, an identifier that does not exist, and
  /// `""` emitted nothing at all (a missing argument) — so any host with a string default failed to
  /// compile its generated type map. And every numeric literal here is boxed into an `object?`, so its
  /// C# type is the runtime type of the default: a `decimal` default written as `0.5` would box a
  /// `double`, and `(byte)1` written as `1` an `int`, silently changing the schema's argument type.
  /// Suffixes and casts keep the boxed type the one the method actually declares.</para>
  /// </summary>
  public static string GetDefaultValueSource(object? def, HashSet<string> usings) {
    if (def == null || def is DBNull) return "null";
    usings.Add(def.GetType().Namespace!);
    if (def.GetType().IsEnum) {
      return def.GetType().Name + "." + Enum.GetName(def.GetType(), def)!;
    }
    return def switch {
      bool b => b ? "true" : "false",
      string s => Quote(s),
      char c => $"'{Escape(c.ToString())}'",
      decimal m => m.ToString(CultureInfo.InvariantCulture) + "m",
      double d => d.ToString("R", CultureInfo.InvariantCulture) + "d",
      float f => f.ToString("R", CultureInfo.InvariantCulture) + "f",
      long l => l.ToString(CultureInfo.InvariantCulture) + "L",
      ulong ul => ul.ToString(CultureInfo.InvariantCulture) + "UL",
      uint u => u.ToString(CultureInfo.InvariantCulture) + "U",
      int i => i.ToString(CultureInfo.InvariantCulture),
      // byte/sbyte/short/ushort have no literal suffix, so the cast is what stops them boxing as int.
      _ => $"({def.GetType().Name}) {Convert.ToString(def, CultureInfo.InvariantCulture)}",
    };
  }

  private static string Quote(string s) => $"\"{Escape(s)}\"";

  private static string Escape(string s) {
    var sb = new StringBuilder(s.Length + 2);
    foreach (char c in s) {
      switch (c) {
        case '\\': sb.Append(@"\\"); break;
        case '"': sb.Append("\\\""); break;
        case '\'': sb.Append(@"\'"); break;
        case '\0': sb.Append(@"\0"); break;
        case '\a': sb.Append(@"\a"); break;
        case '\b': sb.Append(@"\b"); break;
        case '\f': sb.Append(@"\f"); break;
        case '\n': sb.Append(@"\n"); break;
        case '\r': sb.Append(@"\r"); break;
        case '\t': sb.Append(@"\t"); break;
        case '\v': sb.Append(@"\v"); break;
        default:
          if (char.IsControl(c)) sb.Append("\\u").Append(((int)c).ToString("x4"));
          else sb.Append(c);
          break;
      }
    }
    return sb.ToString();
  }

  public string GetSource(IZTypeMap typeMap, HashSet<string> namespaces) {
    if (ParameterType.Namespace != null) namespaces.Add(ParameterType.Namespace);
    var pt = typeMap.LoadTypeDescriptor(ParameterType);
    string dv = GetDefaultValueSource(DefaultValue, namespaces);
    return $"new ZParameterDescriptor(typeMap, \"{FieldName}\", typeof({pt.ToSystemTypeName()}), {dv}, " +
           $"{IsTopic.ToString().ToLowerInvariant()}, {IsEventMessage.ToString().ToLowerInvariant()}, {IsOptional.ToString().ToLowerInvariant()})";
  }
}

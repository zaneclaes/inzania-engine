#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Api.Types;

// Describes a concrete node type, without nullable/list/etc. decoration
public class ZObjectDescriptor : IAmInternal {
  private readonly List<MethodInfo> _methodInfos = new List<MethodInfo>();

  public bool IsFile { get; set; }

  public bool IsScalar { get; set; }

  public Type ObjectType { get; set; }

  public string TypeName { get; }

  public string InputTypeName { get; }

  // API Object properties (not used as API objects, but used to build fragments)
  public Dictionary<string, ZPropertyDescriptor> ObjectProperties { get; } = new Dictionary<string, ZPropertyDescriptor>();

  public Dictionary<string, ZPropertyDescriptor> ScalarProperties { get; } = new Dictionary<string, ZPropertyDescriptor>();

  public Dictionary<string, ZPropertyDescriptor> Inputs { get; } = new Dictionary<string, ZPropertyDescriptor>();

  public List<ZPropertyDescriptor> AllProperties => _properties.Values.ToList();

  public Dictionary<string, ZMethodDescriptor> Methods { get; } = new Dictionary<string, ZMethodDescriptor>();

  public ZPropertyDescriptor? GetProperty(string name) => AllProperties.FirstOrDefault(p => p.FieldName == name);

  // Accessible on all requests (queries or mutations)
  public Dictionary<string, ZFieldDescriptor> FieldMap { get; } = new Dictionary<string, ZFieldDescriptor>();

  private readonly Dictionary<string, ZPropertyDescriptor> _properties = new Dictionary<string, ZPropertyDescriptor>();

  public static T? ConvertValue<T>(string? val) => (T?) ConvertValue(typeof(T), val);

  public static object? ConvertValue(Type t, string? val) {
    if (val == null) return null;
    if (t == typeof(string)) return val;
    if (t == typeof(int)) return int.Parse(val);
    if (t == typeof(uint)) return uint.Parse(val);
    if (t == typeof(short)) return short.Parse(val);
    if (t == typeof(ushort)) return ushort.Parse(val);
    if (t == typeof(long)) return long.Parse(val);
    if (t == typeof(ulong)) return ulong.Parse(val);
    if (t == typeof(float)) return float.Parse(val);
    if (t == typeof(bool)) return bool.Parse(val);
    if (t == typeof(decimal)) return decimal.Parse(val);
    if (t == typeof(double)) return double.Parse(val);
    if (t.IsEnum) return val.IsNumeric() ? int.Parse(val) : Enum.Parse(t, val, true);
    ZEnv.Log.Warning("[TYPE] {type} unknown from {val}", t.Name, val);
    return val;
  }

  public object? ConvertValue(string? val) => ConvertValue(ObjectType, val);

  private ZObjectDescriptor(Type t) {
    ObjectType = t;

    if (t == typeof(long)) TypeName = "Long";
    else TypeName = t.Name;

    IsFile = ObjectType.HasAssignableType<IFileUpload>();

    if (IsFile) {
      IsScalar = false;
      InputTypeName = "Upload";
    } else if (t.HasAssignableType<ApiObject>() || t.HasAssignableType<ZRequestBase>()) {
      IsScalar = false;
      InputTypeName = TypeName + "Input";
      List<PropertyInfo> parentProps = t.BaseType?.GetProperties().Where(p => p.CanRead).ToList() ?? new List<PropertyInfo>();
      List<PropertyInfo> props = t.GetProperties().Where(p => p.CanRead).ToList();

      foreach (var prop in props) {
        var parentProp = parentProps.FirstOrDefault(p => p.Name == prop.Name);
        _properties[prop.Name] = new ZPropertyDescriptor(prop, parentProp);
        _methodInfos.Add(prop.GetGetMethod()!);
        if (prop.CanWrite) {
          _methodInfos.Add(prop.GetSetMethod()!);
        }

        string fieldName = prop.Name.ToCamelCase();
        var ignore = prop.GetCustomAttribute<ApiIgnoreAttribute>(true);
        if (ignore != null || prop.PropertyType.HasAssignableType(typeof(IAmInternal))) {
          continue;
        }

        if (!_properties[prop.Name].IsInputIgnored) {
          Inputs[fieldName] = _properties[prop.Name];
        }

        if (prop.PropertyType.IsAssignableToBaseType<ApiObject>()) {
          // Non-scalar (nested) objects are excluded as properties, UNLESS there's an explicit format provided.
          ObjectProperties[fieldName] = _properties[prop.Name];
          if (prop.GetCustomAttribute<ApiFormatAttribute>() != null) {
            FieldMap[fieldName] = ObjectProperties[fieldName];
          }
        } else {
          ScalarProperties[fieldName] = _properties[prop.Name];
          if (prop.CanWrite || prop.GetCustomAttribute<ApiFormatAttribute>() != null) {
            FieldMap[fieldName] = ScalarProperties[fieldName];
          }
        }
      }
      Methods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance) // BindingFlags.DeclaredOnly |
        .Where(mi =>
          !_methodInfos.Contains(mi) &&
          mi.GetCustomAttribute<ApiMethodAttribute>(true) != null &&
          !mi.ReturnType.HasAssignableType(typeof(IAmInternal)))
        .Select(m => new ZMethodDescriptor(m))
        .ToDictionary(p => p.FieldName, p => p);

      foreach (string fieldName in Methods.Keys) {
        if (FieldMap.ContainsKey(fieldName))
          throw new SystemException($"Duplicate field {fieldName} on {t.Name}");
        if (ObjectProperties.TryGetValue(fieldName, out var property)) {
          if (Methods[fieldName].Parameters.Any(p => !p.IsOptional))
            ZEnv.Log.Warning("[OBJ] {type}.{field} has an execution method, but it has parameters", t.Name, fieldName);
          property.ExecutionMethod = Methods[fieldName];
        }
        FieldMap[fieldName] = Methods[fieldName];
      }
    } else {
      IsScalar = true;
      InputTypeName = TypeName;
    }

    ZEnv.Log.Verbose("[OBJ] {@obj}", this);
  }

  public override string ToString() => $"{TypeName} {{ {string.Join(", ", FieldMap.Keys)} }}";

  public static readonly Dictionary<string, ZObjectDescriptor> ObjectTypes =
    new Dictionary<string, ZObjectDescriptor>();

  private static Type StripOuterTypes(Type type) {
    if (type.GenericTypeArguments.Any()) {
      if (type.GenericTypeArguments.Length > 1) throw new ArgumentException($"Cannot strip outer types from {type}");
      return StripOuterTypes(type.GenericTypeArguments.First());
    }
    var elementType = type.GetElementType();
    if (elementType != null) {
      return StripOuterTypes(elementType);
    }
    return type;
  }

  internal static ZObjectDescriptor LoadZObjectDescriptor(Type t) {
    var innerType = StripOuterTypes(t);
    string key = innerType.Name;
    if (key.Contains("`") || key.Contains("[]")) throw new SystemException($"Invalid type {innerType} from {t}");
    if (ObjectTypes.TryGetValue(key, out var d)) return d;
    var descriptor = new ZObjectDescriptor(innerType);
    ObjectTypes[key] = descriptor;
    return descriptor;
  }

  public static ZObjectDescriptor? FindZObjectDescriptor(string key) => ObjectTypes.GetValueOrDefault(key);
}

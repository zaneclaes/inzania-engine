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
public class TuneObjectDescriptor : IAmInternal {
  private readonly List<MethodInfo> _methodInfos = new List<MethodInfo>();

  public bool IsFile { get; set; }

  public bool IsScalar { get; set; }

  public Type ObjectType { get; set; }

  public string TypeName { get; }

  public string InputTypeName { get; }

  // API Object properties (not used as API objects, but used to build fragments)
  public Dictionary<string, TunePropertyDescriptor> ObjectProperties { get; } = new Dictionary<string, TunePropertyDescriptor>();

  public Dictionary<string, TunePropertyDescriptor> ScalarProperties { get; } = new Dictionary<string, TunePropertyDescriptor>();

  public Dictionary<string, TunePropertyDescriptor> Inputs { get; } = new Dictionary<string, TunePropertyDescriptor>();

  public List<TunePropertyDescriptor> AllProperties => _properties.Values.ToList();

  public Dictionary<string, TuneMethodDescriptor> Methods { get; } = new Dictionary<string, TuneMethodDescriptor>();

  public TunePropertyDescriptor? GetProperty(string name) => AllProperties.FirstOrDefault(p => p.FieldName == name);

  // Accessible on all requests (queries or mutations)
  public Dictionary<string, TuneFieldDescriptor> FieldMap { get; } = new Dictionary<string, TuneFieldDescriptor>();

  private readonly Dictionary<string, TunePropertyDescriptor> _properties = new Dictionary<string, TunePropertyDescriptor>();

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
    if (t.IsEnum) return int.Parse(val);
    IZEnv.Log.Warning("[TYPE] {type} unknown from {val}", t.Name, val);
    return val;
  }

  public object? ConvertValue(string? val) => ConvertValue(ObjectType, val);

  private TuneObjectDescriptor(Type t) {
    ObjectType = t;

    if (t == typeof(long)) TypeName = "Long";
    else TypeName = t.Name;

    IsFile = ObjectType.HasAssignableType<IFileUpload>();

    if (IsFile) {
      IsScalar = false;
      InputTypeName = "Upload";
    } else if (t.HasAssignableType<ApiObject>() || t.HasAssignableType<TuneRequestBase>()) {
      IsScalar = false;
      InputTypeName = TypeName + "Input";
      List<PropertyInfo> props = t.GetProperties().Where(p => p.CanRead).ToList();

      foreach (var prop in props) {
        _properties[prop.Name] = new TunePropertyDescriptor(prop);
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
        .Select(m => new TuneMethodDescriptor(m))
        .ToDictionary(p => p.FieldName, p => p);

      foreach (string fieldName in Methods.Keys) {
        if (FieldMap.ContainsKey(fieldName))
          throw new SystemException($"Duplicate field {fieldName} on {t.Name}");
        if (ObjectProperties.TryGetValue(fieldName, out var property)) {
          if (Methods[fieldName].Parameters.Any(p => !p.IsOptional))
            IZEnv.Log.Warning("[OBJ] {type}.{field} has an execution method, but it has parameters", t.Name, fieldName);
          property.ExecutionMethod = Methods[fieldName];
        }
        FieldMap[fieldName] = Methods[fieldName];
      }
    } else {
      IsScalar = true;
      InputTypeName = TypeName;
    }

    IZEnv.Log.Verbose("[OBJ] {@obj}", this);
  }

  public override string ToString() => $"{TypeName} {{ {string.Join(", ", FieldMap.Keys)} }}";

  public static readonly Dictionary<string, TuneObjectDescriptor> ObjectTypes =
    new Dictionary<string, TuneObjectDescriptor>();

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

  internal static TuneObjectDescriptor LoadTuneObjectDescriptor(Type t) {
    // t = TuneTypeDescriptor.MakeBaseType(t); // makeBase ? TuneTypeDescriptor.MakeBaseType(t) : t;
    var innerType = StripOuterTypes(t);
    string key = innerType.Name;
    if (key.Contains("`") || key.Contains("[]")) throw new SystemException($"Invalid type {innerType} from {t}");
    if (ObjectTypes.TryGetValue(key, out var d)) return d;
    var descriptor = new TuneObjectDescriptor(innerType);
    ObjectTypes[key] = descriptor;
    return descriptor;
  }

  public static TuneObjectDescriptor? FindTuneObjectDescriptor(string key) => ObjectTypes.GetValueOrDefault(key);
}

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Api.Types;

// Describes a concrete node type, without nullable/list/etc. decoration
public class ZObjectDescriptor : IAmInternal {
  protected readonly IZTypeMap _typeMap;

  public bool IsFile { get; set; }

  public bool IsScalar { get; set; }

  public Type ObjectType { get; set; }

  public string TypeName { get; }

  public string InputTypeName { get; }

  public string? PolymorphicDiscriminatorName { get; protected set; }

  public List<Type> PolymorphicTypes { get; protected set; } = new List<Type>();

  public byte PacketDiscriminator { get; }

  // API Object properties (not used as API objects, but used to build fragments)
  public Dictionary<string, ZPropertyDescriptor> ObjectProperties { get; } = new Dictionary<string, ZPropertyDescriptor>();

  public Dictionary<string, ZPropertyDescriptor> ScalarProperties { get; } = new Dictionary<string, ZPropertyDescriptor>();

  public Dictionary<string, ZPropertyDescriptor> Inputs { get; } = new Dictionary<string, ZPropertyDescriptor>();

  public List<ZPropertyDescriptor> AllProperties => _properties.Values.ToList();

  public Dictionary<string, ZMethodDescriptor> Methods { get; } = new Dictionary<string, ZMethodDescriptor>();

  // Accessible on all requests (queries or mutations)
  public Dictionary<string, ZFieldDescriptor> FieldMap { get; } = new Dictionary<string, ZFieldDescriptor>();

  private readonly Dictionary<string, ZPropertyDescriptor> _properties = new Dictionary<string, ZPropertyDescriptor>();

  public List<ZPropertyDescriptor> GetPropertiesForFormat(string? format = null) => AllProperties
    .Where(p => !p.IsIgnoredForFormat(format))
    .ToList();

  protected ZObjectDescriptor(IZTypeMap typeMap, string name, string inputTypeName, Type t, bool isFile, bool isScalar, byte packetDiscriminator) {
    _typeMap = typeMap;
    ObjectType = t;
    TypeName = name;
    InputTypeName = inputTypeName;
    IsFile = isFile;
    IsScalar = isScalar;
    PacketDiscriminator = packetDiscriminator;
  }

  public string GetSource(IZTypeMap typeMap, string className, string ns) {
    var inits = new List<string>();
    var usings = new HashSet<string>() {ObjectType.Namespace!, "System.Collections.Generic", "IZ.Core.Api"};
    if (PolymorphicDiscriminatorName != null) {
      inits.Add($"PolymorphicDiscriminatorName = \"{PolymorphicDiscriminatorName}\";");
      inits.Add($"PolymorphicTypes = new List<Type>() {{ typeof({string.Join("), typeof(", PolymorphicTypes.Select(t => t.Name))}) }};");
      foreach (var pt in PolymorphicTypes) usings.Add(pt.Namespace!);
    }
    var classes = new List<string>();
    foreach (var propName in _properties.Keys) {
      var prop = _properties[propName];
      var propClass = $"{className}_{prop.Name}_Property";
      classes.Add(prop.GetClassSource(typeMap, propClass, TypeName, usings));
      inits.Add($"LoadProperty(new {propClass}(typeMap));");
    }
    foreach (var methodName in Methods.Keys) {
      var method = Methods[methodName];
      var methodClass = $"{className}_{method.Name}_Method";
      classes.Add(method.GetClassSource(typeMap, methodClass, TypeName, usings));
      inits.Add($"LoadMethod(new {methodClass}(typeMap));");
    }
    return $@"using IZ.Core.Api.Types;
using {string.Join(";\nusing ", usings)};

namespace {ns};

public class {className} : ZObjectDescriptor {{
  public {className}(IZTypeMap typeMap) : base(
    typeMap,
    ""{TypeName}"", 
    ""{InputTypeName}"",
    typeof({ObjectType.Name}),
    {IsFile.ToString().ToLowerInvariant()},
    {IsScalar.ToString().ToLowerInvariant()},
    {PacketDiscriminator}
  ) {{ 
    {string.Join("\n    ", inits)}
  }}
}}

{string.Join("\n\n", classes)}
".Trim();
  }

  public ZObjectDescriptor(IZTypeMap typeMap, Type t) {
    _typeMap = typeMap;
    ObjectType = t;

    // if (t == typeof(long)) TypeName = "Long";
    // else
    TypeName = t.Name;

    IsFile = ObjectType.HasAssignableType<IFileUpload>();
    PacketDiscriminator = ObjectType.GetCustomAttribute<ApiPacketAttribute>()?.PacketDiscriminator ?? 0;
    PolymorphicDiscriminatorName = ObjectType.GetCustomAttribute<JsonPolymorphicAttribute>(true)?.TypeDiscriminatorPropertyName;
    PolymorphicTypes = ObjectType.GetCustomAttributes<JsonDerivedTypeAttribute>(true).Select(it => it.DerivedType).ToList();

    if (IsFile) {
      IsScalar = false;
      InputTypeName = "Upload";
    } else if (!t.IsScalar()) { // t.HasAssignableType<ApiObject>() || t.HasAssignableType<ZRequestBase>()
      List<MethodInfo> methodInfos = new List<MethodInfo>();
      IsScalar = false;
      InputTypeName = TypeName + "Input";
      List<PropertyInfo> parentProps = t.BaseType?.GetProperties().Where(p => p.CanRead).ToList() ?? new List<PropertyInfo>();
      List<PropertyInfo> props = t.GetProperties().Where(p => p.CanRead).ToList();

      foreach (var prop in props) {
        var parentProp = parentProps.FirstOrDefault(p => p.Name == prop.Name);
        var propDesc = new ZPropertyDescriptor(typeMap, prop, parentProp);
        methodInfos.Add(prop.GetGetMethod()!);
        if (prop.CanWrite) {
          methodInfos.Add(prop.GetSetMethod()!);
        }
        LoadProperty(propDesc);
      }
      Methods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance) // BindingFlags.DeclaredOnly |
        .Where(mi =>
          !methodInfos.Contains(mi) &&
          mi.GetCustomAttribute<ApiFormatAttribute>(true) != null &&
          !mi.ReturnType.HasAssignableType(typeof(IAmInternal)))
        .Select(m => new ZMethodDescriptor(typeMap, m))
        .ToDictionary(p => p.FieldName, p => p);

      foreach (string fieldName in Methods.Keys) {
        if (FieldMap.ContainsKey(fieldName))
          throw new SystemException($"Duplicate field {fieldName} on {t.Name}");
        if (ObjectProperties.TryGetValue(fieldName, out var property) || ScalarProperties.TryGetValue(fieldName, out property)) {
          if (Methods[fieldName].Parameters.Any(p => !p.IsOptional))
            ZEnv.Log.Warning("[OBJ] {type}.{field} has an execution method, but it has parameters", t.Name, fieldName);
          if (property.IsOutputIgnored)
            throw new SystemException($"[OBJ] {t.Name}.{fieldName} has an execution method, but it is also ignored for output ({property})");
          property.Formats = Methods[fieldName].Formats;
        }
        FieldMap[fieldName] = Methods[fieldName];
      }
    } else {
      IsScalar = true;
      InputTypeName = TypeName;
    }

    ZEnv.Log.Verbose("[OBJ] {@obj}", this);
  }

  protected void LoadMethod(ZMethodDescriptor md) {
    Methods[md.FieldName] = md;
    FieldMap[md.FieldName] = md;
  }

  protected void LoadProperty(ZPropertyDescriptor propDesc) {
    _properties[propDesc.Name] = propDesc;
    string fieldName = propDesc.FieldName;// propDesc.Name.ToCamelCase();
    if (!propDesc.IsInputIgnored) {
      Inputs[fieldName] = propDesc;
    }

    if (propDesc.FieldType.IsAssignableToBaseType<ApiObject>()) {
      // Non-scalar (nested) objects are excluded as properties, UNLESS there's an explicit format provided.
      ObjectProperties[fieldName] = propDesc;
      if (propDesc.Formats.Any()) {
        FieldMap[fieldName] = ObjectProperties[fieldName];
      }
    } else if (propDesc.FieldType.IsScalar()) {
      ScalarProperties[fieldName] = propDesc;
      if (!propDesc.IsOutputIgnored || propDesc.Formats.Any()) {
        FieldMap[fieldName] = propDesc;
      }
    }
  }

  public List<string?> ExpectedFormats => AllProperties
    .SelectMany(p => p.Formats)
    .Union(Methods.Values.SelectMany(p => p.Formats))
    .Union(FieldMap.Values.SelectMany(p => p.Formats))
    .Union(new List<string?>() { null })
    .Distinct().ToList();

  public ZPropertyDescriptor? GetProperty(string name) => AllProperties.FirstOrDefault(p => p.FieldName == name);

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
    if (t == typeof(double)) return double.Parse(val);
    if (t == typeof(decimal)) return decimal.Parse(val);
    if (t == typeof(byte)) return byte.Parse(val);
    if (t == typeof(bool)) return bool.Parse(val);
    if (t.IsEnum) return val.IsNumeric() ? int.Parse(val) : Enum.Parse(t, val, true);
    ZEnv.Log.Warning("[TYPE] {type} unknown from {val} ({scalar})", t.Name, val, t.IsScalar() ? "scalar" : "non-scalar");
    return val;
  }

  public object? ConvertValue(string? val) => ConvertValue(ObjectType, val);

  public override string ToString() => $"{TypeName} {{ {string.Join(", ", FieldMap.Keys)} }}";

  public static Type StripOuterTypes(Type type) {
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
}

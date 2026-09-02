#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Api.Types;

public class ZPropertyDescriptor : ZFieldDescriptor {
  public bool IsIgnoredForFormat(string? format = null) {
    if (!IsSettable) return true;

    // If it has an explicit separate GetXXX accessor, use THOSE formats
    var checkFormats = Formats;
    if (checkFormats.Any()) {
      return !checkFormats.Contains(format);
    }

    // Only consider the API ignore flag if it was not explicitly included
    if (IsOutputIgnored) return true;

    return !FieldTypeDescriptor.ObjectDescriptor.IsScalar;
  }

  public ZPropertyDescriptor(IZTypeMap typeMap, PropertyInfo propertyInfo, PropertyInfo? parentProp) : base(typeMap, propertyInfo, propertyInfo.PropertyType, propertyInfo.GetMethod != null && new NullabilityInfoContext()
    .Create(propertyInfo.GetMethod!.ReturnParameter!).ReadState == NullabilityState.Nullable) {

    var jsonPropName = propertyInfo.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
    PropertyInfo = propertyInfo;
    IsInherited = parentProp != null;
    Name = propertyInfo.Name;
    FieldName = jsonPropName ?? propertyInfo.Name.ToFieldName();
    IsSettable = propertyInfo.CanWrite && (propertyInfo.GetSetMethod()?.IsPublic ?? false);
    Order = propertyInfo.GetCustomAttribute<ApiOrderAttribute>()?.Order ?? -1;
    var obs = propertyInfo.GetCustomAttribute<ObservableAttribute>();
    IsObservable = obs != null;
    MetricName = obs?.MetricName;
    var isValid = IsSettable && propertyInfo.GetCustomAttribute<JsonIgnoreAttribute>(true) == null && !FieldType.HasAssignableType(typeof(IAmInternal));
    IsOutputIgnored = propertyInfo.GetCustomAttribute<OutputIgnoreAttribute>(true) != null || !isValid;
    // IsLogIgnored = propertyInfo.GetCustomAttribute<LogIgnoreAttribute>() != null || hasJsonIgnore;
    IsInputIgnored = propertyInfo.GetCustomAttribute<InputIgnoreAttribute>() != null || !isValid;

    var defVal = propertyInfo.GetCustomAttribute<DefaultValueAttribute>();
    if (defVal != null) DefaultValue = defVal.Value;
    else if (FieldType == typeof(int)) DefaultValue = 0;
    else if (FieldType == typeof(uint)) DefaultValue = (uint) 0;
    else if (FieldType == typeof(short)) DefaultValue = (short) 0;
    else if (FieldType == typeof(ushort)) DefaultValue = (ushort) 0;
    else if (FieldType == typeof(long)) DefaultValue = (long) 0;
    else if (FieldType == typeof(ulong)) DefaultValue = (ulong) 0;
    else if (FieldType == typeof(double)) DefaultValue = (double) 0;
    else if (FieldType == typeof(float)) DefaultValue = (float) 0;
    else if (FieldType == typeof(decimal)) DefaultValue = (decimal) 0;
    else if (FieldType == typeof(byte)) DefaultValue = (byte) 0;
    else if (FieldType == typeof(bool)) DefaultValue = (bool) false;

    var parent = propertyInfo.GetCustomAttribute<ApiParentAttribute>();
    if (parent != null) {
      ChildPropertyName = parent.ChildProperty;
      ThroughPropertyType = parent.ThroughModelType;
      ChildDeleteBehavior = parent.DeleteBehavior;
    }
  }

  protected ZPropertyDescriptor(
    IZTypeMap typeMap, string name, string fieldName, Type fieldType, object? defaultValue,
    HashSet<string?>? formats = null, IApiAuthorize? auth = null, bool enforceOptional = false
  ) : base(typeMap, fieldType, formats, auth, enforceOptional) {
    Name = name;
    FieldName = fieldName;
    DefaultValue = defaultValue;
  }

  private PropertyInfo? PropertyInfo { get; }

  // public bool IsLogIgnored { get; }

  public bool IsInputIgnored { get; protected set; }

  public bool IsOutputIgnored { get; protected set; }

  public bool IsSettable { get; protected set; }

  public bool IsInherited { get;  protected set; }

  public object? DefaultValue { get;  protected set; }

  public int Order { get; protected set; } = -1;

  public string? ChildPropertyName { get; protected set; }

  public ApiDeleteBehavior ChildDeleteBehavior { get; protected set; } = ApiDeleteBehavior.SetNull;

  public Type? ThroughPropertyType { get; protected set; }

  public bool IsObservable { get; protected set; }

  public string? MetricName { get; protected set; }

  public virtual object? GetValue(object obj) =>
    PropertyInfo == null ? throw new NullReferenceException(nameof(PropertyInfo)) : PropertyInfo.GetValue(obj);

  public virtual void SetValue(object obj, object? val) {
    if (!IsSettable) throw new SystemException($"{this} is not settable");
    if (PropertyInfo?.SetMethod == null) throw new SystemException($"{this} has no setter");
    PropertyInfo.SetMethod!.Invoke(obj, new[] {
      val
    });
  }

  public string GetClassSource(IZTypeMap typeMap, string className, string objectName, HashSet<string> usings) {
    var rt = typeMap.LoadTypeDescriptor(FieldType);
    usings.Add(rt.ObjectDescriptor.ObjectType.Namespace!);
    var fm = "new HashSet<string?>()";
    if (Formats.Any()) {
      fm += "{ " + string.Join(", ", Formats.Select(f => f == null ? "null" : $"\"{f}\"")) + " }";
    }
    var auth = Auth == null ? "null" : Auth.GetSource();

    // The reflective descriptor reaches every property through PropertyInfo, so two shapes that are
    // perfectly legal there are not expressible as the direct field access the generator emits:
    //  - a STATIC property cannot be read or written off an instance (CS0176), and
    //  - an `init` setter can only be assigned from an initializer or a constructor (CS8852).
    // Both keep their descriptor — dropping them would change the schema — and fall back to exactly
    // what the reflective path does. The generated descriptor carries no PropertyInfo of its own, so
    // the fallback names the property on the type rather than deferring to the base implementation.
    bool isStatic = PropertyInfo?.GetMethod?.IsStatic ?? PropertyInfo?.SetMethod?.IsStatic ?? false;
    bool isInitOnly = PropertyInfo?.SetMethod?.ReturnParameter.GetRequiredCustomModifiers()
      .Any(m => m.Name == "IsExternalInit") ?? false;
    string instance = $"(o as {objectName} ?? throw new NullReferenceException($\"{{o.GetType()}} is not a {objectName}\"))";

    var getter = isStatic
      ? $"\n\n  public override object? GetValue(object o) =>\n    {objectName}.{Name};"
      : $"\n\n  public override object? GetValue(object o) =>\n    {instance}.{Name};";

    var setter = !IsSettable ? "" :
      isInitOnly ? $"\n\n  public override void SetValue(object o, object? val) =>\n    typeof({objectName}).GetProperty(\"{Name}\")!.SetValue(o, val);" :
      isStatic ? $"\n\n  public override void SetValue(object o, object? val) =>\n    {objectName}.{Name} = {rt.ToCast("val")};" :
      $"\n\n  public override void SetValue(object o, object? val) =>\n    {instance}.{Name} = {rt.ToCast("val")};";

    List<string> inits = new List<string>();
    if (Order != -1) inits.Add($"Order = {Order};");
    if (IsInputIgnored) inits.Add($"IsInputIgnored = true;");
    if (IsOutputIgnored) inits.Add($"IsOutputIgnored = true;");
    if (IsSettable) inits.Add($"IsSettable = true;");
    if (IsInherited) inits.Add($"IsInherited = true;");
    if (IsObservable) inits.Add($"IsObservable = true;");
    if (MetricName != null) inits.Add($"MetricName = \"{MetricName}\";");
    if (ChildPropertyName != null) inits.Add($"ChildPropertyName = \"{ChildPropertyName}\";");
    if (ThroughPropertyType != null) {
      usings.Add(ThroughPropertyType.Namespace!);
      // The property is ThroughPropertyType; `ThroughModelType` names nothing and was a CS0103 in
      // every generated descriptor for a many-to-many navigation.
      inits.Add($"ThroughPropertyType = typeof({ZTypeDescriptor.CSharpName(ThroughPropertyType)});");
    }
    if (ChildDeleteBehavior != ApiDeleteBehavior.SetNull) {
      usings.Add(typeof(ApiDeleteBehavior).Namespace!);
      inits.Add($"ChildDeleteBehavior = ApiDeleteBehavior.{ChildDeleteBehavior};");
    }

    return $@"public class {className} : ZPropertyDescriptor {{
  public {className}(IZTypeMap typeMap) : base(
    typeMap,
    ""{Name}"",
    ""{FieldName}"",
    typeof({rt.ToSystemTypeName()}),
    {ZParameterDescriptor.GetDefaultValueSource(DefaultValue, usings)},
    {fm},
    {auth},
    {EnforceOptional.ToString().ToLower()}
  ) {{
    {string.Join("\n    ", inits)}
  }}{getter}{setter}
}}";
  }

  public override string ToString() => $"<{Name}: {FieldTypeDescriptor} {IsSettable}>";
}
